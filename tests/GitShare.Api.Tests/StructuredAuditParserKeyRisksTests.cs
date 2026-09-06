using System.Text.Json;
using GitShare.Api.Models;
using GitShare.Api.Services;
using Xunit;

namespace GitShare.Api.Tests;

public sealed class StructuredAuditParserKeyRisksTests
{
    [Fact]
    public void Key_risks_survive_parse_despite_json_ignore_on_property()
    {
        const string json = """
            {
              "Projects": [
                {
                  "RepoName": "demo",
                  "ProjectClass": "Production App",
                  "Framework": "ASP.NET Core",
                  "LayoutType": "Clean Architecture",
                  "KeyFiles": ["Program.cs"],
                  "ArchitectureSummary": "Модульная монолитная сборка.",
                  "TechnicalDebt": "Нет выделенного слоя доменных сервисов.",
                  "DebtSeverity": "Warning",
                  "KeyRisks": ["Тонкая доменная модель", "Нет тестов на слой данных"],
                  "InterviewTrapQuestion": "Как изолирована доменная логика?"
                }
              ],
              "CoreEngineeringFocus": "Backend на .NET"
            }
            """;

        var parsed = StructuredAuditParser.TryParse(json, AuditContentLocale.Ru);

        Assert.NotNull(parsed);
        var project = Assert.Single(parsed.Projects);
        Assert.Equal(["Тонкая доменная модель", "Нет тестов на слой данных"], project.KeyRisks);
    }

    [Fact]
    public void Key_risks_accept_single_string_instead_of_array()
    {
        const string json = """
            {
              "Projects": [
                {
                  "RepoName": "demo",
                  "KeyRisks": "Нет тестов",
                  "TechnicalDebt": "Долг"
                }
              ],
              "CoreEngineeringFocus": "focus"
            }
            """;

        var parsed = StructuredAuditParser.TryParse(json, AuditContentLocale.Ru);

        Assert.NotNull(parsed);
        Assert.Equal(["Нет тестов"], parsed.Projects[0].KeyRisks);
    }

    [Fact]
    public void Key_risks_stay_internal_and_are_not_serialized_to_client()
    {
        var response = new StructuredAuditResponse
        {
            Projects =
            [
                new ProjectAuditDetail
                {
                    RepoName = "demo",
                    KeyRisks = ["Нет тестов"]
                }
            ]
        };

        var json = JsonSerializer.Serialize(response);

        Assert.DoesNotContain(nameof(ProjectAuditDetail.KeyRisks), json);
    }
}
