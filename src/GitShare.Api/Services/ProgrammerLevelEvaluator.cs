using GitShare.Api.Models;

namespace GitShare.Api.Services;

/// <summary>
/// Оценка уровня по открытому GitHub-портфелю с учётом трека (GameDev, DevOps, OSS, обучение).
/// </summary>
internal static class ProgrammerLevelEvaluator
{
    private const double MinSignalConfidence = 0.5;

    private static readonly string[] DevOpsMarkers =
    [
        "devops", "terraform", "ansible", "kubernetes", "k8s", "docker", "pulumi",
        "helm", "cloudformation", "yaml", "infrastructure"
    ];

    public static ProgrammerLevelInfo Evaluate(DevCardProfile profile)
    {
        var signals = PortfolioSignals.FromProfile(profile);
        var factors = new List<string>();

        var rawScore = 0;
        rawScore += ScoreOpenSourceInfluence(profile, signals, factors);
        rawScore += ScorePortfolioDepth(profile, signals, factors);
        rawScore += ScoreTechnicalBreadth(signals, factors);
        rawScore += ScoreProductionCraft(profile.AuditData, signals, factors);
        rawScore += ScoreCommunitySignals(profile, signals, factors);
        rawScore = Math.Clamp(rawScore, 0, 100);

        var confidence = ComputeSignalConfidence(profile, signals, out var confidenceNote);
        if (!string.IsNullOrEmpty(confidenceNote))
        {
            factors.Insert(0, confidenceNote);
        }

        var adjustedScore = (int)Math.Round(rawScore * confidence, MidpointRounding.AwayFromZero);
        adjustedScore = ApplyTrackScoreBounds(adjustedScore, signals);
        adjustedScore = ApplyBarrierScoreFloors(adjustedScore, signals);
        adjustedScore = Math.Clamp(adjustedScore, 0, 100);

        var scoreTier = MapScoreToTier(adjustedScore);
        var barrierFloorTier = MapBarrierFloorTier(signals);
        var tier = MaxTier(scoreTier, barrierFloorTier);
        var maxAllowedCode = ResolveMaxAllowedTier(signals);
        var diagramCap = GetDiagramTierCap(adjustedScore, profile);
        var finalTier = CapTier(tier, MinTierCode(maxAllowedCode, diagramCap));

        if (signals.Track == PortfolioTrack.Learning && !MeetsLearningJuniorBarriers(signals))
        {
            finalTier = CapTier(finalTier, "trainee");
        }

        return new ProgrammerLevelInfo
        {
            Code = finalTier.Code,
            Title = finalTier.Title,
            Score = adjustedScore,
            RawScore = rawScore,
            SignalConfidence = confidence,
            IsLowConfidence = confidence < 1.0,
            Rationale = factors.Count > 0
                ? string.Join("; ", factors.Take(6))
                : "Недостаточно данных по репозиториям."
        };
    }

    private static int ScoreOpenSourceInfluence(
        DevCardProfile profile,
        PortfolioSignals signals,
        List<string> factors)
    {
        var stars = signals.TotalStars;
        if (stars <= 0)
        {
            return 0;
        }

        var points = stars switch
        {
            >= 50_000 => 28,
            >= 10_000 => 24,
            >= 5_000 => 20,
            >= 2_000 => 16,
            >= 500 => 12,
            >= 150 => 9,
            >= 50 => 6,
            >= 15 => 4,
            >= 5 => 2,
            _ => 1
        };

        if (signals.MaxTopRepoStars >= 1_000)
        {
            points += 2;
        }

        if (signals.GameDevMaturity >= GameDevMaturity.Specialist)
        {
            points = (int)Math.Round(points * 0.92, MidpointRounding.AwayFromZero);
            factors.Add($"⭐ {stars} (GameDev OSS — зрелый портфель)");
        }
        else if (signals.Track == PortfolioTrack.GameDev)
        {
            points = (int)Math.Round(points * 0.55, MidpointRounding.AwayFromZero);
            factors.Add($"⭐ {stars} (GameDev — ранний портфель)");
        }
        else if (signals.Track == PortfolioTrack.Learning)
        {
            points = Math.Min(points, 3);
            factors.Add($"⭐ {stars} (учебный портфель)");
        }
        else
        {
            factors.Add($"⭐ {stars} OSS-влияние");
        }

        return Math.Min(points, 30);
    }

