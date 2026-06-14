using GitShare.Api.Services;
using Xunit;

namespace GitShare.Api.Tests;

public sealed class DontrushitRegressionTests
{
    [Fact]
    public void WinFormsCatalog_with_docker_compose_prefers_winforms_stack()
    {
        var paths = WinFormsCatalogTree();
        var analysis = ProjectStackCatalog.Analyze(paths, "WinFormsCatalog");
        var manifest = TargetFileSignatureAnalyzer.BuildManifest("WinFormsCatalog", paths, "C#");

        Assert.Contains("WinForms", analysis.Framework, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DevOps", analysis.Framework, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Program.cs", analysis.KeyFiles, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("WinForms", manifest, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WinFormsCatalog_classified_as_pet_desktop_utility()
    {
        var paths = WinFormsCatalogTree();
        var manifest = TargetFileSignatureAnalyzer.BuildManifest("WinFormsCatalog", paths, "C#");
        var projectClass = ProjectClassClassifier.Classify("WinFormsCatalog", manifest);

        Assert.Equal(ProjectClassClassifier.UtilityAutomation, projectClass);
        Assert.DoesNotContain(
            "Консольное приложение",
            ProjectClassClassifier.DefaultTechnicalDebtForClass(projectClass, "WinFormsCatalog", manifest));
    }

    [Fact]
    public void WinFormsCatalog_devops_manifest_still_pet_desktop_utility()
    {
        const string manifest = """
            Repo: WinFormsCatalog
            Primary framework: DevOps (K8s/Docker)
            Suggested layout: Container / Deployment
            Entry Points: Program.cs
            Key Files: docker-compose.yml, Program.cs, WinFormsCatalog.csproj
            """;

        var projectClass = ProjectClassClassifier.Classify("WinFormsCatalog", manifest);

        Assert.Equal(ProjectClassClassifier.UtilityAutomation, projectClass);
    }

    [Fact]
    public void WinFormsCatalog_pet_desktop_level_capped_for_utility()
    {
        var facts = CodeEvidenceFacts.From(
            ["Program.cs", "WinFormsCatalog.Core/Data/AppDbContext.cs", "Services/CatalogService.cs"],
            "Primary framework: .NET, WinForms\nServices/\nRepository",
            new Dictionary<string, string>
            {
                ["WinFormsCatalog.Core/Data/AppDbContext.cs"] = "public class AppDbContext : DbContext { }",
            });

        var forensics = new RepositoryForensics(
            "WinFormsCatalog",
            string.Empty,
            string.Empty,
            string.Empty,
            "Primary framework: .NET, WinForms\nProgram.cs\nWinFormsCatalog.csproj",
            [],
            [],
            [],
            "{}",
            StackEvidenceProfile.WinForms,
            Facts: facts);

        var level = RepositoryLevelEvaluator.Evaluate(
            forensics,
            ProjectClassClassifier.UtilityAutomation,
            AuditContentLocale.Ru);

        Assert.True(level.Score is >= 38 and <= 48, $"EF pet desktop utility expected 38-48, got {level.Score}");
    }

    [Fact]
    public void GitShare_DevCard_docker_compose_still_fullstack_production()
    {
        var paths = ProjectClassClassifierTests.GitShareLikeTree();
        var analysis = ProjectStackCatalog.Analyze(paths, "GitShare_DevCard");
        var manifest = TargetFileSignatureAnalyzer.BuildManifest("GitShare_DevCard", paths, "C#");

        Assert.Contains("ASP.NET", analysis.Framework, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ProjectClassClassifier.ProductionApp, ProjectClassClassifier.Classify("GitShare_DevCard", manifest));
    }

    private static List<string> WinFormsCatalogTree() =>
    [
        "docker-compose.yml",
        "WinFormsCatalog/Program.cs",
        "WinFormsCatalog/WinFormsCatalog.csproj",
        "WinFormsCatalog/Form1.cs",
        "WinFormsCatalog/Helpers/DbHelper.cs",
    ];
}
