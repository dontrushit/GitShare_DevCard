namespace GitShare.Api.Services;

/// <summary>
/// Выбор ключевых файлов для LLM по stack-profile (Production App).
/// </summary>
internal static class LlmEvidenceFileSelector
{
    public const int MaxFilesForLlm = StackEvidenceFileProfiles.MaxLlmSourceFiles;
    public const int MaxInfraFilesForLlm = StackEvidenceFileProfiles.MaxLlmInfraFiles;
    public const int MaxCharsPerFile = StackEvidenceFileProfiles.MaxCharsLlmSource;
    public const int MaxCharsPerInfraFile = StackEvidenceFileProfiles.MaxCharsLlmInfra;

    public static LlmEvidenceSelection Select(
        string repoName,
        IReadOnlyList<string> blobPaths,
        string signatureManifest,
        string projectClass)
    {
        var effectiveClass = ProjectClassProsCons.ResolveEffectiveClass(projectClass, repoName, signatureManifest);
        if (blobPaths.Count == 0)
        {
            return LlmEvidenceSelection.Empty;
        }

        var profile = StackEvidenceProfileResolver.Resolve(signatureManifest, blobPaths);

        if (EnterpriseAuditLexicon.IsProductionClass(effectiveClass))
        {
            return new LlmEvidenceSelection(
                profile,
                StackEvidenceFileProfiles.SelectLlmSourcePaths(profile, blobPaths, signatureManifest),
                StackEvidenceFileProfiles.SelectLlmInfraPaths(profile, blobPaths));
        }

        if (effectiveClass != ProjectClassClassifier.UtilityAutomation &&
            effectiveClass != ProjectClassClassifier.QaTesting)
        {
            return LlmEvidenceSelection.Empty;
        }

        var utilitySources = StackEvidenceFileProfiles.SelectCodeEvidencePaths(
                profile,
                blobPaths,
                signatureManifest)
            .Take(StackEvidenceFileProfiles.MaxLlmUtilitySourceFiles)
            .ToList();

        return new LlmEvidenceSelection(profile, utilitySources, []);
    }

    public static List<string> SelectPaths(
        string repoName,
        IReadOnlyList<string> blobPaths,
        string signatureManifest,
        string projectClass) =>
        Select(repoName, blobPaths, signatureManifest, projectClass).SourcePaths.ToList();
}

internal sealed record LlmEvidenceSelection(
    StackEvidenceProfile Profile,
    IReadOnlyList<string> SourcePaths,
    IReadOnlyList<string> InfraPaths)
{
    public static LlmEvidenceSelection Empty { get; } =
        new(StackEvidenceProfile.GenericProduction, [], []);
}
