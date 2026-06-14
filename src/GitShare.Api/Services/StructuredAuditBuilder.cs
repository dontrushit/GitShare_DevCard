using GitShare.Api.Models;

namespace GitShare.Api.Services;

internal static class StructuredAuditBuilder
{
    public static StructuredAuditResponse BuildFromForensics(
        IReadOnlyList<RepositoryForensics> forensics,
        AuditContentLocale locale = AuditContentLocale.Ru,
        int portfolioTotalStars = -1)
    {
        if (forensics.Count == 0)
        {
            return new StructuredAuditResponse
            {
                CoreEngineeringFocus = AuditContentCatalog.InsufficientDataFocus(locale),
                Projects =
                [
                    new ProjectAuditDetail
                    {
                        RepoName = "N/A",
                        ProjectClass = ProjectClassClassifier.DocOpsKnowledgeBase,
                        Framework = AuditContentCatalog.UndefinedFramework(locale),
                        LayoutType = AuditContentCatalog.UnknownLayout(locale),
                        KeyFiles = [],
                        TechnicalDebt = AuditContentCatalog.FileSignaturesUnavailable(locale),
                        DebtSeverity = "Warning",
                        InterviewTrapQuestion = AuditContentCatalog.DefaultInterviewQuestion(locale),
                        Pros = [],
                        Cons = []
                    }
                ]
            };
        }

        var projects = forensics.Select(f => BuildProjectDetail(f, locale, portfolioTotalStars)).ToList();
        var response = new StructuredAuditResponse
        {
            Projects = projects,
            CoreEngineeringFocus = locale == AuditContentLocale.En
                ? InferCoreFocusEn(projects)
                : InferCoreFocus(forensics, projects)
        };
        GitTelemetryAnalyzer.ApplyTelemetryFields(response, new GitHubActivityTelemetry(), null, locale);
        return response;
    }

    public static StructuredAuditResponse Normalize(
        StructuredAuditResponse raw,
        AuditContentLocale locale = AuditContentLocale.Ru)
    {
        var projects = (raw.Projects ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p.RepoName))
            .Select(p => NormalizeProject(p, locale))
            .ToList();

