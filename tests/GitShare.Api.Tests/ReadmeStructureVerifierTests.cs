using GitShare.Api.Models;
using GitShare.Api.Services;
using Xunit;

namespace GitShare.Api.Tests;

public sealed class ReadmeStructureVerifierTests
{
    [Fact]
    public void Analyze_downgrades_pet_desktop_with_warning_debt_and_zero_stars()
    {
        var analysis = ReadmeStructureVerifier.Analyze(
            "WinFormsCatalog",
            "Simple catalog app for learning WinForms.",
            "WinForms, .NET",
            "Warning",
            portfolioTotalStars: 0);

        Assert.True(analysis.ShouldDowngradeForScoring);
    }

    [Fact]
    public void Analyze_detects_readme_architecture_mismatch()
    {
        var analysis = ReadmeStructureVerifier.Analyze(
            "WpfPhonesCatalog",
            "Built with Clean Architecture, SOLID and MVVM best practices.",
            "WPF, .NET, Program.cs, DbHelper.cs",
            "Warning",
            portfolioTotalStars: 0);

        Assert.True(analysis.HasStructureMismatch);
        Assert.True(analysis.ShouldDowngradeForScoring);
    }

    [Fact]
    public void AdjustProjectClass_keeps_asp_net_web()
    {
        var adjusted = ReadmeStructureVerifier.AdjustProjectClass(
            ProjectClassClassifier.ProductionApp,
            "MyApi",
            "Production REST API with layered architecture.",
            "ASP.NET Core, Controllers/, Services/, Repositories/",
            "Minor",
            portfolioTotalStars: 50);

        Assert.Equal(ProjectClassClassifier.ProductionApp, adjusted);
    }
}

public sealed class DontrushitLevelTests
{
    [Fact]
    public void Evaluate_DesktopPetPortfolio_IsNotSenior()
    {
        var profile = new DevCardProfile
        {
            Username = "dontrushit",
            OwnRepositoryCount = 4,
            PublicRepos = 4,
            TotalStars = 0,
            TotalForks = 0,
            LanguageStack = [new LanguageMetric { Language = "C#", Percentage = 100 }],
            TopRepositories =
            [
                new RepoSummary { Name = "WinFormsCatalog", Stars = 0, Language = "C#" },
                new RepoSummary { Name = "WpfPhonesCatalog", Stars = 0, Language = "C#" },
                new RepoSummary { Name = "TaskManagerApp", Stars = 0, Language = "C#" }
            ],
            AuditData = new StructuredAuditResponse
            {
                CoreEngineeringFocus = "Desktop .NET (WinForms/WPF)",
                Projects =
                [
                    new ProjectAuditDetail
                    {
                        RepoName = "WinFormsCatalog",
                        ProjectClass = ProjectClassClassifier.UtilityAutomation,
                        Framework = "WinForms, .NET",
                        DebtSeverity = "Warning",
                        TechnicalDebt = "DbHelper static, no DI."
                    },
                    new ProjectAuditDetail
                    {
                        RepoName = "WpfPhonesCatalog",
                        ProjectClass = ProjectClassClassifier.UtilityAutomation,
                        Framework = "WPF, .NET",
                        DebtSeverity = "Warning",
                        TechnicalDebt = "Logic in MainWindow.xaml.cs."
                    },
                    new ProjectAuditDetail
                    {
                        RepoName = "TaskManagerApp",
                        ProjectClass = ProjectClassClassifier.UtilityAutomation,
                        Framework = ".NET Console",
                        DebtSeverity = "CLEAN"
                    }
                ]
            }
        };

        var level = ProgrammerLevelEvaluator.Evaluate(profile);

        Assert.True(TierRank(level.Code) <= TierRank("junior"),
            $"Expected trainee/junior, got {level.Code} (score={level.Score}, rationale={level.Rationale})");
    }

    [Fact]
    public void Enforcer_downgrades_llm_production_for_pet_desktop()
    {
        var forensics = new List<RepositoryForensics>
        {
            new(
                "WinFormsCatalog",
                "Clean Architecture and SOLID principles in WinForms.",
                "tree",
                "commits",
                "WinForms, Program.cs, DbHelper.cs",
                [],
                [],
                [],
                "{}",
                StackEvidenceProfile.WinForms),
            new(
                "WpfPhonesCatalog",
                "MVVM WPF sample catalog.",
                "tree",
                "commits",
                "WPF, App.xaml, MainWindow.xaml.cs",
                [],
                [],
                [],
                "{}",
                StackEvidenceProfile.Wpf)
        };

        var llm = new StructuredAuditResponse
        {
            Projects =
            [
                new ProjectAuditDetail
                {
                    RepoName = "WinFormsCatalog",
                    ProjectClass = "Production App",
                    Framework = "WinForms",
                    DebtSeverity = "Warning",
                    TechnicalDebt = "No DI."
                },
                new ProjectAuditDetail
                {
                    RepoName = "WpfPhonesCatalog",
                    ProjectClass = "Production App",
                    Framework = "WPF",
                    DebtSeverity = "Warning",
                    TechnicalDebt = "Logic in code-behind."
                }
            ]
        };

        var merged = AuditEvidenceEnforcer.Apply(
            llm,
            forensics,
            new GitHubActivityTelemetry(),
            AuditContentLocale.Ru,
            portfolioTotalStars: 0);

        Assert.All(merged.Projects, p =>
            Assert.Equal(ProjectClassClassifier.UtilityAutomation, p.ProjectClass));

        var profile = new DevCardProfile
        {
            Username = "dontrushit",
            OwnRepositoryCount = 4,
            TotalStars = 0,
            LanguageStack = [new LanguageMetric { Language = "C#", Percentage = 100 }],
            AuditData = merged
        };

        var level = ProgrammerLevelEvaluator.Evaluate(profile);
        Assert.True(TierRank(level.Code) <= TierRank("junior"));
    }

    private static int TierRank(string code) => code switch
    {
        "senior" => 3,
        "middle" => 2,
        "junior" => 1,
        _ => 0
    };
}
