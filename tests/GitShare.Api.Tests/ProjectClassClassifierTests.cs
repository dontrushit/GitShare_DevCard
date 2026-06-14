using GitShare.Api.Services;
using Xunit;

namespace GitShare.Api.Tests;

public sealed class ProjectClassClassifierTests
{
  [Fact]
  public void Classify_fullstack_monorepo_with_docker_is_production_app()
  {
    var paths = GitShareLikeTree();
    var manifest = TargetFileSignatureAnalyzer.BuildManifest("GitShare_DevCard", paths, "C#");

    var projectClass = ProjectClassClassifier.Classify("GitShare_DevCard", manifest);

    Assert.Equal(ProjectClassClassifier.ProductionApp, projectClass);
    Assert.DoesNotContain(
      "Консольное приложение",
      ProjectClassClassifier.DefaultTechnicalDebtForClass(projectClass, "GitShare_DevCard", manifest));
  }

  [Fact]
  public void Classify_solution_monorepo_with_tests_is_production_app()
  {
    var paths = GitShareLikeTree();
    Assert.Contains(paths, p => p.EndsWith(".sln", StringComparison.OrdinalIgnoreCase));

    var manifest = TargetFileSignatureAnalyzer.BuildManifest("GitShare_DevCard", paths, "C#");
    Assert.Equal(ProjectClassClassifier.ProductionApp, ProjectClassClassifier.Classify("GitShare_DevCard", manifest));
  }

  [Fact]
  public void StackCatalog_detects_fullstack_dotnet_react()
  {
    var analysis = ProjectStackCatalog.Analyze(GitShareLikeTree(), "GitShare_DevCard");

    Assert.Contains("ASP.NET", analysis.Framework, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("React", analysis.Framework, StringComparison.OrdinalIgnoreCase);
    Assert.Equal("Web API + SPA", analysis.Layout);
  }

  public static List<string> GitShareLikeTree() =>
  [
    "GitShare.sln",
    "docker-compose.yml",
    "docker/api/Dockerfile",
    "docker/client/Dockerfile",
    "src/GitShare.Api/Program.cs",
    "src/GitShare.Api/appsettings.json",
    "src/GitShare.Api/Controllers/ProfileController.cs",
    "src/GitShare.Api/Services/GitHubAnalyticsService.cs",
    "src/GitShare.Api/Data/AppDbContext.cs",
    "src/GitShare.Api/GitShare.Api.csproj",
    "src/GitShare.Client/package.json",
    "src/GitShare.Client/vite.config.ts",
    "src/GitShare.Client/src/App.tsx",
    "src/GitShare.Client/src/main.tsx",
    "tests/GitShare.Api.Tests/GitShare.Api.Tests.csproj",
  ];
}