    private static int ScorePortfolioDepth(
        DevCardProfile profile,
        PortfolioSignals signals,
        List<string> factors)
    {
        var points = Math.Min(signals.ProductionAppCount * 8, 24);

        var substantiveTop = profile.TopRepositories.Count(r => r.Stars >= 20);
        points += Math.Min(substantiveTop * 3, 9);

        var highImpactTools = profile.TopRepositories.Count(r => r.Stars >= 40);
        if (signals.Track == PortfolioTrack.GameDev && highImpactTools > 0)
        {
            points += Math.Min(highImpactTools * 3, 9);
            factors.Add($"{highImpactTools} публичных Unity/OSS-репо (★≥40)");
        }

        if (signals.DevOpsFrameworkCount >= 2 && signals.TotalStars >= 80)
        {
            points += 4;
            factors.Add("DevOps-портфель с подтверждённым влиянием");
        }

        if (signals.ProductionAppCount > 0)
        {
            factors.Add($"{signals.ProductionAppCount} production в аудите");
        }
        else if (substantiveTop > 0)
        {
            factors.Add($"{substantiveTop} топ-репо с ★≥20");
        }

        return Math.Min(points, 24);
    }

    private static int ScoreTechnicalBreadth(PortfolioSignals signals, List<string> factors)
    {
        var families = signals.DistinctStackFamilies;
        var points = families switch
        {
            >= 5 => 12,
            4 => 10,
            3 => 8,
            2 => 5,
            1 => 2,
            _ => 0
        };

        if (signals.Track == PortfolioTrack.Learning)
        {
            points = Math.Min(points, 4);
        }

        if (points > 0)
        {
            factors.Add($"{families} техн. семейств");
        }

        return Math.Min(points, 14);
    }

    private static int ScoreProductionCraft(
        StructuredAuditResponse? audit,
        PortfolioSignals signals,
        List<string> factors)
    {
        if (audit?.Projects is not { Count: > 0 } projects)
        {
            return 0;
        }

        var productionProjects = projects
            .Where(p => EnterpriseAuditLexicon.IsProductionClass(p.ProjectClass))
            .ToList();

        if (productionProjects.Count == 0)
        {
            if (signals.Track is PortfolioTrack.DevOps or PortfolioTrack.OpenSource)
            {
                return 4;
            }

            if (signals.GameDevMaturity >= GameDevMaturity.Specialist)
            {
                var polishedTools = projects.Count(p =>
                    p.Framework.Contains("Unity", StringComparison.OrdinalIgnoreCase) &&
                    p.KeyFiles is { Count: >= 5 } &&
                    string.Equals(p.DebtSeverity, "CLEAN", StringComparison.OrdinalIgnoreCase));
                if (polishedTools > 0)
                {
                    factors.Add($"craft: {polishedTools} зрелых Unity-инструментов");
                    return Math.Min(6 + polishedTools * 2, 14);
                }
            }

            return 0;
        }

        var points = 6;
        foreach (var project in productionProjects)
        {
            switch (project.DebtSeverity?.Trim())
            {
                case "Critical":
                    points -= 5;
                    break;
                case "Warning":
                    points -= 2;
                    break;
                case "Minor":
                case "CLEAN":
                    points += 3;
                    break;
            }

            if (project.KeyFiles is { Count: > 0 })
            {
                points += 1;
            }
        }

        factors.Add($"craft: {productionProjects.Count} production");
        return Math.Clamp(points, 0, 20);
    }

