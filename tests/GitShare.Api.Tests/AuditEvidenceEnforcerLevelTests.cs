using GitShare.Api.Models;
using GitShare.Api.Services;
using Xunit;

namespace GitShare.Api.Tests;

public sealed class AuditEvidenceEnforcerLevelTests
{
    [Fact]
    public void Apply_recomputes_repository_level_when_class_downgraded_to_utility()
    {
        var forensics = new RepositoryForensics(
            "WinFormsCatalog",
            string.Empty,
            string.Empty,
            string.Empty,
            "Primary framework: .NET, WinForms\nProgram.cs\nappsettings.json\nWinFormsCatalog.csproj",
            [],
            [],
            [],
            "{}",
            StackEvidenceProfile.WinForms,
            Facts: CodeEvidenceFacts.From(
                ["Program.cs", "appsettings.json", "Data/AppDbContext.cs"],
                "Primary framework: .NET, WinForms\nServices/\nRepository",
                new Dictionary<string, string>
                {
                    ["Data/AppDbContext.cs"] = "public class AppDbContext : DbContext { }",
                }));

        var evidence = StructuredAuditBuilder.BuildFromForensics([forensics], AuditContentLocale.Ru, portfolioTotalStars: 1);
        var winForms = evidence.Projects.Single(p =>
            p.RepoName.Equals("WinFormsCatalog", StringComparison.OrdinalIgnoreCase));

        var llm = new StructuredAuditResponse
        {
            Projects =
            [
                new ProjectAuditDetail
                {
                    RepoName = "WinFormsCatalog",
                    ProjectClass = ProjectClassClassifier.ProductionApp,
                    TechnicalDebt = "LLM debt text that is long enough to pass validation checks here.",
                    ArchitectureSummary = "LLM summary that should be replaced after level reconcile for pet desktop.",
                }
            ],
            CoreEngineeringFocus = "Desktop .NET samples",
        };

        var merged = AuditEvidenceEnforcer.Apply(
            llm,
            [forensics],
            new GitHubActivityTelemetry(),
            AuditContentLocale.Ru,
            portfolioTotalStars: 1);

        var project = merged.Projects.Single(p =>
            p.RepoName.Equals("WinFormsCatalog", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(ProjectClassClassifier.UtilityAutomation, project.ProjectClass);
        Assert.Equal(ProjectClassClassifier.UtilityAutomation, project.ProjectClass);
        Assert.True(project.RepositoryLevel?.Score is >= 38 and <= 48);
        Assert.Contains("WinFormsCatalog", project.ArchitectureSummary, StringComparison.OrdinalIgnoreCase);
    }
}
