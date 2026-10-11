using System.Text.Json;
using System.Text.Json.Serialization;

namespace AtmCashManagement.Api;

internal static class JsonDefaults
{
    // Enums travel as the names used in the specification, e.g. IN_SERVICE.
    public static void Configure(JsonSerializerOptions options) =>
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
}
