namespace AtmCashManagement.Domain.Common;

/// <summary>Category of a business error; the API maps each category to an HTTP status code.</summary>
public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Forbidden,
}
