using System.Text.RegularExpressions;

namespace GitShare.Api.Services;

internal static partial class ReadmeCleaner
{
    public static string CleanReadmeContent(string rawMarkdown)
    {
        if (string.IsNullOrWhiteSpace(rawMarkdown))
        {
            return string.Empty;
        }

        var text = rawMarkdown;
        text = CodeBlockRegex().Replace(text, " ");
        text = InlineCodeRegex().Replace(text, " ");
        text = ImageRegex().Replace(text, " ");
        text = MarkdownLinkRegex().Replace(text, "$1");
        text = HtmlTagRegex().Replace(text, " ");
        text = UrlRegex().Replace(text, " ");
        text = BadgeRegex().Replace(text, " ");
        text = HeadingMarkersRegex().Replace(text, "$1 ");
        text = WhitespaceRegex().Replace(text, " ").Trim();

        return text.Length <= 1500 ? text : text[..1500];
    }

    [GeneratedRegex(@"```[\s\S]*?```", RegexOptions.Multiline)]
    private static partial Regex CodeBlockRegex();

    [GeneratedRegex(@"`[^`]+`")]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"!\[[^\]]*\]\([^)]+\)", RegexOptions.IgnoreCase)]
    private static partial Regex ImageRegex();

    [GeneratedRegex(@"\[([^\]]+)\]\([^)]+\)")]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"https?://\S+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"\[!?\[[^\]]*\]\([^)]+\)\]\([^)]+\)")]
    private static partial Regex BadgeRegex();

    [GeneratedRegex(@"#{1,6}\s*")]
    private static partial Regex HeadingMarkersRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
