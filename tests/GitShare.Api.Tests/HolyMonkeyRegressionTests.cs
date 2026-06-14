using GitShare.Api.Models;
using GitShare.Api.Services;
using Xunit;

namespace GitShare.Api.Tests;

public sealed class HolyMonkeyRegressionTests
{
    [Fact]
    public void PickTopForAudit_includes_flagship_unity_repos()
    {
        var repos = new List<RepoListMetadata>
        {
            MakeRepo("unity-typed-scenes", "C#", stars: 81, sizeKb: 3_000),
            MakeRepo("unity-architecture-examples", "C#", stars: 53, sizeKb: 2_500),
            MakeRepo("Asteroids", "C#", stars: 73, sizeKb: 4_000),
            MakeRepo("slowsoap", "JavaScript", stars: 12, sizeKb: 800),
            MakeRepo("DissolveShader", "C#", stars: 5, sizeKb: 600),
            MakeRepo("youtube-example-ai-studio", "C#", stars: 2, sizeKb: 400),
            MakeRepo("random-cpp", "C", stars: 40, sizeKb: 12_000),
            MakeRepo("random-cpp-2", "C", stars: 35, sizeKb: 11_000),
        };

        var picked = RepositorySelection.PickTopForAudit(repos, "HolyMonkey", count: 4);
        var names = picked.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("unity-typed-scenes", names);
        Assert.True(
            names.Contains("Asteroids") || names.Contains("unity-architecture-examples"),
            $"Expected flagship C# repo, got: {string.Join(", ", names)}");
    }

    [Fact]
    public void DissolveShader_classified_as_unity_utility_not_production()
    {
        var manifest = "Primary framework: Unity\nSuggested layout: Unity Project\nShaderLab";
        var projectClass = ProjectClassClassifier.Classify("DissolveShader", manifest);

        Assert.Equal(ProjectClassClassifier.UtilityAutomation, projectClass);
        Assert.Contains("shader", ProjectClassClassifier.DefaultTechnicalDebtForClass(projectClass, "DissolveShader", manifest), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Asteroids_classified_as_unity_game_utility_with_composition_root_debt()
    {
        var manifest = "Primary framework: Unity\nSuggested layout: Unity Project\nCompositeRoot\nShipCompositeRoot";
        var projectClass = ProjectClassClassifier.Classify("Asteroids", manifest);

        Assert.Equal(ProjectClassClassifier.UtilityAutomation, projectClass);
        Assert.Contains("Composition Root", ProjectClassClassifier.DefaultTechnicalDebtForClass(projectClass, "Asteroids", manifest));
    }

    [Fact]
    public void Unity_typed_scenes_toolkit_severity_clean_without_structural_risks()
    {
        var facts = CodeEvidenceFacts.From(
            ["Assets/Plugins/TypedScenes/SceneAnalyzer.cs", "Assets/Plugins/TypedScenes/TypedScene.cs"],
            "Primary framework: Unity\nPlugins/",
            new Dictionary<string, string>
            {
                ["Assets/Plugins/TypedScenes/SceneAnalyzer.cs"] = "public class SceneAnalyzer { }",
                ["Assets/Plugins/TypedScenes/TypedScene.cs"] = "public class TypedScene { }",
            });

        var forensics = new RepositoryForensics(
            "unity-typed-scenes",
            string.Empty,
            string.Empty,
            string.Empty,
            "Primary framework: Unity\nPlugins/",
            [],
            [],
            [],
            "{}",
            StackEvidenceProfile.Unity,
            Stars: 81,
            Facts: facts);

        var severity = ArchitectureSeverityResolver.Resolve(
            forensics,
            ProjectClassClassifier.UtilityAutomation,
            "Warning",
            ["В выборке кода критичных нарушений не видно"]);

        Assert.Equal("CLEAN", severity);
    }

    private static RepoListMetadata MakeRepo(string name, string language, int stars, int sizeKb) =>
        new(name, stars, language, Fork: false, sizeKb, ForksCount: 0, Description: null,
            HtmlUrl: $"https://github.com/HolyMonkey/{name}", PushedAt: null, UpdatedAt: null);
}
