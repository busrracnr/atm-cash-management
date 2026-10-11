using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Common;

namespace AtmCashManagement.UnitTests.Domain;

public sealed class AtmTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_starts_unconfigured_with_normalized_fields()
    {
        var atm = Atm.Create("  ist-kdk-001 ", " Kadıköy ", Now);

        Assert.NotEqual(Guid.Empty, atm.Id);
        Assert.Equal("IST-KDK-001", atm.Code);
        Assert.Equal("Kadıköy", atm.Location);
        Assert.Equal(AtmStatus.Unconfigured, atm.Status);
        Assert.Equal(Now, atm.CreatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("THIS-CODE-IS-DEFINITELY-LONGER-THAN-32")]
    public void Create_rejects_invalid_code(string code)
    {
        var exception = Assert.Throws<DomainException>(() => Atm.Create(code, "Kadıköy", Now));

        Assert.Equal("ATM_CODE_INVALID", exception.Error.Code);
        Assert.Equal(ErrorType.Validation, exception.Error.Type);
    }

    [Fact]
    public void Create_rejects_blank_location()
    {
        var exception = Assert.Throws<DomainException>(() => Atm.Create("IST-KDK-001", "  ", Now));

        Assert.Equal("ATM_LOCATION_INVALID", exception.Error.Code);
    }
}
