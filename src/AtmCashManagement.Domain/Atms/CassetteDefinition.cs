using AtmCashManagement.Domain.Cash;

namespace AtmCashManagement.Domain.Atms;

/// <summary>Requested configuration of one cassette slot. Currency and denomination apply to recycling cassettes only.</summary>
public sealed record CassetteDefinition(int Slot, CassetteType Type, Currency? Currency = null, int? Denomination = null);

/// <summary>Notes loaded by hand into a recycling cassette.</summary>
public sealed record CashLoadItem(int Slot, int Count);
