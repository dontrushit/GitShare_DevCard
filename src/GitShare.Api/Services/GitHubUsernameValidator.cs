using System.Text.RegularExpressions;

namespace GitShare.Api.Services;

/// <summary>Валидация логина GitHub (официальные правила имени пользователя).</summary>
internal static partial class GitHubUsernameValidator
{
    [GeneratedRegex(@"^[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,37}[a-zA-Z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernameRegex();

    public static bool IsValid(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        return UsernameRegex().IsMatch(username);
    }
}
