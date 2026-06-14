using System.Text.Json;
using System.Text.Json.Serialization;

namespace GitShare.Api.Services;

/// <summary>
/// Структурированные факты с сервера для LLM (фаза D): модель синтезирует текст, не выдумывает архитектуру.
/// </summary>
internal static class RepositoryEvidenceDigestBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static string BuildJson(
        string repoName,
        string signatureManifest,
        StackEvidenceProfile stackProfile,
        CodeEvidenceFacts? facts,
        IReadOnlyList<string> verifiedPros,
        IReadOnlyList<string> verifiedCons)
    {
        var digest = new RepositoryEvidenceDigest
        {
            RepoName = repoName,
            StackProfile = stackProfile.ToString(),
            PrimaryFramework = StructuredAuditBuilder.ExtractManifestValue(signatureManifest, "Primary framework:"),
            SuggestedLayout = StructuredAuditBuilder.ExtractManifestValue(signatureManifest, "Suggested layout:"),
            VerifiedPros = verifiedPros.Take(3).ToList(),
            VerifiedCons = verifiedCons.Take(3).ToList(),
            CodeSignals = facts is null ? null : new CodeSignalsDigest
            {
                HasDiRegistration = facts.HasDiRegistration,
                HasAsyncAwait = facts.HasAsyncAwait,
                HasWebApi = facts.HasWebApiSignals,
                HasServicesFolder = facts.HasServicesFolder,
                HasRepositoryLayer = facts.HasRepositoryInTree,
                HasWpfMvvm = facts.HasWpfSignals && facts.HasConvertersFolder,
                HasWinForms = facts.HasWinFormsSignals,
                HasOleDbWithoutRepository = facts.HasOleDbInSource && !facts.HasRepositoryInTree,
                HasStaticDbHelper = facts.HasStaticDbHelper,
                HasIStorageAbstraction = facts.HasIStorageAbstraction || facts.HasInterfacesFolder,
                HasUnityTestsInTree = facts.HasUnityTestsInTree,
                HasTryCatch = facts.HasTryCatchInDataLayer,
                TryCatchFile = facts.TryCatchFile,
                HasDbContext = facts.HasDbContextInTree,
                HasTestProject = facts.HasTestProjectInTree,
                HasStorageFolder = facts.HasStorageFolder,
                PaginationInCodeBehind = facts.PaginationInCodeBehind
            }
        };

        return JsonSerializer.Serialize(digest, JsonOptions);
    }

    private sealed class RepositoryEvidenceDigest
    {
        public string RepoName { get; set; } = string.Empty;
        public string StackProfile { get; set; } = string.Empty;
        public string? PrimaryFramework { get; set; }
        public string? SuggestedLayout { get; set; }
        public IReadOnlyList<string> VerifiedPros { get; set; } = [];
        public IReadOnlyList<string> VerifiedCons { get; set; } = [];
        public CodeSignalsDigest? CodeSignals { get; set; }
    }

    private sealed class CodeSignalsDigest
    {
        public bool HasDiRegistration { get; set; }
        public bool HasAsyncAwait { get; set; }
        public bool HasWebApi { get; set; }
        public bool HasServicesFolder { get; set; }
        public bool HasRepositoryLayer { get; set; }
        public bool HasWpfMvvm { get; set; }
        public bool HasWinForms { get; set; }
        public bool HasOleDbWithoutRepository { get; set; }
        public bool HasStaticDbHelper { get; set; }
        public bool HasIStorageAbstraction { get; set; }
        public bool HasUnityTestsInTree { get; set; }
        public bool HasTryCatch { get; set; }
        public string? TryCatchFile { get; set; }
        public bool HasDbContext { get; set; }
        public bool HasTestProject { get; set; }
        public bool HasStorageFolder { get; set; }
        public bool PaginationInCodeBehind { get; set; }
    }
}
