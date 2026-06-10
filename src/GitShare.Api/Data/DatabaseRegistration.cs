using Microsoft.EntityFrameworkCore;

namespace GitShare.Api.Data;

internal static class DatabaseRegistration
{
    public static void AddGitShareDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=gitshare.db";

        services.AddDbContext<AppDbContext>(options =>
        {
            if (IsPostgreSql(connectionString))
            {
                options.UseNpgsql(connectionString);
            }
            else
            {
                options.UseSqlite(connectionString);
            }
        });
    }

    public static void MigrateGitShareDatabase(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
    }

    private static bool IsPostgreSql(string connectionString)
    {
        if (connectionString.StartsWith("postgres", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) &&
               connectionString.Contains("Database=", StringComparison.OrdinalIgnoreCase);
    }
}
