using System.Text.Json;
using System.Text.Json.Serialization;
using GitShare.Api.Data;
using GitShare.Api.Data.Entities;
using GitShare.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GitShare.Api.Services;

/// <summary>
/// Двухуровневый кэш: IMemoryCache (мгновенно при F5) + БД (TTL 24 ч).
/// </summary>
public sealed class ProfileAnalysisCacheService(
    AppDbContext dbContext,
    GitHubAnalyticsService analyticsService,
    IMemoryCache memoryCache,
    IConfiguration configuration,
    ILogger<ProfileAnalysisCacheService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public async Task<DevCardProfile> GetOrAnalyzeAsync(
        string username,
        AuditContentLocale contentLocale = AuditContentLocale.Ru,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var targetUser = username.Trim().ToLowerInvariant();
        var localeCode = AuditContentLocaleParser.ToCode(contentLocale);
        var ttl = GetCacheTtl();
        var memoryKey = ProfileCacheKeys.ForUsername(targetUser, contentLocale);

        if (!forceRefresh)
        {
            if (memoryCache.TryGetValue(memoryKey, out DevCardProfile? memoryProfile)
                && memoryProfile is not null
                && AuditContentLocaleGuard.ProfileMatchesRequestedLocale(memoryProfile, localeCode))
            {
                logger.LogInformation("Profile cache MEMORY HIT for {Username}", targetUser);
                memoryProfile.ServedFromCache = true;
                return memoryProfile;
            }

            if (memoryProfile is not null)
            {
                memoryCache.Remove(memoryKey);
            }

            var dbEntry = await TryGetValidDbEntryAsync(targetUser, localeCode, ttl, cancellationToken);
            if (dbEntry is not null)
            {
                logger.LogInformation(
                    "Profile cache DB HIT for {Username} (analyzed {AnalyzedAt:u})",
                    targetUser,
                    dbEntry.AnalyzedAt);

                var profile = DeserializeProfile(dbEntry.FullDataJson, targetUser);
                if (!AuditContentLocaleGuard.ProfileMatchesRequestedLocale(profile, localeCode))
                {
                    logger.LogInformation(
                        "Profile cache DB rejected for {Username} (cached locale {Cached}, requested {Requested})",
                        targetUser,
                        profile.ContentLocale,
                        localeCode);
                }
                else
                {
                    profile.AnalyzedAtUtc = dbEntry.AnalyzedAt;
                    profile.ServedFromCache = true;
                    SetMemoryCache(memoryKey, profile, ttl);
                    return profile;
                }
            }
        }
        else
        {
            logger.LogInformation("Profile cache BYPASS (forceRefresh) for {Username}", targetUser);
            memoryCache.Remove(memoryKey);
        }

        logger.LogInformation("Profile cache MISS — full pipeline for {Username}", targetUser);

        var fresh = await analyticsService.BuildProfileAsync(targetUser, contentLocale, cancellationToken);
        fresh.Username = fresh.Username.Length > 0 ? fresh.Username : targetUser;
        fresh.ContentLocale = localeCode;
        fresh.AnalyzedAtUtc = DateTime.UtcNow;
        fresh.ServedFromCache = false;

        await UpsertDbCacheAsync(targetUser, fresh, cancellationToken);
        SetMemoryCache(memoryKey, fresh, ttl);

        return fresh;
    }

    private async Task<AnalyzedProfile?> TryGetValidDbEntryAsync(
        string targetUser,
        string localeCode,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        try
        {
            var dbKey = ProfileDbKeys.ForUser(targetUser, localeCode);
            var entry = await dbContext.AnalyzedProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Username == dbKey, cancellationToken);

            if (entry is null && localeCode == "ru")
            {
                entry = await dbContext.AnalyzedProfiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Username == targetUser, cancellationToken);
            }

            if (entry is null)
            {
                return null;
            }

            return DateTime.UtcNow - entry.AnalyzedAt < ttl ? entry : null;
        }
        catch (Exception ex) when (ex is InvalidCastException or InvalidOperationException)
        {
            logger.LogWarning(
                ex,
                "Profile cache DB read failed for {Username} — will re-analyze",
                targetUser);
            return null;
        }
    }

    private async Task UpsertDbCacheAsync(
        string targetUser,
        DevCardProfile profile,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(profile, JsonOptions);
        var now = DateTime.UtcNow;

        var storedLocale = string.IsNullOrWhiteSpace(profile.ContentLocale)
            ? "ru"
            : profile.ContentLocale.Trim().ToLowerInvariant();
        var dbKey = ProfileDbKeys.ForUser(targetUser, storedLocale);
        var existing = await dbContext.AnalyzedProfiles
            .FirstOrDefaultAsync(p => p.Username == dbKey, cancellationToken);

        if (existing is null)
        {
            dbContext.AnalyzedProfiles.Add(new AnalyzedProfile
            {
                Username = dbKey,
                FullDataJson = json,
                AnalyzedAt = now
            });
        }
        else
        {
            existing.FullDataJson = json;
            existing.AnalyzedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Profile cache DB STORED for {Username} at {AnalyzedAt:u}", targetUser, now);
    }

    private void SetMemoryCache(string memoryKey, DevCardProfile profile, TimeSpan ttl)
    {
        memoryCache.Set(
            memoryKey,
            profile,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl,
                Size = 1
            });
    }

    private TimeSpan GetCacheTtl()
    {
        var hours = configuration.GetValue("Cache:ProfileTtlHours", 24);
        return TimeSpan.FromHours(Math.Clamp(hours, 1, 168));
    }

    private static DevCardProfile DeserializeProfile(string json, string targetUser)
    {
        var profile = JsonSerializer.Deserialize<DevCardProfile>(json, JsonOptions)
                      ?? throw new InvalidOperationException("Cached profile JSON is invalid.");

        if (string.IsNullOrWhiteSpace(profile.Username))
        {
            profile.Username = targetUser;
        }

        return profile;
    }

}

internal static class ProfileDbKeys
{
    public static string ForUser(string normalizedUsername, string localeCode) =>
        $"{normalizedUsername}:{localeCode}";
}

internal static class ProfileCacheKeys
{
    public static string ForUsername(string normalizedUsername, AuditContentLocale locale) =>
        $"profile:v1:{normalizedUsername}:{AuditContentLocaleParser.ToCode(locale)}";
}
