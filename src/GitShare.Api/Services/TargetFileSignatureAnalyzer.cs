using System.Text;



namespace GitShare.Api.Services;



internal static class TargetFileSignatureAnalyzer

{

    private static readonly string[] EntryPointFileNames =

    [

        "Program.cs",

        "App.xaml.cs",

        "MainViewModel.cs"

    ];



    private static readonly string[] ConfigurationExtensions =

    [

        ".csproj",

        ".sln",

        ".config",

        ".props",

        ".targets"

    ];



    private static readonly string[] ConfigurationFileNames =

    [

        "appsettings.json",

        "appsettings.Development.json",

        "global.json",

        "Directory.Build.props",

        "App.config",

        "web.config",

        "launchSettings.json",

        "manifest.json",

        "ProjectVersion.txt",

        "package.json",

        "pyproject.toml",

        "requirements.txt",

        "go.mod",

        "Cargo.toml",

        "composer.json",

        "Gemfile",

        "pubspec.yaml",

        "playwright.config.ts",

        "docker-compose.yml"

    ];



    public static string BuildManifest(string repoName, IEnumerable<string> blobPaths, string? gitHubPrimaryLanguage = null)

    {

        var paths = blobPaths

            .Select(p => p.Replace('\\', '/'))

            .Distinct(StringComparer.OrdinalIgnoreCase)

            .ToList();



        if (paths.Count == 0)

        {

            return $"Repo: {repoName} — file tree empty or unavailable.";

        }



        var isUnity = ProjectStackDetector.IsUnityProject(paths);



        var entryPoints = isUnity ? DetectUnityEntryPoints(paths) : DetectEntryPoints(paths);

        var dataLayer = DetectDataLayerFiles(paths);

        var patterns = DetectPatternFiles(paths, isUnity);

        var configuration = DetectConfigurationFiles(paths, isUnity);

        var stackAnalysis = ProjectStackCatalog.AnalyzeWithGitHubLanguageHint(paths, gitHubPrimaryLanguage, repoName);

        var stackSignals = stackAnalysis.StackSignals.Count > 0
            ? stackAnalysis.StackSignals.ToList()
            : DetectStackSignals(paths, isUnity);

        var architectureSignals = DetectArchitectureSignals(paths, isUnity);

        var keyFiles = stackAnalysis.KeyFiles.Count > 0
            ? stackAnalysis.KeyFiles.ToList()
            : DetectKeyFiles(paths, isUnity, entryPoints, dataLayer, patterns, configuration);



        var sb = new StringBuilder();

        sb.AppendLine($"Repo: {repoName}");

        sb.AppendLine($"Primary framework: {stackAnalysis.Framework}");

        sb.AppendLine($"Suggested layout: {stackAnalysis.Layout}");

        if (stackAnalysis.IsScriptTestOrInfraUtility)
        {
            sb.AppendLine("Utility/test stack: yes");
        }

        sb.AppendLine($"Entry Points: {FormatList(entryPoints, "none detected")}");

        sb.AppendLine($"Data Layer: {FormatList(dataLayer, "no dedicated data-layer files")}");

        sb.AppendLine($"Patterns (Controller/Hub/DTO/Converter): {FormatList(patterns, "none")}");

        sb.AppendLine($"Configuration: {FormatList(configuration, "no project/config files")}");

        sb.AppendLine($"Stack signals: {FormatList(stackSignals, "generic layout")}");

        sb.AppendLine($"Architecture signals: {FormatList(architectureSignals, "none")}");

        sb.AppendLine(

            keyFiles.Count > 0

                ? $"Detected key files: {string.Join(", ", keyFiles)}"

                : "Detected key files: (insufficient architectural markers)");



        return sb.ToString().Trim();

    }



    private static List<string> DetectKeyFiles(

        IReadOnlyList<string> paths,

        bool isUnity,

        IReadOnlyList<string> entryPoints,

        IReadOnlyList<string> dataLayer,

        IReadOnlyList<string> patterns,

        IReadOnlyList<string> configuration)

    {

        if (isUnity)

        {

            var unityKeys = ProjectStackDetector.SelectUnityKeyFileNames(paths);

            if (unityKeys.Count > 0)

            {

                return unityKeys;

            }

        }



        return entryPoints

            .Concat(dataLayer)

            .Concat(patterns)

            .Concat(configuration)

            .Distinct(StringComparer.OrdinalIgnoreCase)

            .Take(12)

            .ToList();

    }



    private static List<string> DetectUnityEntryPoints(IReadOnlyList<string> paths)

