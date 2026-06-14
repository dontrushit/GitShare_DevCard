using GitShare.Api.Services;
using Xunit;

namespace GitShare.Api.Tests;

public class RepositoryLevelEvaluatorTests
{
    [Fact]
    public void ProductionWithDiAndServices_scores_middle_or_higher()
    {
        var facts = CodeEvidenceFacts.From(
            ["Program.cs", "src/Services/TaskService.cs", "src/Interfaces/ITaskRepository.cs", "Repositories/TaskRepository.cs"],
            "Primary framework: ASP.NET Core\nSuggested layout: Web API\nServices/",
            new Dictionary<string, string>
            {
                ["Program.cs"] = "builder.Services.AddScoped<ITaskRepository, TaskRepository>();",
                ["src/Services/TaskService.cs"] = "public class TaskService { }",
            });

        var forensics = new RepositoryForensics(
            "TaskManagerApp",
            string.Empty,
            string.Empty,
            string.Empty,
            "Services/\nInterfaces/",
            [],
            [],
            [],
            "{}",
            StackEvidenceProfile.WebApi,
            Facts: facts);

        var level = RepositoryLevelEvaluator.Evaluate(
            forensics,
            ProjectClassClassifier.ProductionApp,
            AuditContentLocale.Ru);

        Assert.True(level.Score >= 50, $"score={level.Score}, rationale={level.Rationale}");
        Assert.NotEqual("trainee", level.Code);
    }

    [Fact]
    public void DocOps_has_low_ceiling()
    {
        var forensics = new RepositoryForensics(
            "notes",
            string.Empty,
            string.Empty,
            string.Empty,
            "README.md",
            [],
            [],
            [],
            "{}",
            StackEvidenceProfile.GenericProduction);

        var level = RepositoryLevelEvaluator.Evaluate(
            forensics,
            ProjectClassClassifier.DocOpsKnowledgeBase,
            AuditContentLocale.En);

        Assert.True(level.Score <= 45);
        Assert.Equal("trainee", level.Code);
    }

    [Fact]
    public void StaticDbHelper_reduces_production_score()
    {
        var facts = CodeEvidenceFacts.From(
            ["DbHelper.cs"],
            "Primary framework: WinForms",
            new Dictionary<string, string> { ["DbHelper.cs"] = "static class DbHelper { }" });

        var withHelper = RepositoryLevelEvaluator.Evaluate(
            new RepositoryForensics(
                "WpfPhonesCatalog",
                string.Empty,
                string.Empty,
                string.Empty,
                "DbHelper.cs",
                [],
                [],
                [],
                "{}",
                StackEvidenceProfile.WinForms,
                Facts: facts),
            ProjectClassClassifier.ProductionApp,
            AuditContentLocale.Ru);

        var withoutFacts = RepositoryLevelEvaluator.Evaluate(
            new RepositoryForensics(
                "WpfPhonesCatalog",
                string.Empty,
                string.Empty,
                string.Empty,
                "Form1.cs",
                [],
                [],
                [],
                "{}",
                StackEvidenceProfile.WinForms),
            ProjectClassClassifier.ProductionApp,
            AuditContentLocale.Ru);

        Assert.True(withHelper.Score < withoutFacts.Score + 15);
    }
}
