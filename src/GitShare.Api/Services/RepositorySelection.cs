using GitShare.Api.Models;

namespace GitShare.Api.Services;

/// <summary>
/// Эвристический отбор репозиториев для глубокого аудита (дерево + LLM).
/// В анализ попадают только топ-N «реальных» проектов, не конспекты и не форки.
/// </summary>
internal static class RepositorySelection
{
    /// <summary>Имена/токены, по которым репо роняется в приоритете для аудита.</summary>
    private static readonly string[] AuditBlacklistKeywords =
    [
        "conf",
        "meetup",
        "fest",
        "docs",
        "notes",
        "slides",
        "interview",
        "homework",
        "tutorial",
        "awesome",
        "conference",
        "mindmap",
        "habrahabr",
        "presentation",
        "workshop",
        "github.io"
    ];

    /// <summary>Языки с «живым» кодом — бонус к рангу для детального разбора дерева.</summary>
    private static readonly HashSet<string> AuditCodeLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "C#",
        "Python",
        "Go",
        "Java",
        "TypeScript",
        "JavaScript",
        "Rust",
        "Kotlin"
    };

    private const int BlacklistPenalty = 1000;
    private const int CodeLanguageBonus = 50;
    private const int HasLanguageBonus = 30;
    private const int SizeBonusMedium = 25;
    private const int SizeBonusLarge = 15;

    public static bool MatchesAuditBlacklist(string repoName)
    {
        var lower = repoName.ToLowerInvariant();
        return AuditBlacklistKeywords.Any(kw => lower.Contains(kw, StringComparison.Ordinal));
    }

    /// <summary>
    /// Ранг для сортировки: звёзды + бонусы за код − штраф за мусорные имена.
    /// </summary>
    public static double CalculateAuditRankScore(RepoListMetadata repo)
    {
        double score = repo.StargazersCount;

        if (MatchesAuditBlacklist(repo.Name))
        {
            score -= BlacklistPenalty;
        }

        if (!string.IsNullOrWhiteSpace(repo.Language))
        {
            score += HasLanguageBonus;

            if (AuditCodeLanguages.Contains(repo.Language))
            {
                score += CodeLanguageBonus;
            }
        }
        else if (repo.SizeKb < 250)
        {
            // Почти наверняка README/заметки без кода
            score -= BlacklistPenalty / 2;
        }

        if (repo.SizeKb >= 500)
        {
            score += SizeBonusMedium;
        }

        if (repo.SizeKb >= 2_000)
        {
            score += SizeBonusLarge;
        }

        return score;
    }

    public static bool IsLikelyDocumentationOrEventRepo(string repoName, string? language, int sizeKb) =>
        MatchesAuditBlacklist(repoName) ||
        (string.IsNullOrWhiteSpace(language) && sizeKb < 250);

    public static int AuditPriorityScore(RepoListMetadata repo) =>
        (int)Math.Round(CalculateAuditRankScore(repo));

    /// <summary>
    /// Топ-N собственных (не fork) репозиториев для forensics (tree + raw) и LLM.
    /// </summary>
    public static List<RepoSummary> PickTopForAudit(
        IReadOnlyList<RepoListMetadata> nonForkRepos,
        string username,
        int count = 4)
    {
        var candidates = nonForkRepos
            .Where(repo => !IsProfileReadmeRepository(username, repo.Name))
            .ToList();

        if (candidates.Count == 0)
        {
            return [];
        }

        // Маленький портфель: не отбрасываем pet-репо только из-за звёзд — берём все кандидаты (до count).
        if (candidates.Count <= count)
        {
            return candidates
                .OrderByDescending(r => UnityRepositoryHeuristics.AdjustAuditRankScore(
                    r.Name, r.SizeKb, CalculateAuditRankScore(r)))
                .ThenByDescending(r => r.StargazersCount)
                .Select(r => new RepoSummary
                {
                    Name = r.Name,
                    Description = r.Description ?? string.Empty,
                    Stars = r.StargazersCount,
                    Language = r.Language ?? string.Empty,
                    Url = r.HtmlUrl
                })
                .ToList();
        }

        var ranked = candidates
            .Select(r => (
                Repo: r,
                Score: UnityRepositoryHeuristics.AdjustAuditRankScore(
                    r.Name, r.SizeKb, CalculateAuditRankScore(r))))
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Repo.StargazersCount)
            .ThenByDescending(x => x.Repo.SizeKb)
            .ToList();

        // Сначала репозитории с кодом (не awesome/docs blacklist), иначе у мега-OSS в аудит
        // попадают только curated lists (sindresorhus/awesome и т.п.).
        var codeCandidates = ranked
            .Where(x => !MatchesAuditBlacklist(x.Repo.Name) &&
                        !IsLikelyDocumentationOrEventRepo(x.Repo.Name, x.Repo.Language, x.Repo.SizeKb))
            .ToList();

        var codeRepos = PickDiverseFromRanked(codeCandidates, count, static x => x.Repo);
        codeRepos = InjectFlagshipRepositories(codeRepos, ranked.Select(x => x.Repo).ToList(), count);

        if (codeRepos.Count < count)
        {
            var filler = ranked
                .Where(x => !codeRepos.Any(c => c.Name.Equals(x.Repo.Name, StringComparison.OrdinalIgnoreCase)))
                .Select(x => x.Repo)
                .Take(count - codeRepos.Count)
                .ToList();
            codeRepos.AddRange(filler);
        }

        return codeRepos
            .Select(r => new RepoSummary
            {
                Name = r.Name,
                Description = r.Description ?? string.Empty,
                Stars = r.StargazersCount,
                Language = r.Language ?? string.Empty,
                Url = r.HtmlUrl
            })
            .ToList();
    }

    private static bool IsProfileReadmeRepository(string username, string repoName) =>
        repoName.Equals(username, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Гарантирует попадание флагманских OSS-репо (unity-typed-scenes, Asteroids, …) в аудит.
    /// </summary>
    internal static List<RepoListMetadata> InjectFlagshipRepositories(
        IReadOnlyList<RepoListMetadata> picked,
        IReadOnlyList<RepoListMetadata> rankedPool,
        int count)
    {
        if (count <= 0 || rankedPool.Count == 0)
        {
            return picked.ToList();
        }

        var result = picked.ToList();
        var flagships = rankedPool
            .Where(r => UnityRepositoryHeuristics.IsFlagshipQualityRepository(r.Name) ||
                        OssRepositoryHeuristics.IsOssFlagshipRepository(r.Name, r.StargazersCount))
            .OrderByDescending(r => r.StargazersCount)
            .Take(Math.Min(2, count))
            .ToList();

        foreach (var flagship in flagships)
        {
            if (result.Any(r => r.Name.Equals(flagship.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (result.Count >= count)
            {
                var replaceIndex = result
                    .Select((repo, index) => (repo, index, score: CalculateAuditRankScore(repo)))
                    .OrderBy(x => x.score)
                    .First().index;
                result[replaceIndex] = flagship;
            }
            else
            {
                result.Add(flagship);
            }
        }

        return result;
    }

    /// <summary>
    /// Вес репозитория при агрегации языков: крупные и популярные проекты важнее заметок и pet-репо.
    /// </summary>
    public static double LanguageRepoWeight(RepoListMetadata repo)
    {
        if (MatchesAuditBlacklist(repo.Name) ||
            IsLikelyDocumentationOrEventRepo(repo.Name, repo.Language, repo.SizeKb))
        {
            return 0.15;
        }

        var sizeFactor = Math.Log10(Math.Max(repo.SizeKb, 10));
        var starsFactor = Math.Log10(Math.Max(repo.StargazersCount, 0) + 1);
        return Math.Max(1.0, sizeFactor * (1.0 + starsFactor * 0.5));
    }

    public static List<RepoListMetadata> PickDiverseFromRanked<T>(
        IReadOnlyList<T> ranked,
        int count,
        Func<T, RepoListMetadata> selectRepo)
    {
        if (ranked.Count == 0 || count <= 0)
        {
            return [];
        }

        var picked = new List<RepoListMetadata>();
        var usedLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in ranked)
        {
            if (picked.Count >= count)
            {
                break;
            }

            var repo = selectRepo(item);
            var lang = repo.Language ?? string.Empty;

            if (picked.Count == 0 ||
                string.IsNullOrWhiteSpace(lang) ||
                !usedLanguages.Contains(lang))
            {
                picked.Add(repo);
                if (!string.IsNullOrWhiteSpace(lang))
                {
                    usedLanguages.Add(lang);
                }
            }
        }

        foreach (var item in ranked)
        {
            if (picked.Count >= count)
            {
                break;
            }

            var repo = selectRepo(item);
            if (picked.Any(p => p.Name.Equals(repo.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            picked.Add(repo);
        }

        return picked;
    }

}
