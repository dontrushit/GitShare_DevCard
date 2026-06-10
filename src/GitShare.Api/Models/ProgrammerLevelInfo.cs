namespace GitShare.Api.Models;

public class ProgrammerLevelInfo
{
    /// <summary>Код уровня: trainee, junior, middle, senior, lead, principal.</summary>
    public string Code { get; set; } = "junior";

    /// <summary>Короткая подпись для UI (рус.)</summary>
    public string Title { get; set; } = "Джуниор";

    /// <summary>Итоговая оценка 0–100 после коэффициента уверенности и шлагбаумов.</summary>
    public int Score { get; set; }

    /// <summary>Сырой балл до коэффициента уверенности (7 групп).</summary>
    public int RawScore { get; set; }

    /// <summary>Коэффициент уверенности сигнала портфеля (0.5–1.0).</summary>
    public double SignalConfidence { get; set; } = 1.0;

    /// <summary>Низкая уверенность: мало репо, звёзд или один язык в стеке.</summary>
    public bool IsLowConfidence { get; set; }

    /// <summary>Одна строка — на чём основан уровень.</summary>
    public string Rationale { get; set; } = string.Empty;

    /// <summary>2–3 предложения: аргументация уровня (модель или эвристический fallback).</summary>
    public string AssessmentSummary { get; set; } = string.Empty;

    /// <summary>Короткая оговорка: уровень по GitHub-портфелю, не по должности.</summary>
    public string Disclaimer { get; set; } =
        "По открытому GitHub-портфелю (топ-репо и аудит), не по должности в компании.";
}
