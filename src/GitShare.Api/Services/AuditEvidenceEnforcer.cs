using System.Text.RegularExpressions;
using GitShare.Api.Models;

namespace GitShare.Api.Services;

/// <summary>
/// Сливает ответ модели с фактами из сигнатур: структура и KeyFiles — только из evidence,
/// формулировки долга/капканов — из ответа модели, если прошли санитайзер.
/// </summary>
internal static partial class AuditEvidenceEnforcer
{
    public static StructuredAuditResponse Apply(
        StructuredAuditResponse? llmParsed,
        IReadOnlyList<RepositoryForensics> forensics,
        GitHubActivityTelemetry telemetry,
        AuditContentLocale locale = AuditContentLocale.Ru,
        int portfolioTotalStars = -1)
    {
        var evidence = StructuredAuditBuilder.BuildFromForensics(forensics, locale, portfolioTotalStars);

        if (llmParsed is null)
        {
            GitTelemetryAnalyzer.ApplyTelemetryFields(evidence, telemetry, null, locale);
            return evidence;
        }

        var readmeByRepo = forensics
            .GroupBy(f => f.RepoName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Readme, StringComparer.OrdinalIgnoreCase);

        var llmByRepo = (llmParsed.Projects ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p.RepoName))
            .GroupBy(p => p.RepoName.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var forensicsByRepo = forensics
            .GroupBy(f => f.RepoName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var mergedProjects = evidence.Projects
            .Select(ep => MergeProject(
                ep,
                llmByRepo,
                forensicsByRepo.GetValueOrDefault(ep.RepoName),
                readmeByRepo.GetValueOrDefault(ep.RepoName),
                locale,
                portfolioTotalStars))
            .ToList();

        var focus = PromptInjectionGuard.SanitizeNarrative(
            AuditTextSanitizer.SanitizeSummary(
                llmParsed.CoreEngineeringFocus,
                evidence.CoreEngineeringFocus,
                locale),
            locale);

        var result = new StructuredAuditResponse
        {
            Projects = mergedProjects,
            CoreEngineeringFocus = focus
        };

        GitTelemetryAnalyzer.ApplyTelemetryFields(result, telemetry, llmParsed, locale);
        SanitizeLlmNarratives(result, locale);
        ReconcileRepositoryLevel(result, forensicsByRepo, locale);
        ReconcileArchitectureSeverity(result, forensicsByRepo);
        return result;
    }

    private static void ReconcileRepositoryLevel(
        StructuredAuditResponse response,
        IReadOnlyDictionary<string, RepositoryForensics> forensicsByRepo,
        AuditContentLocale locale)
    {
        foreach (var project in response.Projects)
        {
            if (!forensicsByRepo.TryGetValue(project.RepoName, out var repo))
            {
                continue;
            }

            var level = RepositoryLevelEvaluator.Evaluate(repo, project.ProjectClass, locale);
            project.RepositoryLevel = level;

            var assessment = RepositoryArchitectureAssessmentBuilder.Build(
                repo,
                project.ProjectClass,
                project.Framework,
                project.LayoutType,
                project.DebtSeverity,
                project.Pros,
                project.Cons,
                level,
                locale);

            project.ArchitectureSummary = assessment.ArchitectureSummary;
        }
    }

    private static void ReconcileArchitectureSeverity(
        StructuredAuditResponse response,
        IReadOnlyDictionary<string, RepositoryForensics> forensicsByRepo)
    {
        foreach (var project in response.Projects)
        {
            forensicsByRepo.TryGetValue(project.RepoName, out var repo);
            project.DebtSeverity = ArchitectureSeverityResolver.Resolve(
                repo,
                project.ProjectClass,
                project.DebtSeverity,
                project.Cons);
        }
    }

    private static void SanitizeLlmNarratives(StructuredAuditResponse response, AuditContentLocale locale)
    {
        response.CoreEngineeringFocus = PromptInjectionGuard.SanitizeNarrative(
            response.CoreEngineeringFocus,
            locale);

        response.ExperienceProfile = PromptInjectionGuard.SanitizeNarrative(
            response.ExperienceProfile,
            locale);

        response.OpenSourceImpact = PromptInjectionGuard.SanitizeNarrative(
            response.OpenSourceImpact,
            locale);

        foreach (var project in response.Projects)
        {
            project.TechnicalDebt = PromptInjectionGuard.SanitizeNarrative(project.TechnicalDebt, locale);
            project.InterviewTrapQuestion = PromptInjectionGuard.SanitizeNarrative(
                project.InterviewTrapQuestion,
                locale);
            project.ArchitectureSummary = PromptInjectionGuard.SanitizeNarrative(
                project.ArchitectureSummary,
                locale);
        }
    }

    private static ProjectAuditDetail MergeProject(
        ProjectAuditDetail evidenceProject,
        IReadOnlyDictionary<string, ProjectAuditDetail> llmByRepo,
        RepositoryForensics? repoForensics,
        string? readme,
        AuditContentLocale locale,
        int portfolioTotalStars)
    {
        if (!llmByRepo.TryGetValue(evidenceProject.RepoName, out var llmProject))
        {
            return ApplyReadmeStructureGate(evidenceProject, readme, portfolioTotalStars);
        }

        var debt = PickLlmOrEvidence(
            llmProject.TechnicalDebt,
            evidenceProject.TechnicalDebt,
            text => AuditNarrativeValidator.IsValidNarrative(text, locale));

        var layout = PickLlmOrEvidence(
            llmProject.LayoutType,
            evidenceProject.LayoutType,
            static text => !string.IsNullOrWhiteSpace(text));

        var projectClass = ResolveProjectClass(
            llmProject.ProjectClass,
            evidenceProject.ProjectClass,
            evidenceProject.RepoName,
            evidenceProject.Framework);

        var trap = ResolveInterviewQuestion(
            llmProject.InterviewTrapQuestion,
            evidenceProject.InterviewTrapQuestion,
            projectClass,
            evidenceProject.Framework,
            locale);

        var debtSeverity = ResolveDebtSeverity(
            llmProject.DebtSeverity,
            evidenceProject,
            projectClass);

        projectClass = ReadmeStructureVerifier.AdjustProjectClass(
            projectClass,
            evidenceProject.RepoName,
            readme,
            evidenceProject.Framework,
            debtSeverity,
            portfolioTotalStars);

        var analysis = ReadmeStructureVerifier.Analyze(
            evidenceProject.RepoName,
            readme,
            evidenceProject.Framework,
            debtSeverity,
            portfolioTotalStars);

        var architectureSummary = ArchitectureSummarySanitizer.PickSummary(
            llmProject.ArchitectureSummary,
            evidenceProject.ArchitectureSummary,
            repoForensics?.Facts,
            locale);

        return new ProjectAuditDetail
        {
            RepoName = evidenceProject.RepoName,
            ProjectClass = projectClass,
            Framework = evidenceProject.Framework,
            KeyFiles = evidenceProject.KeyFiles,
            LayoutType = layout,
            TechnicalDebt = PromptInjectionGuard.SanitizeNarrative(
                ReadmeStructureVerifier.AppendMismatchNote(
                    ResolveTechnicalDebt(
                        debt,
                        evidenceProject.TechnicalDebt,
                        projectClass,
                        evidenceProject.RepoName,
                        locale),
                    analysis),
                locale),
            DebtSeverity = debtSeverity,
            InterviewTrapQuestion = PromptInjectionGuard.SanitizeNarrative(trap, locale),
            RepositoryLevel = evidenceProject.RepositoryLevel,
            ArchitectureSummary = PromptInjectionGuard.SanitizeNarrative(architectureSummary, locale),
            Pros = evidenceProject.Pros,
            Cons = MergeLlmRisks(evidenceProject.Cons, llmProject.KeyRisks, llmProject.Cons, locale)
        };
    }

    private static ProjectAuditDetail ApplyReadmeStructureGate(
        ProjectAuditDetail project,
        string? readme,
        int portfolioTotalStars)
    {
        var adjustedClass = ReadmeStructureVerifier.AdjustProjectClass(
            project.ProjectClass,
            project.RepoName,
            readme,
            project.Framework,
            project.DebtSeverity,
            portfolioTotalStars);

        if (adjustedClass == project.ProjectClass)
        {
            return project;
        }

        var analysis = ReadmeStructureVerifier.Analyze(
            project.RepoName,
            readme,
            project.Framework,
            project.DebtSeverity,
            portfolioTotalStars);

        return new ProjectAuditDetail
        {
            RepoName = project.RepoName,
            ProjectClass = adjustedClass,
            Framework = project.Framework,
            KeyFiles = project.KeyFiles,
            LayoutType = project.LayoutType,
            TechnicalDebt = ReadmeStructureVerifier.AppendMismatchNote(project.TechnicalDebt, analysis),
            DebtSeverity = project.DebtSeverity,
            InterviewTrapQuestion = project.InterviewTrapQuestion,
            RepositoryLevel = project.RepositoryLevel,
            ArchitectureSummary = project.ArchitectureSummary,
            Pros = project.Pros,
            Cons = project.Cons
        };
    }

    private static List<string> MergeLlmRisks(
        IReadOnlyList<string> evidenceRisks,
        IReadOnlyList<string> llmKeyRisks,
        IReadOnlyList<string> llmCons,
        AuditContentLocale locale)
    {
        var merged = evidenceRisks.ToList();
        var extras = (llmKeyRisks ?? [])
            .Concat(llmCons ?? [])
            .Where(r => AuditNarrativeValidator.IsValidNarrative(r, locale))
            .Select(r => r.Trim());

        foreach (var risk in extras)
        {
            if (merged.Any(existing => existing.Contains(risk, StringComparison.OrdinalIgnoreCase) ||
                                       risk.Contains(existing, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (merged.Count >= 3)
            {
                break;
            }

            merged.Add(risk);
        }

        return merged;
    }

    private static string ResolveProjectClass(
        string? llmClass,
        string evidenceClass,
        string repoName,
        string framework)
    {
        var evidence = string.IsNullOrWhiteSpace(evidenceClass)
            ? ProjectClassClassifier.UtilityAutomation
            : evidenceClass;

        if (evidence is ProjectClassClassifier.DocOpsKnowledgeBase or ProjectClassClassifier.QaTesting)
        {
            return evidence;
        }

        if (evidence == ProjectClassClassifier.UtilityAutomation &&
            !EnterpriseAuditLexicon.IsProductionClass(
                ProjectClassClassifier.NormalizeProjectClass(llmClass) ?? string.Empty))
        {
            return evidence;
        }

        if (UnityRepositoryHeuristics.IsUnityToolkitRepository(repoName, framework) ||
            ProjectClassClassifier.IsSmallPetConsoleGame(repoName, framework) ||
            ProjectClassClassifier.IsUnityArchitectureExamples(repoName) ||
            ProjectClassClassifier.IsPetDesktopApplication(repoName, framework))
        {
            return ProjectClassClassifier.UtilityAutomation;
        }

        var llm = ProjectClassClassifier.NormalizeProjectClass(llmClass);
        if (string.IsNullOrWhiteSpace(llm))
        {
            return evidence;
        }

        if (evidence == ProjectClassClassifier.UtilityAutomation &&
            EnterpriseAuditLexicon.IsProductionClass(llm) &&
            !framework.Contains("Next.js", StringComparison.OrdinalIgnoreCase))
        {
            return evidence;
        }

        if (evidence == ProjectClassClassifier.UtilityAutomation &&
            EnterpriseAuditLexicon.IsProductionClass(llm) &&
            framework.Contains("Next.js", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectClassClassifier.ProductionApp;
        }

        if (EnterpriseAuditLexicon.IsProductionClass(evidence) &&
            !EnterpriseAuditLexicon.IsProductionClass(llm) &&
            !UnityRepositoryHeuristics.IsUnityToolkitRepository(repoName, framework))
        {
            return evidence;
        }

        return llm;
    }

    private static string ResolveTechnicalDebt(
        string mergedDebt,
        string evidenceDebt,
        string projectClass,
        string repoName,
        AuditContentLocale locale)
    {
        string DefaultDebt() =>
            AuditContentCatalog.DefaultTechnicalDebt(projectClass, repoName, null, locale);

        if (ProjectClassClassifier.IsUnityArchitectureExamples(repoName))
        {
            return AuditContentCatalog.DefaultTechnicalDebt(
                ProjectClassClassifier.UtilityAutomation,
                repoName,
                null,
                locale);
        }

        if (projectClass == ProjectClassClassifier.DocOpsKnowledgeBase)
        {
            return DefaultDebt();
        }

        if (projectClass is ProjectClassClassifier.UtilityAutomation or ProjectClassClassifier.QaTesting)
        {
            if (IsGenericUtilityDebtTemplate(mergedDebt) && !IsGenericUtilityDebtTemplate(evidenceDebt))
            {
                return evidenceDebt;
            }

            if (!AuditNarrativeValidator.IsValidNarrative(mergedDebt, locale) ||
                AuditNarrativeValidator.IsEnglishBoilerplate(mergedDebt) ||
                AuditNarrativeValidator.UsesInstructionalTone(mergedDebt) ||
                EnterpriseAuditLexicon.ContainsEnterpriseOnlyTerms(mergedDebt))
            {
                return AuditNarrativeValidator.IsValidNarrative(evidenceDebt, locale)
                    ? evidenceDebt
                    : DefaultDebt();
            }
        }

        if (!AuditNarrativeValidator.IsValidNarrative(mergedDebt, locale) ||
            AuditNarrativeValidator.UsesInstructionalTone(mergedDebt))
        {
            if (AuditNarrativeValidator.IsValidNarrative(evidenceDebt, locale))
            {
                return evidenceDebt;
            }

            var fallback = DefaultDebt();
            if (!string.IsNullOrWhiteSpace(fallback))
            {
                return fallback;
            }

            return AuditContentCatalog.ProductionTechnicalDebtFallback(locale);
        }

        return mergedDebt;
    }

    private static bool IsGenericUtilityDebtTemplate(string text) =>
        text.TrimStart().StartsWith("Utility / automation:", StringComparison.OrdinalIgnoreCase);

    private static string ResolveInterviewQuestion(
        string? llmTrap,
        string evidenceTrap,
        string projectClass,
        string framework,
        AuditContentLocale locale)
    {
        var llm = llmTrap?.Trim();
        if (string.IsNullOrWhiteSpace(llm) ||
            !AuditNarrativeValidator.IsValidNarrative(llm, locale))
        {
            return evidenceTrap;
        }

        if (IsNativeCFramework(framework) &&
            (llm.Contains("Python", StringComparison.OrdinalIgnoreCase) ||
             llm.Contains("MVVM", StringComparison.OrdinalIgnoreCase) ||
             llm.Contains("ViewModel", StringComparison.OrdinalIgnoreCase) ||
             llm.Contains("DI-контейнер", StringComparison.OrdinalIgnoreCase) ||
             llm.Contains("Repository", StringComparison.OrdinalIgnoreCase)))
        {
            return evidenceTrap;
        }

        if (framework.Contains("React", StringComparison.OrdinalIgnoreCase) &&
            !framework.Contains("Playwright", StringComparison.OrdinalIgnoreCase) &&
            llm.Contains("Playwright", StringComparison.OrdinalIgnoreCase))
        {
            return evidenceTrap;
        }

        if (projectClass is ProjectClassClassifier.UtilityAutomation or ProjectClassClassifier.QaTesting &&
            AuditNarrativeValidator.IsGenericUtilityInterviewQuestion(llm))
        {
            return evidenceTrap;
        }

        return llm;
    }

    private static bool IsNativeCFramework(string framework) =>
        framework.Contains("C (native)", StringComparison.OrdinalIgnoreCase) ||
        framework.Contains("Linux kernel", StringComparison.OrdinalIgnoreCase) ||
        framework.Equals("C", StringComparison.OrdinalIgnoreCase);

    private static string PickLlmOrEvidence(
        string? llmValue,
        string evidenceValue,
        Func<string, bool> isValid)
    {
        var trimmed = llmValue?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmed) && isValid(trimmed))
        {
            return trimmed;
        }

        return evidenceValue;
    }

    private static string ResolveDebtSeverity(
        string? llmSeverity,
        ProjectAuditDetail evidenceProject,
        string projectClass)
    {
        var llm = AuditSeverityNormalizer.Normalize(llmSeverity);
        var evidence = AuditSeverityNormalizer.Normalize(evidenceProject.DebtSeverity);

        if (projectClass == ProjectClassClassifier.DocOpsKnowledgeBase)
        {
            return "NONE";
        }

        if (ProjectClassClassifier.IsUnityArchitectureExamples(evidenceProject.RepoName))
        {
            return "CLEAN";
        }

        if (projectClass is ProjectClassClassifier.UtilityAutomation or ProjectClassClassifier.QaTesting)
        {
            var structural = ArchitectureSeverityResolver.FilterStructuralRisks(evidenceProject.Cons);
            var merged = ArchitectureSeverityResolver.Merge(
                evidence,
                llm is "NONE" ? "CLEAN" : llm);

            if (structural.Count >= 2)
            {
                return ArchitectureSeverityResolver.Merge(merged, "Warning");
            }

            if (structural.Count == 1)
            {
                return ArchitectureSeverityResolver.Merge(merged, "Minor");
            }

            return merged;
        }

        if (IsStackAuditNotApplicable(evidenceProject.Framework) &&
            string.Equals(llm, "Critical", StringComparison.OrdinalIgnoreCase))
        {
            return evidence;
        }

        if (string.Equals(llm, "Critical", StringComparison.OrdinalIgnoreCase) &&
            evidence is "Warning" or "Minor" or "CLEAN")
        {
            return evidence;
        }

        if (string.Equals(llm, "Warning", StringComparison.OrdinalIgnoreCase) &&
            evidence is "Minor" or "CLEAN")
        {
            return evidence;
        }

        if (EnterpriseAuditLexicon.IsProductionClass(projectClass) &&
            string.Equals(llm, "Warning", StringComparison.OrdinalIgnoreCase) &&
            HasCompositionRootKeyFile(evidenceProject.KeyFiles))
        {
            return "Minor";
        }

        return llm;
    }

    private static bool HasCompositionRootKeyFile(IReadOnlyList<string> keyFiles) =>
        keyFiles.Any(f =>
            f.Contains("CompositeRoot", StringComparison.OrdinalIgnoreCase) ||
            f.Contains("CompositionRoot", StringComparison.OrdinalIgnoreCase));

    private static bool IsStackAuditNotApplicable(string framework) =>
        string.IsNullOrWhiteSpace(framework) ||
        framework.Contains("не определён", StringComparison.OrdinalIgnoreCase) ||
        framework.Contains("undefined", StringComparison.OrdinalIgnoreCase);
}

internal static partial class AuditTextSanitizer
{
    private static readonly string[] ForbiddenSubstrings =
    [
        "неправильн",
        "incorrect use",
        "некорректн",
        "плохое использование",
        "misuse",
        "антипаттерн linq",
        "linq для работы",
        "olebd для",
        "npgsql для",
        "entity framework",
        "проект представляет собой",
        "данный репозиторий",
        "реализация приложения",
        "включает в себя",
        "в данном проекте",
        "наблюдается отсутствие",
        "как вы оцениваете сложность"
    ];

    public static string SanitizeSummary(
        string? llmSummary,
        string evidenceSummary,
        AuditContentLocale locale)
    {
        if (string.IsNullOrWhiteSpace(llmSummary) ||
            ContainsForbiddenLanguage(llmSummary) ||
            !AuditNarrativeValidator.IsValidNarrative(llmSummary, locale))
        {
            return evidenceSummary;
        }

        return llmSummary.Trim();
    }

    public static bool ContainsForbiddenLanguage(string text)
    {
        foreach (var forbidden in ForbiddenSubstrings)
        {
            if (text.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return TechnologyClaimRegex().IsMatch(text);
    }

    [GeneratedRegex(
        @"\b(OleDb|Npgsql|LINQ|Entity\s*Framework|ADO\.NET)\b.*\b(неправильн|некорректн|ошибочн|плох|wrong|incorrect)",
        RegexOptions.IgnoreCase)]
    private static partial Regex TechnologyClaimRegex();
}
