using AtmCashManagement.Application.Abstractions;
using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Ledger;
using AtmCashManagement.Domain.Withdrawals;

using Microsoft.EntityFrameworkCore;

namespace AtmCashManagement.Application.Withdrawals;

/// <summary>Releases reservations of open quotes. Callers must hold the ATM lock (<see cref="IAtmRepository"/>).</summary>
public sealed class OpenQuotes(IAppDbContext db)
{
    public const string ExpirySweeper = "system:quote-expiry";

    /// <summary>Expires the ATM's quotes whose 60 seconds are over and gives their notes back.</summary>
    public async Task<int> ExpireDueAsync(Atm atm, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var due = await db.WithdrawalQuotes
            .Where(q => q.AtmId == atm.Id && q.Status == QuoteStatus.Open && q.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        foreach (var quote in due)
        {
            atm.ReleaseReservation(quote.Expire(now), new MovementContext(ExpirySweeper, now, quote.Id));
        }

        return due.Count;
    }

    /// <summary>Cancels every open quote of the ATM, e.g. when it leaves service.</summary>
    public async Task<int> CancelAllAsync(Atm atm, string performedBy, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var open = await db.WithdrawalQuotes
            .Where(q => q.AtmId == atm.Id && q.Status == QuoteStatus.Open)
            .ToListAsync(cancellationToken);

        foreach (var quote in open)
        {
            atm.ReleaseReservation(quote.Withdraw(now), new MovementContext(performedBy, now, quote.Id));
        }

        return open.Count;
    }
}
