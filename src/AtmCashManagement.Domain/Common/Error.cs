namespace AtmCashManagement.Domain.Common;

/// <param name="Code">Stable, machine-readable code (e.g. ATM_NOT_FOUND) that clients can branch on.</param>
/// <param name="Message">Human-readable explanation.</param>
/// <param name="Type">Error category.</param>
public sealed record Error(string Code, string Message, ErrorType Type);
