using GitShare.Api.Models;

namespace GitShare.Api.Services;

internal static class LevelSummaryFallbackBuilder
{
    public static string Build(DevCardProfile profile, ProgrammerLevelInfo level, AuditContentLocale locale)
    {
        var productionCount = profile.AuditData?.Projects?.Count(p =>
            EnterpriseAuditLexicon.IsProductionClass(p.ProjectClass)) ?? 0;
        var ossUtilityCount = profile.AuditData?.Projects?.Count(p =>
            p.ProjectClass == ProjectClassClassifier.UtilityAutomation &&
            (p.Framework.Contains("Go", StringComparison.OrdinalIgnoreCase) ||
             p.Framework.Contains("DevOps", StringComparison.OrdinalIgnoreCase) ||
             OssRepositoryHeuristics.IsKnownDevOpsTool(p.RepoName))) ?? 0;
        var externalPrs = profile.ActivityTelemetry?.ExternalPullRequests?.Count ?? 0;
        var focus = profile.AuditData?.CoreEngineeringFocus?.Trim();
        var topLang = profile.LanguageStack.FirstOrDefault()?.Language ?? "—";
        var title = LocalizeTitle(level.Code, locale);

        if (locale == AuditContentLocale.En)
        {
            return BuildEnglish(
                profile,
                level,
                title,
                productionCount,
                ossUtilityCount,
                externalPrs,
                focus,
                topLang);
        }

        return BuildRussian(
            profile,
            level,
            title,
            productionCount,
            ossUtilityCount,
            externalPrs,
            focus,
            topLang);
    }

    private static string BuildRussian(
        DevCardProfile profile,
        ProgrammerLevelInfo level,
        string title,
        int productionCount,
        int ossUtilityCount,
        int externalPrs,
        string? focus,
        string topLang)
    {
        var portfolioLine = profile.TotalStars > 0
            ? $"Публичный портфель @{profile.Username}: {profile.OwnRepositoryCount} собственных репо, ★{profile.TotalStars}, доминирует {topLang}."
            : $"Публичный портфель @{profile.Username}: {profile.OwnRepositoryCount} репо без заметного OSS-влияния (★{profile.TotalStars}), основной язык — {topLang}.";

        var auditLine = productionCount > 0
            ? $"В архитектурном аудите — {productionCount} production-проект(ов)"
            : ossUtilityCount > 0 && profile.TotalStars >= 500
                ? $"В аудите — {ossUtilityCount} CLI/DevOps-утилит(ы); критерии enterprise Production не применимы"
            : externalPrs > 0
                ? $"Production в аудите не выделен, но есть вклад в чужие репозитории ({externalPrs} PR)"
                : "Аудит не показал зрелых production-следов";

        if (!string.IsNullOrWhiteSpace(focus))
        {
            auditLine += $"; фокус: {TrimSentence(focus)}";
        }

        auditLine += ".";

        var confidenceSuffix = level.IsLowConfidence
            ? $" Сигнал портфеля ослаблен (×{level.SignalConfidence:0.##}), поэтому итог {level.Score}/100 консервативен."
            : $" Итоговый балл {level.Score}/100 согласуется с этими фактами.";

        var verdict = $"Уровень «{title}» отражает текущий публичный след, а не должность в компании.";

        return $"{portfolioLine} {auditLine}{confidenceSuffix} {verdict}";
    }

    private static string BuildEnglish(
        DevCardProfile profile,
        ProgrammerLevelInfo level,
        string title,
        int productionCount,
        int ossUtilityCount,
        int externalPrs,
        string? focus,
        string topLang)
    {
        var portfolioLine = profile.TotalStars > 0
            ? $"Public portfolio @{profile.Username}: {profile.OwnRepositoryCount} own repos, ★{profile.TotalStars}, dominated by {topLang}."
            : $"Public portfolio @{profile.Username}: {profile.OwnRepositoryCount} repos with limited OSS footprint (★{profile.TotalStars}), primary language {topLang}.";

        var auditLine = productionCount > 0
            ? $"Architecture audit surfaced {productionCount} production project(s)"
            : ossUtilityCount > 0 && profile.TotalStars >= 500
                ? $"Audit covers {ossUtilityCount} CLI/DevOps utility project(s); enterprise Production criteria do not apply"
            : externalPrs > 0
                ? $"No production class in audit, but external contributions ({externalPrs} PRs) add signal"
                : "Audit did not surface mature production footprints";

        if (!string.IsNullOrWhiteSpace(focus))
        {
            auditLine += $"; focus: {TrimSentence(focus)}";
        }

        auditLine += ".";

        var confidenceSuffix = level.IsLowConfidence
            ? $" Portfolio signal is weak (×{level.SignalConfidence:0.##}), so the {level.Score}/100 score stays conservative."
            : $" The {level.Score}/100 score aligns with these facts.";

        var verdict = $"The {title} tier reflects open GitHub activity, not a corporate job title.";

        return $"{portfolioLine} {auditLine}{confidenceSuffix} {verdict}";
    }

    private static string LocalizeTitle(string code, AuditContentLocale locale)
    {
        if (locale == AuditContentLocale.En)
        {
            return code switch
            {
                "principal" => "Principal",
                "lead" => "Lead",
                "senior" => "Senior",
                "middle" => "Middle",
                "junior" => "Junior",
                _ => "Trainee"
            };
        }

        return code switch
        {
            "principal" => "Принципал",
            "lead" => "Тимлид",
            "senior" => "Сеньор",
            "middle" => "Мидл",
            "junior" => "Джуниор",
            _ => "Стажёр"
        };
    }

    private static string TrimSentence(string text) =>
        text.Length <= 120 ? text : text[..117].TrimEnd() + "…";
}
