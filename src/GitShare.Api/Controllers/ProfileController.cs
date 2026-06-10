using GitShare.Api.Hosting;
using GitShare.Api.Models;
using GitShare.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GitShare.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ProfileController(
    ProfileAnalysisCacheService profileCacheService,
    ForceRefreshGatekeeper forceRefreshGatekeeper) : ControllerBase
{
    /// <param name="forceRefresh">true — игнорировать кэш и заново запросить GitHub + LLM.</param>
    /// <param name="locale">Язык AI-аудита: <c>ru</c> (по умолчанию) или <c>en</c>.</param>
    [HttpGet("{username}")]
    [EnableRateLimiting("profile-analysis")]
    [ProducesResponseType(typeof(DevCardProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<DevCardProfile>> GetProfile(
        string username,
        [FromQuery] bool forceRefresh = false,
        [FromQuery] string? locale = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid username",
                "Username is required."));
        }

        if (!string.Equals(username, username.Trim(), StringComparison.Ordinal))
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid username",
                "Username must not contain leading or trailing whitespace."));
        }

        var normalizedUsername = username.Trim();
        if (!GitHubUsernameValidator.IsValid(normalizedUsername))
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid username",
                "Username must match GitHub rules: letters, digits, hyphens; 1–39 characters; no special characters."));
        }

        if (forceRefresh)
        {
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
            if (!forceRefreshGatekeeper.TryAcquire(clientIp, normalizedUsername))
            {
                return StatusCode(
                    StatusCodes.Status429TooManyRequests,
                    CreateProblemDetails(
                        StatusCodes.Status429TooManyRequests,
                        "Force refresh rate limit",
                        "Force refresh is allowed once every 10 minutes."));
            }
        }

        var contentLocale = AuditContentLocaleParser.Parse(locale);
        var profile = await profileCacheService.GetOrAnalyzeAsync(
            normalizedUsername,
            contentLocale,
            forceRefresh,
            cancellationToken);

        return Ok(profile);
    }

    private ProblemDetails CreateProblemDetails(int status, string title, string detail) =>
        new()
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = HttpContext.Request.Path,
        };
}
