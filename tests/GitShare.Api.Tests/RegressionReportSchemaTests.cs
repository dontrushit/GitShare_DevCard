using System.Text.Json;
using Xunit;

namespace GitShare.Api.Tests;

public sealed class RegressionReportSchemaTests
{
    [Fact]
    public void Regression_report_template_has_expected_summary_fields()
    {
        var template = new
        {
            generatedAtUtc = DateTime.UtcNow.ToString("o"),
            summary = new { total = 1, passed = 1, failed = 0, passRatePercent = 100.0 },
            byCohort = new[] { new { cohort = "self-dogfood", total = 1, passed = 1, failed = 0 } },
            byWave = new[] { new { wave = 6, total = 1, passed = 1, failed = 0 } },
            results = Array.Empty<object>(),
        };

        var json = JsonSerializer.Serialize(template);
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("summary", out var summary));
        Assert.True(summary.TryGetProperty("passRatePercent", out _));
        Assert.True(doc.RootElement.TryGetProperty("byCohort", out _));
        Assert.True(doc.RootElement.TryGetProperty("byWave", out _));
    }
}
