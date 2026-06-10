namespace GitShare.Api.Services;

/// <summary>
/// Разбор manifest без ложных срабатываний на шаблонные строки вроде «no Context/Repository/Helper».
/// </summary>
internal static class ManifestSignalParser
{
    public static bool HasArchitectureSignal(string manifest, string signal) =>
        ContainsListedSignal(manifest, "Architecture signals:", signal);

    public static bool HasStackSignal(string manifest, string signal) =>
        ContainsListedSignal(manifest, "Stack signals:", signal);

    public static bool HasConfigurationFile(string manifest, string fileName) =>
        ContainsListedSignal(manifest, "Configuration:", fileName);

    public static bool HasRepositoryLayerInTree(IReadOnlyCollection<string> paths) =>
        paths.Any(p =>
            p.Contains("/Repository/", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(p).EndsWith("Repository.cs", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(p).EndsWith("Context.cs", StringComparison.OrdinalIgnoreCase));

    public static bool HasServicesFolderInTree(IReadOnlyCollection<string> paths) =>
        paths.Any(p => p.Contains("/Services/", StringComparison.OrdinalIgnoreCase));

    public static bool HasConverterArtifacts(IReadOnlyCollection<string> paths) =>
        paths.Any(p => p.Contains("/Converters/", StringComparison.OrdinalIgnoreCase)) ||
        paths.Any(p => Path.GetFileName(p).EndsWith("Converter.cs", StringComparison.OrdinalIgnoreCase));

    public static bool HasJavaStackInTree(IReadOnlyCollection<string> paths) =>
        paths.Any(p => p.EndsWith(".java", StringComparison.OrdinalIgnoreCase)) ||
        paths.Any(p => p.EndsWith("pom.xml", StringComparison.OrdinalIgnoreCase)) ||
        paths.Any(p => p.EndsWith("build.gradle", StringComparison.OrdinalIgnoreCase) ||
                       p.EndsWith("build.gradle.kts", StringComparison.OrdinalIgnoreCase));

    public static bool HasSpringSignalsInTree(IReadOnlyCollection<string> paths) =>
        paths.Any(p => p.Contains("spring-boot", StringComparison.OrdinalIgnoreCase)) ||
        paths.Any(p => p.Contains("/spring/", StringComparison.OrdinalIgnoreCase)) ||
        paths.Any(p => Path.GetFileName(p).Equals("application.yml", StringComparison.OrdinalIgnoreCase)) ||
        paths.Any(p => Path.GetFileName(p).Equals("application.yaml", StringComparison.OrdinalIgnoreCase));

    public static bool ManifestListsRepositoryLayer(string manifest)
    {
        if (LineListsConcreteFiles(manifest, "Data Layer:"))
        {
            var dataLayer = ExtractLineValue(manifest, "Data Layer:") ?? string.Empty;
            if (dataLayer.Contains("Repository", StringComparison.OrdinalIgnoreCase) ||
                dataLayer.Contains("Context.cs", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return DetectedKeyFilesContain(manifest, "Repository.cs", "Context.cs");
    }

    public static bool ManifestListsServicesFolder(string manifest) =>
        DetectedKeyFilesContain(manifest, "/Services/") ||
        HasArchitectureSignal(manifest, "Services folder");

    public static bool DetectedKeyFilesContain(string manifest, params string[] needles)
    {
        var keyFiles = ExtractLineValue(manifest, "Detected key files:");
        if (string.IsNullOrWhiteSpace(keyFiles) ||
            keyFiles.Contains("insufficient", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return needles.Any(needle => keyFiles.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static bool LineListsConcreteFiles(string manifest, string linePrefix)
    {
        var value = ExtractLineValue(manifest, linePrefix);
        return !string.IsNullOrWhiteSpace(value) &&
               !value.Contains("no dedicated", StringComparison.OrdinalIgnoreCase) &&
               !value.Contains("none", StringComparison.OrdinalIgnoreCase) &&
               value.Contains('.', StringComparison.Ordinal);
    }

    private static bool ContainsListedSignal(string manifest, string linePrefix, string signal)
    {
        var value = ExtractLineValue(manifest, linePrefix);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.Contains("none", StringComparison.OrdinalIgnoreCase) ||
            (value.Contains("no ", StringComparison.OrdinalIgnoreCase) &&
             value.Contains("detected", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return value.Contains(signal, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractLineValue(string manifest, string linePrefix)
    {
        return manifest
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(line => line.StartsWith(linePrefix, StringComparison.OrdinalIgnoreCase))
            ?[linePrefix.Length..]
            .Trim();
    }
}
