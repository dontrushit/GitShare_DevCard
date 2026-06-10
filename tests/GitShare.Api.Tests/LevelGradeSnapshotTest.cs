using System.Text.Json;
using System.Text.Json.Serialization;
using GitShare.Api.Models;
using GitShare.Api.Services;
using Xunit;
using Xunit.Abstractions;

namespace GitShare.Api.Tests;

public sealed class LevelGradeSnapshotTest
{
    private readonly ITestOutputHelper _output;

    public LevelGradeSnapshotTest(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Snapshot_NewEvaluatorGrades()
    {
        foreach (var (name, profile) in PolarProfiles.All())
        {
            var level = ProgrammerLevelEvaluator.Evaluate(profile);
            _output.WriteLine($"{name,-16} {level.Code,-9} score={level.Score,3} raw={level.RawScore,3}");
        }
    }

    private static class PolarProfiles
    {
        public static IEnumerable<(string Name, DevCardProfile Profile)> All()
        {
            yield return ("uchtenfrinen", Load("uchtenfrinen-profile.json"));
            yield return ("HolyMonkey", Load("holymonkey-profile.json"));
            yield return ("melsomino", CohortMatrixTests.BuildMelsomino());
            yield return ("YuryS9", CohortMatrixTests.BuildYuryS9());
            yield return ("geerlingguy", ProgrammerLevelEvaluatorTests.BuildGeerlingGuy());
            yield return ("alexellis", Load("alexellis-profile.json"));
            yield return ("jessfraz", ProgrammerLevelEvaluatorTests.BuildJessFraz());
            yield return ("shanselman", ProgrammerLevelEvaluatorTests.BuildScottHanselman());
            yield return ("davidfowl", ProgrammerLevelEvaluatorTests.BuildDavidFowl());
            yield return ("EgorBo", ProgrammerLevelEvaluatorTests.BuildEgorBo());
            yield return ("taylorotwell", ProgrammerLevelEvaluatorTests.BuildTaylorOtwel());
        }

        private static DevCardProfile Load(string fileName)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
            return JsonSerializer.Deserialize<DevCardProfile>(
                File.ReadAllText(path),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = null,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                })!;
        }
    }
}
