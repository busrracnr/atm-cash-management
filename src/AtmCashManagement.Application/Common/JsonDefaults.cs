using System.Text.Json;
using System.Text.Json.Serialization;

namespace AtmCashManagement.Application.Common;

public static class JsonDefaults
{
    /// <summary>The HTTP API's settings; also used for stored responses so an idempotent replay is byte-for-byte the same.</summary>
    public static JsonSerializerOptions Options { get; } = CreateReadOnly();

    // Enums travel as the names used in the specification, e.g. IN_SERVICE.
    public static void Configure(JsonSerializerOptions options) =>
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));

    private static JsonSerializerOptions CreateReadOnly()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Configure(options);
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
