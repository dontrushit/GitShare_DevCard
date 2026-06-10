using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GitShare.Api.Models;

namespace GitShare.Api.Services;

public sealed class GitHubActivityTelemetryCollector(
    IHttpClientFactory httpClientFactory,
    ILogger<GitHubActivityTelemetryCollector> logger)
{
    private const string GitHubHttpClientName = "GitHub";
    private const int CommitPageSize = 40;
    private const int MaxRecentMessages = 40;
    private const int MaxExternalRepositories = 10;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<GitHubActivityTelemetry> AnalyzeRepoActivityAsync(
        string owner,
        string repo,
        CancellationToken cancellationToken = default)
    {
        var telemetry = new GitHubActivityTelemetry();

        var commits = await FetchCommitsAsync(owner, repo, cancellationToken);
        if (commits.Count == 0)
        {
            return telemetry;
        }

        foreach (var item in commits)
        {
            var message = item.Commit?.Message;
            if (!string.IsNullOrWhiteSpace(message))
            {
                var firstLine = message.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                if (firstLine.Length > 0)
                {
                    telemetry.RecentCommitMessages.Add(firstLine);
                }
            }

            var authorDate = item.Commit?.Author?.Date;
            if (authorDate is null)
            {
                continue;
            }

            if (IsWorkingHours(authorDate.Value))
            {
                telemetry.CommitsInWorkingHours++;
            }
            else
            {
                telemetry.CommitsInOffHours++;
            }
        }

        return telemetry;
    }

    public async Task<List<string>> GetExternalPullRequestsAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        var query = $"author:{username}+type:pr";
        var encodedQuery = Uri.EscapeDataString(query);
        var requestUri = $"search/issues?q={encodedQuery}&per_page=30&sort=updated";

        try
        {
            var response = await TryFetchGitHubJsonAsync<GitHubSearchIssuesResponse>(requestUri, cancellationToken);
            if (response?.Items is null || response.Items.Count == 0)
            {
                return [];
            }

            return response.Items
                .Select(ResolveRepositoryFullName)
                .OfType<string>()
                .Where(fullName => !IsOwnedByUser(fullName, username))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxExternalRepositories)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "External PR search failed for {Username}", username);
            return [];
        }
    }

    public async Task<GitHubActivityTelemetry> BuildAggregatedTelemetryAsync(
        string username,
        IReadOnlyList<RepoSummary> repositories,
        CancellationToken cancellationToken = default)
    {
        var externalPrTask = GetExternalPullRequestsAsync(username, cancellationToken);
        var repoTasks = repositories
            .Select(repo => AnalyzeRepoActivityAsync(username, repo.Name, cancellationToken))
            .ToList();

        await Task.WhenAll(repoTasks.Cast<Task>().Append(externalPrTask));

        var aggregated = new GitHubActivityTelemetry
        {
            ExternalPullRequests = await externalPrTask
        };

        foreach (var repoTelemetry in repoTasks.Select(task => task.Result))
        {
            aggregated.CommitsInWorkingHours += repoTelemetry.CommitsInWorkingHours;
            aggregated.CommitsInOffHours += repoTelemetry.CommitsInOffHours;
            aggregated.RecentCommitMessages.AddRange(repoTelemetry.RecentCommitMessages);
        }

        aggregated.RecentCommitMessages = aggregated.RecentCommitMessages
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxRecentMessages)
            .ToList();

        aggregated.TotalStars = repositories.Sum(r => r.Stars);
        aggregated.TopStarredRepos = repositories
            .Where(r => r.Stars > 0)
            .OrderByDescending(r => r.Stars)
            .Take(3)
            .Select(r => $"{r.Name} ({r.Stars}★)")
            .ToList();

        return aggregated;
    }

    public static string FormatForAuditPayload(GitHubActivityTelemetry telemetry)
    {
        var totalCommits = telemetry.CommitsInWorkingHours + telemetry.CommitsInOffHours;
        var workingPercent = totalCommits == 0
            ? 0
            : Math.Round(telemetry.CommitsInWorkingHours * 100.0 / totalCommits, 1);

        var sb = new StringBuilder();
        sb.AppendLine("=== COMMIT ACTIVITY (messages and timestamps) ===");
        sb.AppendLine($"CommitsInWorkingHours (Mon-Fri 10:00-18:00 local): {telemetry.CommitsInWorkingHours}");
        sb.AppendLine($"CommitsInOffHours (evenings/weekends/other): {telemetry.CommitsInOffHours}");
        sb.AppendLine($"WorkingHoursPercent: {workingPercent}%");
        sb.AppendLine($"RecentCommitMessages (sample, max {MaxRecentMessages}):");

        if (telemetry.RecentCommitMessages.Count == 0)
        {
            sb.AppendLine("- (no commit messages captured)");
        }
        else
        {
            foreach (var message in telemetry.RecentCommitMessages)
            {
                sb.AppendLine($"- {message}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("=== OPEN SOURCE CONTRIBUTIONS (external PRs) ===");
        if (telemetry.ExternalPullRequests.Count == 0)
        {
            sb.AppendLine("- (none found in GitHub search)");
        }
        else
        {
            foreach (var repo in telemetry.ExternalPullRequests)
            {
                sb.AppendLine($"- {repo}");
            }
        }

        return sb.ToString();
    }

    private static bool IsWorkingHours(DateTimeOffset commitTime)
    {
        var day = commitTime.DayOfWeek;
        if (day is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return false;
        }

        var hour = commitTime.Hour;
        return hour >= 10 && hour < 18;
    }

    private static bool IsOwnedByUser(string fullName, string username)
    {
        var slash = fullName.IndexOf('/');
        if (slash <= 0)
        {
            return fullName.Equals(username, StringComparison.OrdinalIgnoreCase);
        }

        var owner = fullName[..slash];
        return owner.Equals(username, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveRepositoryFullName(GitHubSearchIssueItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.Repository?.FullName))
        {
            return item.Repository.FullName.Trim();
        }

        if (string.IsNullOrWhiteSpace(item.RepositoryUrl))
        {
            return null;
        }

        const string prefix = "https://api.github.com/repos/";
        if (item.RepositoryUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return item.RepositoryUrl[prefix.Length..].Trim('/');
        }

        return null;
    }

    private async Task<List<GitHubCommitListItem>> FetchCommitsAsync(
        string owner,
        string repo,
        CancellationToken cancellationToken)
    {
        var commits = await TryFetchGitHubJsonAsync<List<GitHubCommitListItem>>(
            $"repos/{owner}/{repo}/commits?per_page={CommitPageSize}",
            cancellationToken);

        return commits ?? [];
    }

    private async Task<T?> TryFetchGitHubJsonAsync<T>(string requestUri, CancellationToken cancellationToken)
        where T : class
    {
        var client = httpClientFactory.CreateClient(GitHubHttpClientName);
        using var response = await client.GetAsync(requestUri, cancellationToken);

        if (response.StatusCode is System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogDebug(
                "GitHub telemetry request failed: {Uri} → {Status} {Body}",
                requestUri,
                (int)response.StatusCode,
                body);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }
}
