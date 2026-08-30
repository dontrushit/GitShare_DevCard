using GitShare.Api.Models;

using static GitShare.Api.Services.ProgrammerLevelBarriers;
using static GitShare.Api.Services.ProgrammerLevelTierScale;

namespace GitShare.Api.Services;

/// <summary>
/// Оценка уровня по открытому GitHub-портфелю с учётом трека (GameDev, DevOps, OSS, обучение).
/// Скоринг вынесен в <see cref="ProgrammerLevelScoring"/>, барьеры — в
/// <see cref="ProgrammerLevelBarriers"/>, шкала грейдов — в <see cref="ProgrammerLevelTierScale"/>.
/// </summary>
internal static class ProgrammerLevelEvaluator
{
    public static ProgrammerLevelInfo Evaluate(DevCardProfile profile)
    {
        var signals = PortfolioSignals.FromProfile(profile);
        var factors = new List<string>();

        var rawScore = ProgrammerLevelScoring.ComputeRawScore(profile, signals, factors);

        var confidence = ProgrammerLevelScoring.ComputeSignalConfidence(
            profile,
            signals,
            out var confidenceNote);
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
}
