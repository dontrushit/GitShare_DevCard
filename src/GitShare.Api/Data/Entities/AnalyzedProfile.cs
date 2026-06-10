namespace GitShare.Api.Data.Entities;

public class AnalyzedProfile
{
    public int Id { get; set; }

    /// <summary>GitHub username в нижнем регистре (уникальный ключ кэша).</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Сериализованный <see cref="Models.DevCardProfile"/> для фронтенда.</summary>
    public string FullDataJson { get; set; } = string.Empty;

    public DateTime AnalyzedAt { get; set; }
}
