using System.Text.Json.Serialization;

namespace GitShare.Api.Models;

internal sealed class GitHubTreeResponse
{
    [JsonPropertyName("tree")]
    public List<GitHubTreeItem>? Tree { get; set; }
}

internal sealed class GitHubTreeItem
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

internal sealed class GitHubCommitListItem
{
    [JsonPropertyName("commit")]
    public GitHubCommitDetail? Commit { get; set; }
}

internal sealed class GitHubCommitDetail
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("author")]
    public GitHubCommitAuthor? Author { get; set; }
}

internal sealed class GitHubCommitAuthor
{
    [JsonPropertyName("date")]
    public DateTimeOffset? Date { get; set; }
}

internal sealed class GitHubSearchIssuesResponse
{
    [JsonPropertyName("items")]
    public List<GitHubSearchIssueItem>? Items { get; set; }
}

internal sealed class GitHubSearchIssueItem
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("repository_url")]
    public string? RepositoryUrl { get; set; }

    [JsonPropertyName("repository")]
    public GitHubSearchIssueRepository? Repository { get; set; }
}

internal sealed class GitHubSearchIssueRepository
{
    [JsonPropertyName("full_name")]
    public string? FullName { get; set; }
}
