using System.Text.Json;
using System.Text.Json.Serialization;
using GitShare.Api.Models;
using GitShare.Api.Services;
using Xunit;
using Xunit.Abstractions;

namespace GitShare.Api.Tests;

/// <summary>
/// Офлайн-проверка грейда по когортам из scripts/profile-matrix.json и JSON-снимкам в Fixtures/profiles/.
/// </summary>
public sealed class CohortMatrixTests
{
    private readonly ITestOutputHelper _output;

    public CohortMatrixTests(ITestOutputHelper output) => _output = output;

    public static IEnumerable<object[]> MatrixRows()
    {
        var matrixPath = ResolveMatrixPath();
        using var doc = JsonDocument.Parse(File.ReadAllText(matrixPath));
        foreach (var entry in doc.RootElement.GetProperty("profiles").EnumerateArray())
        {
            if (entry.TryGetProperty("expectApiError", out var err) && err.GetBoolean())
            {
                continue;
            }

            var username = entry.GetProperty("username").GetString()!;
            var levelIn = entry.GetProperty("levelIn").EnumerateArray()
                .Select(e => e.GetString()!)
                .ToArray();
            yield return [username, levelIn];
        }
    }

    public static IEnumerable<object[]> MatrixRowsWithFixtures()
    {
        foreach (var row in MatrixRows())
        {
            var username = (string)row[0]!;
            if (ResolveFixturePath(username) is not null)
            {
                yield return row;
            }
        }
    }

    [Theory]
    [MemberData(nameof(MatrixRowsWithFixtures))]
    public void Evaluate_FromFixture_MatchesMatrixExpectation(string username, string[] levelIn)
    {
        var fixturePath = ResolveFixturePath(username)
                          ?? throw new InvalidOperationException($"Fixture missing for {username}");

        var profile = LoadProfile(fixturePath);
        var level = ProgrammerLevelEvaluator.Evaluate(profile);

        Assert.True(
            levelIn.Contains(level.Code, StringComparer.OrdinalIgnoreCase),
            $"{username}: matrix expects [{string.Join(", ", levelIn)}], got {level.Code} " +
            $"(score={level.Score}, raw={level.RawScore}). Rationale: {level.Rationale}");
    }

    [Fact]
    public void Juniors_Cohort_SyntheticProfiles_StayTrainee()
    {
        foreach (var profile in new[] { BuildMelsomino(), BuildYuryS9() })
        {
            var level = ProgrammerLevelEvaluator.Evaluate(profile);
            Assert.Equal("trainee", level.Code);
        }
    }

    [Fact]
    public void Uchtenfrinen_FromFixture_IsTrainee()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "uchtenfrinen-profile.json");
        if (!File.Exists(path))
        {
            return;
        }

        var level = ProgrammerLevelEvaluator.Evaluate(LoadProfile(path));
        Assert.Equal("trainee", level.Code);
    }

    private static string ResolveMatrixPath()
    {
        var bundled = Path.Combine(AppContext.BaseDirectory, "Fixtures", "profile-matrix.json");
        if (File.Exists(bundled))
        {
            return bundled;
        }

        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var fromScripts = Path.Combine(repoRoot, "scripts", "profile-matrix.json");
        if (File.Exists(fromScripts))
        {
            return fromScripts;
        }

        throw new FileNotFoundException("profile-matrix.json not found");
    }

    private static string? ResolveFixturePath(string username)
    {
        var profilesDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "profiles");
        var standard = Path.Combine(profilesDir, $"{username}.json");
        if (File.Exists(standard))
        {
            return standard;
        }

        var legacy = Path.Combine(AppContext.BaseDirectory, "Fixtures", $"{username}-profile.json");
        return File.Exists(legacy) ? legacy : null;
    }

    private static DevCardProfile LoadProfile(string path)
    {
        return JsonSerializer.Deserialize<DevCardProfile>(
            File.ReadAllText(path),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = null,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            })!;
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

    public static DevCardProfile BuildMelsomino() => new()
    {
        Username = "melsomino",
        OwnRepositoryCount = 9,
        PublicRepos = 11,
        TotalStars = 3,
        SmallPetProjects = 10,
        MediumProjects = 2,
        ProductionScaleProjects = 0,
        LanguageStack =
        [
            new LanguageMetric { Language = "Pascal", Percentage = 35 },
            new LanguageMetric { Language = "C", Percentage = 30 },
            new LanguageMetric { Language = "Java", Percentage = 25 }
        ],
        TopRepositories =
        [
            new RepoSummary { Name = "algo-labs", Stars = 1, Language = "C" },
            new RepoSummary { Name = "delphi-utils", Stars = 0, Language = "Pascal" }
        ],
        AuditData = new StructuredAuditResponse
        {
            Projects =
            [
                new ProjectAuditDetail
                {
                    RepoName = "algo-labs",
                    ProjectClass = "Utility / Automation",
                    Framework = "C++",
                    DebtSeverity = "CLEAN"
                },
                new ProjectAuditDetail
                {
                    RepoName = "university-forks",
                    ProjectClass = "Utility / Automation",
                    Framework = "Java",
                    DebtSeverity = "CLEAN"
                }
            ],
            GitFormatStandard = "Descriptive / Non-standard"
        }
    };

    public static DevCardProfile BuildYuryS9() => new()
    {
        Username = "YuryS9",
        OwnRepositoryCount = 5,
        PublicRepos = 5,
        TotalStars = 0,
        SmallPetProjects = 5,
        LanguageStack = [new LanguageMetric { Language = "C#", Percentage = 60 }, new LanguageMetric { Language = "Java", Percentage = 40 }],
        TopRepositories = [new RepoSummary { Name = "lab-01", Stars = 0, Language = "C#" }],
        AuditData = new StructuredAuditResponse
        {
            Projects =
            [
                new ProjectAuditDetail
                {
                    RepoName = "lab-01",
                    ProjectClass = "Utility / Automation",
                    Framework = ".NET, Console",
                    DebtSeverity = "CLEAN"
                }
            ],
            GitFormatStandard = "Descriptive / Non-standard"
        }
    };
}
