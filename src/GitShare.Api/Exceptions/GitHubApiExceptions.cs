namespace GitShare.Api.Exceptions;

public sealed class GitHubUserNotFoundException(string username)
    : Exception($"GitHub user '{username}' was not found.");

public sealed class GitHubRateLimitException()
    : Exception(GitHubRateLimitMessages.UserMessage);

public static class GitHubRateLimitMessages
{
    public const string UserMessage =
        "Лимит запросов к GitHub API исчерпан. Пожалуйста, попробуйте позже или подключите персональный токен.";
}
