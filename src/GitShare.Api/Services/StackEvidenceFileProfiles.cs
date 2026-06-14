namespace GitShare.Api.Services;

/// <summary>
/// Профили отбора файлов под стек: LLM и code-evidence читают релевантный код, а не случайный Program.cs.
/// </summary>
internal enum StackEvidenceProfile
{
    FullStackDotNetReact,
    WebApi,
    WinForms,
    Wpf,
    ConsoleUtility,
    Unity,
    JavaSpring,
    GenericProduction
}

internal static class StackEvidenceProfileResolver
{
    public static StackEvidenceProfile Resolve(string signatureManifest, IReadOnlyList<string> blobPaths)
    {
        var layout = StructuredAuditBuilder.ExtractManifestValue(signatureManifest, "Suggested layout:") ?? string.Empty;
        var framework = StructuredAuditBuilder.ExtractManifestValue(signatureManifest, "Primary framework:") ?? string.Empty;

        if (layout.Contains("Web API + SPA", StringComparison.OrdinalIgnoreCase) ||
            (framework.Contains("React", StringComparison.OrdinalIgnoreCase) &&
             ProjectClassClassifier.ManifestDescribesWebApi(signatureManifest)))
        {
            return StackEvidenceProfile.FullStackDotNetReact;
        }

        if (ProjectClassClassifier.ManifestDescribesWebApi(signatureManifest) ||
            layout.Contains("Web API", StringComparison.OrdinalIgnoreCase))
        {
            return StackEvidenceProfile.WebApi;
        }

        if (ProjectStackDetector.IsUnityProject(blobPaths) ||
            signatureManifest.Contains("Unity", StringComparison.OrdinalIgnoreCase))
        {
            return StackEvidenceProfile.Unity;
        }

        if (ManifestSignalParser.HasStackSignal(signatureManifest, "WinForms") ||
            signatureManifest.Contains("Form", StringComparison.OrdinalIgnoreCase))
        {
            return StackEvidenceProfile.WinForms;
        }

        if (ManifestSignalParser.HasStackSignal(signatureManifest, "WPF") ||
            signatureManifest.Contains("WPF", StringComparison.OrdinalIgnoreCase))
        {
            return StackEvidenceProfile.Wpf;
        }

        if (signatureManifest.Contains("FileStorage", StringComparison.OrdinalIgnoreCase) ||
            signatureManifest.Contains("IStorage", StringComparison.OrdinalIgnoreCase) ||
            layout.Contains("Console", StringComparison.OrdinalIgnoreCase))
        {
            return StackEvidenceProfile.ConsoleUtility;
        }

        if (ManifestSignalParser.HasStackSignal(signatureManifest, "Spring Boot") ||
            ManifestSignalParser.HasJavaStackInTree(blobPaths))
        {
            return StackEvidenceProfile.JavaSpring;
        }

        return StackEvidenceProfile.GenericProduction;
    }
}

internal static class StackEvidenceFileProfiles
{
    public const int MaxLlmSourceFiles = 4;
    public const int MaxLlmInfraFiles = 2;
    public const int MaxCodeEvidenceFiles = 12;
    public const int MaxLlmUtilitySourceFiles = 3;
    public const int MaxCharsLlmSource = 1_800;
    public const int MaxCharsLlmInfra = 500;

