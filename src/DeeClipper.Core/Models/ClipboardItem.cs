namespace DeeClipper.Core.Models;

public class ClipboardItem
{
    public long Id { get; set; }
    public ClipboardItemType Type { get; set; } = ClipboardItemType.Text;
    public string? ContentText { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public string? ThumbPath { get; set; }
    public string? FilesJson { get; set; }
    public string Preview { get; set; } = string.Empty;
    public long ByteSize { get; set; }
    public string? SourceApp { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastUsedAt { get; set; } = DateTimeOffset.UtcNow;
    public int UseCount { get; set; } = 1;
    public bool IsPinned { get; set; }
    public int PinOrder { get; set; }
    public string? Label { get; set; }
    public long? GroupId { get; set; }
    public bool IsSensitive { get; set; }

    /// <summary>
    /// Calculated display tags (Email, Phone, Color, Code, Number)
    /// </summary>
    public List<string> Tags { get; set; } = new();

    public static string GeneratePreview(string? text, int maxLength = 200)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Replace newlines with spaces for clean single-line preview
        var normalized = text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}
