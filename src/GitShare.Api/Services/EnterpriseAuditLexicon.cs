namespace GitShare.Api.Services;

/// <summary>
/// Термины enterprise-аудита, запрещённые для DocOps / DevOps / Utility / QA.
/// </summary>
internal static class EnterpriseAuditLexicon
{
    private static readonly string[] ForbiddenSubstrings =
    [
        "di-контейнер",
        "di-фреймворк",
        "di ",
        "repository",
        "context.cs",
        "создаются через new",
        "создание через new",
        "границы сло",
        "слои ооп",
        "appsettings",
        "dbhelper",
        "olebd",
        "winforms + helpers",
        "flat monolith",
        "mvvm без",
        "unit-тесты и параллельный",
        "интерфейсы для инфраструктуры",
        "подменяемость реализаций",
        "база данных",
        "субд",
        ".accdb",
        "addsingleton",
        "addscoped",
        "слой данных",
        "data layer",
        "выделенный слой",
        "выделенного слоя",
        "управления зависимостями",
        "управление зависимостями",
        "monobehaviour для",
        "dependency injection",
        "dependency management",
        "жёсткие зависимости между"
    ];

    public static bool ContainsEnterpriseOnlyTerms(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var lower = text.ToLowerInvariant();
        return ForbiddenSubstrings.Any(term => lower.Contains(term, StringComparison.Ordinal));
    }

    public static bool IsProductionClass(string projectClass) =>
        string.Equals(projectClass, ProjectClassClassifier.ProductionApp, StringComparison.Ordinal);
}
