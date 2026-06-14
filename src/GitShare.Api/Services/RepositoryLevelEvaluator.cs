using GitShare.Api.Models;

namespace GitShare.Api.Services;

/// <summary>
/// Оценка уровня по одному репозиторию: класс проекта, сигналы кода, зрелость структуры.
/// </summary>
internal static class RepositoryLevelEvaluator
{
    public static RepositoryLevelInfo Evaluate(
        RepositoryForensics repo,
        string projectClass,
        AuditContentLocale locale)
    {
        if (repo.IsVendorAssetPack)
        {
            return Build(
                25,
                "junior",
                locale == AuditContentLocale.En
                    ? "Vendor asset pack — authored architecture not scored."
                    : "Vendor asset pack — авторская архитектура не оценивается.",
                locale);
        }

        var factors = new List<string>();
        var score = ScoreByProjectClass(projectClass, factors, locale);

        if (repo.Facts is { } facts)
        {
            score += ScoreProductionCraft(facts, projectClass, factors, locale);
            score += ScoreUtilityCraft(facts, repo, projectClass, factors, locale);
            score += ScoreUnityPluginCraft(repo, facts, projectClass, factors, locale);
        }
        else if (EnterpriseAuditLexicon.IsProductionClass(projectClass))
        {
            score += ScoreManifestOnly(repo.TargetSignatureManifest, factors, locale);
        }
        else
        {
            score += ScoreUtilityManifestCraft(repo.TargetSignatureManifest, projectClass, factors, locale);
            score += ScoreUnityManifestCraft(repo, projectClass, factors, locale);
            score += ScoreGoCliCraft(repo, projectClass, factors, locale);
            score += ScoreOssFlagshipCraft(repo, projectClass, factors, locale);
        }

        score += ScoreRepoInfluence(repo.Stars, projectClass, factors, locale);
        score = ApplyPetDesktopCeiling(score, repo.RepoName, repo.TargetSignatureManifest, projectClass);
        score = ApplyClassCeiling(score, projectClass);
        score = Math.Clamp(score, 0, 100);

        var tier = LevelTierCatalog.MapRepositoryScore(score);
        var rationale = factors.Count > 0
            ? string.Join("; ", factors.Take(5))
            : locale == AuditContentLocale.En
                ? "Limited signals in repository."
                : "Мало сигналов по репозиторию.";

        return Build(score, tier.Code, rationale, locale);
    }

    private static int ScoreByProjectClass(string projectClass, List<string> factors, AuditContentLocale locale)
    {
        if (projectClass == ProjectClassClassifier.DocOpsKnowledgeBase)
        {
            factors.Add(locale == AuditContentLocale.En
                ? "DocOps — engineering depth not applicable"
                : "DocOps — глубина инженерии не применима");
            return 20;
        }

        if (projectClass == ProjectClassClassifier.QaTesting)
        {
            factors.Add(locale == AuditContentLocale.En
                ? "QA repo — test harness focus"
                : "QA-репозиторий — фокус на тестовом контуре");
            return 42;
        }

        if (projectClass == ProjectClassClassifier.UtilityAutomation)
        {
            factors.Add(locale == AuditContentLocale.En
                ? "Utility/automation format"
                : "Формат utility/automation");
            return 36;
        }

        factors.Add(locale == AuditContentLocale.En
            ? "Production application"
            : "Production-приложение");
        return 34;
    }

