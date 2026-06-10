using GitShare.Api.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GitShare.Api.Hosting;

internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception for {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        var (status, title, detail) = exception switch
        {
            GitHubUserNotFoundException => (
                StatusCodes.Status404NotFound,
                "Profile not found",
                "The GitHub user was not found."),
            GitHubRateLimitException => (
                StatusCodes.Status403Forbidden,
                "GitHub rate limit",
                GitHubRateLimitMessages.UserMessage),
            AiModelsRateLimitException => (
                StatusCodes.Status429TooManyRequests,
                "AI rate limit",
                "GitHub Models API rate limit exceeded. Please try again later."),
            AiBridgeException bridge => (
                bridge.StatusCode is > 0 and var code ? code : StatusCodes.Status502BadGateway,
                "AI bridge error",
                bridge.Message),
            HttpRequestException http => (
                StatusCodes.Status502BadGateway,
                "Upstream error",
                http.Message),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Server error",
                "An unexpected error occurred.")
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
