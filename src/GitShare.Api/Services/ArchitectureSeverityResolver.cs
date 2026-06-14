namespace GitShare.Api.Services;

/// <summary>
/// Severity по фактам кода, сигнатурам и подтверждённым архитектурным рискам (сервер — источник истины).
/// </summary>
internal static class ArchitectureSeverityResolver
{
    private static readonly Dictionary<string, int> SeverityRank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NONE"] = 0,
        ["CLEAN"] = 1,
        ["Minor"] = 2,
        ["Warning"] = 3,
        ["Critical"] = 4
    };

    public static string Resolve(
        RepositoryForensics? repo,
        string projectClass,
        string currentSeverity,
        IReadOnlyList<string> risks)
    {
        if (projectClass == ProjectClassClassifier.DocOpsKnowledgeBase)
        {
            return "NONE";
        }

        if (repo?.IsVendorAssetPack == true)
        {
            return "NONE";
        }

        var manifest = repo?.TargetSignatureManifest ?? string.Empty;
        var candidates = new List<string>
        {
            AuditSeverityNormalizer.Normalize(currentSeverity),
            FromManifest(manifest)
        };

        CodeEvidenceFacts? codeFacts = repo?.Facts;
        if (codeFacts is { } facts)
        {
            candidates.Add(FromCodeFacts(facts, projectClass, repo!.StackProfile));
        }

        var structural = FilterStructuralRisks(risks);
        if (structural.Count >= 2)
        {
            candidates.Add("Warning");
        }
        else if (structural.Count == 1)
        {
            candidates.Add("Minor");
        }

        if (projectClass is ProjectClassClassifier.UtilityAutomation or ProjectClassClassifier.QaTesting)
        {
            if (repo is not null &&
                UnityRepositoryHeuristics.IsUnityPluginAuditContext(repo.RepoName, repo.TargetSignatureManifest))
            {
                if (structural.Count == 0 &&
                    (codeFacts is null || FromCodeFacts(codeFacts, projectClass, repo.StackProfile) is "CLEAN" or "Minor"))
                {
                    return "CLEAN";
                }

                return Merge("Minor", structural.Count >= 2 ? "Warning" : "Minor");
            }

            if (repo is not null &&
                OssRepositoryHeuristics.IsGoCliAuditContext(repo.RepoName, manifest))
            {
                if (structural.Count == 0)
                {
                    return "CLEAN";
                }

                return Merge("Minor", structural.Count >= 2 ? "Warning" : "Minor");
            }

            var resolved = MergeMany(candidates);
            return structural.Count > 0
                ? Merge(resolved, "Minor")
                : resolved;
        }

        if (EnterpriseAuditLexicon.IsProductionClass(projectClass) &&
            codeFacts is { } productionFacts &&
            IsMatureProductionStructure(productionFacts) &&
            structural.Count == 0)
        {
            var codeSeverity = FromCodeFacts(productionFacts, projectClass, repo!.StackProfile);
            if (SeverityRank.GetValueOrDefault(codeSeverity, 2) <= SeverityRank["Minor"])
            {
                return "CLEAN";
            }
        }

        return MergeMany(candidates);
    }

    public static string FromCodeFacts(
        CodeEvidenceFacts facts,
        string projectClass,
        StackEvidenceProfile profile)
    {
        var severity = "CLEAN";

        if (facts.HasStaticDbHelper)
        {
            severity = PickStrictest(severity, "Warning");
        }

        if (facts.HasOleDbInSource && !facts.HasRepositoryInTree)
        {
            severity = PickStrictest(severity, "Warning");
        }

        if (facts.PaginationInCodeBehind)
        {
            severity = PickStrictest(severity, "Minor");
        }

        if (facts.HasMessageBoxInCatch)
        {
            severity = PickStrictest(severity, "Minor");
        }

        if (facts.HasHardcodedUserPath)
        {
            severity = PickStrictest(severity, "Minor");
        }

        if (EnterpriseAuditLexicon.IsProductionClass(projectClass))
        {
            if (profile is StackEvidenceProfile.WebApi or StackEvidenceProfile.FullStackDotNetReact &&
                facts.HasWebApiSignals &&
                !facts.HasDiRegistration)
            {
                severity = PickStrictest(severity, "Warning");
            }

            if (facts.HasServicesFolder && !facts.HasRepositoryInTree &&
                !facts.HasIStorageAbstraction && !facts.HasInterfacesFolder &&
                profile is StackEvidenceProfile.ConsoleUtility)
            {
                severity = PickStrictest(severity, "Minor");
            }
        }

        return severity;
    }

    public static string FromManifest(string manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest))
        {
            return "Warning";
        }

        if (manifest.Contains("Unity", StringComparison.OrdinalIgnoreCase))
        {
            if (manifest.Contains("CompositeRoot", StringComparison.OrdinalIgnoreCase) ||
                manifest.Contains("CompositionRoot", StringComparison.OrdinalIgnoreCase))
            {
                return "Minor";
            }

            if (!manifest.Contains("test assemblies", StringComparison.OrdinalIgnoreCase) &&
                manifest.Contains("Plugins/", StringComparison.OrdinalIgnoreCase))
            {
                return "Warning";
            }

            return manifest.Contains("multi-pattern", StringComparison.OrdinalIgnoreCase)
                ? "Minor"
                : "Warning";
        }

        var hasHelper = manifest.Contains("Helper", StringComparison.OrdinalIgnoreCase) ||
                        manifest.Contains("DbHelper", StringComparison.OrdinalIgnoreCase);
        var hasRepository = ManifestSignalParser.ManifestListsRepositoryLayer(manifest);
        var hasContext = ManifestSignalParser.DetectedKeyFilesContain(manifest, "Context.cs");
        var hasServices = ManifestSignalParser.ManifestListsServicesFolder(manifest);
        var hasStorageAbstraction = manifest.Contains("Interfaces/", StringComparison.OrdinalIgnoreCase);
        var hasAccdb = manifest.Contains(".accdb", StringComparison.OrdinalIgnoreCase);

        if (hasHelper && !hasRepository && !hasContext && hasAccdb)
        {
            return "Warning";
        }

        if (hasServices && (hasRepository || hasContext || manifest.Contains("appsettings", StringComparison.OrdinalIgnoreCase)))
        {
            return "Minor";
        }

        if (hasStorageAbstraction && manifest.Contains("FileStorage", StringComparison.OrdinalIgnoreCase))
        {
            return "Warning";
        }

        if (hasRepository || hasContext)
        {
            return "Minor";
        }

        if (hasHelper && !hasRepository)
        {
            return "Warning";
        }

        return "Warning";
    }

    public static IReadOnlyList<string> FilterStructuralRisks(IReadOnlyList<string> risks) =>
        risks
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Where(r => !IsSampleCoverageDisclaimer(r))
            .ToList();

    private static bool IsSampleCoverageDisclaimer(string risk)
    {
        var lower = risk.ToLowerInvariant();
        return lower.Contains("выборке") ||
               lower.Contains("выборка") ||
               lower.Contains("просмотрен") ||
               lower.Contains("sample") ||
               lower.Contains("limited to") ||
               lower.Contains("file sample") ||
               lower.Contains("~10");
    }

    private static bool IsMatureProductionStructure(CodeEvidenceFacts facts) =>
        facts.HasDiRegistration &&
        facts.HasServicesFolder &&
        (facts.HasRepositoryInTree || facts.HasIStorageAbstraction || facts.HasInterfacesFolder);

    public static string Merge(params string[] severities) => PickStrictest(severities);

    public static string MergeMany(IEnumerable<string> severities) =>
        PickStrictest(severities.ToArray());

    public static string ResolveInitial(RepositoryForensics repo, string projectClass)
    {
        if (projectClass == ProjectClassClassifier.DocOpsKnowledgeBase)
        {
            return "NONE";
        }

        var fromManifest = FromManifest(repo.TargetSignatureManifest);
        if (repo.Facts is not { } facts)
        {
            return fromManifest;
        }

        return Merge(fromManifest, FromCodeFacts(facts, projectClass, repo.StackProfile));
    }

    private static string PickStrictest(params string[] severities)
    {
        return severities
            .Select(AuditSeverityNormalizer.Normalize)
            .OrderByDescending(s => SeverityRank.GetValueOrDefault(s, 2))
            .First();
    }

    private static string PickStrictest(string a, string b) =>
        SeverityRank.GetValueOrDefault(a, 2) >= SeverityRank.GetValueOrDefault(b, 2) ? a : b;
}
