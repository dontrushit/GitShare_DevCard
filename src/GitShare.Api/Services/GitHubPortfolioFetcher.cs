using System.Net.Http.Json;
using System.Text.Json;
using GitShare.Api.Models;

namespace GitShare.Api.Services;

/// <summary>
/// Полная загрузка публичного портфеля пользователя через GitHub API (пагинация).
/// Без этого метрики (звёзды, языки, own/fork) считаются только по первой странице (~100 репо).
/// </summary>
internal sealed class GitHubPortfolioFetcher(
    IHttpClientFactory httpClientFactory,
    ILogger<GitHubPortfolioFetcher> logger)
{
    public const string HttpClientName = GitHubApiGuards.HttpClientName;
    public const int PageSize = 100;
    /// <summary>До 1000 репозиториев (10 × 100). Для edge-case аккаунтов можно поднять.</summary>
    public const int MaxPages = 10;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PortfolioSnapshot> LoadAsync(string username, CancellationToken cancellationToken = default)
    {
        var user = await FetchUserAsync(username, cancellationToken);
        var repositories = await FetchAllRepositoriesAsync(username, cancellationToken);

        logger.LogInformation(
            "GitHub portfolio loaded for {Username}: {RepoCount} repos fetched ({Pages} pages max), user.PublicRepos={PublicRepos}",
            username,
            repositories.Count,
            MaxPages,
            user.PublicRepos);

        return new PortfolioSnapshot(user, repositories);
    }

    private async Task<GitHubUserResponse> FetchUserAsync(string username, CancellationToken cancellationToken)
    {
        using var response = await SendAsync($"users/{username}", cancellationToken);
        await GitHubApiGuards.EnsureSuccessOrThrowAsync(response, username, cancellationToken);

        var user = await response.Content.ReadFromJsonAsync<GitHubUserResponse>(JsonOptions, cancellationToken);
        return user ?? throw new InvalidOperationException("GitHub returned an empty user payload.");
    }

    /// <summary>
    /// Выкачивает весь доступный портфель: цикл по page=1..N пока страница не пустая или &lt; PageSize.
    /// </summary>
    /// <summary>
    /// Пагинация списка репозиториев: в память попадают только лёгкие метаданные (имя, звёзды, язык, fork, size).
    /// Tree API и raw-файлы — только для топ-4 в <see cref="GitHubAnalyticsService"/>.
    /// </summary>
    private async Task<IReadOnlyList<RepoListMetadata>> FetchAllRepositoriesAsync(
        string username,
        CancellationToken cancellationToken)
    {
        var all = new List<RepoListMetadata>();

        for (var page = 1; page <= MaxPages; page++)
        {
            using var response = await SendAsync(
                $"users/{username}/repos?per_page={PageSize}&page={page}&sort=pushed",
                cancellationToken);

            await GitHubApiGuards.EnsureSuccessOrThrowAsync(response, username, cancellationToken);

            var batch = await response.Content.ReadFromJsonAsync<List<GitHubRepoResponse>>(JsonOptions, cancellationToken);
            if (batch is not { Count: > 0 })
            {
                break;
            }

            foreach (var repo in batch)
            {
                all.Add(RepoListMetadata.From(repo));
            }

            if (batch.Count < PageSize)
            {
                break;
            }
        }

        return all;
    }

    private async Task<HttpResponseMessage> SendAsync(string requestUri, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        return await client.GetAsync(requestUri, cancellationToken);
    }

    internal sealed record PortfolioSnapshot(
        GitHubUserResponse User,
        IReadOnlyList<RepoListMetadata> Repositories);
}