    private static int ScoreProductionCraft(
        CodeEvidenceFacts facts,
        string projectClass,
        List<string> factors,
        AuditContentLocale locale)
    {
        if (!EnterpriseAuditLexicon.IsProductionClass(projectClass))
        {
            return 0;
        }

        var points = 0;

        if (facts.HasDiRegistration)
        {
            points += 10;
            factors.Add(locale == AuditContentLocale.En ? "DI registration" : "регистрация DI");
        }

        if (facts.HasServicesFolder)
        {
            points += 8;
            factors.Add(locale == AuditContentLocale.En ? "Services layer" : "слой Services");
        }

        if (facts.HasRepositoryInTree || facts.HasIStorageAbstraction || facts.HasInterfacesFolder)
        {
            points += 10;
            factors.Add(locale == AuditContentLocale.En ? "data abstractions" : "абстракции данных");
        }

        if (facts.HasAsyncAwait && (facts.HasWebApiSignals || facts.HasPaginationInSource))
        {
            points += 6;
            factors.Add(locale == AuditContentLocale.En ? "async I/O patterns" : "async-паттерны");
        }

        if (facts.HasUnityTestsInTree || facts.HasGameDiFramework)
        {
            points += 8;
            factors.Add(locale == AuditContentLocale.En ? "tests / game DI" : "тесты / game DI");
        }

        if (facts.HasConvertersFolder && facts.HasViewModelsFolder && facts.HasWpfSignals)
        {
            points += 8;
            factors.Add("WPF MVVM folders");
        }

        if (facts.HasStaticDbHelper)
        {
            points -= 12;
            factors.Add(locale == AuditContentLocale.En ? "static DbHelper" : "static DbHelper");
        }

        if (facts.HasOleDbInSource && !facts.HasRepositoryInTree)
        {
            points -= 10;
            factors.Add("OleDb без Repository");
        }

        if (facts.HasHardcodedUserPath)
        {
            points -= 6;
            factors.Add(locale == AuditContentLocale.En ? "hardcoded paths" : "жёсткие пути");
        }

        if (facts.HasMessageBoxInCatch)
        {
            points -= 4;
            factors.Add("MessageBox в catch");
        }

        return points;
    }

    private static int ScoreUtilityCraft(
        CodeEvidenceFacts facts,
        RepositoryForensics repo,
        string projectClass,
        List<string> factors,
        AuditContentLocale locale)
    {
        if (projectClass != ProjectClassClassifier.UtilityAutomation)
        {
            return 0;
        }

        var manifest = repo.TargetSignatureManifest;
        var points = 0;
        var isConsole = ProjectClassClassifier.IsConsoleUtilityManifest(manifest);
        var isDesktopPet = ProjectClassClassifier.IsPetDesktopApplication(repo.RepoName, manifest);

        if (isConsole)
        {
            if (facts.HasIStorageAbstraction && facts.HasServicesFolder)
            {
                points += 8;
                factors.Add(locale == AuditContentLocale.En
                    ? "console: IStorage + Services"
                    : "консоль: IStorage + Services");
            }
            else if (facts.HasIStorageAbstraction || facts.HasInterfacesFolder)
            {
                points += 5;
                factors.Add(locale == AuditContentLocale.En ? "storage abstraction" : "абстракция хранилища");
            }
            else if (facts.HasStorageFolder)
            {
                points += 3;
                factors.Add("Storage/");
            }
        }

        if (facts.HasWpfSignals || facts.HasViewModelsFolder)
        {
            if (facts is { HasViewModelsFolder: true, HasConvertersFolder: true, HasServicesFolder: true })
            {
                points += 6;
                factors.Add("partial MVVM (VM+Converters+Services)");
            }
            else if (facts is { HasViewModelsFolder: true, HasServicesFolder: true })
            {
                points += 4;
                factors.Add("ViewModels + Services");
            }

            if (facts.PaginationInCodeBehind)
            {
                points -= 4;
                factors.Add(locale == AuditContentLocale.En
                    ? "pagination in code-behind"
                    : "пагинация в code-behind");
            }
        }

        if (facts.HasWinFormsSignals || isDesktopPet)
        {
            if (facts.HasStaticDbHelper)
            {
                points -= 8;
                factors.Add(locale == AuditContentLocale.En ? "static DbHelper" : "static DbHelper");
            }

            if (facts.HasOleDbInSource && !facts.HasRepositoryInTree && !facts.HasDbContextInTree)
            {
                points -= 6;
                factors.Add("OleDb без слоя данных");
            }
        }

        if (facts.HasRepositoryInTree || facts.HasDbContextInTree)
        {
            points += 6;
            factors.Add(locale == AuditContentLocale.En ? "persistence layer" : "слой persistence");
        }

        if (facts.HasTestProjectInTree)
        {
            points += 4;
            factors.Add(locale == AuditContentLocale.En ? "test project in tree" : "тестовый проект в дереве");
        }

        if (facts.HasDiRegistration)
        {
            points += 4;
            factors.Add(locale == AuditContentLocale.En ? "DI registration" : "регистрация DI");
        }

        if (facts.HasTryCatchInDataLayer)
        {
            points += 2;
            factors.Add(locale == AuditContentLocale.En ? "error handling in data path" : "обработка ошибок в data-path");
        }

        return points;
    }

