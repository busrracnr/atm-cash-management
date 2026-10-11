using AtmCashManagement.Domain.Cash;
using AtmCashManagement.Domain.Common;

namespace AtmCashManagement.Domain.Atms;

public static class CassetteConfigurationRules
{
    public const int CassetteCount = 5;
    public const int MinForeignCurrencyCassettes = 1;
    public const int MaxForeignCurrencyCassettes = 2;

    public static void Validate(IReadOnlyCollection<CassetteDefinition> definitions)
    {
        if (definitions.Count != CassetteCount)
        {
            throw new DomainException(CassetteErrors.InvalidCount(definitions.Count));
        }

        var slots = definitions.Select(d => d.Slot).ToList();
        if (slots.Any(slot => slot is < 1 or > CassetteCount) || slots.Distinct().Count() != CassetteCount)
        {
            throw new DomainException(CassetteErrors.InvalidSlots());
        }

        var depositCassettes = definitions.Count(d => d.Type == CassetteType.Deposit);
        if (depositCassettes != 1)
        {
            throw new DomainException(CassetteErrors.InvalidDepositCassetteCount(depositCassettes));
        }

        foreach (var definition in definitions.OrderBy(d => d.Slot))
        {
            ValidateCassette(definition);
        }

        var foreignCassettes = definitions.Count(d => d.Type == CassetteType.Recycling && d.Currency!.Value.IsForeign());
        if (foreignCassettes is < MinForeignCurrencyCassettes or > MaxForeignCurrencyCassettes)
        {
            throw new DomainException(CassetteErrors.InvalidForeignCurrencyCassetteCount(foreignCassettes));
        }
    }

    private static void ValidateCassette(CassetteDefinition definition)
    {
        if (definition.Type == CassetteType.Deposit)
        {
            if (definition.Currency is not null || definition.Denomination is not null)
            {
                throw new DomainException(CassetteErrors.DepositCassetteDefinesNote(definition.Slot));
            }

            return;
        }

        if (definition.Currency is null || definition.Denomination is null)
        {
            throw new DomainException(CassetteErrors.RecyclingCassetteIncomplete(definition.Slot));
        }

        if (!Denominations.IsSupported(definition.Denomination.Value))
        {
            throw new DomainException(CassetteErrors.UnsupportedDenomination(definition.Denomination.Value));
        }
    }
}
