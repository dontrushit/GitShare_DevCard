using GitShare.Api.Models;

namespace GitShare.Api.Services;

/// <summary>
/// Содержательное архитектурное резюме и риски вместо чеклиста «async/await в коде».
/// </summary>
internal static class RepositoryArchitectureAssessmentBuilder
{
    private static readonly string[] ShallowProMarkersRu =
    [
        "async/await", "try/catch", "Services/", "DI ", "регистрация", "папка Services"
    ];

    private static readonly string[] ShallowProMarkersEn =
    [
        "async/await", "try/catch", "Services/", "DI ", "registration"
    ];

    internal sealed record Assessment(
        string ArchitectureSummary,
        IReadOnlyList<string> Strengths,
        IReadOnlyList<string> Risks);

    public static Assessment Build(
        RepositoryForensics repo,
        string projectClass,
        string framework,
        string layout,
        string debtSeverity,
        IReadOnlyList<string> rawPros,
        IReadOnlyList<string> rawCons,
        RepositoryLevelInfo level,
        AuditContentLocale locale)
    {
        var summary = BuildSummary(repo, projectClass, framework, layout, level, locale);
        var strengths = SelectStrengths(rawPros, repo.Facts, repo.StackProfile, locale);
        var risks = BuildRisks(rawCons, repo.Facts, projectClass, debtSeverity, locale);

        return new Assessment(summary, strengths, risks);
    }

    private static string BuildSummary(
        RepositoryForensics repo,
        string projectClass,
        string framework,
        string layout,
        RepositoryLevelInfo level,
        AuditContentLocale locale)
    {
        if (repo.IsVendorAssetPack)
        {
            return locale == AuditContentLocale.En
                ? $"{repo.RepoName}: third-party Unity assets; authored architecture is not part of the audit."
                : $"{repo.RepoName}: сторонние Unity-ассеты; авторская архитектура вне аудита.";
        }

        if (projectClass == ProjectClassClassifier.DocOpsKnowledgeBase)
        {
            return locale == AuditContentLocale.En
                ? $"{repo.RepoName}: knowledge/doc repository — stack depth is measured by content structure, not runtime layers."
                : $"{repo.RepoName}: репозиторий знаний — оценивается структура материалов, а не runtime-слои.";
        }

        var facts = repo.Facts;
        var profile = repo.StackProfile;

        if (locale == AuditContentLocale.En)
        {
            var parts = new List<string>
            {
                $"{repo.RepoName} ({level.Title}, {level.Score}/100): {framework}, layout {layout}."
            };

            AppendStackNarrativeEn(parts, profile, facts, projectClass, repo);
            if (facts is null && EnterpriseAuditLexicon.IsProductionClass(projectClass))
            {
                parts.Add("Assessment leans on tree signatures — sampled source was limited.");
            }

            return string.Join(" ", parts);
        }

        var ruParts = new List<string>
        {
            $"{repo.RepoName} ({level.Title}, {level.Score}/100): {framework}, раскладка {layout}."
        };

        AppendStackNarrativeRu(ruParts, profile, facts, projectClass, repo);
        if (facts is null && EnterpriseAuditLexicon.IsProductionClass(projectClass))
        {
            ruParts.Add("Выводы опираются на дерево и сигнатуры — выборка исходников ограничена.");
        }

        return string.Join(" ", ruParts);
    }

    private static void AppendStackNarrativeEn(
        List<string> parts,
        StackEvidenceProfile profile,
        CodeEvidenceFacts? facts,
        string projectClass,
        RepositoryForensics repo)
    {
        if (!EnterpriseAuditLexicon.IsProductionClass(projectClass))
        {
            AppendPetDesktopEvidenceEn(parts, profile, facts, repo);
            return;
        }

        switch (profile)
        {
            case StackEvidenceProfile.FullStackDotNetReact:
                parts.Add(DescribeFullStackBoundariesEn(facts));
                break;
            case StackEvidenceProfile.WebApi:
                parts.Add(facts?.HasDiRegistration == true
                    ? "Web API with DI — focus on endpoint layering and data access isolation."
                    : "Web API skeleton — DI and persistence boundaries need confirmation in code.");
                break;
            case StackEvidenceProfile.Wpf:
                parts.Add(facts is { HasViewModelsFolder: true, HasConvertersFolder: true }
                    ? "WPF with ViewModels/Converters — MVVM-oriented structure."
                    : "WPF desktop — binding/MVVM discipline depends on code-behind vs ViewModels.");
                break;
            case StackEvidenceProfile.WinForms:
                parts.Add("WinForms UI — separation of forms and data access is the main architecture axis.");
                break;
            case StackEvidenceProfile.Unity:
                parts.Add(facts?.HasMonoBehaviourScripts == true
                    ? "Unity gameplay scripts — composition and testability vs MonoBehaviour god objects."
                    : "Unity project — script organization and asset/code boundaries matter.");
                break;
            default:
                parts.Add("Production codebase — layering and dependency direction define maintainability.");
                break;
        }
    }

