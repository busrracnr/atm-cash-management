using AtmCashManagement.Domain.Alerts;
using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Cash;
using AtmCashManagement.Domain.Deposits;
using AtmCashManagement.Domain.Withdrawals;
using AtmCashManagement.UnitTests.TestData;

namespace AtmCashManagement.UnitTests.Domain;

public sealed class LowCashAlertTests
{
    // Slot 5 holds 20 USD 100 notes in the standard scenario.
    private static CassetteAllocation UsdHundreds(int count) => new(5, 100, count);

    [Fact]
    public void Falling_below_ten_available_notes_raises_one_alert()
    {
        var atm = AtmScenarios.InService();

        atm.ReserveNotes([UsdHundreds(11)], AtmScenarios.Context());

        var alert = Assert.Single(atm.DequeuePendingAlerts());
        Assert.Equal(
            new LowCashAlert(atm.Id, atm.Code, 5, Currency.USD, 100, 9, Cassette.LowCashThreshold, AtmScenarios.Now),
            alert);

        atm.ReserveNotes([UsdHundreds(1)], AtmScenarios.Context());
        Assert.Empty(atm.DequeuePendingAlerts());
    }

    [Fact]
    public void Exactly_ten_available_notes_is_not_low()
    {
        var atm = AtmScenarios.InService();

        atm.ReserveNotes([UsdHundreds(10)], AtmScenarios.Context());

        Assert.False(atm.GetCassette(5).IsLowOnCash);
        Assert.Empty(atm.DequeuePendingAlerts());
    }

    [Fact]
    public void Paying_out_reserved_notes_does_not_alert_again()
    {
        var atm = AtmScenarios.InService();
        atm.ReserveNotes([UsdHundreds(11)], AtmScenarios.Context());
        atm.DequeuePendingAlerts();

        atm.Dispense([UsdHundreds(11)], AtmScenarios.Context());

        Assert.Empty(atm.DequeuePendingAlerts());
    }

    [Fact]
    public void Falling_below_again_after_recovering_raises_a_new_alert()
    {
        var atm = AtmScenarios.InService();
        atm.ReserveNotes([UsdHundreds(11)], AtmScenarios.Context());
        atm.ReleaseReservation([UsdHundreds(11)], AtmScenarios.Context());
        atm.DequeuePendingAlerts();

        atm.ReserveNotes([UsdHundreds(11)], AtmScenarios.Context());

        Assert.Single(atm.DequeuePendingAlerts());
    }

    [Fact]
    public void Filling_an_empty_cassette_does_not_alert()
    {
        var atm = AtmScenarios.Configured();

        atm.LoadCash([new CashLoadItem(2, 5)], AtmScenarios.Context());

        Assert.Empty(atm.DequeuePendingAlerts());
    }

    [Fact]
    public void The_deposit_cassette_never_raises_alerts()
    {
        var atm = AtmScenarios.InService();

        atm.Deposit([new DepositNote(Currency.EUR, 50, 3)], 0, Guid.CreateVersion7(), AtmScenarios.Context());

        Assert.False(atm.GetCassette(1).IsLowOnCash);
        Assert.Empty(atm.DequeuePendingAlerts());
    }
}
