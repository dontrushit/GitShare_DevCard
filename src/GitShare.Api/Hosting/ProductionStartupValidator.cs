namespace GitShare.Api.Hosting;

internal static class ProductionStartupValidator
{
    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        var errors = new List<string>();

        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (origins is not { Length: > 0 })
        {
            errors.Add("Cors:AllowedOrigins must contain at least one origin in Production.");
        }

        var connection = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connection))
        {
            errors.Add("ConnectionStrings:DefaultConnection is required in Production.");
        }
        else if (!LooksLikePostgreSql(connection))
        {
            errors.Add("Production requires a PostgreSQL connection string (not SQLite).");
        }

        var aiKey = configuration["AI:ApiKey"];
        if (!string.IsNullOrWhiteSpace(aiKey) && ContainsPlaceholder(aiKey))
        {
            errors.Add("AI:ApiKey contains a placeholder — set a real token via environment variables.");
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Production configuration is invalid:\n- " + string.Join("\n- ", errors));
        }
    }

    private static bool LooksLikePostgreSql(string connectionString) =>
        connectionString.StartsWith("postgres", StringComparison.OrdinalIgnoreCase) ||
        connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsPlaceholder(string value) =>
        value.Contains("ПОДСТАВЬ", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("YOUR_", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("CHANGEME", StringComparison.OrdinalIgnoreCase);
}