    private static void AppendStackNarrativeRu(
        List<string> parts,
        StackEvidenceProfile profile,
        CodeEvidenceFacts? facts,
        string projectClass,
        RepositoryForensics repo)
    {
        if (!EnterpriseAuditLexicon.IsProductionClass(projectClass))
        {
            AppendPetDesktopEvidenceRu(parts, profile, facts, repo);
            return;
        }

        switch (profile)
        {
            case StackEvidenceProfile.FullStackDotNetReact:
                parts.Add(DescribeFullStackBoundariesRu(facts));
                break;
            case StackEvidenceProfile.WebApi:
                parts.Add(facts?.HasDiRegistration == true
                    ? "Web API с DI — ключевые оси: слои эндпоинтов и изоляция доступа к данным."
                    : "Каркас Web API — DI и границы persistence нужно подтвердить в коде.");
                break;
            case StackEvidenceProfile.Wpf:
                parts.Add(facts is { HasViewModelsFolder: true, HasConvertersFolder: true }
                    ? "WPF с ViewModels/Converters — структура ближе к MVVM."
                    : "WPF desktop — дисциплина binding/MVVM зависит от code-behind vs ViewModels.");
                break;
            case StackEvidenceProfile.WinForms:
                parts.Add("WinForms — главная ось: отделение форм от доступа к данным.");
                break;
            case StackEvidenceProfile.Unity:
                parts.Add(facts?.HasMonoBehaviourScripts == true
                    ? "Unity-скрипты — композиция и тестируемость vs god-object MonoBehaviour."
                    : "Unity — важны организация скриптов и граница ассеты/код.");
                break;
            default:
                parts.Add("Production-код — слои и направление зависимостей определяют сопровождаемость.");
                break;
        }
    }

    private static List<string> SelectStrengths(
        IReadOnlyList<string> rawPros,
        CodeEvidenceFacts? facts,
        StackEvidenceProfile profile,
        AuditContentLocale locale)
    {
        var markers = locale == AuditContentLocale.En ? ShallowProMarkersEn : ShallowProMarkersRu;
        var substantive = rawPros
            .Where(p => !markers.Any(m => p.Contains(m, StringComparison.OrdinalIgnoreCase)))
            .Take(2)
            .ToList();

        if (substantive.Count > 0)
        {
            return substantive;
        }

        if (facts is null)
        {
            return [];
        }

        var synthesized = new List<string>();
        if (facts.HasRepositoryInTree || facts.HasIStorageAbstraction)
        {
            synthesized.Add(locale == AuditContentLocale.En
                ? "Data access is abstracted (repository/storage interfaces) — easier to test and swap implementations."
                : "Доступ к данным абстрагирован (repository/storage) — проще тестировать и менять реализацию.");
        }
        else if (facts.HasDiRegistration && facts.HasServicesFolder)
        {
            synthesized.Add(locale == AuditContentLocale.En
                ? "DI + Services folder — composition root separates wiring from domain logic."
                : "DI и папка Services — composition root отделяет связывание от доменной логики.");
        }
        else if (profile == StackEvidenceProfile.FullStackDotNetReact && facts.HasWebApiSignals)
        {
            synthesized.Add(locale == AuditContentLocale.En
                ? "Distinct API surface alongside client — enables independent evolution of backend contracts."
                : "Отдельный API-контур рядом с клиентом — backend-контракты можно развивать независимо.");
        }

        return synthesized.Take(2).ToList();
    }

