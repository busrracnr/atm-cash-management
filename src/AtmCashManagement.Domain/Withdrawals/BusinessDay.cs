namespace AtmCashManagement.Domain.Withdrawals;

public static class BusinessDay
{
    /// <summary>The bank operates in Türkiye, which keeps UTC+3 all year (no daylight saving time).</summary>
    public static readonly TimeSpan UtcOffset = TimeSpan.FromHours(3);

    public static DateOnly Of(DateTimeOffset instant) => DateOnly.FromDateTime(instant.ToOffset(UtcOffset).DateTime);
}
