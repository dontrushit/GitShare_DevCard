namespace GitShare.Api.Services;

/// <summary>
/// Шкала грейдов программиста: ранги, названия и операции сравнения/ограничения.
/// </summary>
internal static class ProgrammerLevelTierScale
{
    public static int TierRank(string code) => code switch
    {
        "principal" => 5,
        "lead" => 4,
        "senior" => 3,
        "middle" => 2,
        "junior" => 1,
        _ => 0
    };

    public static (string Code, string Title) MapCodeToTier(string code) => code switch
    {
        "principal" => ("principal", "Принципал"),
        "lead" => ("lead", "Тимлид"),
        "senior" => ("senior", "Сеньор"),
        "middle" => ("middle", "Мидл"),
        "junior" => ("junior", "Джуниор"),
        _ => ("trainee", "Стажёр")
    };

    public static (string Code, string Title) MapScoreToTier(int score) =>
        score switch
        {
            >= 86 => ("principal", "Принципал"),
            >= 68 => ("lead", "Тимлид"),
            >= 50 => ("senior", "Сеньор"),
            >= 30 => ("middle", "Мидл"),
            >= 18 => ("junior", "Джуниор"),
            _ => ("trainee", "Стажёр")
        };

    public static string MinTierCode(string a, string? b)
    {
        if (b is null || TierRank(a) <= TierRank(b))
        {
            return a;
        }

        return b;
    }

    public static string MinTierRank(string currentCeiling, string candidate) =>
        TierRank(candidate) < TierRank(currentCeiling) ? candidate : currentCeiling;

    public static (string Code, string Title) MaxTier(
        (string Code, string Title) a,
        (string Code, string Title) b) =>
        TierRank(a.Code) >= TierRank(b.Code) ? a : b;

    public static (string Code, string Title) CapTier(
        (string Code, string Title) tier,
        string maxAllowedCode)
    {
        if (TierRank(tier.Code) <= TierRank(maxAllowedCode))
        {
            return tier;
        }

        return MapCodeToTier(maxAllowedCode);
    }
}
