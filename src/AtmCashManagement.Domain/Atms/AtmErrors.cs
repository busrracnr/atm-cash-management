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
}