    private static int ScoreCommunitySignals(
        DevCardProfile profile,
        PortfolioSignals signals,
        List<string> factors)
    {
        var points = 0;

        if (signals.ConventionalCommits)
        {
            points += 6;
            factors.Add("Conventional Commits");
        }
        else if (!string.IsNullOrWhiteSpace(profile.AuditData?.GitFormatStandard))
        {
            points += 2;
        }

        var externalPrs = signals.ExternalPullRequestCount;
        if (externalPrs > 0)
        {
            points += Math.Min(externalPrs, 8);
            factors.Add($"вклад в чужие репо ({externalPrs})");
        }

        if (signals.Track == PortfolioTrack.OpenSource && externalPrs >= 5)
        {
            points += 2;
        }

        return Math.Min(points, 16);
    }

    private static double ComputeSignalConfidence(
        DevCardProfile profile,
        PortfolioSignals signals,
        out string? note)
    {
        note = null;
        var confidence = 1.0;
        var penalties = new List<string>();

        if (profile.OwnRepositoryCount <= 5)
        {
            confidence -= 0.12;
            penalties.Add("мало собственных репо");
        }

        if (profile.TotalStars < 10)
        {
            confidence -= 0.12;
            penalties.Add("мало звёзд");
        }

        if (profile.LanguageStack.Count <= 1)
        {
            confidence -= 0.08;
            penalties.Add("узкий языковой стек");
        }

        if (signals.Track == PortfolioTrack.Learning && profile.OwnRepositoryCount >= 3)
        {
            confidence = Math.Max(confidence, 0.72);
        }

        if (signals.GameDevMaturity >= GameDevMaturity.Specialist)
        {
            confidence = Math.Max(confidence, 0.92);
        }

        confidence = Math.Max(confidence, MinSignalConfidence);

        if (penalties.Count > 0 && confidence < 1.0)
        {
            note = $"слабый сигнал портфеля (×{confidence:0.##}: {string.Join(", ", penalties)})";
        }

        return confidence;
    }

    private static int ApplyTrackScoreBounds(int score, PortfolioSignals signals)
    {
        if (signals.GameDevMaturity >= GameDevMaturity.Specialist)
        {
            score = Math.Max(score, 52);
        }
        else if (signals.GameDevMaturity == GameDevMaturity.Contributor)
        {
            score = Math.Max(score, 32);
        }

        if (signals.Track == PortfolioTrack.OpenSource)
        {
            score = signals.TotalStars switch
            {
                >= 50_000 => Math.Max(score, 72),
                >= 10_000 => Math.Max(score, 58),
                >= 2_000 => Math.Max(score, 50),
                _ => score
            };
        }

        if (signals.Track == PortfolioTrack.DevOps && signals.TotalStars >= 500)
        {
            score = Math.Max(score, 52);
        }

        return score;
    }

    private static int ApplyBarrierScoreFloors(int score, PortfolioSignals signals)
    {
        if (MeetsPrincipalBarriers(signals))
        {
            return Math.Max(score, 86);
        }

        if (MeetsLeadBarriers(signals))
        {
            return Math.Max(score, 68);
        }

        if (MeetsSeniorBarriers(signals))
        {
            return Math.Max(score, 50);
        }

        return score;
    }

    private static string ResolveMaxAllowedTier(PortfolioSignals signals)
    {
        var ceiling = signals.Track switch
        {
            PortfolioTrack.Nascent => "trainee",
            PortfolioTrack.Learning => MeetsLearningJuniorBarriers(signals) ? "junior" : "trainee",
            PortfolioTrack.GameDev => ResolveGameDevCeiling(signals),
            _ => "principal"
        };

        if (MeetsPrincipalBarriers(signals))
        {
            ceiling = MinTierRank(ceiling, "principal");
        }
        else if (MeetsLeadBarriers(signals))
        {
            ceiling = MinTierRank(ceiling, "lead");
        }
        else if (MeetsSeniorBarriers(signals))
        {
            ceiling = MinTierRank(ceiling, "senior");
        }
        else if (MeetsMiddleBarriers(signals))
        {
            ceiling = MinTierRank(ceiling, "middle");
        }
        else if (signals.OwnRepositoryCount >= 1 && QualifiesForJuniorCeiling(signals))
        {
            ceiling = MinTierRank(ceiling, "junior");
        }
        else
        {
            ceiling = "trainee";
        }

        if (signals.Track == PortfolioTrack.Learning)
        {
            ceiling = MinTierRank(
                ceiling,
                MeetsLearningJuniorBarriers(signals) ? "junior" : "trainee");
        }

        return ceiling;
    }

