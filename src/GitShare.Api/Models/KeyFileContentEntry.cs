namespace GitShare.Api.Models;

/// <summary>
/// Фрагмент исходника, переданный в LLM для аудита (не только имя файла).
/// </summary>
public sealed class KeyFileContentEntry
{
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