    private static List<string> BuildRisks(
        IReadOnlyList<string> rawCons,
        CodeEvidenceFacts? facts,
        string projectClass,
        string debtSeverity,
        AuditContentLocale locale)
    {
        var risks = rawCons
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Take(3)
            .ToList();

        if (risks.Count > 0)
        {
            return risks;
        }

        if (!EnterpriseAuditLexicon.IsProductionClass(projectClass))
        {
            return [];
        }

        if (facts?.HasStaticDbHelper == true)
        {
            risks.Add(locale == AuditContentLocale.En
                ? "Static data helper couples callers to a single persistence implementation."
                : "Статический data-helper связывает вызывающий код с одной реализацией persistence.");
        }

        if (facts?.HasOleDbInSource == true && facts.HasRepositoryInTree == false)
        {
            risks.Add(locale == AuditContentLocale.En
                ? "OleDb usage without repository layer — harder to mock and migrate off legacy ADO."
                : "OleDb без слоя repository — сложнее мокать и уходить с legacy ADO.");
        }

        if (risks.Count == 0 && (debtSeverity is "Warning" or "Critical"))
        {
            risks.Add(locale == AuditContentLocale.En
                ? "Tree/manifest signals structural debt — expand file sample to confirm coupling hotspots."
                : "Сигнатуры дерева указывают на структурный долг — нужна расширенная выборка файлов.");
        }

        if (risks.Count == 0)
        {
            risks.Add(locale == AuditContentLocale.En
                ? "No critical issues in the code sample; conclusions are limited to ~10 audited files."
                : "В выборке кода критичных нарушений не видно; вывод ограничен ~10 просмотренными файлами.");
        }

        return risks.Take(3).ToList();
    }

    private static string DescribeFullStackBoundariesRu(CodeEvidenceFacts? facts)
    {
        if (facts is { HasDiRegistration: true, HasServicesFolder: true } &&
            (facts.HasRepositoryInTree || facts.HasIStorageAbstraction || facts.HasInterfacesFolder))
        {
            return "В коде: composition root (Program.cs) → Controllers → Services → абстракции данных; React SPA — отдельный контур.";
        }

        return facts?.HasWebApiSignals == true
            ? "Контур API + SPA; границы DTO и зависимости Controller→Service нужно держать явными."
            : "Full-stack раскладка — границы API/SPA подтверждаются по выборке файлов.";
    }

    private static string DescribeFullStackBoundariesEn(CodeEvidenceFacts? facts)
    {
        if (facts is { HasDiRegistration: true, HasServicesFolder: true } &&
            (facts.HasRepositoryInTree || facts.HasIStorageAbstraction || facts.HasInterfacesFolder))
        {
            return "In code: composition root (Program.cs) → Controllers → Services → data abstractions; React SPA is a separate boundary.";
        }

        return facts?.HasWebApiSignals == true
            ? "API + SPA boundary — keep DTO contracts and Controller→Service dependencies explicit."
            : "Full-stack layout — API/SPA boundaries confirmed from the file sample.";
    }

