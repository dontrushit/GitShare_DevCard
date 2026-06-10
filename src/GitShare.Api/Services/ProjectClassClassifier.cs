namespace GitShare.Api.Services;

/// <summary>
/// Классификация репозитория перед архитектурным аудитом (правило «сначала тип — потом критерии»).
/// </summary>
internal static class ProjectClassClassifier
{
    public const string ProductionApp = "Production App";
    public const string UtilityAutomation = "Utility / Automation";
    public const string QaTesting = "QA / Testing";
    public const string DocOpsKnowledgeBase = "DocOps / Knowledge Base";

    public static bool IsUnityArchitectureExamples(string repoName) =>
        repoName.Contains("architecture-examples", StringComparison.OrdinalIgnoreCase);

    public static string Classify(string repoName, string manifest)
    {
        if (IsQaByRepoName(repoName) || IsQaManifest(manifest))
        {
            return QaTesting;
        }

        if (UnityRepositoryHeuristics.IsUnityToolkitRepository(repoName, manifest) &&
            manifest.Contains("Unity", StringComparison.OrdinalIgnoreCase))
        {
            return UtilityAutomation;
        }

        if (IsSmallPetConsoleGame(repoName, manifest))
        {
            return UtilityAutomation;
        }

        if (UnityRepositoryHeuristics.IsUnityLearningRepository(repoName) &&
            manifest.Contains("Unity", StringComparison.OrdinalIgnoreCase))
        {
            return UtilityAutomation;
        }

        if (RepositorySelection.MatchesAuditBlacklist(repoName) ||
            manifest.Contains("Static / Documentation", StringComparison.OrdinalIgnoreCase) ||
            manifest.Contains("Suggested layout: Documentation", StringComparison.OrdinalIgnoreCase))
        {
            return DocOpsKnowledgeBase;
        }

        if (manifest.Contains("Primary framework: Next.js", StringComparison.OrdinalIgnoreCase) ||
            manifest.Contains("Web Application (SSR)", StringComparison.OrdinalIgnoreCase))
        {
            return ProductionApp;
        }

        if (manifest.Contains("Utility/test stack: yes", StringComparison.OrdinalIgnoreCase))
        {
            if (IsQaManifest(manifest))
            {
                return QaTesting;
            }

            return UtilityAutomation;
        }

        if (IsProductionManifest(manifest) && HasApplicationCodeSignals(manifest))
        {
            return ProductionApp;
        }

        if (manifest.Contains("Linux kernel", StringComparison.OrdinalIgnoreCase) ||
            (manifest.Contains("C (native)", StringComparison.OrdinalIgnoreCase) &&
             manifest.Contains("Kconfig", StringComparison.OrdinalIgnoreCase)))
        {
            return ProductionApp;
        }

        if (IsDocOpsByContent(manifest))
        {
            return DocOpsKnowledgeBase;
        }

        if ((manifest.Contains("Python", StringComparison.OrdinalIgnoreCase) ||
             manifest.Contains("Script / Library", StringComparison.OrdinalIgnoreCase)) &&
            !manifest.Contains("C (native)", StringComparison.OrdinalIgnoreCase) &&
            !manifest.Contains("Linux kernel", StringComparison.OrdinalIgnoreCase))
        {
            return UtilityAutomation;
        }

        if (manifest.Contains("Node.js", StringComparison.OrdinalIgnoreCase) ||
            manifest.Contains("Console Utility", StringComparison.OrdinalIgnoreCase) ||
            manifest.Contains("DevOps", StringComparison.OrdinalIgnoreCase) ||
            manifest.Contains("Terraform", StringComparison.OrdinalIgnoreCase))
        {
            return UtilityAutomation;
        }

        if (string.IsNullOrWhiteSpace(manifest) ||
            manifest.Contains("file tree empty", StringComparison.OrdinalIgnoreCase))
        {
            return DocOpsKnowledgeBase;
        }

        return UtilityAutomation;
    }

    public static string DefaultDebtSeverityForClass(string projectClass) =>
        projectClass switch
        {
            DocOpsKnowledgeBase => "NONE",
            UtilityAutomation or QaTesting => "CLEAN",
            _ => "Warning"
        };