    /// <summary>
    /// Junior на Learning-треке — только при «взрослом» публичном следе, не за счёт 2–3 console pet.
    /// </summary>
    private static bool MeetsLearningJuniorBarriers(PortfolioSignals signals) =>
        signals.ProductionAppCount >= 1 ||
        signals.QaProjectCount >= 1 ||
        signals.TotalStars >= 5 ||
        signals.ExternalPullRequestCount >= 1;

    private static bool QualifiesForJuniorCeiling(PortfolioSignals signals) =>
        signals.Track switch
        {
            PortfolioTrack.Nascent => false,
            PortfolioTrack.Learning => MeetsLearningJuniorBarriers(signals),
            _ => true
        };

    private static string ResolveGameDevCeiling(PortfolioSignals signals) => signals.GameDevMaturity switch
    {
        GameDevMaturity.Luminary => "lead",
        GameDevMaturity.Specialist => "senior",
        GameDevMaturity.Contributor => "middle",
        _ => "junior"
    };

    private static bool MeetsPrincipalBarriers(PortfolioSignals signals)
    {
        if (signals.Track == PortfolioTrack.DevOps && signals.ProductionAppCount == 0)
        {
            return false;
        }

        if (signals.Track == PortfolioTrack.GameDev)
        {
            return signals.GameDevMaturity == GameDevMaturity.Luminary &&
                   signals.TotalStars >= 5_000 &&
                   signals.ProductionAppCount >= 1;
        }

        return (signals.TotalStars >= 25_000 &&
                (signals.ProductionAppCount >= 1 || signals.ExternalPullRequestCount >= 8)) ||
               (signals.TotalStars >= 5_000 &&
                signals.ProductionAppCount >= 1 &&
                signals.ExternalPullRequestCount >= 3) ||
               (signals.MaxTopRepoStars >= 15_000 && signals.ProductionAppCount >= 1);
    }

    private static bool MeetsLeadBarriers(PortfolioSignals signals)
    {
        if (signals.Track == PortfolioTrack.GameDev)
        {
            return signals.GameDevMaturity == GameDevMaturity.Luminary && signals.TotalStars >= 2_000;
        }

        if (signals.Track == PortfolioTrack.DevOps)
        {
            return signals.TotalStars >= 300;
        }

        return signals.TotalStars >= 2_000 &&
               (signals.ProductionAppCount >= 1 ||
                signals.ExternalPullRequestCount >= 3 ||
                signals.DevOpsFrameworkCount >= 2);
    }

    private static bool MeetsSeniorBarriers(PortfolioSignals signals)
    {
        if (signals.Track == PortfolioTrack.GameDev)
        {
            return signals.GameDevMaturity >= GameDevMaturity.Specialist ||
                   (signals.TotalStars >= 100 &&
                    signals.MaxTopRepoStars >= 35 &&
                    (signals.ProductionAppCount >= 1 || signals.HighImpactRepoCount >= 2));
        }

        if (signals.Track == PortfolioTrack.DevOps)
        {
            return signals.TotalStars >= 80;
        }

        if (signals.Track == PortfolioTrack.OpenSource)
        {
            return signals.TotalStars >= 200;
        }

        return signals.TotalStars >= 120 ||
               (signals.ProductionAppCount >= 2 && signals.TotalStars >= 25) ||
               (signals.ProductionAppCount >= 1 && signals.MaxTopRepoStars >= 100);
    }

