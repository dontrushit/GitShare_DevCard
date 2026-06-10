using System.Text.Json;
using System.Text.Json.Serialization;
using GitShare.Api.Models;
using GitShare.Api.Services;
using Xunit;

namespace GitShare.Api.Tests;

public sealed class ProgrammerLevelEvaluatorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Theory]
    [MemberData(nameof(PolarProfileExpectations))]
    public void Evaluate_PolarProfiles_MatchMarketGrade(
        string fixtureOrNull,
        DevCardProfile? inlineProfile,
        string[] allowedTiers,
        string username)
    {
        var profile = fixtureOrNull is not null
            ? LoadFixture(fixtureOrNull)
            : inlineProfile ?? throw new ArgumentException("Profile required");

        var level = ProgrammerLevelEvaluator.Evaluate(profile);

        Assert.True(
            allowedTiers.Contains(level.Code, StringComparer.OrdinalIgnoreCase),
            $"{username}: expected [{string.Join(", ", allowedTiers)}], got {level.Code} (score={level.Score}, raw={level.RawScore}, rationale={level.Rationale})");
    }

    public static TheoryData<string?, DevCardProfile?, string[], string> PolarProfileExpectations =>
        new()
        {
            { "uchtenfrinen-profile.json", null, ["trainee"], "uchtenfrinen" },
            { "holymonkey-profile.json", null, ["senior", "lead"], "HolyMonkey" },
            { null, CohortMatrixTests.BuildMelsomino(), ["trainee"], "melsomino" },
            { null, CohortMatrixTests.BuildYuryS9(), ["trainee"], "YuryS9" },
            { null, BuildGeerlingGuy(), ["senior", "lead", "middle"], "geerlingguy" },
            { "alexellis-profile.json", null, ["senior", "lead"], "alexellis" },
            { null, BuildJessFraz(), ["senior", "lead"], "jessfraz" },
            { null, BuildScottHanselman(), ["senior", "lead"], "shanselman" },
            { null, BuildDavidFowl(), ["principal", "lead"], "davidfowl" },
            { null, BuildEgorBo(), ["principal", "senior", "lead"], "EgorBo" },
            { null, BuildTaylorOtwel(), ["principal", "lead"], "taylorotwell" }
        };

    [Fact]
    public void Evaluate_HolyMonkey_AtLeastSenior()
    {
        var profile = LoadFixture("holymonkey-profile.json");
        var level = ProgrammerLevelEvaluator.Evaluate(profile);
        Assert.True(
            level.Code is "senior" or "lead",
            $"HolyMonkey must be at least senior, got {level.Code} (score={level.Score})");
        Assert.True(level.Score >= 50);
    }

    [Fact]
    public void Evaluate_Uchtenfrinen_IsTrainee()
    {
        var profile = LoadFixture("uchtenfrinen-profile.json");
        var level = ProgrammerLevelEvaluator.Evaluate(profile);
        Assert.Equal("trainee", level.Code);
        Assert.True(level.IsLowConfidence);
    }

    [Fact]
    public void Evaluate_LearningWithProductionApp_CanBeJunior()
    {
        var profile = LoadFixture("uchtenfrinen-profile.json");
        profile.AuditData!.Projects[0].ProjectClass = "Production App";
        profile.AuditData.Projects[0].Framework = "ASP.NET";

        var level = ProgrammerLevelEvaluator.Evaluate(profile);

        Assert.Equal("junior", level.Code);
    }

    [Fact]
    public void Evaluate_Uchtenfrinen_NotMiddleOrAbove()
    {
        var profile = LoadFixture("uchtenfrinen-profile.json");
        var level = ProgrammerLevelEvaluator.Evaluate(profile);
        Assert.True(TierRank(level.Code) <= TierRank("junior"));
    }

    private static DevCardProfile LoadFixture(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<DevCardProfile>(json, JsonOptions)
               ?? throw new InvalidOperationException($"Failed to load {fileName}");
    }

    private static int TierRank(string code) => code switch
    {
        "principal" => 5,
        "lead" => 4,
        "senior" => 3,
        "middle" => 2,
        "junior" => 1,
        _ => 0
    };

    public static DevCardProfile BuildGeerlingGuy() => new()
    {
        Username = "geerlingguy",
        OwnRepositoryCount = 180,
        PublicRepos = 220,
        TotalStars = 45_000,
        SmallPetProjects = 120,
        MediumProjects = 40,
        ProductionScaleProjects = 15,
        LanguageStack =
        [
            new LanguageMetric { Language = "YAML", Percentage = 35 },
            new LanguageMetric { Language = "Python", Percentage = 25 },
            new LanguageMetric { Language = "Shell", Percentage = 20 },
            new LanguageMetric { Language = "Jinja", Percentage = 10 }
        ],
        TopRepositories =
        [
            new RepoSummary { Name = "ansible-role-repo", Stars = 1200, Language = "YAML" },
            new RepoSummary { Name = "ansible-for-devops", Stars = 9000, Language = "Python" }
        ],
        AuditData = new StructuredAuditResponse
        {
            Projects =
            [
                new ProjectAuditDetail
                {
                    RepoName = "ansible-role-repo",
                    ProjectClass = "Utility / Automation",
                    Framework = "Ansible",
                    DebtSeverity = "CLEAN"
                },
                new ProjectAuditDetail
                {
                    RepoName = "devops-playbooks",
                    ProjectClass = "Utility / Automation",
                    Framework = "DevOps (K8s/Docker)",
                    DebtSeverity = "CLEAN"
                },
                new ProjectAuditDetail
                {
                    RepoName = "infra-docs",
                    ProjectClass = "DocOps / Knowledge Base",
                    Framework = "Markdown",
                    DebtSeverity = "NONE"
                }
            ],
            GitFormatStandard = "Descriptive / Non-standard"
        }
    };

    public static DevCardProfile BuildJessFraz() => new()
    {
        Username = "jessfraz",
        OwnRepositoryCount = 90,
        TotalStars = 12_000,
        TopRepositories = [new RepoSummary { Name = "dockerfiles", Stars = 4000, Language = "Go" }],
        LanguageStack =
        [
            new LanguageMetric { Language = "Go", Percentage = 55 },
            new LanguageMetric { Language = "Shell", Percentage = 25 }
        ],
        AuditData = new StructuredAuditResponse
        {
            Projects =
            [
                new ProjectAuditDetail
                {
                    RepoName = "dockerfiles",
                    ProjectClass = "Utility / Automation",
                    Framework = "Go",
                    DebtSeverity = "CLEAN"
                },
                new ProjectAuditDetail
                {
                    RepoName = "infra-tool",
                    ProjectClass = "Utility / Automation",
                    Framework = "DevOps (K8s/Docker)",
                    DebtSeverity = "CLEAN"
                }
            ],
            GitFormatStandard = "Conventional Commits compliant"
        },
        ActivityTelemetry = new GitHubActivityTelemetry
        {
            ExternalPullRequests = ["linux", "moby"]
        }
    };

    public static DevCardProfile BuildScottHanselman() => new()
    {
        Username = "shanselman",
        OwnRepositoryCount = 110,
        TotalStars = 8_500,
        TopRepositories = [new RepoSummary { Name = "samples", Stars = 1200, Language = "C#" }],
        LanguageStack =
        [
            new LanguageMetric { Language = "C#", Percentage = 60 },
            new LanguageMetric { Language = "HTML", Percentage = 20 }
        ],
        AuditData = new StructuredAuditResponse
        {
            Projects =
            [
                new ProjectAuditDetail
                {
                    RepoName = "samples",
                    ProjectClass = "Utility / Automation",
                    Framework = "ASP.NET",
                    DebtSeverity = "Minor"
                },
                new ProjectAuditDetail
                {
                    RepoName = "tiny-tools",
                    ProjectClass = "Utility / Automation",
                    Framework = ".NET, Console",
                    DebtSeverity = "CLEAN"
                }
            ],
            GitFormatStandard = "Descriptive / Non-standard"
        }
    };

    public static DevCardProfile BuildDavidFowl() => new()
    {
        Username = "davidfowl",
        OwnRepositoryCount = 140,
        TotalStars = 35_000,
        TopRepositories = [new RepoSummary { Name = "aspnetcore", Stars = 5000, Language = "C#" }],
        LanguageStack = [new LanguageMetric { Language = "C#", Percentage = 95 }],
        AuditData = new StructuredAuditResponse
        {
            Projects =
            [
                new ProjectAuditDetail
                {
                    RepoName = "aspnetcore",
                    ProjectClass = "Production App",
                    Framework = "ASP.NET",
                    DebtSeverity = "CLEAN",
                    KeyFiles = ["Program.cs"]
                },
                new ProjectAuditDetail
                {
                    RepoName = "orleans",
                    ProjectClass = "Production App",
                    Framework = "ASP.NET",
                    DebtSeverity = "Minor",
                    KeyFiles = ["Startup.cs"]
                }
            ],
            GitFormatStandard = "Conventional Commits compliant"
        },
        ActivityTelemetry = new GitHubActivityTelemetry
        {
            ExternalPullRequests = ["dotnet", "aspnetcore", "orleans", "signalr", "runtime"]
        }
    };

    public static DevCardProfile BuildEgorBo() => new()
    {
        Username = "EgorBo",
        OwnRepositoryCount = 45,
        TotalStars = 6_000,
        TopRepositories = [new RepoSummary { Name = "coreclr", Stars = 800, Language = "C#" }],
        LanguageStack =
        [
            new LanguageMetric { Language = "C#", Percentage = 70 },
            new LanguageMetric { Language = "C++", Percentage = 20 }
        ],
        AuditData = new StructuredAuditResponse
        {
            Projects =
            [
                new ProjectAuditDetail
                {
                    RepoName = "runtime-perf",
                    ProjectClass = "Production App",
                    Framework = ".NET",
                    DebtSeverity = "CLEAN",
                    KeyFiles = ["Program.cs"]
                },
                new ProjectAuditDetail
                {
                    RepoName = "jit-lab",
                    ProjectClass = "Utility / Automation",
                    Framework = "C++",
                    DebtSeverity = "CLEAN"
                }
            ],
            GitFormatStandard = "Conventional Commits compliant"
        },
        ActivityTelemetry = new GitHubActivityTelemetry
        {
            ExternalPullRequests = ["dotnet", "runtime", "roslyn"]
        }
    };

    public static DevCardProfile BuildTaylorOtwel() => new()
    {
        Username = "taylorotwell",
        OwnRepositoryCount = 80,
        TotalStars = 120_000,
        TopRepositories = [new RepoSummary { Name = "laravel", Stars = 80_000, Language = "PHP" }],
        LanguageStack =
        [
            new LanguageMetric { Language = "PHP", Percentage = 80 },
            new LanguageMetric { Language = "Blade", Percentage = 10 }
        ],
        AuditData = new StructuredAuditResponse
        {
            Projects =
            [
                new ProjectAuditDetail
                {
                    RepoName = "laravel",
                    ProjectClass = "Production App",
                    Framework = "Laravel, PHP",
                    DebtSeverity = "CLEAN",
                    KeyFiles = ["composer.json"]
                },
                new ProjectAuditDetail
                {
                    RepoName = "framework-samples",
                    ProjectClass = "Production App",
                    Framework = "Laravel, PHP",
                    DebtSeverity = "Minor",
                    KeyFiles = ["artisan"]
                }
            ],
            GitFormatStandard = "Conventional Commits compliant"
        },
        ActivityTelemetry = new GitHubActivityTelemetry
        {
            ExternalPullRequests = ["symfony", "php-src"]
        }
    };
}
