namespace AtmCashManagement.Domain.Common;

/// <summary>Thrown when a business rule is violated; translated to a ProblemDetails response by the API.</summary>
public sealed class DomainException(Error error) : Exception(error.Message)
{
    public Error Error { get; } = error;
}
