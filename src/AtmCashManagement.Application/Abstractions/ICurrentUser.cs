namespace AtmCashManagement.Application.Abstractions;

/// <summary>Caller identity taken from the X-User request header (authentication is out of scope).</summary>
public interface ICurrentUser
{
    string Name { get; }
}

public static class CurrentUserLimits
{
    public const string Anonymous = "anonymous";
    public const int NameMaxLength = 100;
}
