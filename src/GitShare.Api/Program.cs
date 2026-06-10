using GitShare.Api.Data;
using GitShare.Api.Hosting;
using GitShare.Api.Services;
using System.Net;
using System.Net.Sockets;

var builder = WebApplication.CreateBuilder(args);

ProductionStartupValidator.Validate(builder.Configuration, builder.Environment);

builder.Services.AddGitShareDatabase(builder.Configuration);
builder.Services.AddGitShareHosting(builder.Configuration, builder.Environment);

builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 256;
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddHttpClient("GitHub", (sp, client) =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("GitShare-App");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

    var token = sp.GetRequiredService<IConfiguration>()["GitHub:Token"];
    if (!string.IsNullOrWhiteSpace(token))
    {
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    client.Timeout = TimeSpan.FromMinutes(5);
});

builder.Services.AddHttpClient("GitHubModels", (sp, client) =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["AI:BaseUrl"] ?? "https://models.inference.ai.azure.com";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("GitShare-App");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    client.Timeout = TimeSpan.FromSeconds(90);
});

builder.Services.AddScoped<GitHubActivityTelemetryCollector>();
builder.Services.AddScoped<GitHubRawContentFetcher>();
builder.Services.AddScoped<LlmKeyFileContentCollector>();
builder.Services.AddScoped<RepositoryCodeEvidenceService>();
builder.Services.AddScoped<GitHubAnalyticsService>();
builder.Services.AddScoped<ProfileAnalysisCacheService>();

var app = builder.Build();

var dbConnection = app.Configuration.GetConnectionString("DefaultConnection") ?? "";
if (app.Environment.IsDevelopment()
    && dbConnection.Contains("Host=", StringComparison.OrdinalIgnoreCase)
    && !dbConnection.Contains("Password=", StringComparison.OrdinalIgnoreCase))
{
    app.Logger.LogWarning(
        "PostgreSQL: пароль не задан в ConnectionStrings. " +
        "dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" " +
        "\"Host=localhost;Port=5432;Database=gitshare;Username=postgres;Password=<пароль>\" --project src/GitShare.Api");
}

if (app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", !app.Environment.IsProduction()))
{
    app.MigrateGitShareDatabase();
}
else
{
    app.Logger.LogInformation("Database migrations skipped (Database:ApplyMigrationsOnStartup=false).");
}

if (app.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(app.Configuration["GitHub:Token"]))
{
    app.Logger.LogWarning(
        "GitHub:Token не задан. Лимит без токена — 60 запросов/час. " +
        "Выполните: dotnet user-secrets set \"GitHub:Token\" \"<ваш_pat>\" --project src/GitShare.Api");
}

var aiKey = app.Configuration["AI:ApiKey"];
if (string.IsNullOrWhiteSpace(aiKey) || aiKey.Contains("ПОДСТАВЬ", StringComparison.Ordinal))
{
    app.Logger.LogWarning("AI:ApiKey не задан — AI-аудит будет rule-based.");
}
else if (app.Environment.IsDevelopment())
{
    var modelId = app.Configuration["AI:ModelId"] ?? "gpt-4o";
    var baseUrl = app.Configuration["AI:BaseUrl"] ?? "https://models.inference.ai.azure.com";
    app.Logger.LogInformation(
        "GitHub Models enabled: {BaseUrl}/chat/completions, model={Model}",
        baseUrl.TrimEnd('/'),
        modelId);
}

if (!app.Environment.IsDevelopment())
{
    app.UseForwardedHeaders();
    app.UseExceptionHandler();
}

app.UseRateLimiter();
app.UseCors("Frontend");
app.UseHttpsRedirection();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");

const int defaultApiPort = 5188;
if (app.Environment.IsDevelopment() && IsLocalPortInUse(defaultApiPort))
{
    app.Logger.LogError(
        "Порт {Port} уже занят — закройте другой экземпляр GitShare.Api.",
        defaultApiPort);
    Environment.Exit(1);
}

app.Run();

static bool IsLocalPortInUse(int port)
{
    try
    {
        using var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        listener.Stop();
        return false;
    }
    catch (SocketException)
    {
        return true;
    }
}
