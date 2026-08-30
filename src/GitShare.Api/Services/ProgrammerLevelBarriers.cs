using GitShare.Api.Models;

using static GitShare.Api.Services.ProgrammerLevelTierScale;

namespace GitShare.Api.Services;

/// <summary>
/// Барьеры грейдов: нижние границы балла, потолки по треку и «диаграммные» ограничения.
/// </summary>
internal static class ProgrammerLevelBarriers
{
    public static int ApplyTrackScoreBounds(int score, PortfolioSignals signals)
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

    public static int ApplyBarrierScoreFloors(int score, PortfolioSignals signals)
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

    public static (string Code, string Title) MapBarrierFloorTier(PortfolioSignals signals)
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

    public static string ResolveMaxAllowedTier(PortfolioSignals signals)
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

    public static string? GetDiagramTierCap(int adjustedScore, DevCardProfile profile)
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

    /// <summary>
    /// Junior на Learning-треке — только при «взрослом» публичном следе, не за счёт 2–3 console pet.
    /// </summary>
    public static bool MeetsLearningJuniorBarriers(PortfolioSignals signals) =>
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
}
