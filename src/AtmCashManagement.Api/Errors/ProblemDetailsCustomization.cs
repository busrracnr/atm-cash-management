using System.Diagnostics;

namespace AtmCashManagement.Api.Errors;

/// <summary>Guarantees every error response carries an errorCode and a traceId, whoever produced it.</summary>
internal static class ProblemDetailsCustomization
{
    public const string ErrorCodeKey = "errorCode";
    private const string TraceIdKey = "traceId";

    public static void Apply(ProblemDetailsContext context)
    {
        context.ProblemDetails.Extensions.TryAdd(ErrorCodeKey, DefaultErrorCode(context.ProblemDetails.Status));
        context.ProblemDetails.Extensions.TryAdd(TraceIdKey, Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
    }

    private static string DefaultErrorCode(int? status) => status switch
    {
        StatusCodes.Status400BadRequest => "VALIDATION_FAILED",
        StatusCodes.Status404NotFound => "RESOURCE_NOT_FOUND",
        StatusCodes.Status405MethodNotAllowed => "METHOD_NOT_ALLOWED",
        StatusCodes.Status415UnsupportedMediaType => "UNSUPPORTED_MEDIA_TYPE",
        StatusCodes.Status500InternalServerError => "INTERNAL_ERROR",
        _ => $"HTTP_{status}",
    };
}
