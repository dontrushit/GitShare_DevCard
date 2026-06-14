namespace GitShare.Api.Models;

/// <summary>Уровень инженерии в рамках одного репозитория (не портфеля).</summary>
public class RepositoryLevelInfo
{
    /// <summary>trainee | junior | middle | senior</summary>
    public string Code { get; set; } = "junior";

    public string Title { get; set; } = "Джуниор";

    /// <summary>0–100 по рубрике архитектуры и зрелости репозитория.</summary>
    public int Score { get; set; }

    /// <summary>Коротко: на чём основан уровень (слои, DI, тесты, класс проекта).</summary>
    public string Rationale { get; set; } = string.Empty;
}
