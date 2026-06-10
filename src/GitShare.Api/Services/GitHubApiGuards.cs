using System.Net;
using GitShare.Api.Exceptions;

namespace GitShare.Api.Services;

internal static class GitHubApiGuards
{
    public const string HttpClientName = "GitHub";

    public static async Task EnsureSuccessOrThrowAsync(
        HttpResponseMessage response,
        string username,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            throw new GitHubUserNotFoundException(username);
        }

        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
        {
            var remaining = response.Headers.TryGetValues("X-RateLimit-Remaining", out var values)
                ? values.FirstOrDefault()
                : null;

            if (remaining == "0" || response.StatusCode is HttpStatusCode.TooManyRequests)
            {
                throw new GitHubRateLimitException();
            }
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"GitHub API request failed with {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
    }
}
