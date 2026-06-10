namespace GitShare.Api.Exceptions;

public sealed class AiBridgeException(string message, int? statusCode = null)
    : Exception(message)
{
    public int? StatusCode { get; } = statusCode;
}
