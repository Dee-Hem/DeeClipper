using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DeeClipper.Core.Text;

public static class TextTransforms
{
    private static readonly HashSet<string> SpecificTrackingParams = new(StringComparer.OrdinalIgnoreCase)
    {
        "fbclid",
        "gclid",
        "gclsrc",
        "mc_cid",
        "mc_eid",
        "igshid",
        "msclkid",
        "twclid",
        "_ga",
        "_gl"
    };

    private static readonly Regex MultipleSpacesRegex = new(@"[^\S\r\n]+", RegexOptions.Compiled);
    private static readonly Regex UrlInTextRegex = new(@"(https?://[^\s<>""]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Converts text to plain unformatted text with normalized line endings.
    /// </summary>
    public static string ToPlainText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Normalize line endings to Windows \r\n
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            // Allow printable characters, tabs, and newlines
            if (!char.IsControl(c) || c == '\t' || c == '\n')
            {
                sb.Append(c);
            }
        }
        return sb.ToString().Replace("\n", Environment.NewLine);
    }

    /// <summary>
    /// Converts text to UPPERCASE.
    /// </summary>
    public static string ToUpperCase(string text)
    {
        return text?.ToUpperInvariant() ?? string.Empty;
    }

    /// <summary>
    /// Converts text to lowercase.
    /// </summary>
    public static string ToLowerCase(string text)
    {
        return text?.ToLowerInvariant() ?? string.Empty;
    }

    /// <summary>
    /// Converts text to Title Case.
    /// </summary>
    public static string ToTitleCase(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text.ToLowerInvariant());
    }

    /// <summary>
    /// Removes all line breaks, replaces them with spaces, and collapses consecutive spaces.
    /// </summary>
    public static string ToSingleLine(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var noBreaks = text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
        var collapsed = Regex.Replace(noBreaks, @"\s+", " ");
        return collapsed.Trim();
    }

    /// <summary>
    /// Trims leading and trailing whitespace from the text and trailing whitespace on each line.
    /// </summary>
    public static string Trim(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var trimmedLines = lines.Select(l => l.TrimEnd());
        var joined = string.Join(Environment.NewLine, trimmedLines);
        return joined.Trim();
    }

    /// <summary>
    /// Strips tracking parameters (utm_*, fbclid, gclid, etc.) from URLs.
    /// Works for direct URLs and URLs embedded within text.
    /// </summary>
    public static string StripTrackingParameters(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // If the whole input is a single URL
        if (Uri.TryCreate(text.Trim(), UriKind.Absolute, out var directUri) &&
            (directUri.Scheme == Uri.UriSchemeHttp || directUri.Scheme == Uri.UriSchemeHttps))
        {
            return CleanUrl(directUri);
        }

        // If input contains URLs embedded in text
        return UrlInTextRegex.Replace(text, match =>
        {
            if (Uri.TryCreate(match.Value, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return CleanUrl(uri);
            }
            return match.Value;
        });
    }

    public static string CleanUrl(Uri uri)
    {
        if (string.IsNullOrEmpty(uri.Query) || uri.Query == "?")
        {
            return uri.ToString();
        }

        var query = uri.Query.TrimStart('?');
        var pairs = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
        var remainingParams = new List<string>();

        foreach (var pair in pairs)
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);

            if (IsTrackingParameter(key))
            {
                continue;
            }

            remainingParams.Add(pair);
        }

        var builder = new UriBuilder(uri);
        if (remainingParams.Count > 0)
        {
            builder.Query = string.Join("&", remainingParams);
        }
        else
        {
            builder.Query = string.Empty;
        }

        var result = builder.Uri.ToString();
        // UriBuilder adds :80 or :443 if default port is omitted in original string, ensure clean standard output
        if (uri.IsDefaultPort && result.Contains($":{uri.Port}"))
        {
            result = result.Replace($":{uri.Port}", "");
        }

        return result;
    }

    private static bool IsTrackingParameter(string key)
    {
        if (key.StartsWith("utm_", StringComparison.OrdinalIgnoreCase))
            return true;

        return SpecificTrackingParams.Contains(key);
    }
}