    private static void AppendPetDesktopEvidenceEn(
        List<string> parts,
        StackEvidenceProfile profile,
        CodeEvidenceFacts? facts,
        RepositoryForensics repo)
    {
        if (profile == StackEvidenceProfile.Unity &&
            repo.TargetSignatureManifest.Contains("Unity", StringComparison.OrdinalIgnoreCase))
        {
            if (UnityRepositoryHeuristics.IsUnityToolkitRepository(repo.RepoName, repo.TargetSignatureManifest))
            {
                parts.Add("Unity plugin: Plugins/ runtime API; judged on testability, not Repository/DI.");
                return;
            }

            if (UnityRepositoryHeuristics.IsUnityShaderRepository(repo.RepoName, repo.TargetSignatureManifest))
            {
                parts.Add("Unity shader/VFX — Assets/ structure, not application architecture.");
                return;
            }

            if (UnityRepositoryHeuristics.IsUnityCompositionRootGame(repo.RepoName, repo.TargetSignatureManifest) ||
                UnityRepositoryHeuristics.HasCompositionRootPattern(repo.BlobPaths, repo.TargetSignatureManifest))
            {
                parts.Add("Unity game: CompositeRoot/* + View — manual dependency composition across scenes and systems.");
                return;
            }
        }

        if (OssRepositoryHeuristics.IsGoCliAuditContext(repo.RepoName, repo.TargetSignatureManifest))
        {
            parts.Add(OssRepositoryHeuristics.IsOssFlagshipRepository(repo.RepoName, repo.Stars)
                ? "Go CLI OSS tool: go.mod + commands; judged on modularity and operability, not Repository/DI."
                : "Go CLI: go.mod + entrypoint; judged on command structure, not enterprise layers.");
            return;
        }

        if (facts?.HasStaticDbHelper == true)
        {
            parts.Add("WinForms/WPF with static DbHelper — UI and data access are not separated.");
            return;
        }

        if (facts is { HasOleDbInSource: true, HasRepositoryInTree: false })
        {
            parts.Add("OleDb in source without repository layer — tight coupling to ADO.");
            return;
        }

        if (facts?.PaginationInCodeBehind == true)
        {
            parts.Add($"Pagination in code-behind ({facts.PaginationFile}) — list logic outside ViewModel.");
            return;
        }

        if (profile == StackEvidenceProfile.Wpf && facts is { HasConvertersFolder: true, HasServicesFolder: true })
        {
            parts.Add("Partial MVVM (Services/Converters) — data layer boundaries still weak.");
            return;
        }

        parts.Add("Pet/utility format — architecture judged on coupling and folder boundaries, not enterprise layers.");
    }

    private static void AppendPetDesktopEvidenceRu(
        List<string> parts,
        StackEvidenceProfile profile,
        CodeEvidenceFacts? facts,
        RepositoryForensics repo)
    {
        if (profile == StackEvidenceProfile.Unity &&
            repo.TargetSignatureManifest.Contains("Unity", StringComparison.OrdinalIgnoreCase))
        {
            if (UnityRepositoryHeuristics.IsUnityToolkitRepository(repo.RepoName, repo.TargetSignatureManifest))
            {
                parts.Add("Unity plugin: API и runtime в Plugins/; критерий — testability и чистота API, не Repository/DI.");
                return;
            }

            if (UnityRepositoryHeuristics.IsUnityShaderRepository(repo.RepoName, repo.TargetSignatureManifest))
            {
                parts.Add("Unity shader/VFX — оценка структуры Assets/, не application architecture.");
                return;
            }

            if (UnityRepositoryHeuristics.IsUnityCompositionRootGame(repo.RepoName, repo.TargetSignatureManifest) ||
                UnityRepositoryHeuristics.HasCompositionRootPattern(repo.BlobPaths, repo.TargetSignatureManifest))
            {
                parts.Add("Unity game: CompositeRoot/* + View — ручная композиция зависимостей между сценами и системами.");
                return;
            }
        }

        if (OssRepositoryHeuristics.IsGoCliAuditContext(repo.RepoName, repo.TargetSignatureManifest))
        {
            parts.Add(OssRepositoryHeuristics.IsOssFlagshipRepository(repo.RepoName, repo.Stars)
                ? "Go CLI OSS-утилита: go.mod + команды; критерий — модульность и эксплуатация, не Repository/DI."
                : "Go CLI: go.mod + точка входа; оценка по структуре команд, не по enterprise-слоям.");
            return;
        }

        if (facts?.HasStaticDbHelper == true)
        {
            parts.Add("WinForms/WPF со static DbHelper — UI и доступ к данным не разведены.");
            return;
        }

        if (facts is { HasOleDbInSource: true, HasRepositoryInTree: false })
        {
            parts.Add("OleDb в коде без repository — жёсткая связь с ADO.");
            return;
        }

        if (facts?.PaginationInCodeBehind == true)
        {
            parts.Add($"Пагинация в code-behind ({facts.PaginationFile}) — логика списка вне ViewModel.");
            return;
        }

        if (profile == StackEvidenceProfile.Wpf && facts is { HasConvertersFolder: true, HasServicesFolder: true })
        {
            parts.Add("Частичный MVVM (Services/Converters) — границы data-слоя всё ещё слабые.");
            return;
        }

        parts.Add("Pet/utility — оценка по связности и папкам, не по enterprise-слоям.");
    }
}
