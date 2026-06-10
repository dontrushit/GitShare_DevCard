namespace GitShare.Api.Services;

/// <summary>Локализация полей аудита, приходящих из сигнатур/каталогов (layout, framework).</summary>
internal static class AuditFieldLocalizer
{
    public static string LocalizeLayout(string layout, AuditContentLocale locale)
    {
        if (string.IsNullOrWhiteSpace(layout) || locale == AuditContentLocale.En)
        {
            return layout.Trim();
        }

        if (ContainsCyrillic(layout))
        {
            return layout.Trim();
        }

        return layout.Trim() switch
        {
            "Console Utility" => "Консольная утилита",
            "Flat Monolith" => "Плоский монолит",
            "Flat Monolith (WinForms)" => "Плоский монолит (WinForms)",
            "MVVM (UI/Service Isolation)" => "MVVM (изоляция UI/сервисов)",
            "Layered (Services + Interfaces)" => "Слоистая (Services + Interfaces)",
            "Layered (Repository/Context)" => "Слоистая (Repository/Context)",
            "Web API / MVC" => "Web API / MVC",
            "Unity Project" => "Unity-проект",
            "Unity Package / Plugin" => "Unity-пакет / плагин",
            "Unity Editor + Runtime" => "Unity Editor + Runtime",
            "Unity Multi-Pattern Samples" => "Unity: примеры паттернов",
            "Multi-Module Microservices" => "Мультимодульные микросервисы",
            "Algorithm / Exercise Collection" => "Алгоритмы / упражнения",
            _ => layout.Trim()
        };
    }

    public static string LocalizeFramework(string framework, AuditContentLocale locale)
    {
        if (string.IsNullOrWhiteSpace(framework) || locale == AuditContentLocale.En)
        {
            return framework.Trim();
        }

        if (ContainsCyrillic(framework))
        {
            return framework.Trim();
        }

        return framework.Trim() switch
        {
            ".NET, Console" => ".NET, консоль",
            ".NET, WinForms" => ".NET, WinForms",
            ".NET, WPF" => ".NET, WPF",
            "DevOps (IaC/Containers)" => "DevOps (IaC/контейнеры)",
            "Unity, C# (Package/Plugin)" => "Unity, C# (пакет/плагин)",
            "Unity, C# (Game/Sample)" => "Unity, C# (игра/пример)",
            "Unity, C# (Architecture Samples)" => "Unity, C# (примеры архитектуры)",
            "Playwright, TypeScript" => "Playwright, TypeScript",
            "TypeScript, React" => "TypeScript, React",
            "Next.js, React" => "Next.js, React",
            _ => framework.Trim()
        };
    }

    private static bool ContainsCyrillic(string text) =>
        text.Any(static c => c is >= '\u0400' and <= '\u04FF');
}
