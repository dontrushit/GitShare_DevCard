using System.Text.Json;
using GitShare.Api.Models;

namespace GitShare.Api.Services;

internal static class LevelSummaryPayloadBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Build(DevCardProfile profile, ProgrammerLevelInfo level)
    {
        var audit = profile.AuditData;
        var productionCount = audit?.Projects?.Count(p =>
            EnterpriseAuditLexicon.IsProductionClass(p.ProjectClass)) ?? 0;

        var payload = new
        {
            Username = profile.Username,
            AssignedLevel = new
            {
                level.Code,
                level.Title,
                level.Score,
                level.RawScore,
                level.SignalConfidence,
                level.IsLowConfidence,
                level.Rationale
            },
            Portfolio = new
            {
                profile.PublicRepos,
                profile.OwnRepositoryCount,
                profile.TotalStars,
                profile.TotalForks,
                Languages = profile.LanguageStack
                    .Take(5)
                    .Select(m => $"{m.Language} {m.Percentage:0}%"),
                TopRepositories = profile.TopRepositories
                    .Take(4)
                    .Select(r => new { r.Name, r.Stars, r.Language }),
                profile.ProductionScaleProjects,
                profile.MediumProjects,
                profile.SmallPetProjects
            },
            Audit = audit is null
                ? null
                : new
                {
                    audit.CoreEngineeringFocus,
                    audit.ExperienceProfile,
                    audit.OpenSourceImpact,
                    audit.GitFormatStandard,
                    ProductionProjectCount = productionCount,
                    ProjectClasses = audit.Projects
                        .Take(6)
                        .Select(p => new { p.RepoName, p.ProjectClass, p.DebtSeverity })
                },
            ExternalPullRequests = profile.ActivityTelemetry?.ExternalPullRequests?.Count ?? 0
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        return LlmAuditPayloadBuilder.WrapUntrustedEvidence(json);
    }
}