    public static string DefaultTechnicalDebtForClass(string projectClass, string repoName, string? manifest = null)
    {
        if (IsUnityArchitectureExamples(repoName))
        {
            return "Учебная витрина архитектурных стилей (Flat / MVC / MV). Сравнение паттернов, а не production runtime.";
        }

        if (projectClass == UtilityAutomation && !string.IsNullOrWhiteSpace(manifest))
        {
            if (manifest.Contains("C (native)", StringComparison.OrdinalIgnoreCase) ||
                manifest.Contains("Linux kernel", StringComparison.OrdinalIgnoreCase))
            {
                return "Нативный C-код: модульная структура и Makefile; enterprise-слои (DI/Repository) не применяются.";
            }

            var hasServices = manifest.Contains("Services folder", StringComparison.OrdinalIgnoreCase) ||
                              manifest.Contains("/Services/", StringComparison.OrdinalIgnoreCase);
            var hasStorage = manifest.Contains("/Storage/", StringComparison.OrdinalIgnoreCase) ||
                             manifest.Contains("FileStorage", StringComparison.OrdinalIgnoreCase) ||
                             manifest.Contains("IStorage", StringComparison.OrdinalIgnoreCase);

            if (hasServices && hasStorage)
            {
                return "Консольное приложение: Services/ и Storage/ в дереве; DI-контейнер и Repository-слой отсутствуют.";
            }

            if (hasServices)
            {
                return "Консольное приложение: прикладная логика в Services/; DI-контейнер и data-слой Repository отсутствуют.";
            }
        }

        if (projectClass == ProductionApp &&
            (manifest?.Contains("Linux kernel", StringComparison.OrdinalIgnoreCase) == true ||
             repoName.Equals("linux", StringComparison.OrdinalIgnoreCase)))
        {
            return "Ядро Linux: подсистемы drivers/kernel/mm; оценка по сигнатурам Kconfig/Makefile, не по enterprise DI.";
        }

        return projectClass switch
        {
            DocOpsKnowledgeBase =>
                "Информационный репозиторий / материалы выступлений. Архитектурный анализ неприменим.",
            QaTesting =>
                "QA / автотесты: enterprise-слои (Repository/Services/DI) отсутствуют. Структура задаётся тестовым контуром и конфигами.",
            UtilityAutomation =>
                "Utility / automation: data-слои и DI отсутствуют — для CLI и скриптов типично. Структура опирается на модули и конфигурацию.",
            _ => string.Empty
        };
    }

