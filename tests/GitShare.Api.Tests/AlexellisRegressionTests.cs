using GitShare.Api.Models;
using GitShare.Api.Services;
using Xunit;

namespace GitShare.Api.Tests;

public sealed class AlexellisRegressionTests
{
    [Fact]
    public void PickTopForAudit_includes_k3sup_and_arkade_flagships()
    {
        var repos = new List<RepoListMetadata>
        {
            MakeRepo("k3sup", "Go", stars: 7_384, sizeKb: 2_000),
            MakeRepo("arkade", "Go", stars: 4_580, sizeKb: 3_500),
            MakeRepo("growlab", "Python", stars: 12, sizeKb: 400),
            MakeRepo("HandsOnDocker", "Dockerfile", stars: 8, sizeKb: 300),
            MakeRepo("k8s-on-raspbian", null, stars: 200, sizeKb: 150),
            MakeRepo("random-notebook", "Jupyter Notebook", stars: 5, sizeKb: 100),
        };

        var picked = RepositorySelection.PickTopForAudit(repos, "alexellis", count: 4);
        var names = picked.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("k3sup", names);
        Assert.Contains("arkade", names);
    }

    [Fact]
    public void K3sup_level_is_middle_for_flagship_go_cli()
    {
        var forensics = new RepositoryForensics(
            "k3sup",
            string.Empty,
            string.Empty,
            string.Empty,
            "Primary framework: Go\ngo.mod\nmain.go",
            [],
            [],
            [],
            "{}",
            StackEvidenceProfile.ConsoleUtility,
            Stars: 7_384,
            Facts: null);

        var level = RepositoryLevelEvaluator.Evaluate(
            forensics,
            ProjectClassClassifier.UtilityAutomation,
            AuditContentLocale.Ru);

        Assert.Equal("middle", level.Code);
        Assert.True(level.Score >= 58, $"Expected >=58, got {level.Score}");
    }

    [Fact]
    public void K3sup_go_cli_severity_clean_without_structural_risks()
    {
        var forensics = new RepositoryForensics(
            "k3sup",
            string.Empty,
            string.Empty,
            string.Empty,
            "go.mod\nmain.go",
            [],
            [],
            [],
            "{}",
            StackEvidenceProfile.ConsoleUtility,
            Stars: 7_384,
            Facts: null);

        var severity = ArchitectureSeverityResolver.Resolve(
            forensics,
            ProjectClassClassifier.UtilityAutomation,
            "Warning",
            ["В выборке кода критичных нарушений не видно"]);

        Assert.Equal("CLEAN", severity);
        Assert.Contains("Go CLI", ProjectClassClassifier.DefaultTechnicalDebtForClass(
            ProjectClassClassifier.UtilityAutomation, "k3sup", forensics.TargetSignatureManifest));
    }

    private static RepoListMetadata MakeRepo(string name, string? language, int stars, int sizeKb) =>
        new(name, stars, language, Fork: false, sizeKb, ForksCount: 0, Description: null,
            HtmlUrl: $"https://github.com/alexellis/{name}", PushedAt: null, UpdatedAt: null);
}