    {

        var keyScripts = ProjectStackDetector.SelectUnityKeyFileNames(paths, maxCount: 4);

        if (keyScripts.Count > 0)

        {

            return keyScripts;

        }



        if (ProjectStackDetector.HasUnityPluginLayout(paths))

        {

            return ["Unity plugin (Assets/Plugins/)"];

        }



        return ["Unity scripts (Assets/)"];

    }



    private static List<string> DetectEntryPoints(IReadOnlyList<string> paths)

    {

        var found = new List<string>();



        foreach (var expected in EntryPointFileNames)

        {

            if (paths.Any(path => Path.GetFileName(path)

                    .Equals(expected, StringComparison.OrdinalIgnoreCase)))

            {

                found.Add(expected);

            }

        }



        return found;

    }



    private static List<string> DetectDataLayerFiles(IReadOnlyList<string> paths) =>

        paths

            .Where(p => !ProjectStackDetector.IsUnityAssetPath(p))

            .Select(Path.GetFileName)

            .Where(name => !string.IsNullOrWhiteSpace(name))

            .Where(name =>

                name!.EndsWith("Context.cs", StringComparison.OrdinalIgnoreCase) ||

                name.EndsWith("Repository.cs", StringComparison.OrdinalIgnoreCase) ||

                name.Contains("Helper", StringComparison.OrdinalIgnoreCase))

            .Cast<string>()

