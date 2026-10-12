using AtmCashManagement.Domain.Common;
using AtmCashManagement.Domain.Withdrawals;

namespace AtmCashManagement.UnitTests.Withdrawals;

public sealed class CardNumberTests
{
    [Theory]
    [InlineData("4111111111111111", "4111111111111111")]
    [InlineData("4111 1111 1111 1111", "4111111111111111")]
    [InlineData("5555-5555-5555-4444", "5555555555554444")]
    public void Spaces_and_dashes_are_removed(string input, string expected)
    {
        Assert.Equal(expected, CardNumber.Normalize(input));
    }

    [Theory]
    [InlineData("4111111111111112")]
    [InlineData("41111111111")]
    [InlineData("41111111111111111111")]
    [InlineData("4111-1111-1111-111A")]
    [InlineData("")]
    public void Malformed_numbers_are_rejected(string input)
    {
        var exception = Assert.Throws<DomainException>(() => CardNumber.Normalize(input));

        Assert.Equal("CARD_NUMBER_INVALID", exception.Error.Code);
    }
}
