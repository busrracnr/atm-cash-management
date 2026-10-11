using AtmCashManagement.Domain.Cash;
using AtmCashManagement.Domain.Common;

namespace AtmCashManagement.Domain.Atms;

public static class CassetteErrors
{
    public static Error InvalidCount(int given) =>
        new("CASSETTE_COUNT_INVALID",
            $"An ATM has exactly {CassetteConfigurationRules.CassetteCount} cassettes; {given} were given.",
            ErrorType.Validation);

    public static Error InvalidSlots() =>
        new("CASSETTE_SLOTS_INVALID",
            $"Cassette slots must be unique and between 1 and {CassetteConfigurationRules.CassetteCount}.",
            ErrorType.Validation);

    public static Error InvalidDepositCassetteCount(int given) =>
        new("DEPOSIT_CASSETTE_COUNT_INVALID",
            $"Exactly one cassette must be the deposit cassette; {given} were given.",
            ErrorType.Validation);

    public static Error DepositCassetteDefinesNote(int slot) =>
        new("DEPOSIT_CASSETTE_DEFINITION_INVALID",
            $"The deposit cassette (slot {slot}) accepts any note, so it must not define a currency or denomination.",
            ErrorType.Validation);

    public static Error RecyclingCassetteIncomplete(int slot) =>
        new("RECYCLING_CASSETTE_DEFINITION_INVALID",
            $"The recycling cassette in slot {slot} must define both a currency and a denomination.",
            ErrorType.Validation);

    public static Error UnsupportedDenomination(int denomination) =>
        new("DENOMINATION_NOT_SUPPORTED",
            $"Denomination {denomination} is not supported. Supported denominations: {string.Join(", ", Denominations.Supported.Order())}.",
            ErrorType.Validation);

    public static Error InvalidForeignCurrencyCassetteCount(int given) =>
        new("FOREIGN_CURRENCY_CASSETTE_COUNT_INVALID",
            $"Between {CassetteConfigurationRules.MinForeignCurrencyCassettes} and {CassetteConfigurationRules.MaxForeignCurrencyCassettes} " +
            $"recycling cassettes must hold a foreign currency (USD or EUR); {given} were given.",
            ErrorType.Validation);

    public static Error NotEmpty(int slot, int noteCount) =>
        new("CASSETTE_NOT_EMPTY",
            $"The cassette in slot {slot} still holds {noteCount} notes; unload it before changing its configuration.",
            ErrorType.Conflict);

    public static Error NotFound(int slot) =>
        new("CASSETTE_NOT_FOUND", $"Slot {slot} does not exist on this ATM.", ErrorType.Validation);

    public static Error NotLoadable(int slot) =>
        new("CASSETTE_NOT_LOADABLE",
            $"Slot {slot} is the deposit cassette; cash is loaded into recycling cassettes only.",
            ErrorType.Validation);

    public static Error CapacityExceeded(int slot, int requested, int free) =>
        new("CASSETTE_CAPACITY_EXCEEDED",
            $"Loading {requested} notes into slot {slot} exceeds its capacity of {Cassette.Capacity} notes ({free} free).",
            ErrorType.Validation);

    public static Error HasReservedNotes(int slot, int reserved) =>
        new("CASSETTE_HAS_RESERVED_NOTES",
            $"The cassette in slot {slot} has {reserved} notes reserved by open withdrawal quotes.",
            ErrorType.Conflict);

    public static Error InvalidCashLoad(string reason) =>
        new("CASH_LOAD_INVALID", reason, ErrorType.Validation);

    public static Error InvalidCashUnload(string reason) =>
        new("CASH_UNLOAD_INVALID", reason, ErrorType.Validation);
}