        return new StructuredAuditResponse
        {
            Projects = projects,
            CoreEngineeringFocus = AuditTextSanitizer.ContainsForbiddenLanguage(raw.CoreEngineeringFocus ?? string.Empty)
                ? locale == AuditContentLocale.En
                    ? InferCoreFocusEn(projects)
                    : InferCoreFocus([], projects)
                : string.IsNullOrWhiteSpace(raw.CoreEngineeringFocus)
                    ? locale == AuditContentLocale.En
                        ? InferCoreFocusEn(projects)
                        : InferCoreFocus([], projects)
                    : raw.CoreEngineeringFocus.Trim(),
            GitFormatStandard = GitTelemetryAnalyzer.NormalizeFormatStandard(raw.GitFormatStandard)
                is { Length: > 0 } normalized
                    ? normalized
                    : raw.GitFormatStandard?.Trim() ?? string.Empty,
            ExperienceProfile = raw.ExperienceProfile?.Trim() ?? string.Empty,
            OpenSourceImpact = raw.OpenSourceImpact?.Trim() ?? string.Empty
        };
    }

    private static string InferCoreFocusEn(IReadOnlyList<ProjectAuditDetail> projects)
    {
        var stacks = projects
            .Select(p => p.Framework)
            .Where(f => !string.IsNullOrWhiteSpace(f) &&
                        !f.Contains("не определён", StringComparison.OrdinalIgnoreCase) &&
                        !f.Contains("undefined", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();

        return stacks.Count > 0
            ? $"Dominant stacks in audited repos: {string.Join(", ", stacks)}."
            : AuditContentCatalog.InsufficientDataFocus(AuditContentLocale.En);
    }

    private static ProjectAuditDetail BuildProjectDetail(
        RepositoryForensics repo,
        AuditContentLocale locale,
        int portfolioTotalStars)
    {
        var manifest = repo.TargetSignatureManifest;
        var keyFiles = ParseKeyFiles(manifest);
        var projectClass = repo.IsVendorAssetPack
            ? ProjectClassClassifier.UtilityAutomation
            : ProjectClassProsCons.ResolveEffectiveClass(
                ProjectClassClassifier.Classify(repo.RepoName, manifest),
                repo.RepoName,
                manifest);

        string technicalDebt;
        string debtSeverity;
        string interviewTrap;

        if (repo.IsVendorAssetPack)
        {
            technicalDebt = locale == AuditContentLocale.En
                ? "Third-party assets (Asset Store / VFX pack). Architectural audit of authored code does not apply."
                : "Сторонние ассеты (Asset Store / VFX pack). Архитектурный аудит авторского кода неприменим.";
            debtSeverity = "NONE";
            interviewTrap = locale == AuditContentLocale.En
                ? $"In {repo.RepoName}: how do you separate vendor assets from your own code in Unity projects?"
                : $"В {repo.RepoName}: как вы отделяете vendor-ассеты от собственного кода в Unity-проектах?";
        }
        else if (projectClass == ProjectClassClassifier.ProductionApp)
        {
            var isLinuxKernel = repo.RepoName.Equals("linux", StringComparison.OrdinalIgnoreCase) ||
                                manifest.Contains("Linux kernel", StringComparison.OrdinalIgnoreCase);

            if (isLinuxKernel)
            {
                technicalDebt = AuditContentCatalog.DefaultTechnicalDebt(projectClass, repo.RepoName, manifest, locale);
                debtSeverity = "CLEAN";
                interviewTrap = locale == AuditContentLocale.En
                    ? $"In {repo.RepoName}: how do you navigate subsystem boundaries and review blast radius for kernel changes?"
                    : ProjectClassClassifier.DefaultInterviewQuestionForClass(
                        projectClass, repo.RepoName, manifest);
            }
            else
            {
                technicalDebt = BuildTechnicalDebt(manifest, locale);
                debtSeverity = ArchitectureSeverityResolver.FromManifest(manifest);
                interviewTrap = BuildInterviewQuestion(manifest, repo.RepoName, keyFiles, locale);
            }
        }
        else
        {
            technicalDebt = AuditContentCatalog.DefaultTechnicalDebt(projectClass, repo.RepoName, manifest, locale);
            debtSeverity = ArchitectureSeverityResolver.ResolveInitial(repo, projectClass);
            interviewTrap = locale == AuditContentLocale.En
                ? AuditContentCatalog.DefaultInterviewQuestion(locale)
                : ProjectClassClassifier.DefaultInterviewQuestionForClass(projectClass, repo.RepoName, manifest);
        }

        var bullets = ProjectClassProsCons.Build(
            projectClass,
            repo.RepoName,
            manifest,
            keyFiles,
            repo.VerifiedPros,
            repo.VerifiedCons,
            locale);

        var detail = new ProjectAuditDetail
        {
            RepoName = repo.RepoName,
            ProjectClass = projectClass,
            Framework = AuditFieldLocalizer.LocalizeFramework(InferFramework(manifest, locale), locale),
            LayoutType = AuditFieldLocalizer.LocalizeLayout(InferLayout(manifest, locale), locale),
            KeyFiles = UnityRepositoryHeuristics.FilterKeyFilesForDisplay(keyFiles, repo.BlobPaths),
            TechnicalDebt = technicalDebt,
            DebtSeverity = debtSeverity,
            InterviewTrapQuestion = interviewTrap,
            Pros = bullets.Pros.ToList(),
            Cons = ConsBulletSanitizer.Finalize(bullets.Cons, projectClass)
        };

        var analysis = ReadmeStructureVerifier.Analyze(
            repo.RepoName,
            repo.Readme,
            detail.Framework,
            detail.DebtSeverity,
            portfolioTotalStars);

        detail.ProjectClass = ReadmeStructureVerifier.AdjustProjectClass(
            detail.ProjectClass,
            repo.RepoName,
            repo.Readme,
            detail.Framework,
            detail.DebtSeverity,
            portfolioTotalStars);
        detail.TechnicalDebt = ReadmeStructureVerifier.AppendMismatchNote(detail.TechnicalDebt, analysis);

        var repoLevel = RepositoryLevelEvaluator.Evaluate(repo, detail.ProjectClass, locale);
        var assessment = RepositoryArchitectureAssessmentBuilder.Build(
            repo,
            detail.ProjectClass,
            detail.Framework,
            detail.LayoutType,
            detail.DebtSeverity,
            detail.Pros,
            detail.Cons,
            repoLevel,
            locale);

        detail.RepositoryLevel = repoLevel;
        detail.ArchitectureSummary = assessment.ArchitectureSummary;
        detail.Pros = assessment.Strengths.ToList();
        detail.Cons = assessment.Risks.ToList();
        detail.DebtSeverity = ArchitectureSeverityResolver.Resolve(
            repo,
            detail.ProjectClass,
            detail.DebtSeverity,
            detail.Cons);

        return detail;
    }

    private static ProjectAuditDetail NormalizeProject(ProjectAuditDetail project, AuditContentLocale locale) =>
        new()
        {
            RepoName = project.RepoName.Trim(),
            ProjectClass = NormalizeProjectClass(project.ProjectClass),
            Framework = string.IsNullOrWhiteSpace(project.Framework)
                ? AuditContentCatalog.UndefinedFramework(locale)
                : project.Framework.Trim(),
            LayoutType = string.IsNullOrWhiteSpace(project.LayoutType)
                ? AuditContentCatalog.UnknownLayout(locale)
                : project.LayoutType.Trim(),
            KeyFiles = (project.KeyFiles ?? [])
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Select(f => f.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .ToList(),
            TechnicalDebt = AuditTextSanitizer.ContainsForbiddenLanguage(project.TechnicalDebt)
                ? locale == AuditContentLocale.En
                    ? "Architecture assessment from signatures: folder layout and key files."
                    : "Архитектурная оценка по сигнатурам: структура папок и KeyFiles."
                : project.TechnicalDebt.Trim(),
            DebtSeverity = AuditSeverityNormalizer.Normalize(project.DebtSeverity),
            InterviewTrapQuestion = AuditTextSanitizer.ContainsForbiddenLanguage(project.InterviewTrapQuestion)
                ? locale == AuditContentLocale.En
                    ? $"In {project.RepoName}: explain responsibility split between the key files (from signatures)."
                    : $"По сигнатурам в {project.RepoName}: объясните разделение ответственности между ключевыми файлами."
                : project.InterviewTrapQuestion.Trim(),
            RepositoryLevel = project.RepositoryLevel,
            ArchitectureSummary = string.IsNullOrWhiteSpace(project.ArchitectureSummary)
                ? string.Empty
                : project.ArchitectureSummary.Trim(),
            Pros = FilterBulletsForContentLocale(
                SanitizeProsConsForClass(
                    NormalizeBulletList(project.Pros),
                    project.ProjectClass,
                    project.KeyFiles,
                    isPros: true),
                locale),
            Cons = ConsBulletSanitizer.Finalize(
                FilterBulletsForContentLocale(
                    SanitizeProsConsForClass(
                        NormalizeBulletList(project.Cons),
                        project.ProjectClass,
                        project.KeyFiles,
                        isPros: false),
                    locale),
                project.ProjectClass)
        };

    private static List<string> FilterBulletsForContentLocale(
        List<string> items,
        AuditContentLocale locale)
    {
        if (locale == AuditContentLocale.En)
        {
            return items
                .Where(static item => item.Count(static c => c is >= '\u0400' and <= '\u04FF') == 0)
                .ToList();
        }

        return items
            .Where(static item =>
            {
                if (string.IsNullOrWhiteSpace(item))
                {
                    return false;
                }

                var cyrillic = item.Count(static c => c is >= '\u0400' and <= '\u04FF');
                if (cyrillic >= 4)
                {
                    return true;
                }

                var latin = item.Count(static c => char.IsLetter(c) && c is (< '\u0400' or > '\u04FF'));
                return latin < 24;
            })
            .ToList();
    }

    private static List<string> NormalizeBulletList(IEnumerable<string>? items) =>
        (items ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Where(s => !AuditTextSanitizer.ContainsForbiddenLanguage(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

    internal static List<string> BuildProsFromManifest(string manifest, AuditContentLocale locale = AuditContentLocale.Ru) =>
        locale == AuditContentLocale.En
            ? StructuredAuditNarrativesEn.Pros(manifest)
            : BuildPros(manifest);

    internal static List<string> BuildConsFromManifest(string manifest, AuditContentLocale locale = AuditContentLocale.Ru) =>
        locale == AuditContentLocale.En
            ? StructuredAuditNarrativesEn.Cons(manifest)
            : BuildCons(manifest);

    private static List<string> SanitizeProsConsForClass(
        List<string> items,
        string projectClass,
        IReadOnlyList<string> keyFiles,
        bool isPros)
    {
        if (!isPros)
        {
            return ConsBulletSanitizer.Filter(items);
        }

        if (EnterpriseAuditLexicon.IsProductionClass(projectClass) || items.Count == 0)
        {
            return items;
        }

        if (!items.Any(EnterpriseAuditLexicon.ContainsEnterpriseOnlyTerms))
        {
            return items;
        }

        return ProjectClassProsCons.GetContextualPros(projectClass, string.Empty, keyFiles, string.Empty);
    }

    private static List<string> BuildPros(string manifest)
    {
        var pros = new List<string>();

        if (manifest.Contains("Unity", StringComparison.OrdinalIgnoreCase))
        {
            if (manifest.Contains("Plugins/", StringComparison.OrdinalIgnoreCase))
            {
                pros.Add("Структура Unity-плагина: Assets/Plugins/ — runtime и Editor отделены от сцен.");
            }

            if (manifest.Contains("multi-pattern", StringComparison.OrdinalIgnoreCase))
            {
                pros.Add("Набор архитектурных примеров (MVC/MV/Flat) в одном репозитории — наглядно для обучения.");
            }

            if (manifest.Contains("Editor scripts", StringComparison.OrdinalIgnoreCase))
            {
                pros.Add("Editor-скрипты в дереве — автоматизация сборки/валидации в Unity.");
            }

            if (manifest.Contains("test assemblies", StringComparison.OrdinalIgnoreCase))
            {
                pros.Add("Тестовый контур Unity (Tests/) обнаружен в структуре репозитория.");
            }
        }

        if (manifest.Contains("WPF", StringComparison.OrdinalIgnoreCase) &&
            manifest.Contains("Converters", StringComparison.OrdinalIgnoreCase))
        {
            pros.Add("Изоляция UI через MVVM: Views отделены от Services/Converters.");
        }

        if (ManifestSignalParser.HasStackSignal(manifest, "Java"))
        {
            pros.Add("Java-стек в дереве (Maven/Gradle + .java) — стек однозначен по сигнатурам.");
        }

        if (ManifestSignalParser.HasStackSignal(manifest, "Spring Boot"))
        {
            pros.Add("Spring Boot в структуре — enterprise-микросервисный контур.");
        }

        if (ManifestSignalParser.ManifestListsServicesFolder(manifest))
        {
            pros.Add("Выделенный слой Services — бизнес-логика не смешана с разметкой.");
        }

        if (ManifestSignalParser.ManifestListsRepositoryLayer(manifest))
        {
            pros.Add("Data-access вынесен в Repository/Context — проще тестировать персистентность.");
        }

        if (manifest.Contains("Interfaces/", StringComparison.OrdinalIgnoreCase) ||
            manifest.Contains("IStorage", StringComparison.OrdinalIgnoreCase))
        {
            pros.Add("Абстракция хранилища через интерфейсы — задел под DIP и подмену реализации.");
        }

        if (manifest.Contains("appsettings", StringComparison.OrdinalIgnoreCase))
        {
            pros.Add("Конфигурация через appsettings — параметры не захардкожены в коде.");
        }

        if (pros.Count == 0 && ProjectClassClassifier.HasApplicationCodeSignals(manifest))
        {
            pros.Add("Структура репозитория читаема по сигнатурам ключевых файлов.");
            pros.Add("Стек и тип приложения однозначно определяются по дереву.");
        }

        return pros.Take(3).ToList();
    }

    private static List<string> BuildCons(string manifest)
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
                cons.Add("В дереве нет Unity Tests/EditModeTests — модульные тесты не видны.");
            }

            if (!isToolkit &&
                !hasCompositionRoot &&
                !manifest.Contains("Zenject", StringComparison.OrdinalIgnoreCase) &&
                !manifest.Contains("VContainer", StringComparison.OrdinalIgnoreCase) &&
                !manifest.Contains("IServiceCollection", StringComparison.OrdinalIgnoreCase))
            {
                cons.Add("DI-контейнер (Zenject/VContainer/MS.DI) в сигнатурах не найден — типичная связность через сцены.");
            }
        }

        if (manifest.Contains("DbHelper", StringComparison.OrdinalIgnoreCase) &&
            !ManifestSignalParser.ManifestListsRepositoryLayer(manifest))
        {
            cons.Add("DbHelper без Repository/DI — жёсткая связность UI и data-access.");
        }

        if (manifest.Contains(".accdb", StringComparison.OrdinalIgnoreCase) &&
            !ManifestSignalParser.ManifestListsRepositoryLayer(manifest))
        {
            cons.Add("Прямая работа с .accdb — нет слоя миграций и масштабируемой СУБД.");
        }

        if (manifest.Contains("Form", StringComparison.OrdinalIgnoreCase) &&
            manifest.Contains("Helpers", StringComparison.OrdinalIgnoreCase) &&
            !manifest.Contains("Services folder", StringComparison.OrdinalIgnoreCase))
        {
            cons.Add("Flat Monolith WinForms: Form + Helpers без application/repository слоя.");
        }

        if (manifest.Contains("WPF", StringComparison.OrdinalIgnoreCase) &&
            ManifestSignalParser.ManifestListsServicesFolder(manifest) &&
            !ManifestSignalParser.ManifestListsRepositoryLayer(manifest))
        {
            cons.Add("MVVM без Repository/Context — data-слой не отделён от UI-цепочки.");
        }

        if (!manifest.Contains("Interfaces/", StringComparison.OrdinalIgnoreCase) &&
            (manifest.Contains("FileStorage", StringComparison.OrdinalIgnoreCase) ||
             manifest.Contains("DbHelper", StringComparison.OrdinalIgnoreCase)))
        {
            cons.Add("Отсутствие интерфейсов для подмены хранилища (DIP).");
        }

        if (manifest.Contains("FileStorage", StringComparison.OrdinalIgnoreCase))
        {
            cons.Add("Жёсткая привязка персистентности к файловому I/O (JSON).");
        }

        if (manifest.Contains("Program.cs", StringComparison.OrdinalIgnoreCase) &&
            !manifest.Contains("appsettings", StringComparison.OrdinalIgnoreCase) &&
            !manifest.Contains("Unity", StringComparison.OrdinalIgnoreCase))
        {
            cons.Add("Нет appsettings.json — конфигурация, вероятно, в коде.");
        }

        return cons.Take(3).ToList();
    }

    private static string NormalizeProjectClass(string? projectClass)
    {
        var value = projectClass?.Trim() ?? string.Empty;
        return value switch
        {
            ProjectClassClassifier.ProductionApp => ProjectClassClassifier.ProductionApp,
            ProjectClassClassifier.UtilityAutomation => ProjectClassClassifier.UtilityAutomation,
            ProjectClassClassifier.QaTesting => ProjectClassClassifier.QaTesting,
            ProjectClassClassifier.DocOpsKnowledgeBase => ProjectClassClassifier.DocOpsKnowledgeBase,
            _ => string.Empty
        };
    }

    private static string InferFramework(string manifest, AuditContentLocale locale = AuditContentLocale.Ru)
    {
        var primary = ExtractManifestValue(manifest, "Primary framework:");
        if (!string.IsNullOrWhiteSpace(primary) &&
            !primary.Contains("не определён", StringComparison.OrdinalIgnoreCase) &&
            !primary.Contains("undefined", StringComparison.OrdinalIgnoreCase))
        {
            return primary.Trim();
        }

        var stack = ExtractManifestValue(manifest, "Stack signals:") ?? string.Empty;
        var arch = ExtractManifestValue(manifest, "Architecture signals:") ?? string.Empty;

        if (stack.Contains("Playwright", StringComparison.OrdinalIgnoreCase))
        {
            return "Playwright, TypeScript";
        }

        if (stack.Contains("Python", StringComparison.OrdinalIgnoreCase))
        {
            return "Python";
        }

        if (stack.Contains("Selenium IDE", StringComparison.OrdinalIgnoreCase))
        {
            return "Selenium IDE";
        }

        if (stack.Contains("TypeScript", StringComparison.OrdinalIgnoreCase) ||
            manifest.Contains(".ts", StringComparison.OrdinalIgnoreCase) ||
            manifest.Contains("tsconfig", StringComparison.OrdinalIgnoreCase))
        {
            return stack.Contains("React", StringComparison.OrdinalIgnoreCase) ||
                   stack.Contains("Next.js", StringComparison.OrdinalIgnoreCase)
                ? "TypeScript, React"
                : "TypeScript";
        }

        if (stack.Contains("Node.js", StringComparison.OrdinalIgnoreCase) ||
            stack.Contains("React", StringComparison.OrdinalIgnoreCase) ||
            stack.Contains("Next.js", StringComparison.OrdinalIgnoreCase))
        {
            return stack.Contains("Next.js", StringComparison.OrdinalIgnoreCase) ? "Next.js, React" : "Node.js";
        }

        if (stack.Contains("Go module", StringComparison.OrdinalIgnoreCase))
        {
            return "Go";
        }

        if (stack.Contains("Terraform", StringComparison.OrdinalIgnoreCase) ||
            stack.Contains("Kubernetes/Docker", StringComparison.OrdinalIgnoreCase))
        {
            return "DevOps (IaC/Containers)";
        }

        if (stack.Contains("Spring Boot", StringComparison.OrdinalIgnoreCase))
        {
            return "Java, Spring Boot";
        }

        if (stack.Contains("Java", StringComparison.OrdinalIgnoreCase))
        {
            return "Java";
        }

        if (stack.Contains("Unity", StringComparison.OrdinalIgnoreCase))
        {
            if (stack.Contains("package/plugin", StringComparison.OrdinalIgnoreCase) ||
                arch.Contains("Plugins/", StringComparison.OrdinalIgnoreCase))
            {
                return "Unity, C# (Package/Plugin)";
            }

            if (arch.Contains("multi-pattern", StringComparison.OrdinalIgnoreCase) ||
                stack.Contains("architecture samples", StringComparison.OrdinalIgnoreCase))
            {
                return "Unity, C# (Architecture Samples)";
            }

            return "Unity, C#";
        }

        if (stack.Contains("WinForms", StringComparison.OrdinalIgnoreCase) ||
            arch.Contains("Form", StringComparison.OrdinalIgnoreCase))
        {
            return ".NET, WinForms";
        }

        if (stack.Contains("WPF", StringComparison.OrdinalIgnoreCase) ||
            arch.Contains("Converters", StringComparison.OrdinalIgnoreCase))
        {
            return ".NET, WPF";
        }

        if (arch.Contains("JSON file storage", StringComparison.OrdinalIgnoreCase))
        {
            return ".NET, Console";
        }

        if (stack.Contains("ASP.NET", StringComparison.OrdinalIgnoreCase))
        {
            return ".NET, ASP.NET";
        }

        if (stack.Contains("Program.cs", StringComparison.OrdinalIgnoreCase))
        {
            return ".NET";
        }

        return locale == AuditContentLocale.En
            ? AuditContentCatalog.UndefinedFramework(locale)
            : "не определён (по сигнатурам)";
    }

    private static string ClassifySeverity(string manifest)
    {
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

        return "Warning";
    }

    private static string BuildTechnicalDebt(string manifest, AuditContentLocale locale = AuditContentLocale.Ru)
    {
        if (locale == AuditContentLocale.En)
        {
            return StructuredAuditNarrativesEn.TechnicalDebt(manifest);
        }

        var parts = new List<string>();

        if (manifest.Contains(".accdb", StringComparison.OrdinalIgnoreCase) &&
            manifest.Contains("DbHelper", StringComparison.OrdinalIgnoreCase) &&
            !ManifestSignalParser.ManifestListsRepositoryLayer(manifest))
        {
            parts.Add(
                "DbHelper.cs + .accdb. Нет Repository/Context/DI. Data-access в хелпере, UI тянет БД напрямую.");
        }

        if (manifest.Contains("Form", StringComparison.OrdinalIgnoreCase) &&
            manifest.Contains("Helpers", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("Flat Monolith: Form*.cs + Helpers. Нет application/repository слоя в дереве.");
        }

        if (manifest.Contains("WPF", StringComparison.OrdinalIgnoreCase) &&
            manifest.Contains("Converters", StringComparison.OrdinalIgnoreCase) &&
            manifest.Contains("Services folder", StringComparison.OrdinalIgnoreCase))
        {
            if (!ManifestSignalParser.ManifestListsRepositoryLayer(manifest))
            {
                parts.Add(
                    "MVVM: Services + Converters есть. Repository/Context отсутствуют — data-слой не отделён от UI.");
            }
            else
            {
                parts.Add("MVVM: Services/Converters/Repository в сигнатурах. Слои разделены.");
            }
        }

        if (manifest.Contains("FileStorage", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add(
                "FileStorage.cs (JSON). СУБД в дереве нет. Масштабирование упирается в файловый I/O.");
        }

        if (manifest.Contains("Unity", StringComparison.OrdinalIgnoreCase))
        {
            if (!manifest.Contains("test assemblies", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(
                    "Unity-проект. Тестовые сборки в дереве не видны — регрессии ловятся вручную в Editor.");
            }

            if (manifest.Contains("Plugins/", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(
                    "Unity plugin. Runtime завязан на UnityEngine API — перенос логики вне движка без адаптеров невозможен.");
            }
            else
            {
                parts.Add(
                    "Unity game/sample. Логика в Assets/ без видимого DI — типичная связность через сцены и компоненты.");
            }
        }

        if (manifest.Contains("Program.cs", StringComparison.OrdinalIgnoreCase) &&
            !manifest.Contains("appsettings", StringComparison.OrdinalIgnoreCase) &&
            !manifest.Contains(".accdb", StringComparison.OrdinalIgnoreCase) &&
            !manifest.Contains("FileStorage", StringComparison.OrdinalIgnoreCase) &&
            !manifest.Contains("Unity", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("Program.cs без appsettings.json. Конфигурация, вероятно, захардкожена.");
        }

        if (!ProjectClassClassifier.ManifestDescribesWebApi(manifest) &&
            (ManifestSignalParser.ManifestHasUtilityTestStackFlag(manifest) ||
             ProjectStackCatalog.IsUtilityOrTestStack(
                 ExtractManifestValue(manifest, "Primary framework:") ?? string.Empty,
                 ExtractManifestValue(manifest, "Suggested layout:") ?? string.Empty)))
        {
            parts.Add(
                "Утилита, тесты или IaC: enterprise-слои (Repository/Services) к формату не относятся.");
        }

        if (parts.Count == 0)
        {
            return "Критичных архитектурных провалов по сигнатурам не выявлено.";
        }

        return string.Join(" ", parts);
    }

    private static string BuildInterviewQuestion(
        string manifest,
        string repoName,
        IReadOnlyList<string> keyFiles,
        AuditContentLocale locale = AuditContentLocale.Ru)
    {
        if (locale == AuditContentLocale.En)
        {
            return StructuredAuditNarrativesEn.InterviewQuestion(manifest, repoName, keyFiles);
        }

        var citedFile = keyFiles.FirstOrDefault() ?? "ключевой файл из репозитория";

        if (manifest.Contains("DbHelper", StringComparison.OrdinalIgnoreCase))
        {
            return
                $"В {repoName} данные завязаны на DbHelper.cs + .accdb. Как обеспечивается потокобезопасность при параллельных запросах? Кто владеет жизненным циклом подключения?";
        }

        if (manifest.Contains("FileStorage", StringComparison.OrdinalIgnoreCase))
        {
            return
                $"В {repoName} персистентность через FileStorage.cs (JSON). При 10k записей — что меняете первым: IStorage, формат файла или миграцию на СУБД?";
        }

        if (manifest.Contains("DataService", StringComparison.OrdinalIgnoreCase))
        {
            return
                $"В {repoName} DataService.cs на границе с UI. Как тестируете data-слой без поднятия Views/Windows?";
        }

        if (ProjectClassClassifier.ManifestDescribesWebApi(manifest))
        {
            return manifest.Contains("React", StringComparison.OrdinalIgnoreCase) ||
                   manifest.Contains("SPA", StringComparison.OrdinalIgnoreCase)
                ? $"В {repoName}: опишите путь HTTP-запроса от API до БД и где проходит граница между контроллером, сервисом и persistence."
                : $"В {repoName}: как устроен pipeline запроса от контроллера до data-слоя? Где регистрируются зависимости?";
        }

        if (ManifestSignalParser.ManifestHasWpfConverterArtifacts(manifest))
        {
            citedFile = keyFiles.FirstOrDefault(f => f.Contains("Converter", StringComparison.OrdinalIgnoreCase)) ?? citedFile;
            return
                $"В {repoName} {citedFile} в MVVM-цепочке. Почему логика не в ViewModel/Service? Какой binding ломается без этого конвертера?";
        }

        if (manifest.Contains("Unity", StringComparison.OrdinalIgnoreCase) &&
            manifest.Contains("Plugins/", StringComparison.OrdinalIgnoreCase))
        {
            return
                $"В {repoName} плагин в Assets/Plugins/. Как тестируете {citedFile} без поднятия Play Mode и без сцены в Editor?";
        }

        if (manifest.Contains("Unity", StringComparison.OrdinalIgnoreCase))
        {
            return
                $"В {repoName} ({citedFile}): что мокаете при unit-тесте — UnityEngine API, сцену или domain вне MonoBehaviour?";
        }

        return
            $"В {repoName} роль {citedFile}: какие зависимости он обязан НЕ тянуть на себя?";
    }

    private static string InferCoreFocus(
        IReadOnlyList<RepositoryForensics> forensics,
        IReadOnlyList<ProjectAuditDetail> projects)
    {
        var winForms = 0;
        var wpf = 0;
        var console = 0;
        var unity = 0;

        foreach (var repo in forensics)
        {
            var m = repo.TargetSignatureManifest;
            if (m.Contains("Unity", StringComparison.OrdinalIgnoreCase))
            {
                unity++;
            }
            else if (m.Contains("WinForms", StringComparison.OrdinalIgnoreCase) ||
                m.Contains("Form", StringComparison.OrdinalIgnoreCase))
            {
                winForms++;
            }
            else if (m.Contains("WPF", StringComparison.OrdinalIgnoreCase))
            {
                wpf++;
            }
            else if (m.Contains("FileStorage", StringComparison.OrdinalIgnoreCase) ||
                     m.Contains("Console", StringComparison.OrdinalIgnoreCase))
            {
                console++;
            }
        }

        if (unity > 0 && unity >= winForms && unity >= wpf)
        {
            return "Преобладание C# с акцентом на Unity: плагины, примеры архитектуры и игровые прототипы.";
        }

        var java = forensics.Count(f => f.TargetSignatureManifest.Contains("Java", StringComparison.OrdinalIgnoreCase));
        if (java >= 2)
        {
            return "Java-стек с акцентом на алгоритмы и микросервисы.";
        }

        if (winForms > 0 && wpf > 0)
        {
            return "Смешанный desktop-портфель: WinForms и WPF на .NET, разные подходы к UI и данным.";
        }

        if (wpf >= winForms && wpf > 0)
        {
            return "Фокус на WPF/MVVM desktop-приложениях с Services и Converters.";
        }

        if (winForms > 0)
        {
            return "Фокус на WinForms и работе с локальной БД (Access/.accdb) через helper-слой.";
        }

        if (console > 0 && projects.Count == 1)
        {
            return "Консольные .NET-утилиты с файловым хранилищем и абстракцией IStorage.";
        }

        if (console > 0)
        {
            return "Портфель .NET: консольные приложения и desktop-проекты.";
        }

        return "Смешанный .NET-портфель учебных и прикладных pet-проектов.";
    }

    private static string InferLayout(string manifest, AuditContentLocale locale = AuditContentLocale.Ru)
    {
        string L(string value) => AuditFieldLocalizer.LocalizeLayout(value, locale);

        var suggested = ExtractManifestValue(manifest, "Suggested layout:");
        if (!string.IsNullOrWhiteSpace(suggested) &&
            !suggested.Equals("неизвестно", StringComparison.OrdinalIgnoreCase) &&
            !suggested.Equals("unknown", StringComparison.OrdinalIgnoreCase) &&
            !suggested.Equals("Flat Monolith", StringComparison.OrdinalIgnoreCase))
        {
            return AuditFieldLocalizer.LocalizeLayout(suggested.Trim(), locale);
        }

        if (manifest.Contains("Multi-module Maven", StringComparison.OrdinalIgnoreCase) ||
            manifest.Contains("Kubernetes", StringComparison.OrdinalIgnoreCase) ||
            manifest.Contains("Spring Boot", StringComparison.OrdinalIgnoreCase) &&
            manifest.Contains("Services folder", StringComparison.OrdinalIgnoreCase))
        {
            return L("Multi-Module Microservices");
        }

        if (manifest.Contains("Java", StringComparison.OrdinalIgnoreCase) &&
            manifest.Contains("generic layout", StringComparison.OrdinalIgnoreCase))
        {
            return L("Algorithm / Exercise Collection");
        }

        if (manifest.Contains("Unity", StringComparison.OrdinalIgnoreCase))
        {
            if (manifest.Contains("multi-pattern", StringComparison.OrdinalIgnoreCase) ||
                manifest.Contains("MVC/MV/Flat", StringComparison.OrdinalIgnoreCase))
            {
                return L("Unity Multi-Pattern Samples");
            }

            if (manifest.Contains("Plugins/", StringComparison.OrdinalIgnoreCase) ||
                manifest.Contains("package/plugin", StringComparison.OrdinalIgnoreCase))
            {
                return L("Unity Package / Plugin");
            }

            if (manifest.Contains("Editor scripts", StringComparison.OrdinalIgnoreCase))
            {
                return L("Unity Editor + Runtime");
            }

            return L("Unity Project");
        }

        if (manifest.Contains("WPF", StringComparison.OrdinalIgnoreCase) &&
            manifest.Contains("Converters", StringComparison.OrdinalIgnoreCase))
        {
            return L("MVVM (UI/Service Isolation)");
        }

        if (manifest.Contains("WinForms", StringComparison.OrdinalIgnoreCase) ||
            manifest.Contains("Form", StringComparison.OrdinalIgnoreCase))
        {
            return L("Flat Monolith (WinForms)");
        }

        if (manifest.Contains("Services folder", StringComparison.OrdinalIgnoreCase) &&
            manifest.Contains("Interfaces/", StringComparison.OrdinalIgnoreCase))
        {
            return L("Layered (Services + Interfaces)");
        }

        if (manifest.Contains("ASP.NET", StringComparison.OrdinalIgnoreCase))
        {
            return L("Web API / MVC");
        }

        if (ManifestSignalParser.ManifestListsRepositoryLayer(manifest))
        {
            return L("Layered (Repository/Context)");
        }

        if (manifest.Contains("FileStorage", StringComparison.OrdinalIgnoreCase))
        {
            return L("Console Utility");
        }

        return L("Flat Monolith");
    }

    private static List<string> ParseKeyFiles(string manifest)
    {
        var raw = ExtractManifestValue(manifest, "Detected key files:");
        if (string.IsNullOrWhiteSpace(raw) ||
            raw.Contains("insufficient", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(12)
            .ToList();
    }

    internal static string? ExtractManifestValue(string manifest, string prefix)
    {
        var line = manifest
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(item => item.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        return line?[prefix.Length..].Trim();
    }
}
