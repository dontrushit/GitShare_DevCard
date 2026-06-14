using GitShare.Api.Models;
using GitShare.Api.Services;
using Xunit;

namespace GitShare.Api.Tests;

public sealed class RepositorySelectionTests
{
    [Fact]
    public void PickTopForAudit_prefers_diverse_languages()
    {
        var repos = new List<RepoListMetadata>
        {
            MakeRepo("alpha-csharp", "C#", stars: 100, sizeKb: 5_000),
            MakeRepo("beta-csharp", "C#", stars: 90, sizeKb: 4_500),
            MakeRepo("gamma-python", "Python", stars: 80, sizeKb: 4_000),
            MakeRepo("delta-go", "Go", stars: 70, sizeKb: 3_500),
            MakeRepo("epsilon-ts", "TypeScript", stars: 60, sizeKb: 3_000),
        };

        var picked = RepositorySelection.PickTopForAudit(repos, "tester", count: 4);
        var languages = picked.Select(r => r.Language).Distinct(StringComparer.OrdinalIgnoreCase).Count();

        Assert.Equal(4, picked.Count);
        Assert.True(languages >= 3, $"Expected language diversity, got: {string.Join(", ", picked.Select(p => p.Language))}");
    }

    [Fact]
    public void LanguageRepoWeight_downweights_documentation_repos()
    {
        var code = MakeRepo("service-api", "C#", stars: 5, sizeKb: 2_000);
        var notes = MakeRepo("meetup-notes", language: null, stars: 0, sizeKb: 40);

        Assert.True(RepositorySelection.LanguageRepoWeight(code) > RepositorySelection.LanguageRepoWeight(notes) * 3);
    }

    [Fact]
    public void PickDiverseFromRanked_fills_remaining_when_languages_exhausted()
    {
        var ranked = new[]
        {
            (Repo: MakeRepo("a", "C#", 10, 1_000), Score: 100.0),
            (Repo: MakeRepo("b", "C#", 9, 900), Score: 90.0),
            (Repo: MakeRepo("c", "Python", 8, 800), Score: 80.0),
        };

        var picked = RepositorySelection.PickDiverseFromRanked(ranked, 3, static x => x.Repo);

        Assert.Equal(3, picked.Count);
        Assert.Contains(picked, r => r.Name == "c");
    }

    private static RepoListMetadata MakeRepo(
        string name,
        string? language,
        int stars,
        int sizeKb) =>
        new(
            name,
            stars,
            language,
            Fork: false,
            sizeKb,
            ForksCount: 0,
            Description: null,
            HtmlUrl: $"https://github.com/tester/{name}",
            PushedAt: null,
            UpdatedAt: null);
}
