using GitShare.Api.Models;

namespace GitShare.Api.Services;

/// <summary>
/// Скачивает содержимое ключевых файлов (Production и Utility) для промпта LLM.
/// </summary>
public sealed class LlmKeyFileContentCollector(GitHubRawContentFetcher rawFetcher)
{
    public async Task<IReadOnlyList<KeyFileContentEntry>> CollectForRepositoryAsync(
        string owner,
        string repoName,
        IReadOnlyList<string> blobPaths,
        string signatureManifest,
        CancellationToken cancellationToken = default)
    {
        var projectClass = ProjectClassClassifier.Classify(repoName, signatureManifest);
        var selection = LlmEvidenceFileSelector.Select(repoName, blobPaths, signatureManifest, projectClass);

        if (selection.SourcePaths.Count == 0 && selection.InfraPaths.Count == 0)
        {
            return [];
        }

        var sourceTask = rawFetcher.FetchManyAsync(
            owner,
            repoName,
            selection.SourcePaths,
            LlmEvidenceFileSelector.MaxCharsPerFile,
            cancellationToken);

        var infraTask = rawFetcher.FetchManyAsync(
            owner,
            repoName,
            selection.InfraPaths,
            LlmEvidenceFileSelector.MaxCharsPerInfraFile,
            cancellationToken);

        await Task.WhenAll(sourceTask, infraTask);

        return (await sourceTask)
            .Concat(await infraTask)
            .ToList();
    }
}
