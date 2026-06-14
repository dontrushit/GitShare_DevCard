using GitShare.Api.Models;
using GitShare.Api.Services;
using Xunit;

namespace GitShare.Api.Tests;

public sealed class ProgrammerLevelEdgeCaseTests
{
    [Fact]
    public void Sparse_portfolio_marks_low_confidence()
    {
        var profile = new DevCardProfile
        {
            Username = "sparse-dev",
            OwnRepositoryCount = 1,
            PublicRepos = 1,
            TotalStars = 0,
            SmallPetProjects = 1,
            LanguageStack = [new LanguageMetric { Language = "C#", Percentage = 100 }],
            TopRepositories = [new RepoSummary { Name = "hello", Stars = 0, Language = "C#" }],
            AuditData = new StructuredAuditResponse
            {
                Projects =
                [
                    new ProjectAuditDetail
                    {
                        RepoName = "hello",
                        ProjectClass = "Utility / Automation",
                        Framework = ".NET, Console",
                        DebtSeverity = "CLEAN"
                    }
                ]
            }
        };

        var level = ProgrammerLevelEvaluator.Evaluate(profile);

        Assert.True(level.IsLowConfidence);
        Assert.True(level.SignalConfidence < 1.0);
        Assert.Equal("trainee", level.Code);
    }

    [Fact]
    public void Octocat_like_demo_profile_stays_within_wide_matrix_band()
    {
        var profile = new DevCardProfile
        {
            Username = "octocat",
            OwnRepositoryCount = 8,
            PublicRepos = 8,
            TotalStars = 12_000,
            MediumProjects = 2,
            ProductionScaleProjects = 1,
            LanguageStack =
            [
                new LanguageMetric { Language = "Ruby", Percentage = 55 },
                new LanguageMetric { Language = "JavaScript", Percentage = 45 }
            ],
            TopRepositories =
            [
                new RepoSummary { Name = "Spoon-Knife", Stars = 12_000, Language = "Ruby" },
                new RepoSummary { Name = "Hello-World", Stars = 2_000, Language = "Ruby" }
            ],
            AuditData = new StructuredAuditResponse
            {
                Projects =
                [
                    new ProjectAuditDetail
                    {
                        RepoName = "Spoon-Knife",
                        ProjectClass = "Utility / Automation",
                        Framework = "Ruby",
                        DebtSeverity = "CLEAN"
                    }
                ],
                GitFormatStandard = "Conventional Commits"
            }
        };

        var level = ProgrammerLevelEvaluator.Evaluate(profile);
        var allowed = new[] { "trainee", "junior", "middle", "senior" };

        Assert.Contains(level.Code, allowed);
    }
}
