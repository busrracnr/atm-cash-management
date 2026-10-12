namespace AtmCashManagement.Application.Idempotency;

/// <summary>
/// Stored outcome of a request made with an Idempotency-Key. It is written in the same transaction as the
/// operation, so a repeated request gets the original result and the operation never runs twice.
/// </summary>
public sealed class IdempotencyRecord
{
    public const int KeyMaxLength = 100;

    // Required by EF Core for materialisation.
    private IdempotencyRecord()
    {
    }

    public string Scope { get; private set; } = string.Empty;

    public string Key { get; private set; } = string.Empty;

    /// <summary>Fingerprint of the request; the same key with a different request is rejected.</summary>
    public string RequestHash { get; private set; } = string.Empty;

    public string ResponseBody { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static IdempotencyRecord Create(string scope, string key, string requestHash, string responseBody, DateTimeOffset createdAt) =>
        new()
        {
            Scope = scope,
            Key = key,
            RequestHash = requestHash,
            ResponseBody = responseBody,
            CreatedAt = createdAt,
        };
}
