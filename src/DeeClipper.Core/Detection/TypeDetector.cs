using DeeClipper.Core.Models;

namespace DeeClipper.Core.Detection;

public static class TypeDetector
{
    public static ClipboardItemType DetectType(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return ClipboardItemType.Text;

        var trimmed = text.Trim();

        // URLs do not have newlines
        if (trimmed.Contains('\n') || trimmed.Contains('\r'))
            return ClipboardItemType.Text;

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            if ((uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
                !string.IsNullOrWhiteSpace(uri.Host) &&
                uri.Host.Contains('.'))
            {
                return ClipboardItemType.Link;
            }
        }

        return ClipboardItemType.Text;
    }
}
