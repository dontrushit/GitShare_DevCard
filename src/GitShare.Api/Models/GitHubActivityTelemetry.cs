namespace GitShare.Api.Models;

public class GitHubActivityTelemetry
{
    public List<string> RecentCommitMessages { get; set; } = [];
    public int CommitsInWorkingHours { get; set; }
    public int CommitsInOffHours { get; set; }
    public List<string> ExternalPullRequests { get; set; } = [];
    public int TotalStars { get; set; }
    public List<string> TopStarredRepos { get; set; } = [];
}
