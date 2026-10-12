using System.ComponentModel.DataAnnotations;

using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Cash;

namespace AtmCashManagement.Application.Atms;

/// <param name="Code">Unique, human-friendly ATM identifier (e.g. IST-KDK-001). Stored upper-case.</param>
/// <param name="Location">Where the ATM is installed.</param>
public sealed record CreateAtmRequest(
    [Required, StringLength(Atm.CodeMaxLength)] string Code,
    [Required, StringLength(Atm.LocationMaxLength)] string Location);

/// <param name="Status">Target status. Only IN_SERVICE and MAINTENANCE can be requested directly.</param>
public sealed record ChangeAtmStatusRequest([Required] AtmStatus? Status);

/// <param name="Cassettes">All five cassettes: exactly one DEPOSIT and four RECYCLING.</param>
public sealed record ConfigureCassettesRequest([Required] IReadOnlyList<CassetteDefinitionRequest> Cassettes);

/// <param name="Slot">Physical slot, 1 to 5.</param>
/// <param name="Type">DEPOSIT or RECYCLING.</param>
/// <param name="Currency">Recycling cassettes only: TRY, USD or EUR.</param>
/// <param name="Denomination">Recycling cassettes only: 20, 50, 100 or 500.</param>
public sealed record CassetteDefinitionRequest(
    [Range(1, CassetteConfigurationRules.CassetteCount)] int Slot,
    [Required] CassetteType? Type,
    Currency? Currency,
    int? Denomination)
{
    public CassetteDefinition ToDomain() => new(Slot, Type!.Value, Currency, Denomination);
}

public sealed record AtmResponse(
    Guid Id,
    string Code,
    string Location,
    AtmStatus Status,
    DateTimeOffset CreatedAt)
{
    public static AtmResponse From(Atm atm) => new(atm.Id, atm.Code, atm.Location, atm.Status, atm.CreatedAt);
}

/// <param name="PendingReconciliationId">Reconciliation waiting for a second user's approval; the ATM cannot return to service until then.</param>
public sealed record AtmDetailsResponse(
    Guid Id,
    string Code,
    string Location,
    AtmStatus Status,
    DateTimeOffset CreatedAt,
    Guid? PendingReconciliationId,
    IReadOnlyList<CassetteResponse> Cassettes)
{
    public static AtmDetailsResponse From(Atm atm) =>
        new(atm.Id, atm.Code, atm.Location, atm.Status, atm.CreatedAt, atm.PendingReconciliationId,
            atm.Cassettes.OrderBy(c => c.Slot).Select(CassetteResponse.From).ToList());
}

/// <param name="Contents">Notes per currency/denomination; the deposit cassette can hold several.</param>
public sealed record CassetteResponse(
    int Slot,
    CassetteType Type,
    Currency? Currency,
    int? Denomination,
    int Capacity,
    int NoteCount,
    int ReservedCount,
    int AvailableCount,
    IReadOnlyList<NoteBalanceResponse> Contents)
{
    public static CassetteResponse From(Cassette cassette) =>
        new(cassette.Slot, cassette.Type, cassette.Currency, cassette.Denomination, Cassette.Capacity,
            cassette.NoteCount, cassette.ReservedCount, cassette.AvailableCount,
            cassette.Balances
                .Where(b => b.NoteCount > 0)
                .OrderBy(b => b.Currency).ThenByDescending(b => b.Denomination)
                .Select(b => new NoteBalanceResponse(b.Currency, b.Denomination, b.NoteCount, b.ReservedCount))
                .ToList());
}

public sealed record NoteBalanceResponse(Currency Currency, int Denomination, int NoteCount, int ReservedCount);
