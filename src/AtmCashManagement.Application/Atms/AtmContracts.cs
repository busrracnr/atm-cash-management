using System.ComponentModel.DataAnnotations;

using AtmCashManagement.Domain.Atms;

namespace AtmCashManagement.Application.Atms;

/// <param name="Code">Unique, human-friendly ATM identifier (e.g. IST-KDK-001). Stored upper-case.</param>
/// <param name="Location">Where the ATM is installed.</param>
public sealed record CreateAtmRequest(
    [Required, StringLength(Atm.CodeMaxLength)] string Code,
    [Required, StringLength(Atm.LocationMaxLength)] string Location);

public sealed record AtmResponse(
    Guid Id,
    string Code,
    string Location,
    AtmStatus Status,
    DateTimeOffset CreatedAt)
{
    public static AtmResponse From(Atm atm) => new(atm.Id, atm.Code, atm.Location, atm.Status, atm.CreatedAt);
}
