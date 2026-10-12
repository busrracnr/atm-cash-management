using System.Text.Json;

using AtmCashManagement.Application.Common;
using AtmCashManagement.Domain.Alerts;

namespace AtmCashManagement.Application.Outbox;

/// <summary>
/// An event waiting to be published. It is inserted in the same transaction as the change that raised it, so an event
/// is never lost (or invented) if that change fails; a background service publishes it afterwards.
/// </summary>
public sealed class OutboxMessage
{
    public const string LowCashAlertType = "LowCashAlert";
    public const int MaxErrorLength = 500;

    // Required by EF Core for materialisation.
    private OutboxMessage()
    {
    }

    public Guid Id { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public Guid? AtmId { get; private set; }

    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public static OutboxMessage From(LowCashAlert alert) =>
        new()
        {
            Id = Guid.CreateVersion7(alert.OccurredAt),
            Type = LowCashAlertType,
            AtmId = alert.AtmId,
            Payload = JsonSerializer.Serialize(alert, JsonDefaults.Options),
            OccurredAt = alert.OccurredAt,
        };

    public void MarkProcessed(DateTimeOffset processedAt)
    {
        Attempts++;
        ProcessedAt = processedAt;
        LastError = null;
    }

    public void MarkFailed(string error)
    {
        Attempts++;
        LastError = error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;
    }
}
