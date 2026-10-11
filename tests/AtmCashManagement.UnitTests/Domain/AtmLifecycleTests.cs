using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Common;
using AtmCashManagement.UnitTests.TestData;

namespace AtmCashManagement.UnitTests.Domain;

public sealed class AtmLifecycleTests
{
    private static readonly (AtmStatus From, AtmStatus To)[] AllowedDirectTransitions =
    [
        (AtmStatus.Loaded, AtmStatus.InService),
        (AtmStatus.InService, AtmStatus.Maintenance),
        (AtmStatus.Maintenance, AtmStatus.InService),
    ];

    public static TheoryData<AtmStatus, AtmStatus> EveryStatusPair()
    {
        var data = new TheoryData<AtmStatus, AtmStatus>();
        foreach (var from in Enum.GetValues<AtmStatus>())
        {
            foreach (var to in Enum.GetValues<AtmStatus>())
            {
                data.Add(from, to);
            }
        }

        return data;
    }

    [Fact]
    public void Full_lifecycle_follows_the_specified_order()
    {
        var atm = AtmScenarios.Unconfigured();
        Assert.Equal(AtmStatus.Unconfigured, atm.Status);

        atm.ConfigureCassettes(AtmScenarios.StandardCassettes(), AtmScenarios.Now);
        Assert.Equal(AtmStatus.Configured, atm.Status);

        atm.LoadCash(AtmScenarios.StandardLoad(), AtmScenarios.Context());
        Assert.Equal(AtmStatus.Loaded, atm.Status);

        atm.ChangeStatus(AtmStatus.InService);
        Assert.Equal(AtmStatus.InService, atm.Status);

        atm.ChangeStatus(AtmStatus.Maintenance);
        Assert.Equal(AtmStatus.Maintenance, atm.Status);

        atm.ChangeStatus(AtmStatus.InService);
        Assert.Equal(AtmStatus.InService, atm.Status);
    }

    [Theory]
    [MemberData(nameof(EveryStatusPair))]
    public void Direct_status_change_is_allowed_only_for_the_specified_transitions(AtmStatus from, AtmStatus to)
    {
        var atm = AtmScenarios.InState(from);

        if (AllowedDirectTransitions.Contains((from, to)))
        {
            atm.ChangeStatus(to);
            Assert.Equal(to, atm.Status);
        }
        else
        {
            var exception = Assert.Throws<DomainException>(() => atm.ChangeStatus(to));
            Assert.Equal("INVALID_STATE_TRANSITION", exception.Error.Code);
            Assert.Equal(from, atm.Status);
        }
    }

    [Fact]
    public void Invalid_transition_error_explains_current_and_allowed_statuses()
    {
        var atm = AtmScenarios.InService();

        var exception = Assert.Throws<DomainException>(() => atm.ChangeStatus(AtmStatus.Loaded));

        Assert.Equal(ErrorType.Conflict, exception.Error.Type);
        Assert.Equal("IN_SERVICE", exception.Error.Details!["currentStatus"]);
        Assert.Equal(["MAINTENANCE"], (string[])exception.Error.Details["allowedStatuses"]!);
    }

    [Theory]
    [InlineData(AtmStatus.Loaded)]
    [InlineData(AtmStatus.InService)]
    public void Cassettes_cannot_be_configured_outside_setup_or_maintenance(AtmStatus status)
    {
        var atm = AtmScenarios.InState(status);

        AssertOperationRejected(() => atm.ConfigureCassettes(AtmScenarios.StandardCassettes(), AtmScenarios.Now));
    }

    [Theory]
    [InlineData(AtmStatus.Unconfigured)]
    [InlineData(AtmStatus.Loaded)]
    [InlineData(AtmStatus.InService)]
    public void Cash_cannot_be_loaded_outside_configured_or_maintenance(AtmStatus status)
    {
        var atm = AtmScenarios.InState(status);

        AssertOperationRejected(() => atm.LoadCash([new CashLoadItem(2, 1)], AtmScenarios.Context()));
    }

    [Theory]
    [InlineData(AtmStatus.Configured)]
    [InlineData(AtmStatus.Loaded)]
    [InlineData(AtmStatus.InService)]
    public void Cash_can_only_be_unloaded_in_maintenance(AtmStatus status)
    {
        var atm = AtmScenarios.InState(status);

        AssertOperationRejected(() => atm.UnloadCash([2], AtmScenarios.Context()));
    }

    private static void AssertOperationRejected(Action operation)
    {
        var exception = Assert.Throws<DomainException>(operation);
        Assert.Equal("OPERATION_NOT_ALLOWED_IN_CURRENT_STATE", exception.Error.Code);
    }
}
