using AtmCashManagement.Domain.Common;

namespace AtmCashManagement.Domain.Atms;

public sealed class Atm
{
    public const int CodeMaxLength = 32;
    public const int LocationMaxLength = 200;

    // Required by EF Core for materialisation.
    private Atm()
    {
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Location { get; private set; } = string.Empty;

    public AtmStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Atm Create(string code, string location, DateTimeOffset createdAt)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        if (normalizedCode.Length is 0 or > CodeMaxLength)
        {
            throw new DomainException(AtmErrors.InvalidCode());
        }

        var normalizedLocation = location.Trim();
        if (normalizedLocation.Length is 0 or > LocationMaxLength)
        {
            throw new DomainException(AtmErrors.InvalidLocation());
        }

        return new Atm
        {
            // Time-ordered UUIDv7 keeps the primary-key index append-only.
            Id = Guid.CreateVersion7(createdAt),
            Code = normalizedCode,
            Location = normalizedLocation,
            Status = AtmStatus.Unconfigured,
            CreatedAt = createdAt,
        };
    }
}
