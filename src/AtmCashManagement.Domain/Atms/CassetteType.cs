namespace AtmCashManagement.Domain.Atms;

public enum CassetteType
{
    /// <summary>Accepts any note in any currency; cash is never dispensed from it.</summary>
    Deposit,

    /// <summary>Accepts and dispenses a single denomination of a single currency.</summary>
    Recycling,
}
