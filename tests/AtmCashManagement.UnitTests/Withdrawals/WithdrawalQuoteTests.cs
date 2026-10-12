using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Cash;
using AtmCashManagement.Domain.Common;
using AtmCashManagement.Domain.Withdrawals;
using AtmCashManagement.UnitTests.TestData;

namespace AtmCashManagement.UnitTests.Withdrawals;

public sealed class WithdrawalQuoteTests
{
    private const string CardHash = "c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00";
    private static readonly DateTimeOffset Now = AtmScenarios.Now;

    [Fact]
    public void An_exact_quote_holds_the_fewest_notes_for_sixty_seconds()
    {
        var quote = Open(Currency.TRY, 160);

        Assert.Equal([QuoteOption.Exact], quote.Options);
        Assert.Equal(160, quote.AmountOf(QuoteOption.Exact));
        Assert.Equal([new CassetteAllocation(2, 100, 1), new CassetteAllocation(4, 20, 3)], quote.Reservation());
        Assert.Equal(160, quote.LimitAmount);
        Assert.Equal(Now.AddSeconds(60), quote.ExpiresAt);
        Assert.Equal(QuoteStatus.Open, quote.Status);
    }

    [Fact]
    public void With_two_alternatives_each_cassette_holds_enough_for_either()
    {
        var quote = Open(Currency.TRY, 30);

        Assert.Equal([QuoteOption.Lower, QuoteOption.Upper], quote.Options);
        Assert.Equal((20, 40), (quote.AmountOf(QuoteOption.Lower), quote.AmountOf(QuoteOption.Upper)));
        Assert.Equal([new CassetteAllocation(4, 20, 2)], quote.Reservation());
        Assert.Equal(40, quote.LimitAmount);
    }

    [Fact]
    public void Confirming_the_lower_alternative_releases_what_the_upper_one_needed()
    {
        var quote = Open(Currency.TRY, 30);

        var settlement = quote.Confirm(QuoteOption.Lower, Now.AddSeconds(10));

        Assert.Equal([new CassetteAllocation(4, 20, 1)], settlement.Dispensed);
        Assert.Equal([new CassetteAllocation(4, 20, 1)], settlement.Released);
        Assert.Equal(QuoteStatus.Confirmed, quote.Status);
        Assert.Equal(20, quote.DispensedAmount);
        Assert.Equal(20, quote.LimitAmount);
    }

    [Fact]
    public void Confirming_an_exact_quote_needs_no_option()
    {
        var quote = Open(Currency.TRY, 160);

        var settlement = quote.Confirm(null, Now);

        Assert.Equal(QuoteOption.Exact, quote.ConfirmedOption);
        Assert.Empty(settlement.Released);
    }

    [Fact]
    public void Without_an_exact_payment_the_customer_must_choose()
    {
        var quote = Open(Currency.TRY, 30);

        AssertRejected(() => quote.Confirm(null, Now), "QUOTE_OPTION_REQUIRED");
        AssertRejected(() => quote.Confirm(QuoteOption.Exact, Now), "QUOTE_OPTION_NOT_OFFERED");
        Assert.Equal(QuoteStatus.Open, quote.Status);
    }

    [Fact]
    public void A_quote_can_be_confirmed_until_its_sixtieth_second()
    {
        var expiring = Open(Currency.TRY, 160);
        var valid = Open(Currency.TRY, 160);

        AssertRejected(() => expiring.Confirm(null, Now.AddSeconds(60)), "QUOTE_EXPIRED");
        valid.Confirm(null, Now.AddSeconds(59));

        Assert.Equal(QuoteStatus.Expired, expiring.EffectiveStatus(Now.AddSeconds(60)));
        Assert.Equal(QuoteStatus.Confirmed, valid.Status);
    }

    [Fact]
    public void A_quote_is_confirmed_only_once()
    {
        var quote = Open(Currency.TRY, 160);
        quote.Confirm(null, Now);

        AssertRejected(() => quote.Confirm(null, Now), "QUOTE_NOT_OPEN");
    }

    [Fact]
    public void Cancelling_returns_the_whole_reservation_and_is_repeatable()
    {
        var quote = Open(Currency.TRY, 30);

        var released = quote.Cancel(Now);

        Assert.Equal([new CassetteAllocation(4, 20, 2)], released);
        Assert.Equal(QuoteStatus.Cancelled, quote.Status);
        Assert.Equal(0, quote.LimitAmount);
        Assert.Empty(quote.Cancel(Now));
        AssertRejected(() => quote.Confirm(QuoteOption.Lower, Now), "QUOTE_NOT_OPEN");
    }

    [Fact]
    public void Expiring_takes_effect_only_after_the_deadline()
    {
        var quote = Open(Currency.TRY, 160);

        Assert.Empty(quote.Expire(Now.AddSeconds(59)));
        Assert.Equal(QuoteStatus.Open, quote.Status);

        Assert.Equal(2, quote.Expire(Now.AddSeconds(60)).Count);
        Assert.Equal(QuoteStatus.Expired, quote.Status);
        Assert.Equal(0, quote.LimitAmount);
    }

    [Fact]
    public void A_currency_the_atm_does_not_hold_cannot_be_quoted()
    {
        AssertRejected(() => Open(Currency.EUR, 100), "INSUFFICIENT_CASH");
    }

    [Fact]
    public void The_business_day_follows_turkish_time()
    {
        Assert.Equal(new DateOnly(2026, 10, 12), BusinessDay.Of(new DateTimeOffset(2026, 10, 11, 21, 30, 0, TimeSpan.Zero)));
        Assert.Equal(new DateOnly(2026, 10, 11), BusinessDay.Of(new DateTimeOffset(2026, 10, 11, 20, 59, 0, TimeSpan.Zero)));
    }

    private static WithdrawalQuote Open(Currency currency, int amount)
    {
        var atm = AtmScenarios.InService();
        return WithdrawalQuote.Open(atm, CardHash, currency, NoteSelector.Select(atm.AvailableNotes(currency), amount), Now);
    }

    private static void AssertRejected(Action action, string expectedCode)
    {
        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal(expectedCode, exception.Error.Code);
    }
}
