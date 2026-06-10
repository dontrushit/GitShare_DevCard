namespace GitShare.Api.Services;

/// <summary>
/// Cons — только реальные риски структуры/кода. Без оправданий ограничений анализа.
/// </summary>
internal static class ConsBulletSanitizer
{
    private static readonly string[] ForbiddenConsPhrases =
    [
        "не читал",
        "не читалось",
        "не анализировал",
        "не анализировалось",
        "не удалось проанализировать",
        "анализ только",
        "только эвристика",
        "только по дереву",
        "выводы только",
        "содержимое файлов",
        "содержимое yaml",
        "не применим",
        "не имеет смысла",
        "не ожидаются",
        "не применимы",
        "прикладной код отсутствует",
        "оценивайте модульность",
        "автоматический аудит",
        "качество проверяется сценариями",
        "enterprise-сло",
        "oop-сло",
        "дерево не показывает",
        "требуют проверки code review",
        "по сигнатурам не видно",
        "границы слоёв требуют",
        "вероятно, создаются через new",
        "типичная unity-связность: сцены",
        "unityengine api",
        "автоматическая регрессия не видна",
        "прочитаны с github",
        "не к догадкам llm",
        "olebd "
    ];

    public static bool IsForbiddenConsBullet(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var lower = text.Trim().ToLowerInvariant();
        return ForbiddenConsPhrases.Any(phrase => lower.Contains(phrase, StringComparison.Ordinal));
    }

    public static List<string> Filter(IEnumerable<string>? items)
    {
        return (items ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Where(s => !IsForbiddenConsBullet(s))
            .Where(s => !AuditTextSanitizer.ContainsForbiddenLanguage(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();
    }

    /// <summary>
    /// Финальные Cons для UI: non-Production без явных рисков → пустой массив.
    /// </summary>
    public static List<string> Finalize(IEnumerable<string>? items, string projectClass)
    {
        var filtered = Filter(items);

        if (!EnterpriseAuditLexicon.IsProductionClass(projectClass))
        {
            return [];
        }

        return filtered;
    }
}
