using AtmCashManagement.Application.Abstractions;
using AtmCashManagement.Domain.Common;

namespace AtmCashManagement.Api.Users;

internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public const string HeaderName = "X-User";

    public string Name
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.Request.Headers[HeaderName].ToString().Trim();
            if (string.IsNullOrEmpty(value))
            {
                return CurrentUserLimits.Anonymous;
            }

            if (value.Length > CurrentUserLimits.NameMaxLength)
            {
                throw new DomainException(new Error(
                    "X_USER_INVALID",
                    $"The {HeaderName} header must be at most {CurrentUserLimits.NameMaxLength} characters.",
                    ErrorType.Validation));
            }

            return value;
        }
    }
}