            .Distinct(StringComparer.OrdinalIgnoreCase)

            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)

            .Take(8)

            .ToList();



    private static List<string> DetectPatternFiles(IReadOnlyList<string> paths, bool isUnity) =>

        paths

            .Select(p => (Path: p, Name: Path.GetFileName(p)))

            .Where(x => !string.IsNullOrWhiteSpace(x.Name))

            .Where(x =>

                x.Name!.EndsWith("Controller.cs", StringComparison.OrdinalIgnoreCase) ||

                x.Name.EndsWith("Hub.cs", StringComparison.OrdinalIgnoreCase) ||

                x.Name.EndsWith("DTO.cs", StringComparison.OrdinalIgnoreCase) ||

                x.Name.EndsWith("Converter.cs", StringComparison.OrdinalIgnoreCase))

            .Select(x => isUnity && ProjectStackDetector.IsUnityMvcStyleController(x.Path)

                ? $"{x.Name} (Unity)"

                : x.Name!)

            .Distinct(StringComparer.OrdinalIgnoreCase)

            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)

            .Take(8)

            .ToList();



    private static List<string> DetectConfigurationFiles(IReadOnlyList<string> paths, bool isUnity)

    {

        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);



        if (isUnity)

        {

            if (paths.Any(p => p.Contains("ProjectSettings/", StringComparison.OrdinalIgnoreCase)))

            {

                found.Add("ProjectSettings/");

            }



            if (paths.Any(p =>

                    p.Equals("Packages/manifest.json", StringComparison.OrdinalIgnoreCase)))

            {

                found.Add("Packages/manifest.json");

            }

        }



        foreach (var path in paths)

        {

            var fileName = Path.GetFileName(path);

            if (string.IsNullOrWhiteSpace(fileName))

            {

                continue;

            }



            if (ConfigurationFileNames.Any(cfg =>

                    fileName.Equals(cfg, StringComparison.OrdinalIgnoreCase)))

            {

                found.Add(fileName);

                continue;

            }



            if (!isUnity &&

                ConfigurationExtensions.Any(ext =>

                    fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))

            {

                found.Add(fileName);

            }

        }



        return found.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).Take(8).ToList();

    }



    private static List<string> DetectStackSignals(IReadOnlyList<string> paths, bool isUnity)

    {

        var signals = new List<string>();



        if (isUnity)

        {

            signals.Add("Unity (ProjectSettings/Assets)");



            if (ProjectStackDetector.HasUnityPluginLayout(paths))

            {

                signals.Add("Unity package/plugin (Assets/Plugins/)");

            }



            if (ProjectStackDetector.HasUnitySamplePatternFolders(paths))

            {

                signals.Add("Unity architecture samples (MVC/MV/Flat folders)");

            }



            if (paths.Any(p => p.Contains("/Editor/", StringComparison.OrdinalIgnoreCase)))

            {

                signals.Add("Unity Editor scripts");

            }



            return signals.Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList();

        }



        if (ManifestSignalParser.HasJavaStackInTree(paths))

        {

            signals.Add("Java (Maven/Gradle)");

        }



        if (ManifestSignalParser.HasSpringSignalsInTree(paths))

        {

            signals.Add("Spring Boot");

        }



        if (paths.Any(p =>

                p.Contains("kubernetes", StringComparison.OrdinalIgnoreCase) ||

                p.Contains("docker-compose", StringComparison.OrdinalIgnoreCase)))

        {

            signals.Add("Kubernetes/Docker deployment");

        }



        if (paths.Any(p => p.Contains("App.xaml", StringComparison.OrdinalIgnoreCase)))

        {

            signals.Add("WPF (App.xaml)");

        }



        if (paths.Any(p =>

                p.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) &&

                (p.Contains("WinForms", StringComparison.OrdinalIgnoreCase) ||

                 p.Contains("WindowsForms", StringComparison.OrdinalIgnoreCase))))

        {

            signals.Add("WinForms (.csproj)");

        }



        if (ProjectStackDetector.IsWebAspNetProject(paths))

        {

            signals.Add("ASP.NET MVC/API");

        }



        if (paths.Any(p => Path.GetFileName(p)

                .Equals("Program.cs", StringComparison.OrdinalIgnoreCase)))

        {

            signals.Add(".NET Host (Program.cs)");

        }



        if (paths.Any(p => p.Contains("net9", StringComparison.OrdinalIgnoreCase) ||

                           p.Contains("net8", StringComparison.OrdinalIgnoreCase)))

        {

            signals.Add("Modern .NET SDK target");

        }



        if (paths.Any(p => p.EndsWith("appsettings.json", StringComparison.OrdinalIgnoreCase)))

        {

            signals.Add("appsettings.json");

        }



        if (paths.Any(p => p.EndsWith("Form1.cs", StringComparison.OrdinalIgnoreCase) ||

                           p.Contains("/Form", StringComparison.OrdinalIgnoreCase)))

        {

            signals.Add("WinForms UI (Form*.cs)");

        }



        return signals.Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList();

    }



    private static List<string> DetectArchitectureSignals(IReadOnlyList<string> paths, bool isUnity)

    {

        var signals = new List<string>();



        if (isUnity)

        {

            if (ProjectStackDetector.HasUnityPluginLayout(paths))

            {

                signals.Add("Unity Plugins/ layout");

            }



            if (ProjectStackDetector.HasUnitySamplePatternFolders(paths))

            {

                signals.Add("Unity multi-pattern samples");

            }



            if (paths.Any(p => p.Contains("/ViewModels/", StringComparison.OrdinalIgnoreCase)))

            {

                signals.Add("Unity ViewModels/ folder");

            }



            if (paths.Any(p =>

                    p.Contains("/Scripts/", StringComparison.OrdinalIgnoreCase) &&

                    p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)))

            {

                signals.Add("Unity Scripts/ folder");

            }



            if (ProjectStackDetector.HasUnityTestLayout(paths))

            {

                signals.Add("Unity test assemblies");

            }



            return signals.Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList();

        }



        if (paths.Any(p => p.EndsWith(".accdb", StringComparison.OrdinalIgnoreCase)))

        {

            signals.Add("Access database (.accdb)");

        }



        if (paths.Any(p => Path.GetFileName(p)

                .Equals("FileStorage.cs", StringComparison.OrdinalIgnoreCase)))

        {

            signals.Add("JSON file storage (FileStorage.cs)");

        }



        if (paths.Any(p => p.Contains("/Interfaces/", StringComparison.OrdinalIgnoreCase)))

        {

            signals.Add("Storage abstraction (Interfaces/)");

        }



        if (paths.Any(p => Path.GetFileName(p)

                .Equals("DataService.cs", StringComparison.OrdinalIgnoreCase)))

        {

            signals.Add("DataService layer");

        }



        if (paths.Any(p => p.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)))

        {

            signals.Add("SQL scripts (.sql)");

        }



        if (paths.Any(p => p.Contains("/Services/", StringComparison.OrdinalIgnoreCase)))

        {

            signals.Add("Services folder");

        }



        if (paths.Count(p => p.EndsWith("pom.xml", StringComparison.OrdinalIgnoreCase)) >= 2)

        {

            signals.Add("Multi-module Maven");

        }



        if (paths.Any(p => p.Contains("/Converters/", StringComparison.OrdinalIgnoreCase)))

        {

            signals.Add("WPF Converters folder");

        }



        return signals.Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList();

    }



    private static string FormatList(IReadOnlyList<string> items, string emptyLabel) =>

        items.Count == 0 ? emptyLabel : string.Join(", ", items);

}


