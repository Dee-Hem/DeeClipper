using DeeClipper.Core.Detection;
using Xunit;

namespace DeeClipper.Tests;

public class SensitiveDetectionTests
{
    [Theory]
    // Luhn-valid test card numbers
    [InlineData("4532 0150 1234 5671")]     // Visa test number (Luhn valid: sum = 50)
    [InlineData("4532-0150-1234-5671")]
    [InlineData("4532015012345671")]
    [InlineData("5425 2334 3010 9903")]     // Mastercard test number (Luhn valid)
    [InlineData("3782 822463 10005")]       // Amex test number (Luhn valid)
    [InlineData("4000 1234 5678 9017")]     // 16-digit test number (Luhn valid: sum = 60)
    public void IsSensitive_LuhnValidCreditCards_FlagsAsSensitive(string card)
    {
        Assert.True(SensitiveDetector.IsSensitive(card));
    }

    [Theory]
    // Luhn-invalid digit sequences
    [InlineData("4532 0150 1234 5678")]     // Changed check digit (invalid Luhn)
    [InlineData("1234 5678 9012 3456")]     // Invalid Luhn
    [InlineData("0000 0000 0000 0000")]     // All zeros
    [InlineData("Invoice #12345678901")]    // 11 digits (under 13)
    public void IsSensitive_LuhnInvalidOrShortDigits_NotFlaggedAsCard(string notCard)
    {
        Assert.False(SensitiveDetector.IsSensitive(notCard));
    }

    [Fact]
    public void IsSensitive_JwtToken_FlagsAsSensitive()
    {
        var jwt = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIn0.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";
        Assert.True(SensitiveDetector.IsSensitive(jwt));
    }

    [Fact]
    public void IsSensitive_PrivateKeyBlock_FlagsAsSensitive()
    {
        var rsaKey = "-----BEGIN RSA PRIVATE KEY-----\nMIIEowIBAAKCAQEA0Y3...\n-----END RSA PRIVATE KEY-----";
        var ecKey = "-----BEGIN EC PRIVATE KEY-----\nMHcCAQEEI...\n-----END EC PRIVATE KEY-----";
        var genericKey = "-----BEGIN PRIVATE KEY-----\nMIIEvgIBADANBgkqhkiG9w0BAQEFAASC...\n-----END PRIVATE KEY-----";

        Assert.True(SensitiveDetector.IsSensitive(rsaKey));
        Assert.True(SensitiveDetector.IsSensitive(ecKey));
        Assert.True(SensitiveDetector.IsSensitive(genericKey));
    }

    [Theory]
    [InlineData("ghp_9876543210abcdef0123456789abcdef")]
    [InlineData("github_pat_11ABCDEF_0123456789abcdef0123456789")]
    [InlineData("sk-proj-abcdef1234567890abcdef1234567890")]
    [InlineData("AKIAIOSFODNN7EXAMPLE")]
    [InlineData("xoxb-1234567890-abcdef123456")]
    [InlineData("xoxp-1234567890-abcdef123456")]
    [InlineData("AIzaSyD0123456789abcdefABCDEF012345")]
    public void IsSensitive_CommonTokenPrefixes_FlagsAsSensitive(string token)
    {
        Assert.True(SensitiveDetector.IsSensitive(token));
    }

    [Fact]
    public void IsSensitive_HighEntropySingleWord_FlagsAsSensitive()
    {
        // High-entropy 32-character random string (e.g. API secret or password)
        var highEntropyWord = "8fA#kL9@zX2$mN5&qR1*wE7!yU4%pB0^";
        Assert.True(SensitiveDetector.IsSensitive(highEntropyWord));
    }

    [Theory]
    // Rule: "Do not flag normal URLs or sentences."
    [InlineData("https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/")]
    [InlineData("https://en.wikipedia.org/wiki/List_of_programming_languages")]
    [InlineData("http://example.org/path/to/resource?query=hello&category=books")]
    [InlineData("https://github.com/dotnet/runtime/blob/main/README.md")]
    public void IsSensitive_NormalUrls_NotFlagged(string url)
    {
        Assert.False(SensitiveDetector.IsSensitive(url));
    }

    [Theory]
    // Rule: "Do not flag normal URLs or sentences."
    [InlineData("The quick brown fox jumps over the lazy dog near the riverbank.")]
    [InlineData("Please remember to review the pull request before the team meeting today.")]
    [InlineData("Welcome to DeeClipper! This is a simple sentence describing the product.")]
    [InlineData("Error code 404: The requested resource was not found on the server.")]
    public void IsSensitive_NormalSentences_NotFlagged(string sentence)
    {
        Assert.False(SensitiveDetector.IsSensitive(sentence));
    }
}