    private static readonly HashSet<string> SourceExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".java", ".kt", ".fs", ".vb", ".ts", ".tsx"
    };

    private static readonly HashSet<string> InfraExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".json", ".yml", ".yaml", ".csproj", ".props"
    };

    public static List<string> SelectLlmSourcePaths(
        StackEvidenceProfile profile,
        IReadOnlyList<string> blobPaths,
        string signatureManifest) =>
        SelectByRules(profile, Normalize(blobPaths), signatureManifest, MaxLlmSourceFiles, sourceOnly: true);

    public static List<string> SelectLlmInfraPaths(
        StackEvidenceProfile profile,
        IReadOnlyList<string> blobPaths) =>
        SelectInfraPaths(Normalize(blobPaths), MaxLlmInfraFiles);

    public static List<string> SelectCodeEvidencePaths(
        StackEvidenceProfile profile,
        IReadOnlyList<string> blobPaths,
        string signatureManifest)
    {
        var normalized = Normalize(blobPaths)
            .Where(p => p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (normalized.Count == 0)
        {
            return [];
        }

        var profilePaths = SelectByRules(profile, normalized, signatureManifest, MaxCodeEvidenceFiles, sourceOnly: true);

        foreach (var path in normalized)
        {
            if (profilePaths.Count >= MaxCodeEvidenceFiles)
            {
                break;
            }

            if (profilePaths.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (path.Contains("/Helpers/", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/Interfaces/", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/Services/", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/Storage/", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/ViewModels/", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/Repositories/", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/Data/", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/Converters/", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith("DbContext.cs", StringComparison.OrdinalIgnoreCase))
            {
                profilePaths.Add(path);
            }
        }

        return profilePaths.Take(MaxCodeEvidenceFiles).ToList();
    }

    private static List<string> SelectByRules(
        StackEvidenceProfile profile,
        IReadOnlyList<string> paths,
        string signatureManifest,
        int maxFiles,
        bool sourceOnly)
    {
        var rules = GetRules(profile, paths, signatureManifest);

        var selected = new List<string>();
        foreach (var rule in rules.OrderByDescending(r => r.Priority))
        {
            if (selected.Count >= maxFiles)
            {
                break;
            }

            var path = rule.Pick();
            if (path is null || selected.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (sourceOnly && !IsSourcePath(path))
            {
                continue;
            }

            selected.Add(path);
        }

        return selected;
    }

    private static List<EvidencePickRule> GetRules(
        StackEvidenceProfile profile,
        IReadOnlyList<string> paths,
        string signatureManifest) =>
        profile switch
        {
            StackEvidenceProfile.FullStackDotNetReact =>
            [
                Rule(100, () => BestProgramCs(paths)),
                Rule(95, () => FirstMatch(paths, p =>
                    p.Contains("/Controllers/", StringComparison.OrdinalIgnoreCase) &&
                    p.EndsWith("Controller.cs", StringComparison.OrdinalIgnoreCase))),
                Rule(90, () => FirstMatch(paths, p =>
                    p.EndsWith("DbContext.cs", StringComparison.OrdinalIgnoreCase) ||
                    (p.Contains("/Data/", StringComparison.OrdinalIgnoreCase) &&
                     p.EndsWith("Context.cs", StringComparison.OrdinalIgnoreCase)))),
                Rule(88, () => FirstMatch(paths, p =>
                    p.EndsWith("App.tsx", StringComparison.OrdinalIgnoreCase) ||
                    p.EndsWith("App.jsx", StringComparison.OrdinalIgnoreCase))),
                Rule(85, () => FirstMatch(paths, p =>
                    p.EndsWith("api.ts", StringComparison.OrdinalIgnoreCase) ||
                    p.EndsWith("api.tsx", StringComparison.OrdinalIgnoreCase))),
                Rule(80, () => FirstMatch(paths, p =>
                    p.Contains("/Services/", StringComparison.OrdinalIgnoreCase) &&
                    p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
            ],
            StackEvidenceProfile.WebApi =>
            [
                Rule(100, () => BestProgramCs(paths)),
                Rule(95, () => FirstMatch(paths, p =>
                    p.Contains("/Controllers/", StringComparison.OrdinalIgnoreCase) &&
                    p.EndsWith("Controller.cs", StringComparison.OrdinalIgnoreCase))),
                Rule(90, () => FirstMatch(paths, p =>
                    p.EndsWith("DbContext.cs", StringComparison.OrdinalIgnoreCase) ||
                    p.Contains("/Data/", StringComparison.OrdinalIgnoreCase))),
                Rule(85, () => FirstMatch(paths, p =>
                    p.Contains("/Services/", StringComparison.OrdinalIgnoreCase) &&
                    p.EndsWith("Service.cs", StringComparison.OrdinalIgnoreCase)))
            ],
            StackEvidenceProfile.WinForms =>
            [
                Rule(100, () => FirstMatch(paths, p =>
                    Path.GetFileName(p).StartsWith("Form", StringComparison.OrdinalIgnoreCase) &&
                    p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))),
                Rule(98, () => FirstMatch(paths, p => p.EndsWith("DbContext.cs", StringComparison.OrdinalIgnoreCase))),
                Rule(95, () => ByFileName(paths, "DbHelper.cs")),
                Rule(92, () => FirstMatch(paths, p =>
                    p.Contains("/Services/", StringComparison.OrdinalIgnoreCase) &&
                    p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))),
                Rule(90, () => FirstMatch(paths, p => p.Contains("/Helpers/", StringComparison.OrdinalIgnoreCase))),
                Rule(85, () => ByFileName(paths, "Program.cs"))
            ],
            StackEvidenceProfile.Wpf =>
            [
                Rule(100, () => ByFileName(paths, "MainWindow.xaml.cs")),
                Rule(95, () => ByFileName(paths, "DataService.cs")),
                Rule(90, () => FirstMatch(paths, p =>
                    p.Contains("/ViewModels/", StringComparison.OrdinalIgnoreCase) &&
                    p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))),
                Rule(88, () => FirstMatch(paths, p =>
                    p.Contains("Converter", StringComparison.OrdinalIgnoreCase) &&
                    p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))),
                Rule(85, () => ByFileName(paths, "App.xaml.cs"))
            ],
            StackEvidenceProfile.ConsoleUtility =>
            [
                Rule(100, () => BestProgramCs(paths)),
                Rule(95, () => ByFileName(paths, "FileStorage.cs")),
                Rule(90, () => ByFileName(paths, "IStorage.cs")),
                Rule(85, () => FirstMatch(paths, p =>
                    p.Contains("/Interfaces/", StringComparison.OrdinalIgnoreCase))),
                Rule(80, () => FirstMatch(paths, p =>
                    p.Contains("/Services/", StringComparison.OrdinalIgnoreCase)))
            ],
            StackEvidenceProfile.Unity =>
            [
                Rule(100, () => FirstMatch(paths, p =>
                    p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
                    p.Contains("/Plugins/", StringComparison.OrdinalIgnoreCase) &&
                    p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))),
                Rule(95, () => FirstMatch(paths, p =>
                    p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
                    p.Contains("/Scripts/", StringComparison.OrdinalIgnoreCase) &&
                    p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))),
                Rule(90, () => FirstMatch(paths, p =>
                    p.Contains("/Editor/", StringComparison.OrdinalIgnoreCase) &&
                    p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
            ],
            StackEvidenceProfile.JavaSpring =>
            [
                Rule(100, () => ByFileName(paths, "Application.java")),
                Rule(95, () => FirstMatch(paths, p =>
                    p.Contains("Controller", StringComparison.OrdinalIgnoreCase) &&
                    p.EndsWith(".java", StringComparison.OrdinalIgnoreCase))),
                Rule(90, () => FirstMatch(paths, p =>
                    p.EndsWith("Service.java", StringComparison.OrdinalIgnoreCase)))
            ],
            _ =>
            [
                Rule(100, () => BestProgramCs(paths)),
                Rule(90, () => FirstMatch(paths, p =>
                    p.Contains("/Services/", StringComparison.OrdinalIgnoreCase))),
                Rule(85, () => FirstMatch(paths, p =>
                    ManifestSignalParser.DetectedKeyFilesContain(signatureManifest, Path.GetFileName(p)))),
                Rule(80, () => FirstMatch(paths, p =>
                    p.Contains("Helper", StringComparison.OrdinalIgnoreCase) &&
                    p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
            ]
        };

    private static List<string> SelectInfraPaths(IReadOnlyList<string> paths, int maxFiles)
    {
        var candidates = new (int Priority, Func<string?> Pick)[]
        {
            (100, () => ByFileName(paths, "docker-compose.yml")),
            (95, () => FirstMatch(paths, p =>
                p.StartsWith(".github/workflows/", StringComparison.OrdinalIgnoreCase) &&
                (p.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
                 p.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)))),
            (92, () => FirstMatch(paths, p =>
                p.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) &&
                p.Contains("Tests", StringComparison.OrdinalIgnoreCase))),
            (88, () => FirstMatch(paths, p =>
                Path.GetFileName(p).Equals("package.json", StringComparison.OrdinalIgnoreCase) &&
                !p.Contains("/node_modules/", StringComparison.OrdinalIgnoreCase))),
            (85, () => FirstMatch(paths, p =>
                p.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) &&
                (p.Contains("Api", StringComparison.OrdinalIgnoreCase) ||
                 p.Contains("Web", StringComparison.OrdinalIgnoreCase)))),
            (80, () => FirstMatch(paths, p =>
                Path.GetFileName(p).Equals("vite.config.ts", StringComparison.OrdinalIgnoreCase))),
            (75, () => FirstMatch(paths, p =>
                Path.GetFileName(p).Equals("appsettings.json", StringComparison.OrdinalIgnoreCase)))
        };

        var selected = new List<string>();
        foreach (var (_, pick) in candidates.OrderByDescending(c => c.Priority))
        {
            if (selected.Count >= maxFiles)
            {
                break;
            }

            var path = pick();
            if (path is null || selected.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!IsInfraPath(path))
            {
                continue;
            }

            selected.Add(path);
        }

        return selected;
    }

    private static EvidencePickRule Rule(int priority, Func<string?> pick) => new(priority, pick);

    private sealed record EvidencePickRule(int Priority, Func<string?> Pick);

    private static List<string> Normalize(IReadOnlyList<string> blobPaths) =>
        blobPaths
            .Select(p => p.Replace('\\', '/'))
            .Where(p => !IsExcludedPath(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static bool IsExcludedPath(string path) =>
        path.Contains("/node_modules/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/obj/", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase);

    private static bool IsSourcePath(string path)
    {
        var ext = Path.GetExtension(path);
        return !string.IsNullOrEmpty(ext) && SourceExtensions.Contains(ext);
    }

    private static bool IsInfraPath(string path)
    {
        var ext = Path.GetExtension(path);
        return !string.IsNullOrEmpty(ext) && InfraExtensions.Contains(ext);
    }

    private static string? BestProgramCs(IReadOnlyList<string> paths) =>
        paths
            .Where(p => Path.GetFileName(p).Equals("Program.cs", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.Count(c => c == '/'))
            .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

    private static string? ByFileName(IReadOnlyList<string> paths, string fileName) =>
        paths.FirstOrDefault(p => Path.GetFileName(p).Equals(fileName, StringComparison.OrdinalIgnoreCase));

    private static string? FirstMatch(IReadOnlyList<string> paths, Func<string, bool> predicate) =>
        paths.FirstOrDefault(predicate);
}
