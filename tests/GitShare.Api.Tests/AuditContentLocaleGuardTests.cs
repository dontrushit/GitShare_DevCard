using GitShare.Api.Models;
using GitShare.Api.Services;
using Xunit;

namespace GitShare.Api.Tests;

public sealed class AuditContentLocaleGuardTests
{
    [Fact]
    public void ProfileMatchesRequestedLocale_En_RejectsCyrillicAudit()
    {
        var profile = new DevCardProfile
        {
            ContentLocale = "en",
            AuditData = new StructuredAuditResponse
            {
                CoreEngineeringFocus = "Доминирующий стек: C# и WinForms в портфеле разработчика.",
                Projects =
                [
                    new ProjectAuditDetail
                    {
                        RepoName = "demo",
                        TechnicalDebt = "Отсутствует DI и слой данных.",
                        InterviewTrapQuestion = "Как вы внедрите DI в WinForms?",
                        Pros = ["Структура читаема"],
                        Cons = ["Нет Repository"]
                    }
                ]
            }
        };

        Assert.False(AuditContentLocaleGuard.ProfileMatchesRequestedLocale(profile, "en"));
    }

    [Fact]
    public void ProfileMatchesRequestedLocale_Ru_RejectsEnglishOnlyAudit()
    {
        var profile = new DevCardProfile
        {
            ContentLocale = "ru",
            AuditData = new StructuredAuditResponse
            {
                CoreEngineeringFocus =
                    "Dominant stack: .NET console utilities and automation scripts across the portfolio.",
                Projects =
                [
                    new ProjectAuditDetail
                    {
                        RepoName = "demo",
                        TechnicalDebt =
                            "Utility / automation: data layers and DI absent — typical for CLI and scripts.",
                        InterviewTrapQuestion =
                            "How would you refactor this console service to support dependency injection?",
                        Pros = ["Clear module structure"],
                        Cons = ["No repository layer"]
                    }
                ]
            }
        };

        Assert.False(AuditContentLocaleGuard.ProfileMatchesRequestedLocale(profile, "ru"));
    }

    [Fact]
    public void ProfileMatchesRequestedLocale_En_AcceptsEnglishAudit()
    {
        var profile = new DevCardProfile
        {
            ContentLocale = "en",
            AuditData = new StructuredAuditResponse
            {
                CoreEngineeringFocus = "Dominant stack: .NET console utilities.",
                Projects =
                [
                    new ProjectAuditDetail
                    {
                        RepoName = "demo",
                        TechnicalDebt = "No DI container; static helpers for data access.",
                        InterviewTrapQuestion = "How would you introduce DI without breaking Program.cs?",
                        Pros = [],
                        Cons = []
                    }
                ]
            }
        };

        Assert.True(AuditContentLocaleGuard.ProfileMatchesRequestedLocale(profile, "en"));
    }
}
