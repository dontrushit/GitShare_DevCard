using GitShare.Api.Services;
using Xunit;

namespace GitShare.Api.Tests;

public sealed class AuditNarrativeStackTests
{
    [Fact]
    public void FullStack_monorepo_is_not_marked_utility_test_stack()
    {
        var analysis = ProjectStackCatalog.Analyze(ProjectClassClassifierTests.GitShareLikeTree(), "GitShare_DevCard");

        Assert.False(analysis.IsScriptTestOrInfraUtility);
        Assert.Contains("ASP.NET", analysis.Framework, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GitShare_manifest_does_not_trigger_mvvm_converter_interview_trap()
    {
        var manifest = TargetFileSignatureAnalyzer.BuildManifest(
            "GitShare_DevCard",
            ProjectClassClassifierTests.GitShareLikeTree(),
            "C#");

        Assert.DoesNotContain(
            "MVVM",
            StructuredAuditNarrativesEn.InterviewQuestion(manifest, "GitShare_DevCard", ["Program.cs"]));

        var debt = StructuredAuditNarrativesEn.TechnicalDebt(manifest);
        Assert.DoesNotContain("Utility, tests, or IaC", debt);
        Assert.DoesNotContain("enterprise layers", debt);
    }

    [Fact]
    public void Patterns_line_label_does_not_count_as_converter_artifact()
    {
        var manifest = """
            Repo: sample
            Primary framework: .NET, WPF
            Patterns (Controller/Hub/DTO/Converter): none
            Stack signals: WPF (App.xaml)
            """;

        Assert.False(ManifestSignalParser.PatternsLineContains(manifest, "Converter"));
        Assert.False(ManifestSignalParser.ManifestHasWpfConverterArtifacts(manifest));
    }
}
