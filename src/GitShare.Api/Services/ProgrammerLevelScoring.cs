using GitShare.Api.Models;

namespace GitShare.Api.Services;

/// <summary>
/// Сырой скоринг портфеля по группам сигналов и достоверность этих сигналов.
/// </summary>
internal static class ProgrammerLevelScoring
{
    private const double MinSignalConfidence = 0.5;

    /// <summary>
    /// Суммирует вклад всех групп сигналов; побочно наполняет <paramref name="factors"/> пояснениями.
    /// </summary>
    public static int ComputeRawScore(
        DevCardProfile profile,
        PortfolioSignals signals,
        List<string> factors)
    {
        var rawScore = 0;
        rawScore += ScoreOpenSourceInfluence(signals, factors);
        rawScore += ScorePortfolioDepth(profile, signals, factors);
        rawScore += ScoreTechnicalBreadth(signals, factors);
        rawScore += ScoreProductionCraft(profile.AuditData, signals, factors);
        rawScore += ScoreCommunitySignals(profile, signals, factors);

        return Math.Clamp(rawScore, 0, 100);
    }

    public static double ComputeSignalConfidence(
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

    private static int ScoreOpenSourceInfluence(PortfolioSignals signals, List<string> factors)
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
}