    private static int ScoreUtilityManifestCraft(
        string manifest,
        string projectClass,
        List<string> factors,
        AuditContentLocale locale)
    {
        if (projectClass != ProjectClassClassifier.UtilityAutomation)
        {
            return 0;
        }

        var points = 0;

        if (manifest.Contains("IStorage", StringComparison.OrdinalIgnoreCase) ||
            manifest.Contains("/Interfaces/", StringComparison.OrdinalIgnoreCase))
        {
            points += 4;
            factors.Add(locale == AuditContentLocale.En ? "interfaces in tree" : "интерфейсы в дереве");
        }

        if (manifest.Contains("/Services/", StringComparison.OrdinalIgnoreCase))
        {
            points += 3;
            factors.Add("Services/");
        }

        if (manifest.Contains("DbContext", StringComparison.OrdinalIgnoreCase) ||
            manifest.Contains("/Repositories/", StringComparison.OrdinalIgnoreCase))
        {
            points += 4;
            factors.Add(locale == AuditContentLocale.En ? "data layer folders" : "папки data-слоя");
        }

        if (manifest.Contains("Tests.csproj", StringComparison.OrdinalIgnoreCase))
        {
            points += 3;
            factors.Add(locale == AuditContentLocale.En ? "tests in tree" : "тесты в дереве");
        }

        return points;
    }

    private static int ScoreManifestOnly(string manifest, List<string> factors, AuditContentLocale locale)
    {
        var points = 0;
        if (manifest.Contains("Services/", StringComparison.OrdinalIgnoreCase))
        {
            points += 4;
            factors.Add("Services/ в дереве");
        }

        if (manifest.Contains("Repository", StringComparison.OrdinalIgnoreCase) ||
            manifest.Contains("Interfaces/", StringComparison.OrdinalIgnoreCase))
        {
            points += 4;
            factors.Add(locale == AuditContentLocale.En ? "layer folders" : "папки слоёв");
        }

        if (manifest.Contains("docker-compose", StringComparison.OrdinalIgnoreCase) ||
            manifest.Contains("Dockerfile", StringComparison.OrdinalIgnoreCase))
        {
            points += 3;
            factors.Add("containerization");
        }

        return points;
    }

