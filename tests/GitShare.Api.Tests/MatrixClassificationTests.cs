using System.Text.Json;
using GitShare.Api.Services;
using Xunit;

namespace GitShare.Api.Tests;

/// <summary>
/// Офлайн-проверка projectRules из profile-matrix для деревьев репозиториев (без live API).
/// </summary>
public sealed class MatrixClassificationTests
{
    [Fact]
    public void Dontrushit_GitShare_DevCard_matches_matrix_expectations()
    {
        var paths = ProjectClassClassifierTests.GitShareLikeTree();
        var manifest = TargetFileSignatureAnalyzer.BuildManifest("GitShare_DevCard", paths, "C#");
        var projectClass = ProjectClassClassifier.Classify("GitShare_DevCard", manifest);
        var stack = ProjectStackCatalog.Analyze(paths, "GitShare_DevCard");
        var debt = ProjectClassClassifier.DefaultTechnicalDebtForClass(projectClass, "GitShare_DevCard", manifest);

        Assert.Equal(ProjectClassClassifier.ProductionApp, projectClass);
        Assert.Contains("ASP.NET", stack.Framework, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("React", stack.Framework, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Console", stack.Framework, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Консольное приложение", debt);
    }

    [Fact]
    public void Dontrushit_WinFormsCatalog_not_devops_production()
    {
        var paths = new List<string>
        {
            "docker-compose.yml",
            "WinFormsCatalog/Program.cs",
            "WinFormsCatalog/WinFormsCatalog.csproj",
            "WinFormsCatalog/Form1.cs",
            "WinFormsCatalog/Helpers/DbHelper.cs",
        };
        var manifest = TargetFileSignatureAnalyzer.BuildManifest("WinFormsCatalog", paths, "C#");
        var projectClass = ProjectClassClassifier.Classify("WinFormsCatalog", manifest);
        var stack = ProjectStackCatalog.Analyze(paths, "WinFormsCatalog");

        Assert.Equal(ProjectClassClassifier.UtilityAutomation, projectClass);
        Assert.Contains("WinForms", stack.Framework, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DevOps", stack.Framework, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Matrix_contains_self_dogfood_profile()
    {
        var matrixPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "profile-matrix.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(matrixPath));
        var usernames = doc.RootElement.GetProperty("profiles").EnumerateArray()
            .Select(e => e.GetProperty("username").GetString())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("dontrushit", usernames);
    }
}
