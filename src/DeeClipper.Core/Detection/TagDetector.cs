using System.Text.RegularExpressions;
using DeeClipper.Core.Models;

namespace DeeClipper.Core.Detection;

public static class TagDetector
{
    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled
    );

    private static readonly Regex ColorHexRegex = new(
        @"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$",
        RegexOptions.Compiled
    );

    private static readonly Regex NumberRegex = new(
        @"^[+-]?(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d+)?(?:[eE][+-]?\d+)?$",
        RegexOptions.Compiled
    );

    private static readonly Regex PhonePatternRegex = new(
        @"^\+?[\d\s().-]{7,25}$",
        RegexOptions.Compiled
    );

    private static readonly string[] CodeKeywords = new[]
    {
        "function ", "class ", "def ", "import ", "export ", "public ", "private ",
        "protected ", "void ", "const ", "var ", "let ", "namespace ", "interface ",
        "return ", "for (", "while (", "if (", "else {", "switch (", "case ",
        "SELECT ", "INSERT INTO ", "UPDATE ", "DELETE FROM ", "CREATE TABLE ",
        "<html>", "<!DOCTYPE", "System.", "console.log", "print(", "async ", "await "
    };

    public static List<string> DetectTags(string? text)
    {
        var tags = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
            return tags;

        var trimmed = text.Trim();

        // 1. Email
        if (IsEmail(trimmed))
        {
            tags.Add(DisplayTag.Email);
        }

        // 2. Color (Hex)
        if (IsColorHex(trimmed))
        {
            tags.Add(DisplayTag.Color);
        }

        // 3. Number
        if (IsNumber(trimmed))
        {
            tags.Add(DisplayTag.Number);
        }

        // 4. Phone
        if (IsPhone(trimmed))
        {
            tags.Add(DisplayTag.Phone);
        }

        // 5. Code
        if (IsCode(text))
        {
            tags.Add(DisplayTag.Code);
        }

        return tags;
    }

    public static bool IsEmail(string text)
    {
        if (text.Contains('\n') || text.Length > 254)
            return false;

        return EmailRegex.IsMatch(text);
    }

    public static bool IsColorHex(string text)
    {
        return ColorHexRegex.IsMatch(text);
    }

    public static bool IsNumber(string text)
    {
        return NumberRegex.IsMatch(text);
    }

    public static bool IsPhone(string text)
    {
        if (text.Contains('\n'))
            return false;

        if (!PhonePatternRegex.IsMatch(text))
            return false;

        var digitCount = text.Count(char.IsDigit);
        // Valid phone numbers typically have 7 to 15 digits
        return digitCount >= 7 && digitCount <= 15;
    }

    public static bool IsCode(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        // Simple heuristic for code
        var hasBraces = text.Contains('{') && text.Contains('}');
        var hasSemicolonsAtEol = text.Contains(";\n") || text.Contains(";\r\n") || text.TrimEnd().EndsWith(';');

        var hasKeywords = CodeKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        var hasIndentation = lines.Length > 1 && lines.Any(l => l.StartsWith("    ") || l.StartsWith("\t"));

        return (hasBraces && (hasKeywords || lines.Length > 1)) ||
               (hasKeywords && (lines.Length > 1 || hasSemicolonsAtEol)) ||
               (hasIndentation && hasKeywords);
    }
}
