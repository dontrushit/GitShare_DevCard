using GitShare.Api.Models;

namespace GitShare.Api.Services;

/// <summary>
/// Свёрнутые сигналы публичного портфеля: вход для скоринга, барьеров и потолков уровня.
/// </summary>
internal sealed class PortfolioSignals
{
    public PortfolioTrack Track { get; init; }
    public GameDevMaturity GameDevMaturity { get; init; }
    public int TotalStars { get; init; }
    public int MaxTopRepoStars { get; init; }
    public int HighImpactRepoCount { get; init; }
    public int OwnRepositoryCount { get; init; }
    public int ProductionAppCount { get; init; }
    public int QaProjectCount { get; init; }
    public int UnityProjectCount { get; init; }
    public int DevOpsFrameworkCount { get; init; }
    public int ExternalPullRequestCount { get; init; }
    public int DistinctStackFamilies { get; init; }
    public bool ConventionalCommits { get; init; }

    public static PortfolioSignals FromProfile(DevCardProfile profile)
    {
        var audit = profile.AuditData;
        var projects = audit?.Projects ?? [];

        var production = 0;
        var unity = 0;
        var devOps = 0;
        var qa = 0;

        foreach (var project in projects)
        {
            if (EnterpriseAuditLexicon.IsProductionClass(project.ProjectClass))
            {
                production++;
            }

            if (string.Equals(
                    project.ProjectClass,
                    ProjectClassClassifier.QaTesting,
                    StringComparison.OrdinalIgnoreCase))
            {
                qa++;
            }

            if (project.Framework.Contains("Unity", StringComparison.OrdinalIgnoreCase))
            {
                unity++;
            }

            if (PortfolioStackTaxonomy.IsDevOpsFramework(project.Framework))
            {
                devOps++;
            }
        }

        var families = PortfolioStackTaxonomy.CollectStackFamilies(audit);
        foreach (var metric in profile.LanguageStack)
        {
            var family = PortfolioStackTaxonomy.ClassifyLanguageFamily(metric.Language);
            if (!string.Equals(family, "unknown", StringComparison.OrdinalIgnoreCase))
            {
                families.Add(family);
            }
        }

        var totalStars = profile.TotalStars;
        var maxTop = profile.TopRepositories.Count > 0
            ? profile.TopRepositories.Max(r => r.Stars)
            : 0;
        var highImpact = profile.TopRepositories.Count(r => r.Stars >= 40);
        var substantiveTop = profile.TopRepositories.Count(r => r.Stars >= 20);

        var conventional = audit?.GitFormatStandard
            .Contains("Conventional", StringComparison.OrdinalIgnoreCase) == true;

        var externalPrs = profile.ActivityTelemetry?.ExternalPullRequests?.Count ?? 0;

        var gameDevMaturity = ResolveGameDevMaturity(
            totalStars,
            maxTop,
            highImpact,
            substantiveTop,
            production,
            unity,
            projects.Count);

        var track = ClassifyTrack(
            profile,
            production,
            unity,
            devOps,
            totalStars,
            maxTop,
            gameDevMaturity);

        return new PortfolioSignals
        {
            Track = track,
            GameDevMaturity = gameDevMaturity,
            TotalStars = totalStars,
            MaxTopRepoStars = maxTop,
            HighImpactRepoCount = highImpact,
            OwnRepositoryCount = profile.OwnRepositoryCount,
            ProductionAppCount = production,
            QaProjectCount = qa,
            UnityProjectCount = unity,
            DevOpsFrameworkCount = devOps,
            ExternalPullRequestCount = externalPrs,
            DistinctStackFamilies = families.Count,
            ConventionalCommits = conventional
        };
    }

    private static GameDevMaturity ResolveGameDevMaturity(
        int totalStars,
        int maxTop,
        int highImpact,
        int substantiveTop,
        int production,
        int unity,
        int auditedProjects)
    {
        if (auditedProjects == 0 || unity == 0)
        {
            return GameDevMaturity.Hobbyist;
        }

        if (totalStars >= 2_000 || maxTop >= 1_500)
        {
            return GameDevMaturity.Luminary;
        }

        if (totalStars >= 120 &&
            maxTop >= 40 &&
            (production >= 1 || highImpact >= 2 || substantiveTop >= 3))
        {
            return GameDevMaturity.Specialist;
        }

        if (totalStars >= 20 || production >= 1 || substantiveTop >= 1)
        {
            return GameDevMaturity.Contributor;
        }

        return GameDevMaturity.Hobbyist;
    }

    private static PortfolioTrack ClassifyTrack(
        DevCardProfile profile,
        int production,
        int unity,
        int devOps,
        int totalStars,
        int maxTopStars,
        GameDevMaturity gameDevMaturity)
    {
        if (profile.OwnRepositoryCount == 0 && profile.PublicRepos == 0)
        {
            return PortfolioTrack.Nascent;
        }

        var weakPublicPortfolio = totalStars < 20 &&
                                  maxTopStars < 100 &&
                                  profile.OwnRepositoryCount <= 25;
        var mostlyAcademic = production == 0 ||
                             (production <= 1 && profile.OwnRepositoryCount <= 8);

        if (weakPublicPortfolio && mostlyAcademic)
        {
            return PortfolioTrack.Learning;
        }

        var isGameDevDominant = unity >= 2 ||
                                (unity >= 1 && gameDevMaturity >= GameDevMaturity.Contributor);

        if (isGameDevDominant && gameDevMaturity < GameDevMaturity.Luminary &&
            totalStars < 5_000 && maxTopStars < 3_000)
        {
            return PortfolioTrack.GameDev;
        }

        var devOpsHeavy = devOps >= 2 ||
                          (devOps >= 1 && production == 0 && IsDevOpsDominantLanguageStack(profile));

        if (devOpsHeavy && totalStars >= 50)
        {
            return PortfolioTrack.DevOps;
        }

        if (totalStars >= 2_000 || maxTopStars >= 1_500)
        {
            return PortfolioTrack.OpenSource;
        }

        return PortfolioTrack.Enterprise;
    }

    private static bool IsDevOpsDominantLanguageStack(DevCardProfile profile)
    {
        var devOpsLangs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Shell", "PowerShell", "Dockerfile", "HCL", "YAML"
        };

        var devOpsShare = profile.LanguageStack
            .Where(m => devOpsLangs.Contains(m.Language))
            .Sum(m => m.Percentage);

        return devOpsShare >= 25;
    }
}
