using GitShare.Api.Services;
using Xunit;

namespace GitShare.Api.Tests;

public sealed class RepositoryLevelEvaluatorUtilityTests
{
    [Fact]
    public void TaskManager_console_with_istorage_scores_higher_than_bare_utility()
    {
        var facts = CodeEvidenceFacts.From(
            [
                "TaskManagerApp/Program.cs",
                "TaskManagerApp/Services/TaskService.cs",
                "TaskManagerApp/Interfaces/IStorage.cs",
                "TaskManagerApp/Storage/FileStorage.cs",
            ],
            "Primary framework: .NET, Console\nSuggested layout: Console Utility\nServices/\nStorage/",
            new Dictionary<string, string>
            {
                ["TaskManagerApp/Interfaces/IStorage.cs"] = "public interface IStorage { }",
                ["TaskManagerApp/Services/TaskService.cs"] = "public class TaskService { }",
            });

        var level = RepositoryLevelEvaluator.Evaluate(
            MakeForensics("TaskManagerApp", "Primary framework: .NET, Console\nConsole Utility", facts, StackEvidenceProfile.ConsoleUtility),
            ProjectClassClassifier.UtilityAutomation,
            AuditContentLocale.Ru);

        Assert.True(level.Score >= 44, $"Expected >=44, got {level.Score} ({level.Rationale})");
        Assert.NotEqual(level.Score, 36);
    }

    [Fact]
    public void Wpf_partial_mvvm_scores_between_console_and_flat_winforms()
    {
        var facts = CodeEvidenceFacts.From(
            [
                "WpfPhonesCatalog/MainWindow.xaml.cs",
                "WpfPhonesCatalog/ViewModels/MainViewModel.cs",
                "WpfPhonesCatalog/Converters/PhoneConverter.cs",
                "WpfPhonesCatalog/Services/DataService.cs",
            ],
            "Primary framework: .NET, WPF\nMVVM (Desktop)",
            new Dictionary<string, string>
            {
                ["WpfPhonesCatalog/MainWindow.xaml.cs"] = "private void LoadPage() { var page = CurrentPage; }",
                ["WpfPhonesCatalog/Services/DataService.cs"] = "public class DataService { }",
            });

        var wpfLevel = RepositoryLevelEvaluator.Evaluate(
            MakeForensics("WpfPhonesCatalog", "Primary framework: .NET, WPF", facts, StackEvidenceProfile.Wpf),
            ProjectClassClassifier.UtilityAutomation,
            AuditContentLocale.Ru);

        var winFormsFacts = CodeEvidenceFacts.From(
            ["Form1.cs", "Helpers/DbHelper.cs"],
            "Primary framework: .NET, WinForms",
            new Dictionary<string, string> { ["Helpers/DbHelper.cs"] = "static class DbHelper { }" });

        var winFormsLevel = RepositoryLevelEvaluator.Evaluate(
            MakeForensics("WinFormsCatalog", "Primary framework: .NET, WinForms", winFormsFacts, StackEvidenceProfile.WinForms),
            ProjectClassClassifier.UtilityAutomation,
            AuditContentLocale.Ru);

        Assert.True(wpfLevel.Score > winFormsLevel.Score,
            $"WPF {wpfLevel.Score} should exceed flat WinForms {winFormsLevel.Score}");
        Assert.True(wpfLevel.Score >= 38 && wpfLevel.Score <= 46, $"WPF score {wpfLevel.Score}");
        Assert.True(winFormsLevel.Score <= 34, $"WinForms DbHelper score {winFormsLevel.Score}");
    }

    [Fact]
    public void Llm_selector_includes_utility_source_paths()
    {
        var paths = new List<string>
        {
            "TaskManagerApp/Program.cs",
            "TaskManagerApp/Services/TaskService.cs",
            "TaskManagerApp/Interfaces/IStorage.cs",
            "TaskManagerApp/Storage/FileStorage.cs",
        };
        var manifest = TargetFileSignatureAnalyzer.BuildManifest("TaskManagerApp", paths, "C#");

        var selection = LlmEvidenceFileSelector.Select(
            "TaskManagerApp",
            paths,
            manifest,
            ProjectClassClassifier.UtilityAutomation);

        Assert.NotEmpty(selection.SourcePaths);
        Assert.True(selection.SourcePaths.Count <= StackEvidenceFileProfiles.MaxLlmUtilitySourceFiles);
    }

    private static RepositoryForensics MakeForensics(
        string repoName,
        string manifest,
        CodeEvidenceFacts facts,
        StackEvidenceProfile profile) =>
        new(
            repoName,
            string.Empty,
            string.Empty,
            string.Empty,
            manifest,
            [],
            [],
            [],
            "{}",
            profile,
            Facts: facts);
}
