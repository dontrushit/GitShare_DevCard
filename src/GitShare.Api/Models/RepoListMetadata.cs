namespace GitShare.Api.Models;

/// <summary>
/// Лёгкие метаданные репозитория для пагинации портфеля (без tree/raw/LLM по всем репо).
/// </summary>
internal sealed record RepoListMetadata(
    string Name,
    int StargazersCount,
    string? Language,
    bool Fork,
    int SizeKb,
    int ForksCount,
    string? Description,
    string HtmlUrl,
    string? PushedAt,
    string? UpdatedAt)
{
    public static RepoListMetadata From(GitHubRepoResponse repo) =>
        new(
            repo.Name,
            repo.StargazersCount,
            repo.Language,
            repo.Fork,
            repo.Size,
            repo.ForksCount,
            repo.Description,
            repo.HtmlUrl,
            repo.PushedAt,
            repo.UpdatedAt);
}
