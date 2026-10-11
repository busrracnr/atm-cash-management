using System.Text.Json;

namespace AtmCashManagement.Domain.Common;

public static class EnumNames
{
    /// <summary>Formats an enum value the way the API exposes it, e.g. InService → IN_SERVICE.</summary>
    public static string ToApiName<TEnum>(this TEnum value)
        where TEnum : struct, Enum =>
        JsonNamingPolicy.SnakeCaseUpper.ConvertName(value.ToString());

    public static string ToApiNames<TEnum>(this IEnumerable<TEnum> values)
        where TEnum : struct, Enum =>
        string.Join(", ", values.Select(ToApiName));
}
