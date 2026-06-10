using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GitShare.Api.Exceptions;
using GitShare.Api.Models;

namespace GitShare.Api.Services;

public sealed class GitHubAnalyticsService(
    IHttpClientFactory httpClientFactory,
    GitHubActivityTelemetryCollector activityTelemetryCollector,
    RepositoryCodeEvidenceService codeEvidenceService,
    LlmKeyFileContentCollector llmKeyFileContentCollector,
    IConfiguration configuration,
    ILogger<GitHubAnalyticsService> logger,
    ILoggerFactory loggerFactory)
{
    private const string GitHubHttpClientName = GitHubApiGuards.HttpClientName;
    private const string GitHubModelsHttpClientName = "GitHubModels";

    /// <summary>Глубокий forensics (git/trees, raw Program.cs) — только для центральной панели.</summary>
    public const int MaxForensicsRepositories = 4;

    private const int Size10MbKb = 10 * 1024;
    private const int Size50MbKb = 50 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<DevCardProfile> BuildProfileAsync(
        string username,
        AuditContentLocale contentLocale = AuditContentLocale.Ru,
        CancellationToken cancellationToken = default)
    {
        var portfolioFetcher = new GitHubPortfolioFetcher(
            httpClientFactory,
            loggerFactory.CreateLogger<GitHubPortfolioFetcher>());
        var portfolio = await portfolioFetcher.LoadAsync(username, cancellationToken);
        var user = portfolio.User;
        var repos = portfolio.Repositories;

        var nonForkRepos = repos.Where(r => !r.Fork).ToList();
        var forkedRepos = repos.Where(r => r.Fork).ToList();
        var scale = ClassifyRepositoryScale(nonForkRepos);
        var topRepositories = RepositorySelection.PickTopForAudit(
            nonForkRepos,
            user.Login,
            MaxForensicsRepositories);

        var forensicsTask = FetchRepositoryForensicsAsync(user.Login, topRepositories, cancellationToken);
        var telemetryTask = activityTelemetryCollector.BuildAggregatedTelemetryAsync(
            user.Login,
            topRepositories,
            cancellationToken);

        await Task.WhenAll(forensicsTask, telemetryTask);

        var forensics = await forensicsTask;
        var activityTelemetry = await telemetryTask;

        var profile = new DevCardProfile
        {
            ContentLocale = AuditContentLocaleParser.ToCode(contentLocale),
            Username = user.Login,
            AvatarUrl = user.AvatarUrl,
            Bio = user.Bio ?? string.Empty,
            Location = user.Location ?? string.Empty,
            PublicRepos = user.PublicRepos,
            TotalStars = repos.Sum(r => r.StargazersCount),
            TotalForks = nonForkRepos.Sum(r => r.ForksCount),
            OwnRepositoryCount = nonForkRepos.Count,
            ForkedRepositoryCount = forkedRepos.Count,
            ContributionRatio = CalculateContributionRatio(nonForkRepos.Count, repos.Count),
            SmallPetProjects = scale.Small,
            MediumProjects = scale.Medium,
            ProductionScaleProjects = scale.Production,
            TopRepositories = topRepositories,
            LanguageStack = BuildLanguageStack(nonForkRepos),
            CommitRhythm = BuildCommitRhythm(repos),
            ActivityTelemetry = activityTelemetry
        };

        profile.AuditData = await GetStructuredAuditAsync(
            profile,
            forensics,
            activityTelemetry,
            contentLocale,
            cancellationToken);
        profile.ProgrammerLevel = ProgrammerLevelEvaluator.Evaluate(profile);
        profile.ProgrammerLevel.AssessmentSummary = await GenerateLevelAssessmentSummaryAsync(
            profile,
            profile.ProgrammerLevel,
            contentLocale,
            cancellationToken);
        return profile;
    }

    public Task<GitHubActivityTelemetry> AnalyzeRepoActivityAsync(
        string owner,
        string repo,
        CancellationToken cancellationToken = default) =>
        activityTelemetryCollector.AnalyzeRepoActivityAsync(owner, repo, cancellationToken);

    public Task<List<string>> GetExternalPullRequestsAsync(
        string username,
        CancellationToken cancellationToken = default) =>
        activityTelemetryCollector.GetExternalPullRequestsAsync(username, cancellationToken);

    private async Task<StructuredAuditResponse> GetStructuredAuditAsync(
        DevCardProfile profile,
        IReadOnlyList<RepositoryForensics> forensics,
        GitHubActivityTelemetry telemetry,
        AuditContentLocale contentLocale,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveAiApiKey(out var apiKey))
        {
            logger.LogInformation(
                "AI:ApiKey не настроен — rule-based аудит. Задайте user-secrets: AI:ApiKey");
            return AuditEvidenceEnforcer.Apply(null, forensics, telemetry, contentLocale, profile.TotalStars);
        }

        var modelId = configuration["AI:ModelId"] ?? "gpt-4o";
        var baseUrl = configuration["AI:BaseUrl"] ?? "https://models.inference.ai.azure.com";
        var client = httpClientFactory.CreateClient(GitHubModelsHttpClientName);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var maxPayloadChars = configuration.GetValue(
            "AI:MaxUserPayloadChars",
            LlmAuditPayloadBuilder.DefaultMaxUserPayloadChars);
        var userPayload = LlmAuditPayloadBuilder.Build(profile, forensics, telemetry, maxPayloadChars);

        logger.LogInformation(
            "User payload: {Length} chars (limit {Limit})",
            userPayload.Length,
            maxPayloadChars);

        var request = new ChatCompletionRequest
        {
            Model = modelId,
            Temperature = 0.0,
            MaxTokens = 2048,
            ResponseFormat = new ChatCompletionResponseFormat { Type = "json_object" },
            Messages =
            [
                new ChatCompletionMessage
                {
                    Role = "system",
                    Content = AuditPrompts.GetJsonAuditSystemPrompt(contentLocale)
                },
                new ChatCompletionMessage { Role = "user", Content = userPayload }
            ]
        };

        var endpoint = $"{baseUrl.TrimEnd('/')}/chat/completions";
        logger.LogInformation("POST {Endpoint} model={Model}", endpoint, modelId);

        using var response = await client.PostAsJsonAsync(
            "chat/completions",
            request,
            JsonOptions,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            logger.LogWarning("HTTP 429 — лимит GitHub Models.");
            throw new AiModelsRateLimitException();
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError(
                "AI bridge failed: status={StatusCode}, details={Details}",
                (int)response.StatusCode,
                errorContent);

            if (IsPayloadTooLarge(response.StatusCode, errorContent))
            {
                logger.LogWarning(
                    "Payload превышает лимит модели — rule-based аудит без LLM.");
                return AuditEvidenceEnforcer.Apply(null, forensics, telemetry, contentLocale, profile.TotalStars);
            }

            throw new AiBridgeException($"AI Bridge Failed: {response.StatusCode}", (int)response.StatusCode);
        }

        var completionResponse =
            await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(JsonOptions, cancellationToken);
        var content = completionResponse?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();

        if (string.IsNullOrWhiteSpace(content))
        {
            logger.LogError("Пустой content в ответе chat/completions.");
            throw new AiBridgeException("AI Bridge Failed: empty model response");
        }

        logger.LogDebug("Ответ модели получен, длина JSON: {Length} символов.", content.Length);

        var parsed = StructuredAuditParser.TryParse(content, contentLocale);
        if (parsed is null)
        {
            var preview = content.Length > 400 ? content[..400] + "…" : content;
            logger.LogWarning("JSON parse failed, using forensics fallback. Preview: {Preview}", preview);
            return AuditEvidenceEnforcer.Apply(null, forensics, telemetry, contentLocale, profile.TotalStars);
        }

        var focusPreview = parsed.CoreEngineeringFocus ?? "(нет)";
        if (focusPreview.Length > 80)
        {
            focusPreview = focusPreview[..80] + "…";
        }

        logger.LogInformation(
            "JSON распознан: {ProjectCount} проект(ов), focus: {Focus}",
            parsed.Projects.Count,
            focusPreview);

        return AuditEvidenceEnforcer.Apply(parsed, forensics, telemetry, contentLocale, profile.TotalStars);
    }

    private async Task<string> GenerateLevelAssessmentSummaryAsync(
        DevCardProfile profile,
        ProgrammerLevelInfo level,
        AuditContentLocale contentLocale,
        CancellationToken cancellationToken)
    {
        var fallback = LevelSummaryFallbackBuilder.Build(profile, level, contentLocale);

        if (!TryResolveAiApiKey(out var apiKey))
        {
            logger.LogInformation("Level summary: нет AI:ApiKey — rule-based текст.");
            return fallback;
        }

        try
        {
            var modelId = configuration["AI:ModelId"] ?? "gpt-4o";
            var baseUrl = configuration["AI:BaseUrl"] ?? "https://models.inference.ai.azure.com";
            var client = httpClientFactory.CreateClient(GitHubModelsHttpClientName);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var userPayload = LevelSummaryPayloadBuilder.Build(profile, level);
            var request = new ChatCompletionRequest
            {
                Model = modelId,
                Temperature = 0.2,
                MaxTokens = 320,
                Messages =
                [
                    new ChatCompletionMessage
                    {
                        Role = "system",
                        Content = LevelSummaryPrompts.GetSystemPrompt(contentLocale)
                    },
                    new ChatCompletionMessage { Role = "user", Content = userPayload }
                ]
            };

            logger.LogInformation(
                "Level summary → model={Model}, payload={Length} chars",
                modelId,
                userPayload.Length);

            using var response = await client.PostAsJsonAsync(
                "chat/completions",
                request,
                JsonOptions,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                logger.LogWarning("Level summary: HTTP 429 — rule-based fallback.");
                return fallback;
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning(
                    "Level summary failed ({Status}): {Details}",
                    (int)response.StatusCode,
                    errorContent);
                return fallback;
            }

            var completionResponse =
                await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(JsonOptions, cancellationToken);
            var content = completionResponse?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();

            if (string.IsNullOrWhiteSpace(content))
            {
                logger.LogWarning("Level summary: пустой ответ — rule-based fallback.");
                return fallback;
            }

            var sanitized = LevelSummarySanitizer.Normalize(content, contentLocale, fallback);
            logger.LogInformation("Level summary OK ({Length} chars).", sanitized.Length);
            return sanitized;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Level summary exception — rule-based fallback.");
            return fallback;
        }
    }

    private bool TryResolveAiApiKey(out string apiKey)
    {
        apiKey = configuration["AI:ApiKey"]?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var legacyGroq = configuration["Groq:ApiKey"]?.Trim();
            if (!string.IsNullOrWhiteSpace(legacyGroq) &&
                !legacyGroq.Contains("YOUR_", StringComparison.OrdinalIgnoreCase))
            {
                apiKey = legacyGroq;
                logger.LogWarning(
                    "Используется устаревший ключ Groq:ApiKey. Перенесите в AI:ApiKey.");
            }
        }

        if (string.IsNullOrWhiteSpace(apiKey) ||
            apiKey.Contains("ПОДСТАВЬ", StringComparison.Ordinal) ||
            apiKey.Equals("YOUR_GROQ_API_KEY", StringComparison.OrdinalIgnoreCase))
        {
            apiKey = string.Empty;
            return false;
        }

        return true;
    }

    private async Task<IReadOnlyList<RepositoryForensics>> FetchRepositoryForensicsAsync(
        string username,
        IReadOnlyList<RepoSummary> repositories,
        CancellationToken cancellationToken)
    {
        if (repositories.Count == 0)
        {
            return [];
        }

        // Tree API и raw-код — строго топ-N (никогда по всему портфелю).
        var auditRepos = repositories.Take(MaxForensicsRepositories).ToList();

        logger.LogInformation(
            "Forensics for {Username}: {Count} repos (max {Max}), portfolio not scanned with tree API",
            username,
            auditRepos.Count,
            MaxForensicsRepositories);

        var tasks = auditRepos.Select(repo =>
            FetchSingleRepositoryForensicsAsync(username, repo.Name, repo.Language, cancellationToken));

        return await Task.WhenAll(tasks);
    }

    private async Task<RepositoryForensics> FetchSingleRepositoryForensicsAsync(
        string username,
        string repoName,
        string? gitHubPrimaryLanguage,
        CancellationToken cancellationToken)
    {
        var readmeTask = FetchReadmeForRepositoryAsync(username, repoName, cancellationToken);
        var treeDataTask = FetchRepositoryTreeDataAsync(username, repoName, cancellationToken);
        var commitsTask = FetchCommitSnapshotAsync(username, repoName, cancellationToken);

        await Task.WhenAll(readmeTask, treeDataTask, commitsTask);

        var (treeSnapshot, blobPaths) = await treeDataTask;
        var isVendorAssetPack = UnityRepositoryHeuristics.IsEmbeddedAssetPack(repoName, blobPaths);
        var signatureManifest = TargetFileSignatureAnalyzer.BuildManifest(repoName, blobPaths, gitHubPrimaryLanguage);

        IReadOnlyList<string> verifiedPros;
        IReadOnlyList<string> verifiedCons;
        if (isVendorAssetPack)
        {
            verifiedPros = [];
            verifiedCons = [];
            logger.LogInformation(
                "Skipping code evidence for {Repo} — embedded Asset Store / vendor pack detected",
                repoName);
        }
        else
        {
            (verifiedPros, verifiedCons) = await codeEvidenceService.AnalyzeAsync(
                username,
                repoName,
                blobPaths,
                signatureManifest,
                cancellationToken);
        }

        var keyFilesContent = isVendorAssetPack
            ? []
            : await llmKeyFileContentCollector.CollectForRepositoryAsync(
                username,
                repoName,
                blobPaths,
                signatureManifest,
                cancellationToken);

        return new RepositoryForensics(
            repoName,
            await readmeTask,
            treeSnapshot,
            await commitsTask,
            signatureManifest,
            verifiedPros,
            verifiedCons,
            keyFilesContent,
            isVendorAssetPack,
            blobPaths);
    }

    private async Task<string> FetchReadmeForRepositoryAsync(
        string username,
        string repoName,
        CancellationToken cancellationToken)
    {
        try
        {
            var raw = await FetchRawReadmeAsync(username, repoName, cancellationToken);
            return string.IsNullOrWhiteSpace(raw) ? string.Empty : ReadmeCleaner.CleanReadmeContent(raw);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to fetch README for {Repo}", repoName);
            return string.Empty;
        }
    }

    private async Task<(string Snapshot, IReadOnlyList<string> BlobPaths)> FetchRepositoryTreeDataAsync(
        string username,
        string repoName,
        CancellationToken cancellationToken)
    {
        try
        {
            var tree = await FetchRepositoryTreeAsync(username, repoName, username, cancellationToken);
            if (tree is null || tree.Count == 0)
            {
                return ("Tree unavailable or empty.", []);
            }

            var blobPaths = tree
                .Where(item => string.Equals(item.Type, "blob", StringComparison.OrdinalIgnoreCase))
                .Select(item => item.Path)
                .ToList();

            var snapshot = RepositoryForensicsCompressor.CompressTreeSnapshot(blobPaths);
            return (snapshot, blobPaths);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to fetch tree for {Repo}", repoName);
            return ("Tree unavailable.", []);
        }
    }

    private async Task<string> FetchCommitSnapshotAsync(
        string username,
        string repoName,
        CancellationToken cancellationToken)
    {
        try
        {
            var commits = await FetchRecentCommitMessagesAsync(username, repoName, cancellationToken);
            return RepositoryForensicsCompressor.CompressCommitSnapshot(commits);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to fetch commits for {Repo}", repoName);
            return "Commit history unavailable.";
        }
    }

    private async Task<List<GitHubTreeItem>?> FetchRepositoryTreeAsync(
        string username,
        string repoName,
        string rateLimitContextUser,
        CancellationToken cancellationToken)
    {
        var mainTree = await TryFetchGitHubJsonAsync<GitHubTreeResponse>(
            $"repos/{username}/{repoName}/git/trees/main?recursive=1",
            rateLimitContextUser,
            cancellationToken);

        if (mainTree?.Tree is { Count: > 0 })
        {
            return mainTree.Tree;
        }

        var masterTree = await TryFetchGitHubJsonAsync<GitHubTreeResponse>(
            $"repos/{username}/{repoName}/git/trees/master?recursive=1",
            rateLimitContextUser,
            cancellationToken);

        return masterTree?.Tree;
    }

    private async Task<IReadOnlyList<string>> FetchRecentCommitMessagesAsync(
        string username,
        string repoName,
        CancellationToken cancellationToken)
    {
        var commits = await TryFetchGitHubJsonAsync<List<GitHubCommitListItem>>(
            $"repos/{username}/{repoName}/commits?per_page=10",
            username,
            cancellationToken);

        if (commits is null || commits.Count == 0)
        {
            return [];
        }

        return commits
            .Select(item => item.Commit?.Message ?? string.Empty)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToList();
    }

    private async Task<T?> TryFetchGitHubJsonAsync<T>(
        string requestUri,
        string username,
        CancellationToken cancellationToken)
        where T : class
    {
        using var response = await SendGitHubRequestAsync(requestUri, cancellationToken);

        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }

        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
        {
            await GitHubApiGuards.EnsureSuccessOrThrowAsync(response, username, cancellationToken);
        }

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    private async Task<string> FetchRawReadmeAsync(
        string username,
        string repoName,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(GitHubHttpClientName);

        var masterUrl =
            $"https://raw.githubusercontent.com/{username}/{repoName}/master/README.md";
        var masterContent = await TryFetchRawContentAsync(client, masterUrl, cancellationToken);
        if (!string.IsNullOrWhiteSpace(masterContent))
        {
            return masterContent;
        }

        var mainUrl = $"https://raw.githubusercontent.com/{username}/{repoName}/main/README.md";
        return await TryFetchRawContentAsync(client, mainUrl, cancellationToken) ?? string.Empty;
    }

    private static async Task<string?> TryFetchRawContentAsync(
        HttpClient client,
        string url,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(url, cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound || !response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static bool IsPayloadTooLarge(HttpStatusCode statusCode, string errorContent) =>
        statusCode == HttpStatusCode.RequestEntityTooLarge ||
        errorContent.Contains("tokens_limit", StringComparison.OrdinalIgnoreCase) ||
        errorContent.Contains("Request body too large", StringComparison.OrdinalIgnoreCase) ||
        errorContent.Contains("RequestEntityTooLarge", StringComparison.OrdinalIgnoreCase);

    private static double CalculateContributionRatio(int ownCount, int totalCount) =>
        totalCount == 0 ? 0 : Math.Round(ownCount / (double)totalCount, 2);

    private static (int Small, int Medium, int Production) ClassifyRepositoryScale(
        IReadOnlyList<RepoListMetadata> nonForkRepos)
    {
        var small = 0;
        var medium = 0;
        var production = 0;

        foreach (var repo in nonForkRepos)
        {
            if (repo.SizeKb < Size10MbKb)
            {
                small++;
            }
            else if (repo.SizeKb <= Size50MbKb)
            {
                medium++;
            }
            else
            {
                production++;
            }
        }

        return (small, medium, production);
    }

    private async Task<HttpResponseMessage> SendGitHubRequestAsync(
        string requestUri,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(GitHubHttpClientName);
        return await client.GetAsync(requestUri, cancellationToken);
    }

    private static List<LanguageMetric> BuildLanguageStack(IReadOnlyList<RepoListMetadata> nonForkRepos)
    {
        var reposWithLanguage = nonForkRepos
            .Where(r => !string.IsNullOrWhiteSpace(r.Language))
            .ToList();

        if (reposWithLanguage.Count == 0)
        {
            return [];
        }

        return reposWithLanguage
            .GroupBy(r => r.Language!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new LanguageMetric
            {
                Language = g.Key,
                Percentage = Math.Round(g.Count() * 100.0 / reposWithLanguage.Count, 1)
            })
            .OrderByDescending(m => m.Percentage)
            .ToList();
    }

    private static List<HourlyActivity> BuildCommitRhythm(IReadOnlyList<RepoListMetadata> repos)
    {
        var hourCounts = new int[24];

        foreach (var repo in repos.Take(20))
        {
            var timestamp = repo.PushedAt ?? repo.UpdatedAt;
            if (string.IsNullOrWhiteSpace(timestamp))
            {
                continue;
            }

            if (!DateTimeOffset.TryParse(timestamp, out var parsed))
            {
                continue;
            }

            hourCounts[parsed.Hour]++;
        }

        return hourCounts
            .Select((count, hour) => new HourlyActivity { Hour = hour, CommitCount = count })
            .Where(h => h.CommitCount > 0)
            .OrderBy(h => h.Hour)
            .ToList();
    }
}
