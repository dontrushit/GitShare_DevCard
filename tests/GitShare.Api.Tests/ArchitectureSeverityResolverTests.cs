using GitShare.Api.Services;
using Xunit;

namespace GitShare.Api.Tests;

public class ArchitectureSeverityResolverTests
{
    [Fact]
    public void WinFormsDbHelper_is_warning_not_clean()
    {
        var facts = CodeEvidenceFacts.From(
            ["Form1.cs", "Helpers/DbHelper.cs"],
            "Primary framework: WinForms\nDbHelper.cs",
            new Dictionary<string, string> { ["Helpers/DbHelper.cs"] = "static class DbHelper { }" });

        var forensics = new RepositoryForensics(
            "WinFormsCatalog",
            string.Empty,
            string.Empty,
            string.Empty,
            "DbHelper.cs\n.accdb",
            [],
            ["WinForms: DbHelper без Repository"],
            [],
            "{}",
            StackEvidenceProfile.WinForms,
            Facts: facts);

        var severity = ArchitectureSeverityResolver.Resolve(
            forensics,
            ProjectClassClassifier.UtilityAutomation,
            "CLEAN",
            forensics.VerifiedCons);

        Assert.Equal("Warning", severity);
    }

    [Fact]
    public void Production_with_di_and_repository_can_be_clean()
    {
        var facts = CodeEvidenceFacts.From(
            ["Program.cs", "src/Services/TaskService.cs", "src/Interfaces/IStorage.cs"],
            "Primary framework: ASP.NET Core\nServices/",
            new Dictionary<string, string>
            {
                ["Program.cs"] = "builder.Services.AddScoped<IStorage, FileStorage>();",
            });

        var forensics = new RepositoryForensics(
            "GitShare_DevCard",
            string.Empty,
            string.Empty,
            string.Empty,
            "Services/\nInterfaces/",
            [],
            [],
            [],
            "{}",
            StackEvidenceProfile.FullStackDotNetReact,
            Facts: facts);

        var severity = ArchitectureSeverityResolver.Resolve(
            forensics,
            ProjectClassClassifier.ProductionApp,
            "Minor",
            ["В выборке кода критичных нарушений не видно"]);

        Assert.Equal("CLEAN", severity);
    }

    [Fact]
    public void Marketing_summary_rejected_in_favor_of_evidence()
    {
        var evidence = "GitShare_DevCard (Мидл, 76/100): .NET, ASP.NET — DI и Services видны в Program.cs.";
        var llm = "Полноценное зрелое масштабируемое full-stack приложение с современной архитектурой.";

        var picked = ArchitectureSummarySanitizer.PickSummary(
            llm,
            evidence,
            null,
            AuditContentLocale.Ru);

        Assert.Equal(evidence, picked);
    }

    [Fact]
    public void Checklist_llm_summary_with_program_cs_rejected_even_with_facts()
    {
        var evidence =
            "GitShare_DevCard (Мидл, 76/100): в коде Program.cs → Services → абстракции данных; SPA отдельно.";
        var llm =
            "Приложение построено как full-stack решение на ASP.NET и React. " +
            "Используются DI, async/await и обработка ошибок в Program.cs, что улучшает масштабируемость и стабильность.";

        var facts = CodeEvidenceFacts.From(
            ["Program.cs", "src/Services/GitHubAnalyticsService.cs"],
            "Web API",
            new Dictionary<string, string> { ["Program.cs"] = "builder.Services.AddScoped<ITask, Task>();" });

        var picked = ArchitectureSummarySanitizer.PickSummary(llm, evidence, facts, AuditContentLocale.Ru);

        Assert.Equal(evidence, picked);
    }
}
