using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using AtmCashManagement.Application.Abstractions;
using AtmCashManagement.Application.Common;
using AtmCashManagement.Application.Idempotency;
using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Cash;
using AtmCashManagement.Domain.Common;
using AtmCashManagement.Domain.Ledger;
using AtmCashManagement.Domain.Withdrawals;

using Microsoft.EntityFrameworkCore;

namespace AtmCashManagement.Application.Withdrawals;

/// <summary>Two-step withdrawal (requirement B4): quote and reserve, then confirm or cancel.</summary>
public sealed class WithdrawalService(
    IAppDbContext db,
    IUnitOfWork unitOfWork,
    IAtmRepository atms,
    ICardLock cardLock,
    OpenQuotes openQuotes,
    ICardNumberHasher cardNumberHasher,
    ICurrentUser currentUser,
    TimeProvider clock)
{
    private const string ConfirmScope = "withdrawal-confirm";

    public Task<WithdrawalQuoteResponse> CreateQuoteAsync(
        Guid atmId, CreateWithdrawalQuoteRequest request, CancellationToken cancellationToken)
    {
        var cardHash = cardNumberHasher.Hash(CardNumber.Normalize(request.CardNumber));
        var currency = request.Currency!.Value;

        return unitOfWork.ExecuteAsync(async ct =>
        {
            var atm = await atms.GetForUpdateAsync(atmId, ct);
            atm.EnsureAllowed(AtmOperation.Withdraw);

            // Free the notes of quotes that ran out, so they can be offered again right away.
            var now = clock.GetUtcNow();
            await openQuotes.ExpireDueAsync(atm, now, ct);

            await cardLock.AcquireAsync(cardHash, ct);
            var dailyLimit = DailyWithdrawalLimits.For(currency);
            var usedToday = await UsedTodayAsync(cardHash, currency, now, ct);
            if (request.Amount > dailyLimit - usedToday)
            {
                throw new DomainException(WithdrawalErrors.DailyLimitExceeded(currency, dailyLimit, usedToday, request.Amount));
            }

            // The upper alternative is offered only if it still fits within the remaining daily limit.
            var selection = NoteSelector.Select(atm.AvailableNotes(currency), request.Amount, maxUpperAmount: dailyLimit - usedToday);
            var quote = WithdrawalQuote.Open(atm, cardHash, currency, selection, now);
            atm.ReserveNotes(quote.Reservation(), new MovementContext(currentUser.Name, now, quote.Id));
            db.WithdrawalQuotes.Add(quote);

            return WithdrawalQuoteResponse.From(quote, now) with
            {
                RemainingDailyLimit = dailyLimit - usedToday - quote.LimitAmount,
            };
        }, cancellationToken);
    }

    public async Task<WithdrawalQuoteResponse> GetQuoteAsync(Guid quoteId, CancellationToken cancellationToken)
    {
        var quote = await db.WithdrawalQuotes.AsNoTracking().FirstOrDefaultAsync(q => q.Id == quoteId, cancellationToken)
            ?? throw new DomainException(WithdrawalErrors.QuoteNotFound(quoteId));

        return WithdrawalQuoteResponse.From(quote, clock.GetUtcNow());
    }

    public async Task<WithdrawalConfirmationResponse> ConfirmAsync(
        Guid quoteId, string idempotencyKey, ConfirmWithdrawalQuoteRequest request, CancellationToken cancellationToken)
    {
        var requestHash = Fingerprint($"{quoteId:N}:{request.Option?.ToString() ?? "-"}");

        var replay = await FindReplayAsync(idempotencyKey, requestHash, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        return await unitOfWork.ExecuteAsync(async ct =>
        {
            var atm = await LockAtmOfQuoteAsync(quoteId, ct);

            // A request with the same key may have committed while this one waited for the lock.
            var concurrentReplay = await FindReplayAsync(idempotencyKey, requestHash, ct);
            if (concurrentReplay is not null)
            {
                return concurrentReplay;
            }

            var quote = await db.WithdrawalQuotes.SingleAsync(q => q.Id == quoteId, ct);
            var now = clock.GetUtcNow();
            var settlement = quote.Confirm(request.Option, now);
            var context = new MovementContext(currentUser.Name, now, quote.Id);
            atm.Dispense(settlement.Dispensed, context);
            atm.ReleaseReservation(settlement.Released, context);

            var response = WithdrawalConfirmationResponse.From(quote, settlement);
            db.IdempotencyRecords.Add(IdempotencyRecord.Create(
                ConfirmScope, idempotencyKey, requestHash, JsonSerializer.Serialize(response, JsonDefaults.Options), now));

            return response;
        }, cancellationToken);
    }

    public Task<WithdrawalQuoteResponse> CancelAsync(Guid quoteId, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async ct =>
        {
            var atm = await LockAtmOfQuoteAsync(quoteId, ct);
            var quote = await db.WithdrawalQuotes.SingleAsync(q => q.Id == quoteId, ct);
            var now = clock.GetUtcNow();
            atm.ReleaseReservation(quote.Cancel(now), new MovementContext(currentUser.Name, now, quote.Id));
            return WithdrawalQuoteResponse.From(quote, now);
        }, cancellationToken);

    /// <summary>Expires due quotes on every ATM; run periodically by a background service.</summary>
    public async Task<int> ExpireDueQuotesAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var atmIds = await db.WithdrawalQuotes
            .Where(q => q.Status == QuoteStatus.Open && q.ExpiresAt <= now)
            .Select(q => q.AtmId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var expired = 0;
        foreach (var atmId in atmIds)
        {
            expired += await unitOfWork.ExecuteAsync(async ct =>
            {
                var atm = await atms.GetForUpdateAsync(atmId, ct);
                return await openQuotes.ExpireDueAsync(atm, clock.GetUtcNow(), ct);
            }, cancellationToken);
        }

        return expired;
    }

    /// <summary>Confirmed amounts plus amounts still reserved by open, unexpired quotes, for the card's business day.</summary>
    private Task<int> UsedTodayAsync(string cardHash, Currency currency, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var today = BusinessDay.Of(now);
        return db.WithdrawalQuotes
            .Where(q => q.CardHash == cardHash && q.Currency == currency && q.BusinessDate == today)
            .Where(q => q.Status == QuoteStatus.Confirmed || (q.Status == QuoteStatus.Open && q.ExpiresAt > now))
            .SumAsync(q => q.LimitAmount, cancellationToken);
    }

    private async Task<Atm> LockAtmOfQuoteAsync(Guid quoteId, CancellationToken cancellationToken)
    {
        var atmId = await db.WithdrawalQuotes
            .Where(q => q.Id == quoteId)
            .Select(q => (Guid?)q.AtmId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new DomainException(WithdrawalErrors.QuoteNotFound(quoteId));

        return await atms.GetForUpdateAsync(atmId, cancellationToken);
    }

    private async Task<WithdrawalConfirmationResponse?> FindReplayAsync(
        string idempotencyKey, string requestHash, CancellationToken cancellationToken)
    {
        var record = await db.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Scope == ConfirmScope && r.Key == idempotencyKey, cancellationToken);

        if (record is null)
        {
            return null;
        }

        return record.RequestHash == requestHash
            ? JsonSerializer.Deserialize<WithdrawalConfirmationResponse>(record.ResponseBody, JsonDefaults.Options)
            : throw new DomainException(IdempotencyErrors.KeyReused(idempotencyKey));
    }

    private static string Fingerprint(string request) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(request)));
}
