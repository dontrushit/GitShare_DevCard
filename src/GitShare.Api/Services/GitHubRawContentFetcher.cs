using System.Net;
using GitShare.Api.Models;

namespace GitShare.Api.Services;

/// <summary>
/// Загрузка файлов с raw.githubusercontent.com (main/master).
/// </summary>
public sealed class GitHubRawContentFetcher(
    IHttpClientFactory httpClientFactory,
    ILogger<GitHubRawContentFetcher> logger)
{
    public const int DefaultMaxCharsPerFile = 3000;

    public async Task<string?> FetchFileAsync(
        string owner,
        string repoName,
        string path,
        int maxChars = DefaultMaxCharsPerFile,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(GitHubApiGuards.HttpClientName);
        var encodedPath = string.Join('/', path.Replace('\\', '/').Split('/').Select(Uri.EscapeDataString));

        foreach (var branch in new[] { "main", "master" })
        {
            var url = $"https://raw.githubusercontent.com/{owner}/{repoName}/{branch}/{encodedPath}";
            try
            {
                using var response = await client.GetAsync(url, cancellationToken);
                if (response.StatusCode is HttpStatusCode.NotFound || !response.IsSuccessStatusCode)
                {
                    continue;
                }

                var text = await response.Content.ReadAsStringAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }

                return text.Length > maxChars ? text[..maxChars] : text;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Raw fetch failed for {Owner}/{Repo}/{Path} ({Branch})", owner, repoName, path, branch);
            }
        }

        return null;
    }

    public async Task<IReadOnlyList<KeyFileContentEntry>> FetchManyAsync(
        string owner,
        string repoName,
        IReadOnlyList<string> paths,
        int maxCharsPerFile = DefaultMaxCharsPerFile,
        CancellationToken cancellationToken = default)
    {
        if (paths.Count == 0)
        {
            return [];
        }

        var tasks = paths.Select(async path =>
        {
            var content = await FetchFileAsync(owner, repoName, path, maxCharsPerFile, cancellationToken);
            return (path, content);
        });

        var results = await Task.WhenAll(tasks);
        return results
            .Where(r => !string.IsNullOrWhiteSpace(r.content))
            .Select(r => new KeyFileContentEntry
            {
                FileName = r.path.Replace('\\', '/'),
                Content = r.content!.Trim()
            })
            .ToList();
    }
}
