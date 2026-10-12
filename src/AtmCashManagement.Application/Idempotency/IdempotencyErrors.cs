using AtmCashManagement.Domain.Common;

namespace AtmCashManagement.Application.Idempotency;

public static class IdempotencyErrors
{
    public static Error KeyReused(string key) =>
        new("IDEMPOTENCY_KEY_REUSED",
            $"Idempotency-Key '{key}' was already used for a different request; use a new key for a new request.",
            ErrorType.Validation);
}
