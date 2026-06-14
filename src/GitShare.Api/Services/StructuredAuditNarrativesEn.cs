namespace GitShare.Api.Services;

/// <summary>Английские тексты rule-based аудита (fallback и подстраховка санитайзера).</summary>
internal static class StructuredAuditNarrativesEn
{
    public static string TechnicalDebt(string manifest)
    {
        var parts = new List<string>();

        if (manifest.Contains(".accdb", StringComparison.OrdinalIgnoreCase) &&
            manifest.Contains("DbHelper", StringComparison.OrdinalIgnoreCase) &&
            !ManifestSignalParser.ManifestListsRepositoryLayer(manifest))
        {
            parts.Add(
                "DbHelper.cs + .accdb. No Repository/Context/DI. Data access lives in a helper; UI talks to the database directly.");
        }

        if (manifest.Contains("Form", StringComparison.OrdinalIgnoreCase) &&
            manifest.Contains("Helpers", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("Flat monolith: Form*.cs + Helpers. No application/repository layer visible in the tree.");
        }

        if (manifest.Contains("WPF", StringComparison.OrdinalIgnoreCase) &&
            manifest.Contains("Converters", StringComparison.OrdinalIgnoreCase) &&
            manifest.Contains("Services folder", StringComparison.OrdinalIgnoreCase))
        {
            if (!ManifestSignalParser.ManifestListsRepositoryLayer(manifest))
            {
                parts.Add(
                    "MVVM: Services and Converters present. Repository/Context missing — data layer is not separated from UI.");
            }
            else
            {
                parts.Add("MVVM: Services/Converters/Repository appear in signatures. Layers are separated.");
            }
        }

        if (manifest.Contains("FileStorage", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add(
                "FileStorage.cs (JSON). No RDBMS in the tree. Scaling is limited by file I/O.");
        }

        if (manifest.Contains("Unity", StringComparison.OrdinalIgnoreCase))
        {
            if (!manifest.Contains("test assemblies", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(
                    "Unity project. Test assemblies are not visible in the tree — regressions rely on manual Editor runs.");
            }

            if (manifest.Contains("Plugins/", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(
                    "Unity plugin. Runtime depends on UnityEngine APIs — moving logic outside the engine needs adapters.");
            }
            else
            {
                parts.Add(
                    "Unity game/sample. Logic under Assets/ without visible DI — typical scene/component coupling.");
            }
        }

        if (manifest.Contains("Program.cs", StringComparison.OrdinalIgnoreCase) &&
            !manifest.Contains("appsettings", StringComparison.OrdinalIgnoreCase) &&
            !manifest.Contains(".accdb", StringComparison.OrdinalIgnoreCase) &&
            !manifest.Contains("FileStorage", StringComparison.OrdinalIgnoreCase) &&
            !manifest.Contains("Unity", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("Program.cs without appsettings.json. Configuration is likely hard-coded.");
        }

        if (!ProjectClassClassifier.ManifestDescribesWebApi(manifest) &&
            (ManifestSignalParser.ManifestHasUtilityTestStackFlag(manifest) ||
             ProjectStackCatalog.IsUtilityOrTestStack(
                 StructuredAuditBuilder.ExtractManifestValue(manifest, "Primary framework:") ?? string.Empty,
                 StructuredAuditBuilder.ExtractManifestValue(manifest, "Suggested layout:") ?? string.Empty)))
        {
            parts.Add(
                "Utility, tests, or IaC: enterprise layers (Repository/Services) do not apply to this format.");
        }

        return parts.Count == 0
            ? AuditContentCatalog.ProductionTechnicalDebtFallback(AuditContentLocale.En)
            : string.Join(" ", parts);
    }

    public static string InterviewQuestion(string manifest, string repoName, IReadOnlyList<string> keyFiles)
    {
        var citedFile = keyFiles.FirstOrDefault() ?? "a key file from the repository";

        if (manifest.Contains("DbHelper", StringComparison.OrdinalIgnoreCase))
        {
            return
                $"In {repoName}, persistence goes through DbHelper.cs + .accdb. How do you handle thread safety under concurrent requests? Who owns the connection lifecycle?";
        }

        if (manifest.Contains("FileStorage", StringComparison.OrdinalIgnoreCase))
        {
            return
                $"In {repoName}, persistence uses FileStorage.cs (JSON). At ~10k records, what do you change first: IStorage, file format, or a move to an RDBMS?";
        }

        if (manifest.Contains("DataService", StringComparison.OrdinalIgnoreCase))
        {
            return
                $"In {repoName}, DataService.cs sits on the UI boundary. How do you test the data layer without spinning up Views/Windows?";
        }

        if (ProjectClassClassifier.ManifestDescribesWebApi(manifest))
        {
            return manifest.Contains("React", StringComparison.OrdinalIgnoreCase) ||
                   manifest.Contains("SPA", StringComparison.OrdinalIgnoreCase)
                ? $"In {repoName}, trace an HTTP request from the API to the database. Where do controller, service, and persistence split?"
                : $"In {repoName}, how does a request flow from controller to the data layer? Where are dependencies registered?";
        }

        if (ManifestSignalParser.ManifestHasWpfConverterArtifacts(manifest))
        {
            citedFile = keyFiles.FirstOrDefault(f => f.Contains("Converter", StringComparison.OrdinalIgnoreCase)) ?? citedFile;
            return
                $"In {repoName}, what is the role of {citedFile} in the MVVM chain? Why is that logic not in a ViewModel/Service?";
        }

        if (manifest.Contains("Unity", StringComparison.OrdinalIgnoreCase) &&
            manifest.Contains("Plugins/", StringComparison.OrdinalIgnoreCase))
        {
            return
                $"In {repoName}, a plugin lives under Assets/Plugins/. How do you test {citedFile} without Play Mode and without a scene in the Editor?";
        }

        if (manifest.Contains("Unity", StringComparison.OrdinalIgnoreCase))
        {
            return
                $"In {repoName} ({citedFile}): what do you mock in a unit test — UnityEngine APIs, the scene, or domain code outside MonoBehaviour?";
        }

        return
            $"In {repoName}, what is the role of {citedFile}, and which dependencies must it not pull in?";
    }

    public static List<string> Pros(string manifest)
    {
        var pros = new List<string>();

        if (manifest.Contains("Unity", StringComparison.OrdinalIgnoreCase))
        {
            if (manifest.Contains("Plugins/", StringComparison.OrdinalIgnoreCase))
            {
                pros.Add("Unity plugin layout: Assets/Plugins/ separates runtime and Editor from scenes.");
            }

            if (manifest.Contains("multi-pattern", StringComparison.OrdinalIgnoreCase))
            {
                pros.Add("Multiple architecture samples (MVC/MV/Flat) in one repo — useful for teaching.");
            }

            if (manifest.Contains("Editor scripts", StringComparison.OrdinalIgnoreCase))
            {
                pros.Add("Editor scripts in the tree — build/validation automation inside Unity.");
            }

            if (manifest.Contains("test assemblies", StringComparison.OrdinalIgnoreCase))
            {
                pros.Add("Unity test assemblies (Tests/) are visible in the repository structure.");
            }
        }

        if (manifest.Contains("WPF", StringComparison.OrdinalIgnoreCase) &&
            manifest.Contains("Converters", StringComparison.OrdinalIgnoreCase))
        {
            pros.Add("UI isolation via MVVM: Views are separated from Services/Converters.");
        }

        if (ManifestSignalParser.HasStackSignal(manifest, "Java"))
        {
            pros.Add("Java stack in the tree (Maven/Gradle + .java) — stack is unambiguous from signatures.");
        }

        if (ManifestSignalParser.HasStackSignal(manifest, "Spring Boot"))
        {
            pros.Add("Spring Boot in the structure — enterprise microservice contour.");
        }

        if (ManifestSignalParser.ManifestListsServicesFolder(manifest))
        {
            pros.Add("Dedicated Services layer — business logic is not mixed into markup.");
        }

        if (ManifestSignalParser.ManifestListsRepositoryLayer(manifest))
        {
            pros.Add("Data access in Repository/Context — persistence is easier to test.");
        }

        if (manifest.Contains("Interfaces/", StringComparison.OrdinalIgnoreCase) ||
            manifest.Contains("IStorage", StringComparison.OrdinalIgnoreCase))
        {
            pros.Add("Storage abstracted behind interfaces — room for DIP and swapping implementations.");
        }

        if (manifest.Contains("appsettings", StringComparison.OrdinalIgnoreCase))
        {
            pros.Add("Configuration via appsettings — parameters are not hard-coded.");
        }

        if (pros.Count == 0 && ProjectClassClassifier.HasApplicationCodeSignals(manifest))
        {
            pros.Add("Repository structure is readable from key-file signatures.");
            pros.Add("Stack and application type are clear from the tree.");
        }

        return pros.Take(3).ToList();
    }

    public static List<string> Cons(string manifest)
    {
        var cons = new List<string>();

        if (manifest.Contains("Unity", StringComparison.OrdinalIgnoreCase))
        {
            var isToolkit = manifest.Contains("package/plugin", StringComparison.OrdinalIgnoreCase) ||
                            manifest.Contains("Plugins/", StringComparison.OrdinalIgnoreCase);
            var hasCompositionRoot = UnityRepositoryHeuristics.HasCompositionRootPattern([], manifest);

            if (!isToolkit && !hasCompositionRoot &&
                !manifest.Contains("test assemblies", StringComparison.OrdinalIgnoreCase))
            {
                cons.Add("No Unity Tests/EditModeTests in the tree — unit tests are not visible.");
            }

            if (!isToolkit &&
                !hasCompositionRoot &&
                !manifest.Contains("Zenject", StringComparison.OrdinalIgnoreCase) &&
                !manifest.Contains("VContainer", StringComparison.OrdinalIgnoreCase) &&
                !manifest.Contains("IServiceCollection", StringComparison.OrdinalIgnoreCase))
            {
                cons.Add("No DI container (Zenject/VContainer/MS.DI) in signatures — typical scene coupling.");
            }
        }

        if (manifest.Contains("DbHelper", StringComparison.OrdinalIgnoreCase) &&
            !ManifestSignalParser.ManifestListsRepositoryLayer(manifest))
        {
            cons.Add("DbHelper without Repository/DI — tight coupling between UI and data access.");
        }

        if (manifest.Contains(".accdb", StringComparison.OrdinalIgnoreCase) &&
            !ManifestSignalParser.ManifestListsRepositoryLayer(manifest))
        {
            cons.Add("Direct .accdb access — no migration layer or scalable RDBMS.");
        }

        if (manifest.Contains("Form", StringComparison.OrdinalIgnoreCase) &&
            manifest.Contains("Helpers", StringComparison.OrdinalIgnoreCase) &&
            !manifest.Contains("Services folder", StringComparison.OrdinalIgnoreCase))
        {
            cons.Add("Flat WinForms monolith: Form + Helpers without application/repository layer.");
        }

        if (manifest.Contains("WPF", StringComparison.OrdinalIgnoreCase) &&
            ManifestSignalParser.ManifestListsServicesFolder(manifest) &&
            !ManifestSignalParser.ManifestListsRepositoryLayer(manifest))
        {
            cons.Add("MVVM without Repository/Context — data layer is not separated from the UI chain.");
        }

        if (!manifest.Contains("Interfaces/", StringComparison.OrdinalIgnoreCase) &&
            (manifest.Contains("FileStorage", StringComparison.OrdinalIgnoreCase) ||
             manifest.Contains("DbHelper", StringComparison.OrdinalIgnoreCase)))
        {
            cons.Add("No interfaces to swap storage implementations (DIP).");
        }

        if (manifest.Contains("FileStorage", StringComparison.OrdinalIgnoreCase))
        {
            cons.Add("Persistence is tied to file I/O (JSON).");
        }

        if (manifest.Contains("Program.cs", StringComparison.OrdinalIgnoreCase) &&
            !manifest.Contains("appsettings", StringComparison.OrdinalIgnoreCase) &&
            !manifest.Contains("Unity", StringComparison.OrdinalIgnoreCase))
        {
            cons.Add("No appsettings.json — configuration is likely in code.");
        }

        return cons.Take(3).ToList();
    }
}
