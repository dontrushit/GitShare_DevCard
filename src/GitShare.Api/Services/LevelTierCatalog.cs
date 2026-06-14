namespace GitShare.Api.Services;

internal static class LevelTierCatalog
{
    internal sealed record Tier(string Code, string TitleRu, string TitleEn);

    private static readonly Tier[] RepositoryTiers =
    [
        new("trainee", "Стажёр", "Trainee"),
        new("junior", "Джуниор", "Junior"),
        new("middle", "Мидл", "Middle"),
        new("senior", "Сеньор", "Senior")
    ];

    public static Tier MapRepositoryScore(int score) =>
        score switch
        {
            >= 78 => RepositoryTiers[3],
            >= 58 => RepositoryTiers[2],
            >= 38 => RepositoryTiers[1],
            _ => RepositoryTiers[0]
        };

    public static string TitleFor(string code, AuditContentLocale locale)
    {
        var tier = RepositoryTiers.FirstOrDefault(t =>
            t.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        if (tier is null)
        {
            return locale == AuditContentLocale.En ? "Junior" : "Джуниор";
        }

        return locale == AuditContentLocale.En ? tier.TitleEn : tier.TitleRu;
    }
}
