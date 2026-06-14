namespace GitShare.Api.Services;

/// <summary>
/// Распознавание стека и типа проекта по дереву файлов (без чтения содержимого).
/// Новые стеки добавляются правилами в <see cref="Rules"/> — не нужно трогать LLM.
/// </summary>
internal static class ProjectStackCatalog
{
    internal sealed record StackAnalysis(
        string Framework,
        string Layout,
        IReadOnlyList<string> StackSignals,
        IReadOnlyList<string> KeyFiles,
        bool IsScriptTestOrInfraUtility);

    private sealed record StackRule(
        string Id,
        int Priority,
        Func<IReadOnlyList<string>, bool> Matches,
        string StackSignal,
        string Framework,
        string Layout,
        bool IsScriptTestOrInfraUtility,
        Func<IReadOnlyList<string>, IReadOnlyList<string>>? KeyFileSelector = null);

    private static readonly StackRule[] Rules =
    [
        Rule("playwright", 96, HasPlaywright, "Playwright E2E (Node/TS)", "Playwright, TypeScript", "E2E Test Suite", true, SelectPlaywrightKeys),
        Rule("cypress", 96, HasCypress, "Cypress E2E", "Cypress, JavaScript", "E2E Test Suite", true, SelectJsTestKeys),
        Rule("selenium-ide", 95, HasSeleniumIde, "Selenium IDE project", "Selenium IDE", "Test Automation (IDE)", true, p => SelectByNames(p, ".side")),
        Rule("jest-vitest", 92, HasJsUnitTestRunner, "Jest/Vitest tests", "JavaScript, Unit tests", "Test Suite", true, SelectJsTestKeys),
        Rule("spring", 91, ManifestSignalParser.HasSpringSignalsInTree, "Spring Boot", "Java, Spring Boot", "Spring Boot Application", false, SelectJavaKeys),
        Rule("next", 88, HasNextJs, "Next.js", "Next.js, React", "Web Application (SSR)", false, SelectFrontendKeys),
        Rule("fullstack-dotnet-react", 89, IsFullStackDotNetReact, "Full-stack (.NET API + SPA)", ".NET, ASP.NET, React", "Web API + SPA", false, SelectFullStackKeys),
        Rule("react", 86, HasReact, "React (Vite/CRA)", "React, TypeScript", "SPA / Frontend", false, SelectFrontendKeys),
        Rule("vue", 86, HasVue, "Vue / Nuxt", "Vue.js", "SPA / Frontend", false, SelectFrontendKeys),
        Rule("angular", 86, HasAngular, "Angular", "Angular, TypeScript", "SPA / Frontend", false, SelectFrontendKeys),
        Rule("flutter", 85, HasFlutter, "Flutter", "Flutter, Dart", "Mobile / Cross-platform", false, p => SelectByNames(p, "pubspec.yaml", "lib/main.dart")),
        Rule("linux-kernel", 94, HasLinuxKernelTree, "Linux kernel (Kconfig/Makefile)", "C, Linux kernel", "OS kernel / drivers", false, SelectKernelKeys),
        Rule("c-native", 87, HasCDominantNativeCode, "C (native)", "C (native)", "Native C project", false, SelectCNativeKeys),
        Rule("terraform", 84, HasTerraform, "Terraform/IaC", "Terraform", "Infrastructure as Code", true,
            p => SelectByExtension(p, ".tf", max: 6).ToList()),
        Rule("k8s-docker", 83, HasContainerOrchestration, "Kubernetes/Docker", "DevOps (K8s/Docker)", "Container / Deployment", true, SelectDevOpsKeys),
        Rule("python", 82, HasPython, "Python", "Python", "Script / Library / Bot", true, SelectPythonKeys),
        Rule("go", 81, HasGo, "Go module", "Go", "Go Module / CLI", false, SelectGoKeys),
        Rule("rust", 80, HasRust, "Rust (Cargo)", "Rust", "Rust Crate", false, p => SelectByNames(p, "Cargo.toml", "src/main.rs")),
        Rule("ruby", 79, HasRuby, "Ruby", "Ruby", "Ruby Application", false, p => SelectByNames(p, "Gemfile", "config.ru")),
        Rule("php", 78, HasPhp, "PHP (Composer)", "PHP", "PHP Application", false, p => SelectByNames(p, "composer.json", "index.php")),
        Rule("java", 77, ManifestSignalParser.HasJavaStackInTree, "Java (Maven/Gradle)", "Java", "Java Application", false, SelectJavaKeys),
        Rule("dotnet-aspnet", 76, ProjectStackDetector.IsWebAspNetProject, "ASP.NET MVC/API", ".NET, ASP.NET", "Web API / MVC", false, SelectDotNetKeys),
        Rule("wpf", 75, HasWpf, "WPF (App.xaml)", ".NET, WPF", "MVVM (Desktop)", false, SelectDotNetKeys),
        Rule("winforms", 74, HasWinForms, "WinForms", ".NET, WinForms", "Flat Monolith (WinForms)", false, SelectDotNetKeys),
        Rule("dotnet-console", 70, HasDotNetConsole, ".NET Host (Program.cs)", ".NET, Console", "Console Utility", false, SelectDotNetKeys),
        Rule("node-generic", 65, HasNodePackage, "Node.js (package.json)", "Node.js", "Node.js Application", false, SelectJsTestKeys),
        Rule("static-site", 50, HasStaticSite, "Static site / docs", "HTML/CSS (static)", "Static / Documentation", true, SelectStaticKeys),
    ];

