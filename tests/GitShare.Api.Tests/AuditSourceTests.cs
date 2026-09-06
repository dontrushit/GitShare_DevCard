using System.Text.Json;
using GitShare.Api.Models;
using GitShare.Api.Services;
using Xunit;

namespace GitShare.Api.Tests;

public sealed class AuditSourceTests
{
    [Fact]
    public void Rule_based_path_marks_audit_source_as_rules()
    {
        var result = GitHubAnalyticsService.ApplyRuleBasedAudit(
            [],
            new GitHubActivityTelemetry(),
            AuditContentLocale.Ru,
            portfolioTotalStars: 0);

        Assert.Equal(AuditSources.Rules, result.AuditSource);
        Assert.Equal("rules", result.AuditSource);
    }

    [Fact]
    public void Audit_source_is_optional_and_not_serialized_when_absent()
    {
        var withoutSource = JsonSerializer.Serialize(new StructuredAuditResponse());
        Assert.DoesNotContain(nameof(StructuredAuditResponse.AuditSource), withoutSource);

        var legacyCacheJson = "{\"Projects\":[],\"CoreEngineeringFocus\":\"focus\"}";
        var fromCache = JsonSerializer.Deserialize<StructuredAuditResponse>(legacyCacheJson);

        Assert.NotNull(fromCache);
        Assert.Null(fromCache.AuditSource);
    }
}
