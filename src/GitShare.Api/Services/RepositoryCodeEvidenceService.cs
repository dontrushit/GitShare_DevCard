using System.Net;
using System.Text.RegularExpressions;

namespace GitShare.Api.Services;

/// <summary>
/// Загружает ключевые .cs с raw.githubusercontent.com и строит Pros/Cons только из проверяемых сигналов в коде и дереве.
/// </summary>
public sealed class RepositoryCodeEvidenceService(
    IHttpClientFactory httpClientFactory,
    ILogger<RepositoryCodeEvidenceService> logger)
{
    private const string GitHubHttpClientName = "GitHub";
    private const int MaxSourceFiles = 8;
    private const int MaxCharsPerFile = 14_000;

    private static readonly string[] PriorityFileNames =
    [
        "Program.cs",
        "App.xaml.cs",
        "MainWindow.xaml.cs",
        "DbHelper.cs",
        "DataService.cs",
        "FileStorage.cs",
        "TaskService.cs",
        "MainViewModel.cs",
        "IStorage.cs"
    ];

    public async Task<(IReadOnlyList<string> Pros, IReadOnlyList<string> Cons)> AnalyzeAsync(
        string owner,
        string repoName,
        IReadOnlyList<string> blobPaths,
        string signatureManifest,
        CancellationToken cancellationToken)
    {
        var paths = blobPaths
            .Select(p => p.Replace('\\', '/'))
            .Where(p => p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (paths.Count == 0)
        {
            return ([], []);
        }

        var selected = SelectSourcePaths(paths, signatureManifest);
        if (selected.Count == 0)
        {
            return ([], []);
        }

        var sources = await FetchSourcesAsync(owner, repoName, selected, cancellationToken);
        if (sources.Count == 0)
        {
            return ([], []);
        }

        var facts = CodeEvidenceFacts.From(paths, signatureManifest, sources);
        return CodeEvidenceProsConsBuilder.Build(facts);
    }

    private static List<string> SelectSourcePaths(
        IReadOnlyList<string> blobPaths,
        string signatureManifest)
    {
        var isUnity = signatureManifest.Contains("Unity", StringComparison.OrdinalIgnoreCase);
        var selected = new List<string>();

        if (isUnity)
        {
            foreach (var fileName in ProjectStackDetector.SelectUnityKeyFileNames(blobPaths, maxCount: MaxSourceFiles))
            {
                var match = blobPaths.FirstOrDefault(p =>
                    p.EndsWith('/' + fileName, StringComparison.OrdinalIgnoreCase) ||
                    p.Equals(fileName, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                {
                    selected.Add(match);
                }
            }
        }

        foreach (var name in PriorityFileNames)
        {
            if (selected.Count >= MaxSourceFiles)
            {
                break;
            }

            var match = blobPaths.FirstOrDefault(p =>
                p.EndsWith('/' + name, StringComparison.OrdinalIgnoreCase) ||
                p.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (match is not null && !selected.Contains(match, StringComparer.OrdinalIgnoreCase))
            {
                selected.Add(match);
            }
        }

        foreach (var path in blobPaths)
        {
            if (selected.Count >= MaxSourceFiles)
            {
                break;
            }

            if (selected.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (isUnity)
            {
                if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
                    (path.Contains("/Plugins/", StringComparison.OrdinalIgnoreCase) ||
                     path.Contains("/Editor/", StringComparison.OrdinalIgnoreCase) ||
                     path.Contains("/Scripts/", StringComparison.OrdinalIgnoreCase)))
                {
                    selected.Add(path);
                }

                continue;
            }

            if (path.Contains("/Converters/", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/Interfaces/", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/Services/", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/Helpers/", StringComparison.OrdinalIgnoreCase))
            {
                selected.Add(path);
            }
        }

        return selected.Take(MaxSourceFiles).ToList();
    }

    private async Task<Dictionary<string, string>> FetchSourcesAsync(
        string owner,
        string repoName,
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(GitHubHttpClientName);
        var tasks = paths.Select(async path =>
        {
            var content = await FetchRawFileAsync(client, owner, repoName, path, cancellationToken);
            return (path, content);
        });

        var results = await Task.WhenAll(tasks);
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (path, content) in results)
        {
            if (!string.IsNullOrWhiteSpace(content))
            {
                dict[path] = content.Length > MaxCharsPerFile
                    ? content[..MaxCharsPerFile]
                    : content;
            }
        }

        return dict;
    }

    private async Task<string?> FetchRawFileAsync(
        HttpClient client,
        string owner,
        string repoName,
        string path,
        CancellationToken cancellationToken)
    {
        var encodedPath = string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

        foreach (var branch in new[] { "main", "master" })
        {
            var url = $"https://raw.githubusercontent.com/{owner}/{repoName}/{branch}/{encodedPath}";
            try
            {
                using var response = await client.GetAsync(url, cancellationToken);
                if (response.StatusCode is HttpStatusCode.NotFound || !response.IsSuccessStatusCode)
                {
                    continue;
                }

                return await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to fetch {Path} from {Branch}", path, branch);
            }
        }

        return null;
    }
}

internal sealed class CodeEvidenceFacts
{
    private static readonly Regex DataLayerFile = new(
        @"\b(Program|DbHelper|DataService|FileStorage|TaskService|MainWindow)\.cs$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TryBlock = new(@"\btry\s*\{", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CatchWithMessageBox = new(
        @"catch\s*\([^)]*\)\s*\{[^}]*MessageBox",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex PaginationSignal = new(
        @"(PageSize|Skip\(|\.Take\(|CurrentPage)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AsyncAwait = new(
        @"\basync\s+(?:Task|void)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex StaticDbHelper = new(
        @"static\s+class\s+DbHelper",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DiRegistration = new(
        @"(AddSingleton|AddScoped|AddTransient|IServiceCollection)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HardcodedUserPath = new(
        @"[A-Z]:\\Users\\",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MonoBehaviourClass = new(
        @"class\s+\w+\s*:\s*MonoBehaviour",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PlainCSharpClass = new(
        @"class\s+\w+(?:\s*:\s*(?!MonoBehaviour)\w+)?\s*\{",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public bool HasUnityProject { get; init; }
    public bool UsesUnityEngineApi { get; init; }
    public bool HasMonoBehaviourScripts { get; init; }
    public bool HasPureCSharpClasses { get; init; }
    public bool HasUnityPluginsFolder { get; init; }
    public bool HasUnityEditorScripts { get; init; }
    public bool HasUnityTestsInTree { get; init; }
    public bool HasGameDiFramework { get; init; }
    public bool HasUnitySamplePatterns { get; init; }

    public bool HasTryCatchInDataLayer { get; init; }
    public string? TryCatchFile { get; init; }
    public bool HasAsyncAwait { get; init; }
    public bool HasOleDbInSource { get; init; }
    public bool HasMessageBoxInCatch { get; init; }
    public bool HasHardcodedUserPath { get; init; }
    public string? HardcodedPathFile { get; init; }
    public bool HasPaginationInSource { get; init; }
    public bool PaginationInCodeBehind { get; init; }
    public string? PaginationFile { get; init; }
    public bool HasStaticDbHelper { get; init; }
    public bool HasDiRegistration { get; init; }
    public bool HasIStorageAbstraction { get; init; }

    public bool HasRepositoryInTree { get; init; }
    public bool HasInterfacesFolder { get; init; }
    public bool HasServicesFolder { get; init; }
    public bool HasConvertersFolder { get; init; }
    public bool HasViewModelsFolder { get; init; }
    public bool HasAppsettings { get; init; }
    public bool HasWinFormsSignals { get; init; }
    public bool HasWpfSignals { get; init; }
    public bool HasFileStorageFile { get; init; }
    public bool HasProgramCs { get; init; }
    public bool HasCompositionRootPattern { get; init; }

    public static CodeEvidenceFacts From(
        IReadOnlyList<string> blobPaths,
        string manifest,
        IReadOnlyDictionary<string, string> sources)
    {
        var combined = string.Join('\n', sources.Values);
        var pathSet = blobPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var isUnity = ProjectStackDetector.IsUnityProject(blobPaths) ||
                      manifest.Contains("Unity", StringComparison.OrdinalIgnoreCase);

        string? tryCatchFile = null;
        var hasTryCatch = false;
        var hasMessageBoxCatch = false;

        foreach (var (path, text) in sources.OrderBy(kv => TryCatchScanPriority(kv.Key)))
        {
            var isRelevantFile = DataLayerFile.IsMatch(path) ||
                                 path.Contains("DataService", StringComparison.OrdinalIgnoreCase) ||
                                 path.Contains("MainWindow", StringComparison.OrdinalIgnoreCase) ||
                                 path.Contains("Program.cs", StringComparison.OrdinalIgnoreCase) ||
                                 (isUnity && path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase));

            if (!isRelevantFile)
            {
                continue;
            }

            if (TryBlock.IsMatch(text))
            {
                hasTryCatch = true;
                tryCatchFile ??= ShortName(path);
            }

            if (CatchWithMessageBox.IsMatch(text))
            {
                hasMessageBoxCatch = true;
                tryCatchFile ??= ShortName(path);
            }
        }

        string? hardcodedFile = null;
        foreach (var (path, text) in sources)
        {
            if (HardcodedUserPath.IsMatch(text))
            {
                hardcodedFile = ShortName(path);
                break;
            }
        }

        string? paginationFile = null;
        var hasPagination = false;
        var paginationInCodeBehind = false;

        foreach (var (path, text) in sources)
        {
            if (!PaginationSignal.IsMatch(text))
            {
                continue;
            }

            hasPagination = true;
            paginationFile = ShortName(path);
            if (path.Contains("MainWindow", StringComparison.OrdinalIgnoreCase))
            {
                paginationInCodeBehind = true;
            }
        }

        return new CodeEvidenceFacts
        {
            HasUnityProject = isUnity,
            UsesUnityEngineApi = combined.Contains("UnityEngine", StringComparison.OrdinalIgnoreCase),
            HasMonoBehaviourScripts = MonoBehaviourClass.IsMatch(combined),
            HasPureCSharpClasses = isUnity && sources.Values.Any(text =>
                PlainCSharpClass.IsMatch(text) &&
                !text.Contains(": MonoBehaviour", StringComparison.OrdinalIgnoreCase)),
            HasUnityPluginsFolder = ProjectStackDetector.HasUnityPluginLayout(blobPaths),
            HasUnityEditorScripts = pathSet.Any(p =>
                p.Contains("/Editor/", StringComparison.OrdinalIgnoreCase) &&
                p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)),
            HasUnityTestsInTree = ProjectStackDetector.HasUnityTestLayout(blobPaths) ||
                                  manifest.Contains("test assemblies", StringComparison.OrdinalIgnoreCase),
            HasGameDiFramework = combined.Contains("Zenject", StringComparison.OrdinalIgnoreCase) ||
                                 combined.Contains("VContainer", StringComparison.OrdinalIgnoreCase) ||
                                 combined.Contains("[Inject]", StringComparison.OrdinalIgnoreCase),
            HasUnitySamplePatterns = ProjectStackDetector.HasUnitySamplePatternFolders(blobPaths) ||
                                     manifest.Contains("multi-pattern", StringComparison.OrdinalIgnoreCase),
            HasTryCatchInDataLayer = hasTryCatch,
            TryCatchFile = tryCatchFile,
            HasMessageBoxInCatch = hasMessageBoxCatch,
            HasAsyncAwait = AsyncAwait.IsMatch(combined),
            HasOleDbInSource = combined.Contains("OleDb", StringComparison.OrdinalIgnoreCase),
            HasHardcodedUserPath = hardcodedFile is not null,
            HardcodedPathFile = hardcodedFile,
            HasPaginationInSource = hasPagination,
            PaginationInCodeBehind = paginationInCodeBehind,
            PaginationFile = paginationFile,
            HasStaticDbHelper = sources.Any(kv =>
                kv.Key.Contains("DbHelper", StringComparison.OrdinalIgnoreCase) &&
                StaticDbHelper.IsMatch(kv.Value)),
            HasDiRegistration = DiRegistration.IsMatch(combined),
            HasIStorageAbstraction = pathSet.Any(p => p.Contains("IStorage", StringComparison.OrdinalIgnoreCase)) ||
                                     combined.Contains("interface IStorage", StringComparison.OrdinalIgnoreCase),
            HasRepositoryInTree = ManifestSignalParser.HasRepositoryLayerInTree(pathSet),
            HasInterfacesFolder = pathSet.Any(p =>
                p.Contains("/Interfaces/", StringComparison.OrdinalIgnoreCase)),
            HasServicesFolder = ManifestSignalParser.HasServicesFolderInTree(pathSet),
            HasConvertersFolder = ManifestSignalParser.HasConverterArtifacts(pathSet),
            HasViewModelsFolder = pathSet.Any(p =>
                p.Contains("/ViewModels/", StringComparison.OrdinalIgnoreCase)),
            HasAppsettings = pathSet.Any(p =>
                                    p.EndsWith("appsettings.json", StringComparison.OrdinalIgnoreCase)) ||
                                ManifestSignalParser.HasConfigurationFile(manifest, "appsettings.json"),
            HasWinFormsSignals = ManifestSignalParser.HasStackSignal(manifest, "WinForms") ||
                                 ManifestSignalParser.HasArchitectureSignal(manifest, "Form"),
            HasWpfSignals = ManifestSignalParser.HasStackSignal(manifest, "WPF") ||
                            ManifestSignalParser.HasArchitectureSignal(manifest, "Converters"),
            HasFileStorageFile = pathSet.Any(p =>
                p.Contains("FileStorage", StringComparison.OrdinalIgnoreCase)),
            HasProgramCs = pathSet.Any(p => p.EndsWith("Program.cs", StringComparison.OrdinalIgnoreCase)),
            HasCompositionRootPattern = UnityRepositoryHeuristics.HasCompositionRootPattern(blobPaths, manifest)
        };
    }

    private static string ShortName(string path) =>
        path.Replace('\\', '/').Split('/').Last();

    private static int TryCatchScanPriority(string path)
    {
        if (path.Contains("DataService", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (path.Contains("DbHelper", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (path.Contains("FileStorage", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (path.Contains("MainWindow", StringComparison.OrdinalIgnoreCase))
        {
            return 10;
        }

        return 5;
    }
}

internal static class CodeEvidenceProsConsBuilder
{
    public static (IReadOnlyList<string> Pros, IReadOnlyList<string> Cons) Build(CodeEvidenceFacts f)
    {
        var pros = new List<string>();
        var cons = new List<string>();

        if (f.HasUnityProject)
        {
            if (f.HasUnityPluginsFolder)
            {
                pros.Add("Плагин в Assets/Plugins/ — runtime-код отделён от сцен (проверено по дереву).");
            }

            if (f.HasUnityEditorScripts)
            {
                pros.Add("Editor-скрипты в репозитории — сборка/валидация через Unity Editor (дерево + код).");
            }

            if (f.HasUnitySamplePatterns)
            {
                pros.Add("Папки MVC/MV/Flat — несколько архитектурных стилей в одном Unity-репозитории.");
            }

            if (f.HasPureCSharpClasses)
            {
                pros.Add("Есть классы без MonoBehaviour — логика может тестироваться вне Play Mode (проверено в коде).");
            }
            else if (f.HasMonoBehaviourScripts)
            {
                pros.Add("Скрипты MonoBehaviour в выборке — стандартный Unity runtime-контур.");
            }

            if (f.HasGameDiFramework)
            {
                pros.Add("DI для Unity (Zenject/VContainer/[Inject]) найден в исходниках.");
            }

            if (f.HasUnityTestsInTree)
            {
                pros.Add("Тестовые сборки Unity (Tests/) присутствуют в дереве.");
            }

            if (f.HasCompositionRootPattern)
            {
                pros.Add("Composition Root / CompositeRoot в структуре — явная ручная композиция зависимостей (не хаотичный FindObject).");
            }

            var skipGenericUnityQualityCons = f.HasCompositionRootPattern ||
                                              f.HasUnityPluginsFolder ||
                                              f.HasUnityEditorScripts;

            if (!skipGenericUnityQualityCons && !f.HasUnityTestsInTree)
            {
                cons.Add("Тестовые папки Unity в дереве не найдены — автоматическая регрессия не видна.");
            }

            if (!skipGenericUnityQualityCons &&
                !f.HasCompositionRootPattern &&
                !f.HasGameDiFramework &&
                !f.HasDiRegistration)
            {
                cons.Add("DI-фреймворк в коде не обнаружен — зависимости, вероятно, через Inspector/FindObject.");
            }
        }

        if (f.HasTryCatchInDataLayer && f.TryCatchFile is not null)
        {
            pros.Add(
                f.HasMessageBoxInCatch
                    ? $"Обработка ошибок БД/IO: try/catch с уведомлением пользователя в {f.TryCatchFile} (проверено в коде)."
                    : $"Обработка ошибок доступа к данным: try/catch в {f.TryCatchFile} (проверено в коде).");
        }

        if (f.HasAsyncAwait)
        {
            var isDesktopUi = f.HasWinFormsSignals || f.HasWpfSignals;
            pros.Add(isDesktopUi
                ? "Асинхронные методы (async/await) в выборке ключевых файлов — меньше блокировки UI-потока."
                : "Асинхронные методы (async/await) в выборке ключевых файлов — неблокирующий I/O в консольном контуре.");
        }

        if (f.HasWpfSignals && f.HasConvertersFolder && f.HasViewModelsFolder)
        {
            pros.Add("MVVM-структура: ViewModels/ и Converters/ подтверждены в дереве репозитория.");
        }
        else if (f.HasWpfSignals && f.HasConvertersFolder)
        {
            pros.Add("WPF: конвертеры вынесены отдельными классами (*Converter.cs / Converters/) — привязки не в Views.");
        }

        if (f.HasServicesFolder)
        {
            var isDesktopUi = f.HasWinFormsSignals || f.HasWpfSignals;
            pros.Add(isDesktopUi
                ? "Папка Services/ — прикладная логика отделена от UI-слоя (дерево)."
                : "Папка Services/ — прикладная логика отделена от точки входа Program.cs (дерево).");
        }

        if (f.HasRepositoryInTree)
        {
            pros.Add("Repository/Context в структуре — персистентность вынесена из UI.");
        }

        if (f.HasIStorageAbstraction || f.HasInterfacesFolder)
        {
            pros.Add("Абстракция хранилища (IStorage / Interfaces/) — задел под DIP и подмену реализации.");
        }

        if (f.HasDiRegistration)
        {
            pros.Add("Регистрация зависимостей в DI (AddSingleton/AddScoped) — видно в Program.cs.");
        }

        if (f.HasPaginationInSource && f.PaginationFile is not null)
        {
            pros.Add($"Пагинация в коде ({f.PaginationFile}): PageSize/Skip|Take — контроль объёма выборки.");
        }

        if (f.HasOleDbInSource && !f.HasRepositoryInTree)
        {
            cons.Add("Прямой доступ к Access (.accdb) без Repository-слоя — UI и data-access связаны жёстко.");
        }

        if (f.HasStaticDbHelper && !f.HasRepositoryInTree)
        {
            cons.Add("Статический DbHelper — глобальное состояние, сложнее unit-тесты и параллельный доступ.");
        }

        if (f.HasHardcodedUserPath && f.HardcodedPathFile is not null)
        {
            cons.Add($"Захардкоженный путь пользователя в {f.HardcodedPathFile} — среда не переносится без правки кода.");
        }

        if (f.HasFileStorageFile && !f.HasIStorageAbstraction && !f.HasInterfacesFolder)
        {
            cons.Add("FileStorage без интерфейса в дереве — подмена хранилища только через правку класса.");
        }

        if (f.HasWinFormsSignals && !f.HasServicesFolder && !f.HasRepositoryInTree)
        {
            cons.Add("WinForms + Helpers без Services/Repository — flat monolith по структуре.");
        }

        if (f.HasWpfSignals && f.HasServicesFolder && !f.HasRepositoryInTree)
        {
            cons.Add("WPF + Services без Repository/Context — data-access не вынесен в отдельный слой.");
        }

        if (f.HasPaginationInSource && f.PaginationInCodeBehind && f.PaginationFile is not null)
        {
            cons.Add($"Пагинация в code-behind ({f.PaginationFile}) — логика списка в MainWindow, не во ViewModel.");
        }

        if (f.HasProgramCs && !f.HasAppsettings && !f.HasUnityProject)
        {
            cons.Add("Program.cs есть, appsettings.json в дереве нет — конфигурация, вероятно, в коде.");
        }

        EnsureMinimumPros(pros, f);
        return (pros.Take(3).ToList(), ConsBulletSanitizer.Filter(cons));
    }

    private static void EnsureMinimumPros(List<string> pros, CodeEvidenceFacts f)
    {
        if (pros.Count > 0)
        {
            return;
        }

        if (f.HasUnityProject)
        {
            if (f.HasUnityPluginsFolder)
            {
                pros.Add("Unity-плагин: runtime-код отделён от сцен (Assets/Plugins).");
            }
            else if (f.HasMonoBehaviourScripts)
            {
                pros.Add("Игровой Unity-проект: MonoBehaviour-скрипты в выборке ключевых файлов.");
            }
            else
            {
                pros.Add("Unity C#: структура репозитория читаема по дереву исходников.");
            }

            return;
        }

        if (f.HasFileStorageFile || f.HasIStorageAbstraction)
        {
            pros.Add("Абстракция хранилища (FileStorage/IStorage) — логика отделена от формата файла.");
            return;
        }

        if (f.HasServicesFolder || f.HasConvertersFolder)
        {
            pros.Add("Слои Services/Converters в дереве — прикладная логика не смешана с разметкой.");
        }
    }
}
