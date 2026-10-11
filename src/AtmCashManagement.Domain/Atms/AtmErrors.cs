using AtmCashManagement.Domain.Common;

namespace AtmCashManagement.Domain.Atms;

public static class AtmErrors
{
    public static Error NotFound(Guid atmId) =>
        new("ATM_NOT_FOUND", $"ATM '{atmId}' was not found.", ErrorType.NotFound);

    public static Error CodeAlreadyExists(string code) =>
        new("ATM_CODE_ALREADY_EXISTS", $"An ATM with code '{code}' already exists.", ErrorType.Conflict);

    public static Error InvalidCode() =>
        new("ATM_CODE_INVALID", $"ATM code is required and must be at most {Atm.CodeMaxLength} characters.", ErrorType.Validation);

    public static Error InvalidLocation() =>
        new("ATM_LOCATION_INVALID", $"ATM location is required and must be at most {Atm.LocationMaxLength} characters.", ErrorType.Validation);

    public static Error InvalidStateTransition(AtmStatus current, AtmStatus requested, IReadOnlyList<AtmStatus> allowed)
    {
        var hint = current switch
        {
            AtmStatus.Unconfigured => " Configure the cassettes first.",
            AtmStatus.Configured => " Record the cash loaded into the cassettes first.",
            _ => $" Allowed: {allowed.ToApiNames()}.",
        };

        return new(
            "INVALID_STATE_TRANSITION",
            $"Cannot change ATM status from {current.ToApiName()} to {requested.ToApiName()}.{hint}",
            ErrorType.Conflict,
            new Dictionary<string, object?>
            {
                ["currentStatus"] = current.ToApiName(),
                ["requestedStatus"] = requested.ToApiName(),
                ["allowedStatuses"] = allowed.Select(s => s.ToApiName()).ToArray(),
            });
    }

    public static Error OperationNotAllowed(AtmOperation operation, AtmStatus current, IReadOnlyList<AtmStatus> allowed) =>
        new(
            "OPERATION_NOT_ALLOWED_IN_CURRENT_STATE",
            $"{operation.ToApiName()} is not allowed while the ATM is {current.ToApiName()}. Allowed in: {allowed.ToApiNames()}.",
            ErrorType.Conflict,
            new Dictionary<string, object?>
            {
                ["operation"] = operation.ToApiName(),
                ["currentStatus"] = current.ToApiName(),
                ["allowedStatuses"] = allowed.Select(s => s.ToApiName()).ToArray(),
            });
}
