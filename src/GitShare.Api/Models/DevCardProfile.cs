namespace GitShare.Api.Models;

public class DevCardProfile
{
    /// <summary>Язык нарратива аудита: <c>ru</c> или <c>en</c>.</summary>
    public string ContentLocale { get; set; } = "ru";

    public string Username { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int PublicRepos { get; set; }
    public int TotalStars { get; set; }
    public int TotalForks { get; set; }
    public double ContributionRatio { get; set; }
    public int OwnRepositoryCount { get; set; }
    public int ForkedRepositoryCount { get; set; }
    public int SmallPetProjects { get; set; }
    public int MediumProjects { get; set; }
    public int ProductionScaleProjects { get; set; }
    public StructuredAuditResponse? AuditData { get; set; }
    public GitHubActivityTelemetry? ActivityTelemetry { get; set; }
    public List<LanguageMetric> LanguageStack { get; set; } = [];
    public List<HourlyActivity> CommitRhythm { get; set; } = [];
    public List<RepoSummary> TopRepositories { get; set; } = [];
    public ProgrammerLevelInfo ProgrammerLevel { get; set; } = new();

    /// <summary>UTC-время последнего анализа (для отображения свежести кэша).</summary>
    public DateTime? AnalyzedAtUtc { get; set; }

    /// <summary>true — ответ из кэша; false — полный pipeline только что.</summary>
    public bool ServedFromCache { get; set; }
}

public class LanguageMetric
{
    public string Language { get; set; } = string.Empty;
    public double Percentage { get; set; }
}

public class HourlyActivity
{
    public int Hour { get; set; }
    public int CommitCount { get; set; }
}

public class RepoSummary
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Stars { get; set; }
    public string Language { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