    public static string NormalizeProjectClass(string? projectClass)
    {
        var value = projectClass?.Trim() ?? string.Empty;
        if (value.Contains("Production", StringComparison.OrdinalIgnoreCase))
        {
            return ProductionApp;
        }

        if (value.Contains("Utility", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Automation", StringComparison.OrdinalIgnoreCase))
        {
            return UtilityAutomation;
        }

        if (value.Contains("QA", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Testing", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Test", StringComparison.OrdinalIgnoreCase))
        {
            return QaTesting;
        }

        if (value.Contains("DocOps", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Knowledge", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Doc", StringComparison.OrdinalIgnoreCase))
        {
            return DocOpsKnowledgeBase;
        }

        return value switch
        {
            ProductionApp => ProductionApp,
            UtilityAutomation => UtilityAutomation,
            QaTesting => QaTesting,
            DocOpsKnowledgeBase => DocOpsKnowledgeBase,
            _ => string.Empty
        };
    }

    public static string DefaultInterviewQuestionForClass(string projectClass, string repoName, string? manifest = null)
    {
        if (IsUnityArchitectureExamples(repoName))
        {
            return "Какие плюсы и минусы MVC, MVVM и MVP вы видите именно в Unity — и когда какой стиль выбираете?";
        }

        var m = manifest ?? string.Empty;
        if (repoName.Equals("linux", StringComparison.OrdinalIgnoreCase) ||
            m.Contains("Linux kernel", StringComparison.OrdinalIgnoreCase))
        {
            return $"В {repoName}: как вы подходите к ревью изменений в подсистемах и обратной совместимости ABI?";
        }

        if (projectClass == UtilityAutomation)
        {
            if (m.Contains("C (native)", StringComparison.OrdinalIgnoreCase))
            {
                return $"В {repoName}: как вы организуете сборку, тестирование и переносимость нативного C-кода?";
            }

            if (repoName.Contains("k3sup", StringComparison.OrdinalIgnoreCase))
            {
                return $"В {repoName}: как вы обеспечиваете идемпотентность bootstrap K3s по SSH и безопасную работу с ключами?";
            }

            if (repoName.Contains("arkade", StringComparison.OrdinalIgnoreCase))
            {
                return $"В {repoName}: как устроена установка сторонних CLI и проверка версий/архитектур без «магии» в скриптах?";
            }

            if (repoName.Equals("derek", StringComparison.OrdinalIgnoreCase))
            {
                return $"В {repoName}: как бот принимает решения по PR/Issue и где проходит граница между автоматизацией и политикой maintainers?";
            }

            if (m.Contains("Terraform", StringComparison.OrdinalIgnoreCase))
            {
                return $"В {repoName}: как вы версионируете Terraform-модули и изолируете state между окружениями?";
            }
        }

        return projectClass switch
        {
            DocOpsKnowledgeBase =>
                $"В {repoName}: как вы структурируете знания и поддерживаете актуальность материалов для команды?",
            QaTesting =>
                $"В {repoName}: как устроена изоляция тестов, фикстуры и отчёты при падении CI?",
            UtilityAutomation =>
                $"В {repoName}: как вы разделяете конфигурацию, секреты и исполняемую логику в утилите?",
            _ =>
                $"В {repoName}: объясните разделение ответственности между ключевыми модулями по дереву файлов."
        };
    }

    public static bool HasApplicationCodeSignals(string manifest) =>
        manifest.Contains("Program.cs", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains(".csproj", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains("App.xaml", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains(".java", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains("pom.xml", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains("build.gradle", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains("go.mod", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains("Cargo.toml", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains("Unity", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains("Kconfig", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains("Linux kernel", StringComparison.OrdinalIgnoreCase) ||
        (manifest.Contains("C (native)", StringComparison.OrdinalIgnoreCase) &&
         manifest.Contains("Makefile", StringComparison.OrdinalIgnoreCase));

    public static bool IsDocOpsByContent(string manifest)
    {
        if (HasApplicationCodeSignals(manifest))
        {
            return false;
        }

        var hasReadmeOrDocs = manifest.Contains("README", StringComparison.OrdinalIgnoreCase) ||
                              manifest.Contains(".md", StringComparison.OrdinalIgnoreCase) ||
                              manifest.Contains("_config.yml", StringComparison.OrdinalIgnoreCase) ||
                              manifest.Contains(".jpg", StringComparison.OrdinalIgnoreCase) ||
                              manifest.Contains(".png", StringComparison.OrdinalIgnoreCase);

        var noAppStack = !manifest.Contains("Spring Boot", StringComparison.OrdinalIgnoreCase) &&
                         !manifest.Contains("ASP.NET", StringComparison.OrdinalIgnoreCase);

        return hasReadmeOrDocs && noAppStack;
    }

    public static bool IsDevOpsManifest(string manifest) =>
        manifest.Contains("Kubernetes/Docker", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains("DevOps", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains("Terraform", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains("Dockerfile", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains("docker-compose", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains(".yml.j2", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains("ansible", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains("playbook", StringComparison.OrdinalIgnoreCase);

    public static bool IsQaByRepoName(string repoName)
    {
        var lower = repoName.ToLowerInvariant();
        return lower.Contains("automated-testing", StringComparison.Ordinal) ||
               lower.Contains("autotest", StringComparison.Ordinal) ||
               lower.Contains("e2e-test", StringComparison.Ordinal) ||
               lower.Contains("playwright", StringComparison.Ordinal) ||
               lower.EndsWith("-tests", StringComparison.Ordinal) ||
               lower.EndsWith("_tests", StringComparison.Ordinal);
    }

    public static bool IsQaManifest(string manifest) =>
        manifest.Contains("Playwright", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains("Cypress", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains("Selenium", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains("E2E Test", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains("Test Suite", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains("Jest/Vitest", StringComparison.OrdinalIgnoreCase);

    public static bool IsSmallPetConsoleGame(string repoName, string manifestOrFramework)
    {
        var combined = $"{repoName} {manifestOrFramework}";
        var lowerName = repoName.ToLowerInvariant();

        if (!combined.Contains("Console", StringComparison.OrdinalIgnoreCase) &&
            !combined.Contains("Program.cs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (combined.Contains("Unity", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("WinForms", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("WPF", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("Spring Boot", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return lowerName.Contains("snake", StringComparison.Ordinal) ||
               lowerName.Contains("game", StringComparison.Ordinal) ||
               lowerName.Contains("puzzle", StringComparison.Ordinal);
    }

    private static bool IsProductionManifest(string manifest) =>
        manifest.Contains("ASP.NET", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains("WinForms", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains("WPF", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains("Spring Boot", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains("Unity", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains("Web API", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains("Web Application (SSR)", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains("Primary framework: Next.js", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains("Layered (Repository", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains("MVVM", StringComparison.OrdinalIgnoreCase) ||
        manifest.Contains("Linux kernel", StringComparison.OrdinalIgnoreCase) ||
        (manifest.Contains("Program.cs", StringComparison.OrdinalIgnoreCase) &&
         manifest.Contains("appsettings", StringComparison.OrdinalIgnoreCase));
}
