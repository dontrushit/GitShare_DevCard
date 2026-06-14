namespace GitShare.Api.Services;



/// <summary>

/// Pros/Cons по классу проекта — без .NET-шаблонов для DocOps, DevOps, QA и утилит.

/// </summary>

internal static class ProjectClassProsCons

{

    public static (IReadOnlyList<string> Pros, IReadOnlyList<string> Cons) Build(
        string projectClass,
        string repoName,
        string manifest,
        IReadOnlyList<string> keyFiles,
        IReadOnlyList<string> verifiedPros,
        IReadOnlyList<string> verifiedCons,
        AuditContentLocale locale = AuditContentLocale.Ru)
    {
        projectClass = ResolveEffectiveClass(projectClass, repoName, manifest);

        return BuildForClass(projectClass, repoName, manifest, keyFiles, verifiedPros, verifiedCons, locale);
    }



    private static (IReadOnlyList<string> Pros, IReadOnlyList<string> Cons) BuildForClass(
        string projectClass,
        string repoName,
        string manifest,
        IReadOnlyList<string> keyFiles,
        IReadOnlyList<string> verifiedPros,
        IReadOnlyList<string> verifiedCons,
        AuditContentLocale locale)
    {
        var localizedVerifiedPros = FilterBulletsForLocale(verifiedPros, locale).Take(3).ToList();
        var pros = localizedVerifiedPros.Count > 0
            ? localizedVerifiedPros
            : EnterpriseAuditLexicon.IsProductionClass(projectClass)
                ? StructuredAuditBuilder.BuildProsFromManifest(manifest, locale)
                : GetContextualPros(projectClass, manifest, keyFiles, repoName);

        var localizedVerifiedCons = ConsBulletSanitizer.Filter(FilterBulletsForLocale(verifiedCons, locale));
        var cons = localizedVerifiedCons.Count > 0
            ? localizedVerifiedCons
            : EnterpriseAuditLexicon.IsProductionClass(projectClass)
                ? ConsBulletSanitizer.Filter(StructuredAuditBuilder.BuildConsFromManifest(manifest, locale))
                : [];



        return (pros, ConsBulletSanitizer.Finalize(cons, projectClass));

    }



    public static string ResolveEffectiveClass(string projectClass, string repoName, string manifest)

    {

        if (UnityRepositoryHeuristics.IsUnityToolkitRepository(repoName, manifest) &&

            manifest.Contains("Unity", StringComparison.OrdinalIgnoreCase))

        {

            return ProjectClassClassifier.UtilityAutomation;

        }



        if (ProjectClassClassifier.IsSmallPetConsoleGame(repoName, manifest))

        {

            return ProjectClassClassifier.UtilityAutomation;

        }

        if (ProjectClassClassifier.IsPetDesktopApplication(repoName, manifest))
        {
            return ProjectClassClassifier.UtilityAutomation;
        }

        if (EnterpriseAuditLexicon.IsProductionClass(projectClass) &&

            !ProjectClassClassifier.HasApplicationCodeSignals(manifest))

        {

            if (ProjectClassClassifier.IsDocOpsByContent(manifest))

            {

                return ProjectClassClassifier.DocOpsKnowledgeBase;

            }



            if (ProjectClassClassifier.IsQaManifest(manifest))

            {

                return ProjectClassClassifier.QaTesting;

            }



            return ProjectClassClassifier.UtilityAutomation;

        }



        return string.IsNullOrWhiteSpace(projectClass)

            ? ProjectClassClassifier.Classify(repoName, manifest)

            : projectClass;

    }



    internal static List<string> GetContextualPros(

        string projectClass,

        string manifest,

        IReadOnlyList<string> keyFiles,

        string repoName = "")

