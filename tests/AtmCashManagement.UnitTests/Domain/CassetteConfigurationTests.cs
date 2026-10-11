using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Cash;
using AtmCashManagement.Domain.Common;
using AtmCashManagement.UnitTests.TestData;

namespace AtmCashManagement.UnitTests.Domain;

public sealed class CassetteConfigurationTests
{
    [Fact]
    public void Standard_configuration_creates_five_cassettes_and_moves_to_configured()
    {
        var atm = AtmScenarios.Unconfigured();

        atm.ConfigureCassettes(AtmScenarios.StandardCassettes(), AtmScenarios.Now);

        Assert.Equal(AtmStatus.Configured, atm.Status);
        Assert.Equal([1, 2, 3, 4, 5], atm.Cassettes.Select(c => c.Slot).Order());
        var deposit = Assert.Single(atm.Cassettes, c => c.Type == CassetteType.Deposit);
        Assert.Null(deposit.Currency);
        Assert.Equal((Currency.USD, 100), (atm.GetCassette(5).Currency!.Value, atm.GetCassette(5).Denomination!.Value));
        Assert.All(atm.Cassettes, c => Assert.Equal(0, c.NoteCount));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    public void Cassette_count_must_be_exactly_five(int count)
    {
        var definitions = Enumerable.Range(1, count)
            .Select(slot => slot == 1
                ? new CassetteDefinition(slot, CassetteType.Deposit)
                : new CassetteDefinition(slot, CassetteType.Recycling, slot == 2 ? Currency.USD : Currency.TRY, 100))
            .ToList();

        AssertRejected(definitions, "CASSETTE_COUNT_INVALID");
    }

    [Fact]
    public void Slots_must_be_unique()
    {
        var definitions = AtmScenarios.StandardCassettes();
        definitions[4] = definitions[4] with { Slot = 4 };

        AssertRejected(definitions, "CASSETTE_SLOTS_INVALID");
    }

    [Fact]
    public void Slots_must_be_between_one_and_five()
    {
        var definitions = AtmScenarios.StandardCassettes();
        definitions[4] = definitions[4] with { Slot = 6 };

        AssertRejected(definitions, "CASSETTE_SLOTS_INVALID");
    }

    [Fact]
    public void Exactly_one_deposit_cassette_is_required()
    {
        var noDeposit = AtmScenarios.StandardCassettes();
        noDeposit[0] = new CassetteDefinition(1, CassetteType.Recycling, Currency.TRY, 500);

        var twoDeposits = AtmScenarios.StandardCassettes();
        twoDeposits[3] = new CassetteDefinition(4, CassetteType.Deposit);

        AssertRejected(noDeposit, "DEPOSIT_CASSETTE_COUNT_INVALID");
        AssertRejected(twoDeposits, "DEPOSIT_CASSETTE_COUNT_INVALID");
    }

    [Fact]
    public void Deposit_cassette_must_not_define_currency_or_denomination()
    {
        var definitions = AtmScenarios.StandardCassettes();
        definitions[0] = new CassetteDefinition(1, CassetteType.Deposit, Currency.TRY, 100);

        AssertRejected(definitions, "DEPOSIT_CASSETTE_DEFINITION_INVALID");
    }

    [Fact]
    public void Recycling_cassette_needs_currency_and_denomination()
    {
        var definitions = AtmScenarios.StandardCassettes();
        definitions[2] = new CassetteDefinition(3, CassetteType.Recycling, Currency.TRY);

        AssertRejected(definitions, "RECYCLING_CASSETTE_DEFINITION_INVALID");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(200)]
    [InlineData(1000)]
    public void Only_20_50_100_and_500_notes_are_supported(int denomination)
    {
        var definitions = AtmScenarios.StandardCassettes();
        definitions[1] = definitions[1] with { Denomination = denomination };

        AssertRejected(definitions, "DENOMINATION_NOT_SUPPORTED");
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void One_or_two_recycling_cassettes_hold_a_foreign_currency(int foreignCassettes, bool valid)
    {
        var currencies = new[] { Currency.USD, Currency.EUR, Currency.USD, Currency.EUR };
        var definitions = new List<CassetteDefinition> { new(1, CassetteType.Deposit) };
        for (var i = 0; i < 4; i++)
        {
            definitions.Add(new CassetteDefinition(i + 2, CassetteType.Recycling, i < foreignCassettes ? currencies[i] : Currency.TRY, 100));
        }

        var atm = AtmScenarios.Unconfigured();

        if (valid)
        {
            atm.ConfigureCassettes(definitions, AtmScenarios.Now);
            Assert.Equal(AtmStatus.Configured, atm.Status);
        }
        else
        {
            AssertRejected(definitions, "FOREIGN_CURRENCY_CASSETTE_COUNT_INVALID");
        }
    }

    [Fact]
    public void Same_currency_and_denomination_may_be_assigned_to_two_cassettes()
    {
        var definitions = AtmScenarios.StandardCassettes();
        definitions[2] = new CassetteDefinition(3, CassetteType.Recycling, Currency.TRY, 100);

        var atm = AtmScenarios.Unconfigured();
        atm.ConfigureCassettes(definitions, AtmScenarios.Now);

        Assert.Equal(2, atm.Cassettes.Count(c => c is { Currency: Currency.TRY, Denomination: 100 }));
    }

    [Fact]
    public void Reconfiguring_keeps_cassette_identity_for_unchanged_slots()
    {
        var atm = AtmScenarios.Configured();
        var slot2Id = atm.GetCassette(2).Id;
        var definitions = AtmScenarios.StandardCassettes();
        definitions[4] = new CassetteDefinition(5, CassetteType.Recycling, Currency.EUR, 50);

        atm.ConfigureCassettes(definitions, AtmScenarios.Now);

        Assert.Equal(AtmStatus.Configured, atm.Status);
        Assert.Equal(slot2Id, atm.GetCassette(2).Id);
        Assert.Equal((Currency.EUR, 50), (atm.GetCassette(5).Currency!.Value, atm.GetCassette(5).Denomination!.Value));
    }

    [Fact]
    public void A_cassette_holding_notes_cannot_be_redefined_and_nothing_changes()
    {
        var atm = AtmScenarios.InMaintenance();
        var definitions = AtmScenarios.StandardCassettes();
        definitions[1] = new CassetteDefinition(2, CassetteType.Recycling, Currency.TRY, 500);
        definitions[4] = new CassetteDefinition(5, CassetteType.Recycling, Currency.EUR, 100);

        var exception = Assert.Throws<DomainException>(() => atm.ConfigureCassettes(definitions, AtmScenarios.Now));

        Assert.Equal("CASSETTE_NOT_EMPTY", exception.Error.Code);
        Assert.Equal(100, atm.GetCassette(2).Denomination);
        Assert.Equal(Currency.USD, atm.GetCassette(5).Currency);
    }

    [Fact]
    public void An_emptied_cassette_can_be_redefined_in_maintenance()
    {
        var atm = AtmScenarios.InMaintenance();
        atm.UnloadCash([5], AtmScenarios.Context());
        var definitions = AtmScenarios.StandardCassettes();
        definitions[4] = new CassetteDefinition(5, CassetteType.Recycling, Currency.EUR, 100);

        atm.ConfigureCassettes(definitions, AtmScenarios.Now);

        Assert.Equal(AtmStatus.Maintenance, atm.Status);
        Assert.Equal(Currency.EUR, atm.GetCassette(5).Currency);
        Assert.Equal(50, atm.GetCassette(2).NoteCount);
    }

    private static void AssertRejected(List<CassetteDefinition> definitions, string expectedCode)
    {
        var atm = AtmScenarios.Unconfigured();

        var exception = Assert.Throws<DomainException>(() => atm.ConfigureCassettes(definitions, AtmScenarios.Now));

        Assert.Equal(expectedCode, exception.Error.Code);
        Assert.Equal(AtmStatus.Unconfigured, atm.Status);
        Assert.Empty(atm.Cassettes);
    }
}
