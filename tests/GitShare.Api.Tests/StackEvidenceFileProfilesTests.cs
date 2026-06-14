using GitShare.Api.Services;
using Xunit;

namespace GitShare.Api.Tests;

public sealed class StackEvidenceFileProfilesTests
{
    [Fact]
    public void FullStack_selects_api_controller_and_frontend()
    {
        var paths = ProjectClassClassifierTests.GitShareLikeTree();
        var manifest = TargetFileSignatureAnalyzer.BuildManifest("GitShare_DevCard", paths, "C#");
        var profile = StackEvidenceProfileResolver.Resolve(manifest, paths);

        Assert.Equal(StackEvidenceProfile.FullStackDotNetReact, profile);

        var selected = StackEvidenceFileProfiles.SelectLlmSourcePaths(profile, paths, manifest);

        Assert.Contains(selected, p => p.EndsWith("Program.cs", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(selected, p => p.Contains("Controllers", StringComparison.OrdinalIgnoreCase));
        Assert.True(
            selected.Any(p => p.EndsWith("App.tsx", StringComparison.OrdinalIgnoreCase)) ||
            selected.Any(p => p.EndsWith("DbContext.cs", StringComparison.OrdinalIgnoreCase)),
            $"Expected App.tsx or DbContext in: {string.Join(", ", selected)}");
    }

    [Fact]
    public void FullStack_infra_includes_docker_and_tests_project()
    {
        var paths = ProjectClassClassifierTests.GitShareLikeTree();
        var profile = StackEvidenceProfile.FullStackDotNetReact;
        var infra = StackEvidenceFileProfiles.SelectLlmInfraPaths(profile, paths);

        Assert.Contains(infra, p => p.Equals("docker-compose.yml", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(infra, p => p.Contains("Tests", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Wpf_selects_view_model_and_data_service()
    {
        var paths = new List<string>
        {
            "WpfPhonesCatalog/App.xaml.cs",
            "WpfPhonesCatalog/MainWindow.xaml.cs",
            "WpfPhonesCatalog/Services/DataService.cs",
            "WpfPhonesCatalog/Converters/PhoneConverter.cs",
            "WpfPhonesCatalog/ViewModels/MainViewModel.cs",
        };
        var manifest = TargetFileSignatureAnalyzer.BuildManifest("WpfPhonesCatalog", paths, "C#");
        var profile = StackEvidenceProfileResolver.Resolve(manifest, paths);
        var selected = StackEvidenceFileProfiles.SelectLlmSourcePaths(profile, paths, manifest);

        Assert.Equal(StackEvidenceProfile.Wpf, profile);
        Assert.Contains(selected, p => p.Contains("DataService", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(selected, p => p.Contains("MainWindow", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Llm_selector_skips_non_production()
    {
        var selection = LlmEvidenceFileSelector.Select(
            "notes",
            ["readme.md"],
            "Repo: notes\nPrimary framework: HTML/CSS (static)",
            ProjectClassClassifier.DocOpsKnowledgeBase);

        Assert.Empty(selection.SourcePaths);
    }
}
