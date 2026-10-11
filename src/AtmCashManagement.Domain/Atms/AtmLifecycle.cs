namespace AtmCashManagement.Domain.Atms;

public enum AtmOperation
{
    ConfigureCassettes,
    LoadCash,
    UnloadCash,
    Reconcile,
    Withdraw,
    Deposit,
}

/// <summary>
/// State machine: UNCONFIGURED → CONFIGURED → LOADED → IN_SERVICE ⇄ MAINTENANCE.
/// CONFIGURED and LOADED are reached by configuring cassettes and loading cash, not by a direct status change.
/// </summary>
public static class AtmLifecycle
{
    private static readonly Dictionary<AtmStatus, AtmStatus[]> DirectTransitions = new()
    {
        [AtmStatus.Unconfigured] = [],
        [AtmStatus.Configured] = [],
        [AtmStatus.Loaded] = [AtmStatus.InService],
        [AtmStatus.InService] = [AtmStatus.Maintenance],
        [AtmStatus.Maintenance] = [AtmStatus.InService],
    };

    private static readonly Dictionary<AtmOperation, AtmStatus[]> AllowedStates = new()
    {
        [AtmOperation.ConfigureCassettes] = [AtmStatus.Unconfigured, AtmStatus.Configured, AtmStatus.Maintenance],
        [AtmOperation.LoadCash] = [AtmStatus.Configured, AtmStatus.Maintenance],
        [AtmOperation.UnloadCash] = [AtmStatus.Maintenance],
        [AtmOperation.Reconcile] = [AtmStatus.Configured, AtmStatus.Maintenance],
        [AtmOperation.Withdraw] = [AtmStatus.InService],
        [AtmOperation.Deposit] = [AtmStatus.InService],
    };

    public static IReadOnlyList<AtmStatus> AllowedTargets(AtmStatus current) => DirectTransitions[current];

    public static bool CanTransition(AtmStatus current, AtmStatus target) => DirectTransitions[current].Contains(target);

    public static IReadOnlyList<AtmStatus> StatesAllowing(AtmOperation operation) => AllowedStates[operation];

    public static bool IsAllowed(AtmOperation operation, AtmStatus current) => AllowedStates[operation].Contains(current);
}