    private static int ScoreUnityPluginCraft(
        RepositoryForensics repo,
        CodeEvidenceFacts facts,
        string projectClass,
        List<string> factors,
        AuditContentLocale locale)
    {
        if (projectClass != ProjectClassClassifier.UtilityAutomation ||
            !repo.TargetSignatureManifest.Contains("Unity", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var points = 0;

        if (UnityRepositoryHeuristics.IsUnityToolkitRepository(repo.RepoName, repo.TargetSignatureManifest))
        {
            points += 14;
            factors.Add(locale == AuditContentLocale.En ? "Unity toolkit" : "Unity toolkit/plugin");
        }
        else if (UnityRepositoryHeuristics.IsUnityArchitectureExamples(repo.RepoName))
        {
            points += 12;
            factors.Add("Unity architecture showcase");
        }

        if (facts.HasPureCSharpClasses)
        {
            points += 10;
            factors.Add(locale == AuditContentLocale.En
                ? "testable pure C# outside MonoBehaviour"
                : "тестируемый pure C# вне MonoBehaviour");
        }

        if (facts.HasUnityTestsInTree)
        {
            points += 6;
            factors.Add("Unity tests in tree");
        }

        if (facts.HasGameDiFramework)
        {
            points += 6;
            factors.Add("game DI (Zenject/VContainer)");
        }

        if (UnityRepositoryHeuristics.HasCompositionRootPattern(repo.BlobPaths, repo.TargetSignatureManifest))
        {
            points += 16;
            factors.Add(locale == AuditContentLocale.En ? "Composition Root pattern" : "паттерн Composition Root");
        }

        return points;
    }

    private static int ScoreUnityManifestCraft(
        RepositoryForensics repo,
        string projectClass,
        List<string> factors,
        AuditContentLocale locale)
    {
        if (projectClass != ProjectClassClassifier.UtilityAutomation)
        {
            return 0;
        }

        if (UnityRepositoryHeuristics.IsFlagshipQualityRepository(repo.RepoName))
        {
            factors.Add(locale == AuditContentLocale.En ? "flagship OSS repo" : "флагманский OSS-репо");
            return 8;
        }

        return 0;
    }

    private static int ScoreGoCliCraft(
        RepositoryForensics repo,
        string projectClass,
        List<string> factors,
        AuditContentLocale locale)
    {
        if (projectClass != ProjectClassClassifier.UtilityAutomation ||
            !OssRepositoryHeuristics.IsGoCliManifest(repo.TargetSignatureManifest))
        {
            return 0;
        }

        factors.Add(locale == AuditContentLocale.En ? "Go CLI module" : "Go CLI (go.mod)");
        return 10;
    }

    private static int ScoreOssFlagshipCraft(
        RepositoryForensics repo,
        string projectClass,
        List<string> factors,
        AuditContentLocale locale)
    {
        if (projectClass != ProjectClassClassifier.UtilityAutomation ||
            !OssRepositoryHeuristics.IsOssFlagshipRepository(repo.RepoName, repo.Stars))
        {
            return 0;
        }

        factors.Add(locale == AuditContentLocale.En ? "flagship OSS tool" : "флагманская OSS-утилита");
        return 8;
    }

    private static int ScoreRepoInfluence(int stars, string projectClass, List<string> factors, AuditContentLocale locale)
    {
        var isUtility = projectClass == ProjectClassClassifier.UtilityAutomation;
        if (stars < 3 || (!EnterpriseAuditLexicon.IsProductionClass(projectClass) && !(isUtility && stars >= 15)))
        {
            return 0;
        }

        var bonus = stars switch
        {
            >= 5_000 => 14,
            >= 1_000 => 12,
            >= 100 => 8,
            >= 30 => 6,
            >= 10 => 4,
            _ => 2
        };
        factors.Add(locale == AuditContentLocale.En
            ? $"repo traction ({stars}★)"
            : $"востребованность репо ({stars}★)");
        return bonus;
    }

    private static int ApplyPetDesktopCeiling(
        int score,
        string repoName,
        string manifest,
        string projectClass)
    {
        if (projectClass != ProjectClassClassifier.UtilityAutomation ||
            !ProjectClassClassifier.IsPetDesktopApplication(repoName, manifest) ||
            ProjectClassClassifier.IsConsoleUtilityManifest(manifest))
        {
            return score;
        }

        return Math.Min(score, 48);
    }

    private static int ApplyClassCeiling(int score, string projectClass)
    {
        var ceiling = projectClass switch
        {
            ProjectClassClassifier.DocOpsKnowledgeBase => 45,
            ProjectClassClassifier.QaTesting => 62,
            ProjectClassClassifier.UtilityAutomation => 58,
            _ => 92
        };

        return Math.Min(score, ceiling);
    }

    private static RepositoryLevelInfo Build(int score, string code, string rationale, AuditContentLocale locale) =>
        new()
        {
            Code = code,
            Title = LevelTierCatalog.TitleFor(code, locale),
            Score = score,
            Rationale = rationale
        };
}