    private static bool MeetsMiddleBarriers(PortfolioSignals signals) =>
        signals.TotalStars >= 12 ||
        signals.ProductionAppCount >= 1 ||
        signals.GameDevMaturity >= GameDevMaturity.Contributor ||
        (signals.OwnRepositoryCount >= 6 && signals.DistinctStackFamilies >= 2);

    private static string? GetDiagramTierCap(int adjustedScore, DevCardProfile profile)
    {
        var productionCount = profile.AuditData?.Projects?.Count(p =>
            EnterpriseAuditLexicon.IsProductionClass(p.ProjectClass)) ?? 0;

        if (adjustedScore >= 50 && profile.TotalStars == 0 && productionCount == 0)
        {
            return "trainee";
        }

        if (adjustedScore >= 38 &&
            profile.TotalStars == 0 &&
            productionCount <= 1 &&
            profile.OwnRepositoryCount <= 8)
        {
            return "junior";
        }

        if (adjustedScore >= 32 &&
            profile.LanguageStack.Count <= 1 &&
            profile.TotalStars < 20 &&
            productionCount == 0)
        {
            return "trainee";
        }

        return null;
    }

    private static string MinTierCode(string a, string? b)
    {
        if (b is null || TierRank(a) <= TierRank(b))
        {
            return a;
        }

        return b;
    }

    private static string MinTierRank(string currentCeiling, string candidate) =>
        TierRank(candidate) < TierRank(currentCeiling) ? candidate : currentCeiling;

    private static (string Code, string Title) MapBarrierFloorTier(PortfolioSignals signals)
    {
        if (MeetsPrincipalBarriers(signals))
        {
            return MapCodeToTier("principal");
        }

        if (MeetsLeadBarriers(signals))
        {
            return MapCodeToTier("lead");
        }

        if (MeetsSeniorBarriers(signals))
        {
            return MapCodeToTier("senior");
        }

        if (MeetsMiddleBarriers(signals))
        {
            return MapCodeToTier("middle");
        }

        return MapCodeToTier("trainee");
    }

    private static (string Code, string Title) MaxTier(
        (string Code, string Title) a,
        (string Code, string Title) b) =>
        TierRank(a.Code) >= TierRank(b.Code) ? a : b;

    private static (string Code, string Title) CapTier(
        (string Code, string Title) tier,
        string maxAllowedCode)
    {
        if (TierRank(tier.Code) <= TierRank(maxAllowedCode))
        {
            return tier;
        }

        return MapCodeToTier(maxAllowedCode);
    }

    private static int TierRank(string code) => code switch
    {
        "principal" => 5,
        "lead" => 4,
        "senior" => 3,
        "middle" => 2,
        "junior" => 1,
        _ => 0
    };

    private static (string Code, string Title) MapCodeToTier(string code) => code switch
    {
        "principal" => ("principal", "Принципал"),
        "lead" => ("lead", "Тимлид"),
        "senior" => ("senior", "Сеньор"),
        "middle" => ("middle", "Мидл"),
        "junior" => ("junior", "Джуниор"),
        _ => ("trainee", "Стажёр")
    };

    private static (string Code, string Title) MapScoreToTier(int score) =>
        score switch
        {
            >= 86 => ("principal", "Принципал"),
            >= 68 => ("lead", "Тимлид"),
            >= 50 => ("senior", "Сеньор"),
            >= 30 => ("middle", "Мидл"),
            >= 18 => ("junior", "Джуниор"),
            _ => ("trainee", "Стажёр")
        };

    private enum PortfolioTrack
    {
        Nascent,
        Learning,
        GameDev,
        DevOps,
        OpenSource,
        Enterprise
    }

    private enum GameDevMaturity
    {
        Hobbyist = 0,
        Contributor = 1,
        Specialist = 2,
        Luminary = 3
    }