    public static StackAnalysis Analyze(IReadOnlyList<string> paths, string? repoName = null)
    {
        if (paths.Count == 0)
        {
            return new StackAnalysis(
                "не определён (по сигнатурам)",
                "неизвестно",
                [],
                [],
                false);
        }

        if (IsKnownLinuxKernelRepo(repoName, paths))
        {
            var kernelRule = Rules.First(r => r.Id == "linux-kernel");
            return new StackAnalysis(
                kernelRule.Framework,
                kernelRule.Layout,
                [kernelRule.StackSignal],
                kernelRule.KeyFileSelector?.Invoke(paths) ?? SelectKernelKeys(paths),
                false);
        }

        if (ProjectStackDetector.IsUnityProject(paths))
        {
            return new StackAnalysis(
                "Unity, C#",
                "Unity Project",
                ["Unity (ProjectSettings/Assets)"],
                ProjectStackDetector.SelectUnityKeyFileNames(paths),
                false);
        }

        var matched = Rules
            .Where(r => r.Matches(paths))
            .OrderByDescending(r => r.Priority)
            .ToList();

        if (matched.Any(r => r.Id is "linux-kernel" or "c-native") &&
            matched.Any(r => r.Id == "python"))
        {
            matched = matched.Where(r => r.Id != "python").ToList();
        }

        if (matched.Count == 0)
        {
            var fallbackKeys = SelectFallbackKeys(paths);
            return new StackAnalysis(
                "не определён (по сигнатурам)",
                InferGenericLayout(paths),
                ["generic layout"],
                fallbackKeys,
                false);
        }

        var primary = matched[0];
        if (primary.Id is "terraform" or "k8s-docker")
        {
            if (HasRootGoModule(paths))
            {
                var goRule = matched.FirstOrDefault(r => r.Id == "go");
                if (goRule != null)
                {
                    primary = goRule;
                }
            }

            if (primary.Id is "terraform" or "k8s-docker")
            {
                var desktopRule = matched.FirstOrDefault(r => r.Id is "winforms" or "wpf");
                if (desktopRule != null)
                {
                    primary = desktopRule;
                }
                else if (ProjectStackDetector.IsWebAspNetProject(paths))
                {
                    var webRule = matched.FirstOrDefault(r =>
                        r.Id is "fullstack-dotnet-react" or "dotnet-aspnet");
                    if (webRule != null)
                    {
                        primary = webRule;
                    }
                }
            }
        }

        var signals = matched.Select(r => r.StackSignal).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList();
        var keyFiles = primary.KeyFileSelector?.Invoke(paths) ?? SelectFallbackKeys(paths);
        if (keyFiles.Count == 0)
        {
            keyFiles = SelectFallbackKeys(paths);
        }

        // Utility-флаг только у primary-стека: docker-compose рядом с API не делает monorepo «утилитой».
        return new StackAnalysis(
            primary.Framework,
            primary.Layout,
            signals,
            keyFiles,
            primary.IsScriptTestOrInfraUtility);
    }

