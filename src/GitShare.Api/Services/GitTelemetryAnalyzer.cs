using GitShare.Api.Models;



namespace GitShare.Api.Services;



internal static class GitTelemetryAnalyzer

{

    public const string ConventionalCompliant = "Conventional Commits compliant";

    public const string DescriptiveNonStandard = "Descriptive / Non-standard";

    public const string UnstructuredLowDensity = "Unstructured / Low-density";



    public static void ApplyTelemetryFields(
        StructuredAuditResponse response,
        GitHubActivityTelemetry telemetry,
        StructuredAuditResponse? llmParsed,
        AuditContentLocale locale = AuditContentLocale.Ru)
    {
        var fallback = BuildFromTelemetry(telemetry, locale);



        response.GitFormatStandard = PickFormatStandard(llmParsed?.GitFormatStandard, fallback.GitFormatStandard);

        response.ExperienceProfile = PickField(llmParsed?.ExperienceProfile, fallback.ExperienceProfile);

        response.OpenSourceImpact = PickOpenSourceImpact(
            llmParsed?.OpenSourceImpact,
            fallback.OpenSourceImpact,
            telemetry);

    }



    private static string PickField(string? llmValue, string fallback) =>

        string.IsNullOrWhiteSpace(llmValue) ? fallback : llmValue.Trim();

    private static string PickOpenSourceImpact(
        string? llmValue,
        string fallback,
        GitHubActivityTelemetry telemetry)
    {
        var trimmed = llmValue?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return fallback;
        }

        if (telemetry.TotalStars >= 10 &&
            trimmed.Contains("no significant open-source", StringComparison.OrdinalIgnoreCase))
        {
            return fallback;
        }

        if (telemetry.TotalStars == 0 &&
            (trimmed.Contains("no significant open-source", StringComparison.OrdinalIgnoreCase) ||
             trimmed.Contains("open-source footprint", StringComparison.OrdinalIgnoreCase)))
        {
            return fallback;
        }

        return trimmed;
    }

    private static string InferOpenSourceImpact(GitHubActivityTelemetry telemetry, AuditContentLocale locale)
    {
        if (telemetry.ExternalPullRequests.Count > 0)
        {
            return $"External PR targets: {string.Join(", ", telemetry.ExternalPullRequests)}.";
        }

        if (telemetry.TotalStars >= 50)
        {
            var notable = telemetry.TopStarredRepos.Count > 0
                ? $", notable: {string.Join(", ", telemetry.TopStarredRepos)}"
                : string.Empty;
            return $"Own open-source: {telemetry.TotalStars}★ total{notable}.";
        }

        if (telemetry.TotalStars >= 10)
        {
            return $"Own repositories: {telemetry.TotalStars}★ across public repos.";
        }

        if (telemetry.TotalStars > 0)
        {
            return $"Limited public OSS signal: {telemetry.TotalStars}★ on own repos.";
        }

        return AuditContentCatalog.NoPublicOssFootprint(locale);
    }



    private static string PickFormatStandard(string? llmValue, string fallback)

    {

        if (string.IsNullOrWhiteSpace(llmValue))

        {

            return fallback;

        }



        var normalized = NormalizeFormatStandard(llmValue);

        return string.IsNullOrEmpty(normalized) ? fallback : normalized;

    }



    public static string NormalizeFormatStandard(string? value)

    {

        if (string.IsNullOrWhiteSpace(value))

        {

            return string.Empty;

        }



        var trimmed = value.Trim();



        if (trimmed.Contains("Conventional", StringComparison.OrdinalIgnoreCase) &&

            trimmed.Contains("compliant", StringComparison.OrdinalIgnoreCase))

        {

            return ConventionalCompliant;

        }



        if (trimmed.Contains("Descriptive", StringComparison.OrdinalIgnoreCase) ||

            trimmed.Contains("Non-standard", StringComparison.OrdinalIgnoreCase))

        {

            return DescriptiveNonStandard;

        }



        if (trimmed.Contains("Unstructured", StringComparison.OrdinalIgnoreCase) ||

            trimmed.Contains("Low-density", StringComparison.OrdinalIgnoreCase))

        {

            return UnstructuredLowDensity;

        }



        if (trimmed.StartsWith("High", StringComparison.OrdinalIgnoreCase) ||

            trimmed.Contains("Conventional Commits (feat", StringComparison.OrdinalIgnoreCase))

        {

            return ConventionalCompliant;

        }



        if (trimmed.StartsWith("Medium", StringComparison.OrdinalIgnoreCase))

        {

            return DescriptiveNonStandard;

        }



        if (trimmed.StartsWith("Low", StringComparison.OrdinalIgnoreCase))

        {

            return UnstructuredLowDensity;

        }



        return trimmed switch

        {

            ConventionalCompliant or DescriptiveNonStandard or UnstructuredLowDensity => trimmed,

            _ => string.Empty

        };

    }



    private static StructuredAuditResponse BuildFromTelemetry(
        GitHubActivityTelemetry telemetry,
        AuditContentLocale locale)
    {
        var total = telemetry.CommitsInWorkingHours + telemetry.CommitsInOffHours;

        var workingPercent = total == 0 ? 0 : Math.Round(telemetry.CommitsInWorkingHours * 100.0 / total, 1);

        var offPercent = total == 0 ? 0 : Math.Round(100 - workingPercent, 1);

        var formatStandard = InferGitFormatStandard(telemetry.RecentCommitMessages);

        var experience = total == 0
            ? locale == AuditContentLocale.En
                ? "Time distribution: insufficient commit samples in telemetry window."
                : "Распределение по времени: недостаточно коммитов в окне телеметрии."
            : locale == AuditContentLocale.En
                ? $"Time distribution: {workingPercent}% Mon–Fri 10:00–18:00 (local), {offPercent}% off-window."
                : $"Распределение: {workingPercent}% будни 10:00–18:00 (локально), {offPercent}% вне окна.";

        var openSource = InferOpenSourceImpact(telemetry, locale);



        return new StructuredAuditResponse

        {

            GitFormatStandard = formatStandard,

            ExperienceProfile = experience,

            OpenSourceImpact = openSource

        };

    }



    private static string InferGitFormatStandard(IReadOnlyList<string> messages)

    {

        if (messages.Count == 0)

        {

            return UnstructuredLowDensity;

        }



        var conventional = messages.Count(IsConventionalCommit);

        var ratio = conventional * 100.0 / messages.Count;



        return ratio switch

        {

            >= 60 => ConventionalCompliant,

            >= 25 => DescriptiveNonStandard,

            _ => UnstructuredLowDensity

        };

    }



    private static bool IsConventionalCommit(string message)

    {

        if (string.IsNullOrWhiteSpace(message))

        {

            return false;

        }



        var firstLine = message.Split('\n')[0].Trim();

        return firstLine.StartsWith("feat", StringComparison.OrdinalIgnoreCase) ||

               firstLine.StartsWith("fix", StringComparison.OrdinalIgnoreCase) ||

               firstLine.StartsWith("chore", StringComparison.OrdinalIgnoreCase) ||

               firstLine.StartsWith("refactor", StringComparison.OrdinalIgnoreCase) ||

               firstLine.StartsWith("docs", StringComparison.OrdinalIgnoreCase) ||

               firstLine.StartsWith("test", StringComparison.OrdinalIgnoreCase) ||

               firstLine.Contains(':', StringComparison.Ordinal);

    }

}