    {

        var isDevOps = ProjectClassClassifier.IsDevOpsManifest(manifest);



        return projectClass switch

        {

            ProjectClassClassifier.DocOpsKnowledgeBase =>

            [

                "Материалы структурированы: README и сопутствующие файлы легко читаются.",

                keyFiles.Count > 0

                    ? $"Ключевые артефакты ({string.Join(", ", keyFiles.Take(2))}) выделены в паспорте."

                    : "Репозиторий ориентирован на знания, а не на прикладной runtime-код."

            ],



            ProjectClassClassifier.QaTesting =>

            [

                manifest.Contains("Selenium", StringComparison.OrdinalIgnoreCase)

                    ? "Автотесты Selenium/WebDriver: структура под CI и регрессию UI."

                    : manifest.Contains("Playwright", StringComparison.OrdinalIgnoreCase)

                        ? "Playwright/E2E: конфиги и сценарии в дереве репозитория."

                        : "Тестовый контур: specs/конфиги в дереве.",

                "Структура репозитория подходит для повторяемых прогонов в пайплайне."

            ],



            ProjectClassClassifier.UtilityAutomation
                when manifest.Contains("Go module", StringComparison.OrdinalIgnoreCase) =>
            [
                "Go CLI/утилита: go.mod и точка входа в корне, без enterprise-монолита.",
                keyFiles.Count > 0
                    ? $"Точки входа: {string.Join(", ", keyFiles.Take(3))}."
                    : "Компактная структура, типичная для инструментов разработчика."
            ],

            ProjectClassClassifier.UtilityAutomation
                when manifest.Contains("Terraform", StringComparison.OrdinalIgnoreCase) &&
                     !manifest.Contains("Go module", StringComparison.OrdinalIgnoreCase) =>
            [
                "Terraform/IaC: модули и переменные в предсказуемой структуре.",
                keyFiles.Count > 0
                    ? $"Ключевые артефакты: {string.Join(", ", keyFiles.Take(3))}."
                    : "Инфраструктура как код, OOP-слои не предполагаются."
            ],

            ProjectClassClassifier.UtilityAutomation when isDevOps =>

            [

                "Декларативные манифесты и скрипты автоматизации (YAML/Docker/Ansible) в дереве.",

                "DevOps-репозиторий: инфраструктура как код, OOP-слои не предполагаются."

            ],



            ProjectClassClassifier.UtilityAutomation

                when manifest.Contains("multi-pattern", StringComparison.OrdinalIgnoreCase) ||

                     manifest.Contains("Unity multi-pattern", StringComparison.OrdinalIgnoreCase) ||

                     ProjectClassClassifier.IsUnityArchitectureExamples(repoName) =>

            [

                "Учебный репозиторий: сравнение архитектурных стилей (MVC / MV / Flat) в одном месте.",

                "Ключевые файлы Wallet/Controller демонстрируют разделение UI и логики."

            ],



            ProjectClassClassifier.UtilityAutomation

                when manifest.Contains("C (native)", StringComparison.OrdinalIgnoreCase) ||

                     manifest.Contains("Linux kernel", StringComparison.OrdinalIgnoreCase) =>

            [

                "Нативный C-код: Makefile/Kconfig или модульная структура без enterprise DI.",

                keyFiles.Count > 0

                    ? $"Ключевые артефакты: {string.Join(", ", keyFiles.Take(3))}."

                    : "Структура соответствует системному или embedded C-проекту."

            ],



            ProjectClassClassifier.UtilityAutomation

                when UnityRepositoryHeuristics.IsUnityToolkitRepository(repoName, manifest) =>

            [

                "Unity toolkit/plugin: runtime отделён от сцен (Assets/Plugins или Editor).",

                keyFiles.Count > 0

                    ? $"Точки расширения: {string.Join(", ", keyFiles.Take(3))}."

                    : "Назначение пакета понятно по дереву и README."

            ],



            ProjectClassClassifier.UtilityAutomation

                when manifest.Contains("Python", StringComparison.OrdinalIgnoreCase) &&

                     !manifest.Contains("C (native)", StringComparison.OrdinalIgnoreCase) &&

                     repoName.Contains("bot", StringComparison.OrdinalIgnoreCase) =>

            [

                "Python-утилита/бот: компактная структура, конфиг и точка входа в дереве.",

                keyFiles.Count > 0

                    ? $"Ключевые модули: {string.Join(", ", keyFiles.Take(2))}."

                    : "Логика сосредоточена в небольшом наборе скриптов."

            ],



            ProjectClassClassifier.UtilityAutomation

                when manifest.Contains("Python", StringComparison.OrdinalIgnoreCase) &&

                     !manifest.Contains("C (native)", StringComparison.OrdinalIgnoreCase) =>

            [

                "Python-утилита/бот: компактная структура, конфиг и точка входа в дереве.",

                keyFiles.Count > 0

                    ? $"Ключевые модули: {string.Join(", ", keyFiles.Take(2))}."

                    : "Логика сосредоточена в небольшом наборе скриптов."

            ],



            ProjectClassClassifier.UtilityAutomation

                when manifest.Contains("Java", StringComparison.OrdinalIgnoreCase) ||

                     manifest.Contains("pom.xml", StringComparison.OrdinalIgnoreCase) =>

            [

                "Java-модуль: Maven/Gradle и исходники в предсказуемой структуре.",

                keyFiles.Count > 0

                    ? $"Ядро: {string.Join(", ", keyFiles.Take(2))}."

                    : "Назначение видно по pom/build и пакетам."

            ],



            ProjectClassClassifier.UtilityAutomation

                when ProjectClassClassifier.IsSmallPetConsoleGame(repoName, manifest) =>

            [

                "Небольшая консольная игра/демо: Program.cs и игровая логика без лишних слоёв.",

                keyFiles.Count > 0

                    ? $"Игровой контур: {string.Join(", ", keyFiles.Take(2))}."

                    : "Структура соответствует учебному pet-проекту."

            ],



            ProjectClassClassifier.UtilityAutomation

                when manifest.Contains("FileStorage", StringComparison.OrdinalIgnoreCase) ||

                     manifest.Contains("IStorage", StringComparison.OrdinalIgnoreCase) =>

            [

                "Консольная утилита с абстракцией хранилища (FileStorage/IStorage).",

                "Компактная структура — характерна для учебного CLI без enterprise-слоёв."

            ],



            ProjectClassClassifier.UtilityAutomation =>

            [

                "Утилита или пример: компактная структура без enterprise-монолита.",

                keyFiles.Count >= 2

                    ? $"Точки входа: {string.Join(", ", keyFiles.Take(2))}."

                    : "Назначение репозитория понятно по дереву файлов."

            ],



            _ => ["Структура репозитория читаема по дереву файлов."]

        };

    }



    /// <summary>

    /// Для DocOps / QA / Utility — Cons только при явных рисках в manifest (сейчас не генерируем мета-оправдания).

    /// </summary>

    internal static List<string> GetContextualCons(string projectClass, string manifest, IReadOnlyList<string> keyFiles) =>

        [];

    private static IEnumerable<string> FilterBulletsForLocale(
        IEnumerable<string> items,
        AuditContentLocale locale)
    {
        if (locale != AuditContentLocale.En)
        {
            return items;
        }

        return items.Where(static item =>
            !string.IsNullOrWhiteSpace(item) &&
            item.Count(static c => c is >= '\u0400' and <= '\u04FF') == 0);
    }
}

