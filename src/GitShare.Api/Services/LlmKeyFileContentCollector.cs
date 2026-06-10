using GitShare.Api.Models;

namespace GitShare.Api.Services;

/// <summary>
/// Скачивает содержимое ключевых файлов Production-репозиториев для промпта LLM.
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
        projectClass = ProjectClassProsCons.ResolveEffectiveClass(projectClass, repoName, signatureManifest);

        var paths = LlmEvidenceFileSelector.SelectPaths(repoName, blobPaths, signatureManifest, projectClass);
        if (paths.Count == 0)
        {
            return [];
        }

        return await rawFetcher.FetchManyAsync(
            owner,
            repoName,
            paths,
            LlmEvidenceFileSelector.MaxCharsPerFile,
            cancellationToken);
    }
}
