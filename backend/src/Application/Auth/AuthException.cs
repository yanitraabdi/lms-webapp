namespace Academy.Application.Auth;

/// <summary>Domain-level auth failure, mapped to an RFC-7807 problem-details response in the Api.</summary>
public sealed class AuthException(string code, string message, int statusCode = 400) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}
