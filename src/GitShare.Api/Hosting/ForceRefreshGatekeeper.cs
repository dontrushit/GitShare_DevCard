using Microsoft.Extensions.Caching.Memory;

namespace GitShare.Api.Hosting;

/// <summary>Ограничение частоты forceRefresh по IP + username (тяжёлый GitHub + LLM pipeline).</summary>
public sealed class ForceRefreshGatekeeper(IMemoryCache memoryCache)
{
    public static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(10);

    public bool TryAcquire(string clientIp, string normalizedUsername)
    {
        var key = $"force-refresh:{clientIp}:{normalizedUsername.ToLowerInvariant()}";
        if (memoryCache.TryGetValue(key, out _))
        {
            return false;
        }

        memoryCache.Set(
            key,
            true,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = Cooldown,
                Size = 1,
            });

        return true;
    }
}