    public static bool IsUtilityOrTestStack(string framework, string layout) =>
        framework.Contains("Playwright", StringComparison.OrdinalIgnoreCase) ||
        framework.Contains("Cypress", StringComparison.OrdinalIgnoreCase) ||
        framework.Contains("Selenium", StringComparison.OrdinalIgnoreCase) ||
        framework.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
        framework.Contains("DevOps", StringComparison.OrdinalIgnoreCase) ||
        framework.Contains("Terraform", StringComparison.OrdinalIgnoreCase) ||
        framework.Contains("static", StringComparison.OrdinalIgnoreCase) ||
        layout.Contains("E2E", StringComparison.OrdinalIgnoreCase) ||
        layout.Contains("Test Suite", StringComparison.OrdinalIgnoreCase) ||
        layout.Contains("Script", StringComparison.OrdinalIgnoreCase) ||
        layout.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase) ||
        layout.Contains("Documentation", StringComparison.OrdinalIgnoreCase);

    private static StackRule Rule(
        string id,
        int priority,
        Func<IReadOnlyList<string>, bool> matches,
        string stackSignal,
        string framework,
        string layout,
        bool isUtility,
        Func<IReadOnlyList<string>, IReadOnlyList<string>>? keyFileSelector = null) =>
        new(id, priority, matches, stackSignal, framework, layout, isUtility, keyFileSelector);

    private static bool HasPlaywright(IReadOnlyList<string> paths)
    {
        if (paths.Any(p => Path.GetFileName(p).StartsWith("playwright.config", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (paths.Any(p => p.Contains("/playwright/", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (!HasNodePackage(paths))
        {
            return false;
        }

        // Только явный E2E-контур в корне — не unit *.spec.js в npm-библиотеках (react-hot-loader и т.п.).
        return paths.Any(p =>
            p.StartsWith("e2e/", StringComparison.OrdinalIgnoreCase) ||
            p.StartsWith("tests/e2e/", StringComparison.OrdinalIgnoreCase) ||
            p.StartsWith("test/e2e/", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasCypress(IReadOnlyList<string> paths) =>
        paths.Any(p => Path.GetFileName(p).StartsWith("cypress.config", StringComparison.OrdinalIgnoreCase)) ||
        paths.Any(p => p.StartsWith("cypress/", StringComparison.OrdinalIgnoreCase));

    private static bool HasSeleniumIde(IReadOnlyList<string> paths) =>
        paths.Any(p => p.EndsWith(".side", StringComparison.OrdinalIgnoreCase));

    private static bool HasJsUnitTestRunner(IReadOnlyList<string> paths) =>
        HasNodePackage(paths) &&
        paths.Any(p =>
            p.Contains("jest.config", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("vitest.config", StringComparison.OrdinalIgnoreCase) ||
            p.EndsWith(".test.ts", StringComparison.OrdinalIgnoreCase) ||
            p.EndsWith(".test.tsx", StringComparison.OrdinalIgnoreCase));

    private static bool HasNextJs(IReadOnlyList<string> paths) =>
        HasNodePackage(paths) &&
        (paths.Any(p => Path.GetFileName(p).Equals("next.config.js", StringComparison.OrdinalIgnoreCase)) ||
         paths.Any(p => Path.GetFileName(p).Equals("next.config.mjs", StringComparison.OrdinalIgnoreCase)) ||
         paths.Any(p => Path.GetFileName(p).Equals("next.config.ts", StringComparison.OrdinalIgnoreCase)) ||
         paths.Any(p => p.Contains("/app/page.tsx", StringComparison.OrdinalIgnoreCase)) ||
         paths.Any(p => p.Contains("/pages/_app.", StringComparison.OrdinalIgnoreCase)));

    private static bool HasReact(IReadOnlyList<string> paths) =>
        HasNodePackage(paths) &&
        (paths.Any(p => Path.GetFileName(p).Equals("vite.config.ts", StringComparison.OrdinalIgnoreCase)) ||
         paths.Any(p => Path.GetFileName(p).Equals("vite.config.js", StringComparison.OrdinalIgnoreCase)) ||
         paths.Any(p => p.Contains("/src/App.tsx", StringComparison.OrdinalIgnoreCase)) ||
         paths.Any(p => p.Contains("/src/App.jsx", StringComparison.OrdinalIgnoreCase)) ||
         paths.Any(p => p.Contains("/src/main.tsx", StringComparison.OrdinalIgnoreCase)));

    private static bool HasVue(IReadOnlyList<string> paths) =>
        paths.Any(p => Path.GetFileName(p).StartsWith("vue.config", StringComparison.OrdinalIgnoreCase)) ||
        paths.Any(p => Path.GetFileName(p).StartsWith("nuxt.config", StringComparison.OrdinalIgnoreCase)) ||
        paths.Any(p => p.EndsWith(".vue", StringComparison.OrdinalIgnoreCase));

    private static bool HasAngular(IReadOnlyList<string> paths) =>
        paths.Any(p => Path.GetFileName(p).Equals("angular.json", StringComparison.OrdinalIgnoreCase)) ||
        (HasNodePackage(paths) && paths.Any(p => p.Contains("angular", StringComparison.OrdinalIgnoreCase)));

    private static bool HasFlutter(IReadOnlyList<string> paths) =>
        paths.Any(p => Path.GetFileName(p).Equals("pubspec.yaml", StringComparison.OrdinalIgnoreCase));

    private static bool HasTerraform(IReadOnlyList<string> paths) =>
        paths.Any(p =>
            p.EndsWith(".tf", StringComparison.OrdinalIgnoreCase) &&
            !IsVendorPath(p) &&
            !IsContribOrExamplePath(p)) ||
        paths.Any(p =>
            Path.GetFileName(p).Equals(".terraform.lock.hcl", StringComparison.OrdinalIgnoreCase) &&
            !IsContribOrExamplePath(p));

    private static bool HasRootGoModule(IReadOnlyList<string> paths) =>
        paths.Any(p =>
            Path.GetFileName(p).Equals("go.mod", StringComparison.OrdinalIgnoreCase) &&
            !IsVendorPath(p) &&
            !p.Contains('/', StringComparison.Ordinal));

    private static bool IsContribOrExamplePath(string path) =>
        path.Contains("/contrib/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/examples/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/example/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/vendor/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("contrib/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("examples/", StringComparison.OrdinalIgnoreCase);

    private static bool HasContainerOrchestration(IReadOnlyList<string> paths) =>
        paths.Any(p => Path.GetFileName(p).Equals("Dockerfile", StringComparison.OrdinalIgnoreCase)) ||
        paths.Any(p => Path.GetFileName(p).Contains("docker-compose", StringComparison.OrdinalIgnoreCase)) ||
        paths.Any(p => p.Contains("/k8s/", StringComparison.OrdinalIgnoreCase)) ||
        paths.Any(p => p.Contains("/kubernetes/", StringComparison.OrdinalIgnoreCase)) ||
        paths.Any(p => p.Contains("/helm/", StringComparison.OrdinalIgnoreCase)) ||
        paths.Any(p => p.EndsWith("Chart.yaml", StringComparison.OrdinalIgnoreCase));

    private static bool IsKnownLinuxKernelRepo(string? repoName, IReadOnlyList<string> paths) =>
        string.Equals(repoName, "linux", StringComparison.OrdinalIgnoreCase) &&
        (paths.Any(p => Path.GetFileName(p).Equals("Kconfig", StringComparison.OrdinalIgnoreCase)) ||
         paths.Any(p => Path.GetFileName(p).Equals("Makefile", StringComparison.OrdinalIgnoreCase)) ||
         CountNativeCFiles(paths) >= 5);

    private static bool HasLinuxKernelTree(IReadOnlyList<string> paths) =>
        paths.Any(p => Path.GetFileName(p).Equals("Kconfig", StringComparison.OrdinalIgnoreCase)) &&
        paths.Any(p => Path.GetFileName(p).Equals("Makefile", StringComparison.OrdinalIgnoreCase)) &&
        CountNativeCFiles(paths) >= 20;

    private static bool HasCDominantNativeCode(IReadOnlyList<string> paths)
    {
        if (HasLinuxKernelTree(paths))
        {
            return false;
        }

        var cCount = CountNativeCFiles(paths);
        var pyCount = paths.Count(p =>
            p.EndsWith(".py", StringComparison.OrdinalIgnoreCase) && !IsKernelScriptsPath(p));

        return cCount >= 5 && cCount >= Math.Max(pyCount * 2, 3);
    }

    private static int CountNativeCFiles(IReadOnlyList<string> paths) =>
        paths.Count(p =>
            (p.EndsWith(".c", StringComparison.OrdinalIgnoreCase) ||
             p.EndsWith(".h", StringComparison.OrdinalIgnoreCase)) &&
            !IsVendorPath(p));

    private static bool IsKernelScriptsPath(string path) =>
        path.Contains("/scripts/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("scripts/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/Documentation/", StringComparison.OrdinalIgnoreCase);

    private static bool HasPython(IReadOnlyList<string> paths) =>
        !HasLinuxKernelTree(paths) &&
        !HasCDominantNativeCode(paths) &&
        (paths.Any(p => IsPythonManifest(Path.GetFileName(p)) && !IsKernelScriptsPath(p)) ||
         paths.Count(p =>
             p.EndsWith(".py", StringComparison.OrdinalIgnoreCase) && !IsKernelScriptsPath(p)) >= 2);

    public static bool ShouldPreferGitHubLanguage(
        string? gitHubLanguage,
        StackAnalysis stackAnalysis,
        IReadOnlyList<string> paths) =>
        IsNativeGitHubLanguage(gitHubLanguage) &&
        stackAnalysis.Framework.Contains("Python", StringComparison.OrdinalIgnoreCase) &&
        (HasCDominantNativeCode(paths) || CountNativeCFiles(paths) >= 3);

    public static StackAnalysis AnalyzeWithGitHubLanguageHint(
        IReadOnlyList<string> paths,
        string? gitHubLanguage,
        string? repoName = null)
    {
        var analysis = Analyze(paths, repoName);
        if (!ShouldPreferGitHubLanguage(gitHubLanguage, analysis, paths))
        {
            return analysis;
        }

        var cRule = Rules.FirstOrDefault(r => r.Id == "c-native");
        if (cRule is null || !cRule.Matches(paths))
        {
            return analysis;
        }

        var keyFiles = cRule.KeyFileSelector?.Invoke(paths) ?? SelectFallbackKeys(paths);
        return new StackAnalysis(
            cRule.Framework,
            cRule.Layout,
            ["GitHub primary language: C", .. analysis.StackSignals.Take(6)],
            keyFiles,
            false);
    }

    private static bool HasGo(IReadOnlyList<string> paths) =>
        paths.Any(p => Path.GetFileName(p).Equals("go.mod", StringComparison.OrdinalIgnoreCase));

    private static bool HasRust(IReadOnlyList<string> paths) =>
        paths.Any(p => Path.GetFileName(p).Equals("Cargo.toml", StringComparison.OrdinalIgnoreCase));

    private static bool HasRuby(IReadOnlyList<string> paths) =>
        paths.Any(p => Path.GetFileName(p).Equals("Gemfile", StringComparison.OrdinalIgnoreCase));

    private static bool HasPhp(IReadOnlyList<string> paths) =>
        paths.Any(p => Path.GetFileName(p).Equals("composer.json", StringComparison.OrdinalIgnoreCase));

    private static bool HasWpf(IReadOnlyList<string> paths) =>
        !ProjectStackDetector.IsUnityProject(paths) &&
        paths.Any(p => p.Contains("App.xaml", StringComparison.OrdinalIgnoreCase));

    private static bool HasWinForms(IReadOnlyList<string> paths) =>
        paths.Any(p => p.EndsWith("Form1.cs", StringComparison.OrdinalIgnoreCase)) ||
        paths.Any(p =>
            p.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) &&
            (p.Contains("WinForms", StringComparison.OrdinalIgnoreCase) ||
             p.Contains("WindowsForms", StringComparison.OrdinalIgnoreCase)));

    private static bool HasDotNetConsole(IReadOnlyList<string> paths) =>
        !ProjectStackDetector.IsWebAspNetProject(paths) &&
        paths.Any(p => Path.GetFileName(p).Equals("Program.cs", StringComparison.OrdinalIgnoreCase));

    private static bool IsFullStackDotNetReact(IReadOnlyList<string> paths) =>
        ProjectStackDetector.IsWebAspNetProject(paths) && HasReact(paths);

    private static IReadOnlyList<string> SelectFullStackKeys(IReadOnlyList<string> paths)
    {
        var apiKeys = SelectDotNetKeys(paths);
        var clientKeys = SelectFrontendKeys(paths);
        return apiKeys.Concat(clientKeys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
    }

    private static bool HasNodePackage(IReadOnlyList<string> paths) =>
        paths.Any(p =>
            !IsVendorPath(p) &&
            Path.GetFileName(p).Equals("package.json", StringComparison.OrdinalIgnoreCase));

    private static bool HasStaticSite(IReadOnlyList<string> paths)
    {
        var codeFiles = paths.Count(p =>
            p.EndsWith(".py", StringComparison.OrdinalIgnoreCase) ||
            p.EndsWith(".java", StringComparison.OrdinalIgnoreCase) ||
            p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
            HasNodePackage([p]));

        if (codeFiles > 2)
        {
            return false;
        }

        return paths.Any(p => p.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) &&
               paths.Any(p => p.EndsWith(".html", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPythonManifest(string? fileName) =>
        fileName is "requirements.txt" or "pyproject.toml" or "setup.py" or "Pipfile" or "poetry.lock" or
        "requirements-dev.txt";

    private static bool IsVendorPath(string path) =>
        path.Contains("/node_modules/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/vendor/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/.venv/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/dist/", StringComparison.OrdinalIgnoreCase);

    private static string InferGenericLayout(IReadOnlyList<string> paths)
    {
        if (paths.Any(p => p.EndsWith(".ipynb", StringComparison.OrdinalIgnoreCase)))
        {
            return "Notebook / Data";
        }

        if (paths.Count(p => p.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) >= 3 &&
            paths.Count(p => p.Contains('.', StringComparison.Ordinal)) <= 8)
        {
            return "Documentation";
        }

        return "Flat Monolith";
    }

    private static IReadOnlyList<string> SelectPlaywrightKeys(IReadOnlyList<string> paths) =>
        SelectByNames(paths,
                "playwright.config.ts", "playwright.config.js", "package.json")
            .Concat(SelectByExtension(paths, ".spec.ts", max: 4))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

    private static IReadOnlyList<string> SelectJsTestKeys(IReadOnlyList<string> paths) =>
        SelectByNames(paths, "package.json", "tsconfig.json", "jest.config.js", "vitest.config.ts")
            .Concat(paths.Where(p =>
                    !IsVendorPath(p) &&
                    (p.EndsWith(".spec.ts", StringComparison.OrdinalIgnoreCase) ||
                     p.EndsWith(".test.ts", StringComparison.OrdinalIgnoreCase)))
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Cast<string>()
                .Take(4))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

    private static IReadOnlyList<string> SelectFrontendKeys(IReadOnlyList<string> paths) =>
        SelectByNames(paths,
                "package.json", "vite.config.ts", "next.config.js", "angular.json", "tsconfig.json")
            .Concat(paths.Where(p =>
                    !IsVendorPath(p) &&
                    (p.EndsWith("App.tsx", StringComparison.OrdinalIgnoreCase) ||
                     p.EndsWith("App.vue", StringComparison.OrdinalIgnoreCase)))
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Cast<string>()
                .Take(4))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

    private static bool IsNativeGitHubLanguage(string? language) =>
        !string.IsNullOrWhiteSpace(language) &&
        (language.Equals("C", StringComparison.OrdinalIgnoreCase) ||
         language.StartsWith("C++", StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<string> SelectKernelKeys(IReadOnlyList<string> paths) =>
        SelectByNames(paths, "Makefile", "Kconfig", "README")
            .Concat(paths
                .Where(p =>
                    (p.EndsWith(".c", StringComparison.OrdinalIgnoreCase) ||
                     p.EndsWith(".h", StringComparison.OrdinalIgnoreCase)) &&
                    (p.StartsWith("kernel/", StringComparison.OrdinalIgnoreCase) ||
                     p.StartsWith("drivers/", StringComparison.OrdinalIgnoreCase) ||
                     p.StartsWith("include/", StringComparison.OrdinalIgnoreCase) ||
                     p.StartsWith("arch/", StringComparison.OrdinalIgnoreCase)))
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Cast<string>()
                .Take(6))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

    private static IReadOnlyList<string> SelectCNativeKeys(IReadOnlyList<string> paths) =>
        SelectByNames(paths, "Makefile", "README", "readme.md")
            .Concat(paths
                .Where(p =>
                    (p.EndsWith(".c", StringComparison.OrdinalIgnoreCase) ||
                     p.EndsWith(".h", StringComparison.OrdinalIgnoreCase)) &&
                    !IsKernelScriptsPath(p) &&
                    !IsVendorPath(p))
                .OrderBy(p => p.Count(c => c == '/'))
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Cast<string>()
                .Take(8))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

    private static IReadOnlyList<string> SelectPythonKeys(IReadOnlyList<string> paths)
    {
        var manifests = SelectByNames(paths,
            "requirements.txt", "pyproject.toml", "setup.py", "Pipfile", "Dockerfile", "main.py", "app.py", "__main__.py");
        var modules = paths
            .Where(p => p.EndsWith(".py", StringComparison.OrdinalIgnoreCase) && !IsVendorPath(p))
            .Select(Path.GetFileName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .OrderByDescending(n => n!.Equals("main.py", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .Cast<string>()
            .Take(6);
        return manifests.Concat(modules).Distinct(StringComparer.OrdinalIgnoreCase).Take(10).ToList();
    }

    private static IReadOnlyList<string> SelectGoKeys(IReadOnlyList<string> paths) =>
        SelectByNames(paths, "go.mod", "main.go", "go.sum")
            .Concat(paths.Where(p => p.EndsWith(".go", StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Cast<string>()
                .Take(4))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

    private static IReadOnlyList<string> SelectJavaKeys(IReadOnlyList<string> paths) =>
        SelectByNames(paths, "pom.xml", "build.gradle", "build.gradle.kts", "application.yml", "application.yaml")
            .Concat(paths.Where(p => p.EndsWith(".java", StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Cast<string>()
                .Take(4))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

    private static IReadOnlyList<string> SelectDotNetKeys(IReadOnlyList<string> paths) =>
        SelectByNames(paths, "Program.cs", "App.xaml", "appsettings.json")
            .Concat(paths.Where(p => p.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Cast<string>()
                .Take(3))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

    private static IReadOnlyList<string> SelectDevOpsKeys(IReadOnlyList<string> paths) =>
        SelectByNames(paths, "Dockerfile", "docker-compose.yml", "docker-compose.yaml", "Chart.yaml")
            .Concat(SelectByExtension(paths, ".yaml", max: 4))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

    private static IReadOnlyList<string> SelectStaticKeys(IReadOnlyList<string> paths) =>
        paths
            .Where(p => p.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
                        p.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

    private static IReadOnlyList<string> SelectFallbackKeys(IReadOnlyList<string> paths)
    {
        var interesting = paths
            .Where(p => !IsVendorPath(p))
            .Select(p => Path.GetFileName(p))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Where(n =>
                n!.Contains('.', StringComparison.Ordinal) &&
                !n.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) &&
                !n.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
                !n.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(n => n.Length)
            .Take(8)
            .ToList();

        return interesting;
    }

    private static List<string> SelectByNames(IReadOnlyList<string> paths, params string[] fileNames)
    {
        var set = new HashSet<string>(fileNames, StringComparer.OrdinalIgnoreCase);
        return paths
            .Where(p => set.Contains(Path.GetFileName(p)))
            .Select(Path.GetFileName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<string> SelectByExtension(IReadOnlyList<string> paths, string extension, int max)
    {
        return paths
            .Where(p => p.EndsWith(extension, StringComparison.OrdinalIgnoreCase) && !IsVendorPath(p))
            .Select(Path.GetFileName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(max);
    }
}
