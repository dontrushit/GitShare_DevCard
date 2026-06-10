namespace GitShare.Api.Services;

/// <summary>
/// Сверяет заявления README с сигнатурами дерева и снижает класс pet/desktop проектов.
/// </summary>
internal static class ReadmeStructureVerifier
{
    private static readonly string[] AcademicMarkersRu =
    [
        "лаборатор", "курсов", "учебн", "диплом", "контрольн", "практик", "задани",
        "университет", "колледж", "студен", "учебное пособие"
    ];

    private static readonly string[] AcademicMarkersEn =
    [
        "lab work", "coursework", "course project", "homework", "university", "college",
        "student project", "learning project", "practice task", "assignment", "tutorial project"
    ];

    private static readonly string[] ArchitectureClaimMarkers =
    [
        "clean architecture", "solid", "repository pattern", "dependency injection",
        "layered architecture", "mvvm", "onion architecture", "hexagonal",
        "чистая архитектура", "многослойн", "паттерн repository", "внедрени", "зависимост",
        "solid", "mvvm", "repository", "dependency injection", "di container", "di-контейнер"
    ];

    private static readonly string[] EnterpriseStructureMarkers =
    [
        "/services/", "/repositories/", "/repository/", "/infrastructure/", "/domain/",
        "/application/", "dependencyinjection", "addscoped", "addtransient", "irepository",
        "iservice", "dbcontext", "unitofwork", "mediator", "cqrs"
    ];

    private static readonly string[] PetDesktopNameTokens =
    [
        "catalog", "phones", "taskmanager", "todo", "calculator", "contacts", "demo", "sample"
    ];

    public static string AdjustProjectClass(
        string projectClass,
        string repoName,
        string? readme,
        string manifestOrFramework,
        string? debtSeverity,
        int portfolioTotalStars = -1)
    {
        if (!EnterpriseAuditLexicon.IsProductionClass(projectClass))
        {
            return projectClass;
        }

        var analysis = Analyze(repoName, readme, manifestOrFramework, debtSeverity, portfolioTotalStars);
        return analysis.ShouldDowngradeForScoring
            ? ProjectClassClassifier.UtilityAutomation
            : projectClass;
    }

    public static ReadmeStructureAnalysis Analyze(
        string repoName,
        string? readme,
        string manifestOrFramework,
        string? debtSeverity,
        int portfolioTotalStars = -1)
    {
        var combined = $"{manifestOrFramework} {repoName}";
        var readmeText = ReadmeCleaner.CleanReadmeContent(readme ?? string.Empty);
        var isDesktop = IsDesktopStack(combined);
        var isAcademic = ContainsAny(readmeText, AcademicMarkersRu) ||
                           ContainsAny(readmeText, AcademicMarkersEn);
        var claimsArchitecture = ContainsAny(readmeText, ArchitectureClaimMarkers);
        var hasEnterpriseStructure = HasEnterpriseStructureSignals(combined);
        var hasStructureMismatch = claimsArchitecture && !hasEnterpriseStructure;
        var isPetDesktopName = PetDesktopNameTokens.Any(token =>
            repoName.Contains(token, StringComparison.OrdinalIgnoreCase));
        var weakDebt = IsWeakDebt(debtSeverity);
        var weakPortfolio = portfolioTotalStars < 0 || portfolioTotalStars < 20;

        var shouldDowngrade = isDesktop && (
            isAcademic ||
            hasStructureMismatch ||
            (isPetDesktopName && weakDebt && weakPortfolio) ||
            (weakDebt && !hasEnterpriseStructure && weakPortfolio && isPetDesktopName));

        string? note = null;
        if (shouldDowngrade)
        {
            note = hasStructureMismatch
                ? "README заявляет архитектурные паттерны, но дерево/сигнатуры их не подтверждают."
                : isAcademic
                    ? "README или контекст указывают на учебный/pet-проект."
                    : "Desktop pet без enterprise-слоёв — для грейда считается utility, не production.";
        }

        return new ReadmeStructureAnalysis(
            IsAcademicLikely: isAcademic,
            HasStructureMismatch: hasStructureMismatch,
            ShouldDowngradeForScoring: shouldDowngrade,
            MismatchNote: note);
    }

    public static string AppendMismatchNote(string technicalDebt, ReadmeStructureAnalysis analysis)
    {
        if (string.IsNullOrWhiteSpace(analysis.MismatchNote) ||
            technicalDebt.Contains(analysis.MismatchNote, StringComparison.Ordinal))
        {
            return technicalDebt;
        }

        return string.IsNullOrWhiteSpace(technicalDebt)
            ? analysis.MismatchNote
            : $"{technicalDebt.TrimEnd()} {analysis.MismatchNote}";
    }

    private static bool IsDesktopStack(string combined) =>
        combined.Contains("WinForms", StringComparison.OrdinalIgnoreCase) ||
        combined.Contains("WPF", StringComparison.OrdinalIgnoreCase) ||
        (combined.Contains("Console", StringComparison.OrdinalIgnoreCase) &&
         combined.Contains(".csproj", StringComparison.OrdinalIgnoreCase));

    private static bool HasEnterpriseStructureSignals(string combined)
    {
        var lower = combined.ToLowerInvariant();
        return EnterpriseStructureMarkers.Any(marker => lower.Contains(marker, StringComparison.Ordinal));
    }

    private static bool IsWeakDebt(string? debtSeverity) =>
        string.Equals(debtSeverity, "Warning", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(debtSeverity, "Critical", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAny(string text, IEnumerable<string> markers)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var lower = text.ToLowerInvariant();
        return markers.Any(marker => lower.Contains(marker, StringComparison.Ordinal));
    }
}

internal sealed record ReadmeStructureAnalysis(
    bool IsAcademicLikely,
    bool HasStructureMismatch,
    bool ShouldDowngradeForScoring,
    string? MismatchNote);