    private sealed class PortfolioSignals
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

                if (IsDevOpsFramework(project.Framework))
                {
                    devOps++;
                }
            }

            var families = CollectStackFamilies(audit);
            foreach (var metric in profile.LanguageStack)
            {
                var family = ClassifyLanguageFamily(metric.Language);
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

    private static bool IsDevOpsFramework(string framework)
    {
        if (string.IsNullOrWhiteSpace(framework))
        {
            return false;
        }

        return DevOpsMarkers.Any(marker =>
            framework.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static HashSet<string> CollectStackFamilies(StructuredAuditResponse? audit)
    {
        var families = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (audit?.Projects is not { Count: > 0 } projects)
        {
            return families;
        }

        foreach (var project in projects)
        {
            var family = ClassifyStackFamily(project.Framework, project.RepoName);
            if (!string.Equals(family, "unknown", StringComparison.OrdinalIgnoreCase))
            {
                families.Add(family);
            }
        }

        return families;
    }

    private static bool IsUndefinedFramework(string framework) =>
        string.IsNullOrWhiteSpace(framework) ||
        framework.Contains("не определён", StringComparison.OrdinalIgnoreCase);

    private static string ClassifyLanguageFamily(string language) =>
        language.Trim().ToLowerInvariant() switch
        {
            "c#" or "csharp" => ".NET",
            "java" => "Java",
            "javascript" or "typescript" => "JS/TS",
            "python" => "Python",
            "go" => "Go",
            "c" or "c++" => "C/C++",
            "html" or "css" => "Web",
            "ruby" => "Ruby",
            "rust" => "Rust",
            "kotlin" => "Kotlin",
            "shell" or "powershell" => "DevOps",
            "dockerfile" or "hcl" => "DevOps",
            _ => string.IsNullOrWhiteSpace(language) ? "unknown" : language
        };

    private static string ClassifyStackFamily(string framework, string repoName = "")
    {
        var combined = $"{framework} {repoName}";

        if (IsDevOpsFramework(combined) ||
            combined.Contains("ansible", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("terraform", StringComparison.OrdinalIgnoreCase))
        {
            return "DevOps";
        }

        if (combined.Contains("playwright", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("selenium", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("cypress", StringComparison.OrdinalIgnoreCase))
        {
            return "Test automation";
        }

        if (combined.Contains("python", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("django", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("flask", StringComparison.OrdinalIgnoreCase) ||
            repoName.Contains("bot", StringComparison.OrdinalIgnoreCase))
        {
            return "Python";
        }

        if (combined.Contains("javascript", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("typescript", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("node", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("react", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("laravel", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("php", StringComparison.OrdinalIgnoreCase))
        {
            return "JS/TS";
        }

        if (combined.Contains("go ", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("golang", StringComparison.OrdinalIgnoreCase) ||
            framework.Equals("Go", StringComparison.OrdinalIgnoreCase))
        {
            return "Go";
        }

        if (framework.Contains("WinForms", StringComparison.OrdinalIgnoreCase))
        {
            return "WinForms";
        }

        if (framework.Contains("WPF", StringComparison.OrdinalIgnoreCase))
        {
            return "WPF";
        }

        if (framework.Contains("Spring", StringComparison.OrdinalIgnoreCase) ||
            framework.Contains("Java", StringComparison.OrdinalIgnoreCase))
        {
            return "Java";
        }

        if (framework.Contains("Unity", StringComparison.OrdinalIgnoreCase))
        {
            return "Unity";
        }

        if (framework.Contains("ASP.NET", StringComparison.OrdinalIgnoreCase))
        {
            return "ASP.NET";
        }

        if (framework.Contains("Console", StringComparison.OrdinalIgnoreCase) ||
            framework.Equals(".NET", StringComparison.OrdinalIgnoreCase))
        {
            return "Console/.NET";
        }

        return IsUndefinedFramework(framework) ? "unknown" : ".NET";
    }
}
