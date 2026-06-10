namespace GitShare.Api.Services;

/// <summary>Локализованные строки rule-based аудита (когда LLM недоступен или отбрасывает текст).</summary>
internal static class AuditContentCatalog
{
    public static string InsufficientDataFocus(AuditContentLocale locale) =>
        locale == AuditContentLocale.En
            ? "Insufficient repository data to assess engineering focus."
            : "Недостаточно данных по репозиториям для оценки инженерного профиля.";

    public static string FileSignaturesUnavailable(AuditContentLocale locale) =>
        locale == AuditContentLocale.En
            ? "File signatures unavailable."
            : "Сигнатуры файлов недоступны.";

    public static string DefaultInterviewQuestion(AuditContentLocale locale) =>
        locale == AuditContentLocale.En
            ? "Which three repositories best reflect your production experience?"
            : "Какие три репозитория лучше всего отражают ваш production-опыт?";

    public static string NoPublicOssFootprint(AuditContentLocale locale) =>
        locale == AuditContentLocale.En
            ? "No public OSS footprint: 0★, no external PRs observed."
            : "Публичный OSS-след отсутствует: 0★, внешних PR не видно.";

    public static string UndefinedFramework(AuditContentLocale locale) =>
        locale == AuditContentLocale.En ? "undefined (from signatures)" : "не определён (по сигнатурам)";

    public static string UnknownLayout(AuditContentLocale locale) =>
        locale == AuditContentLocale.En ? "unknown" : "неизвестно";

    public static string ProductionTechnicalDebtFallback(AuditContentLocale locale) =>
        locale == AuditContentLocale.En
            ? "No critical architectural gaps identified from file signatures."
            : "Критичных архитектурных провалов по сигнатурам не выявлено.";

    public static string DefaultTechnicalDebt(string projectClass, string repoName, string? manifest, AuditContentLocale locale)
    {
        if (locale == AuditContentLocale.Ru)
        {
            return ProjectClassClassifier.DefaultTechnicalDebtForClass(projectClass, repoName, manifest);
        }

        if (ProjectClassClassifier.IsUnityArchitectureExamples(repoName))
        {
            return "Educational showcase of architecture styles (Flat / MVC / MV). Pattern comparison, not production runtime.";
        }

        return projectClass switch
        {
            ProjectClassClassifier.DocOpsKnowledgeBase =>
                "Informational repository / talk materials. Architectural audit not applicable.",
            ProjectClassClassifier.QaTesting =>
                "QA / test automation: enterprise layers (Repository/Services/DI) absent; structure driven by test layout and configs.",
            ProjectClassClassifier.UtilityAutomation =>
                "Utility / automation: data layers and DI absent — typical for CLI and scripts. Structure relies on modules and configuration.",
            ProjectClassClassifier.ProductionApp =>
                ProductionTechnicalDebtFallback(locale),
            _ => string.Empty
        };
    }
}
