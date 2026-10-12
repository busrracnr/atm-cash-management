using AtmCashManagement.Domain.Cash;
using AtmCashManagement.Domain.Common;

namespace AtmCashManagement.Domain.Withdrawals;

public static class WithdrawalErrors
{
    public static Error InvalidAmount(int amount) =>
        new("WITHDRAWAL_AMOUNT_INVALID", $"Withdrawal amount must be positive; {amount} was given.", ErrorType.Validation);

    public static Error InsufficientCash(Currency currency, int amount) =>
        new("INSUFFICIENT_CASH",
            $"The ATM cannot pay {amount} {currency} or any nearby amount with the notes it has.",
            ErrorType.Validation);

    public static Error QuoteNotFound(Guid quoteId) =>
        new("QUOTE_NOT_FOUND", $"Withdrawal quote '{quoteId}' was not found.", ErrorType.NotFound);

    public static Error QuoteExpired(Guid quoteId, DateTimeOffset expiredAt) =>
        new("QUOTE_EXPIRED",
            $"Withdrawal quote '{quoteId}' expired at {expiredAt:u}; request a new quote.",
            ErrorType.Conflict,
            new Dictionary<string, object?> { ["expiredAt"] = expiredAt });

    public static Error QuoteNotOpen(Guid quoteId, QuoteStatus status) =>
        new("QUOTE_NOT_OPEN",
            $"Withdrawal quote '{quoteId}' is {status.ToApiName()} and can no longer be confirmed or cancelled.",
            ErrorType.Conflict,
            new Dictionary<string, object?> { ["quoteStatus"] = status.ToApiName() });

    public static Error OptionRequired(IReadOnlyList<QuoteOption> offered) =>
        new("QUOTE_OPTION_REQUIRED",
            $"The requested amount could not be paid exactly; choose one of: {offered.ToApiNames()}.",
            ErrorType.Validation,
            new Dictionary<string, object?> { ["offeredOptions"] = offered.Select(o => o.ToApiName()).ToArray() });

    public static Error OptionNotOffered(QuoteOption option, IReadOnlyList<QuoteOption> offered) =>
        new("QUOTE_OPTION_NOT_OFFERED",
            $"Option {option.ToApiName()} is not part of this quote; offered: {offered.ToApiNames()}.",
            ErrorType.Validation,
            new Dictionary<string, object?> { ["offeredOptions"] = offered.Select(o => o.ToApiName()).ToArray() });

    public static Error DailyLimitExceeded(Currency currency, int dailyLimit, int usedToday, int requested) =>
        new("DAILY_LIMIT_EXCEEDED",
            $"{requested} {currency} exceeds the card's daily limit of {dailyLimit} {currency}: " +
            $"{usedToday} {currency} is already withdrawn or reserved today, {dailyLimit - usedToday} {currency} remains.",
            ErrorType.Validation,
            new Dictionary<string, object?>
            {
                ["currency"] = currency.ToApiName(),
                ["dailyLimit"] = dailyLimit,
                ["usedToday"] = usedToday,
                ["remaining"] = dailyLimit - usedToday,
            });

    public static Error InvalidCardNumber() =>
        new("CARD_NUMBER_INVALID", "Card number must be 12 to 19 digits and pass the Luhn check.", ErrorType.Validation);
}
