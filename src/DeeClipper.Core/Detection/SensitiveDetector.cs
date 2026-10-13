using System.Text.RegularExpressions;

namespace DeeClipper.Core.Detection;

public static class SensitiveDetector
{
    private static readonly string[] TokenPrefixes = new[]
    {
        "ghp_",
        "github_pat_",
        "sk-",
        "AKIA",
        "xoxb-",
        "xoxp-",
        "AIza"
    };

    private static readonly Regex PrivateKeyRegex = new(
        @"-----BEGIN [A-Z0-9 ]*PRIVATE KEY-----",
        RegexOptions.Compiled
    );

    // Matches strings that look like card numbers (13-19 digits with optional spaces or dashes)
    private static readonly Regex CardCandidateRegex = new(
        @"\b(?:\d[ -]?){12,18}\d\b",
        RegexOptions.Compiled
    );

    // JWT format: header.payload.signature (base64url)
    private static readonly Regex JwtRegex = new(
        @"\beyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b",
        RegexOptions.Compiled
    );

    public static bool IsSensitive(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        // 1. Private key blocks
        if (PrivateKeyRegex.IsMatch(text))
            return true;

        // 2. Common API token prefixes
        if (ContainsApiToken(text))
            return true;

        // 3. JWTs
        if (JwtRegex.IsMatch(text))
            return true;

        // 4. Luhn-valid card numbers
        if (ContainsLuhnValidCard(text))
            return true;

        // 5. Long high-entropy single-word strings (20+ chars)
        // Must NOT flag normal URLs or sentences
        if (IsHighEntropySingleWord(text))
            return true;

        return false;
    }

    private static bool ContainsApiToken(string text)
    {
        foreach (var prefix in TokenPrefixes)
        {
            var idx = text.IndexOf(prefix, StringComparison.Ordinal);
            if (idx >= 0)
            {
                // Verify it has trailing token characters of meaningful length
                var remainder = text[idx..];
                var tokenMatch = Regex.Match(remainder, @"^[A-Za-z0-9_\-]{10,}");
                if (tokenMatch.Success)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public static bool ContainsLuhnValidCard(string text)
    {
        var matches = CardCandidateRegex.Matches(text);
        foreach (Match match in matches)
        {
            var digitsOnly = new string(match.Value.Where(char.IsDigit).ToArray());
            if (digitsOnly.Length is >= 13 and <= 19)
            {
                // Ensure it's not all the same digit (e.g. 0000000000000000)
                if (digitsOnly.Distinct().Count() > 1 && IsLuhnValid(digitsOnly))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public static bool IsLuhnValid(string digits)
    {
        var sum = 0;
        var alternate = false;

        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var n = digits[i] - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9)
                {
                    n -= 9;
                }
            }

            sum += n;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }

    private static bool IsHighEntropySingleWord(string text)
    {
        var trimmed = text.Trim();

        // Must be a single word: no whitespace of any kind (rules out sentences)
        if (trimmed.Any(char.IsWhiteSpace))
            return false;

        // Must be 20 or more characters
        if (trimmed.Length < 20)
            return false;

        // Must NOT flag normal URLs
        if (IsNormalUrl(trimmed))
            return false;

        // Compute Shannon entropy
        var entropy = ComputeShannonEntropy(trimmed);

        // High entropy threshold: >= 3.6 bits/char indicates random cryptographic/binary tokens
        return entropy >= 3.6;
    }

    private static bool IsNormalUrl(string word)
    {
        if (word.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            word.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            word.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(word, UriKind.Absolute, out var uri))
            {
                return true;
            }
        }
        return false;
    }

    public static double ComputeShannonEntropy(string s)
    {
        if (string.IsNullOrEmpty(s))
            return 0.0;

        var charCounts = new Dictionary<char, int>();
        foreach (var c in s)
        {
            charCounts.TryGetValue(c, out var count);
            charCounts[c] = count + 1;
        }

        var length = (double)s.Length;
        var entropy = 0.0;

        foreach (var count in charCounts.Values)
        {
            var p = count / length;
            entropy -= p * Math.Log2(p);
        }

        return entropy;
    }
}
