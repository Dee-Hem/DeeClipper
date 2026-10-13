using DeeClipper.Core.Text;
using Xunit;

namespace DeeClipper.Tests;

public class TextTransformTests
{
    [Fact]
    public void ToUpperCase_TransformsCorrectly()
    {
        var input = "hello world! DeeClipper 123";
        var result = TextTransforms.ToUpperCase(input);
        Assert.Equal("HELLO WORLD! DEECLIPPER 123", result);
    }

    [Fact]
    public void ToLowerCase_TransformsCorrectly()
    {
        var input = "HELLO WORLD! DeeClipper 123";
        var result = TextTransforms.ToLowerCase(input);
        Assert.Equal("hello world! deeclipper 123", result);
    }

    [Fact]
    public void ToTitleCase_TransformsCorrectly()
    {
        var input = "the quick brown fox jumps over the lazy dog";
        var result = TextTransforms.ToTitleCase(input);
        Assert.Equal("The Quick Brown Fox Jumps Over The Lazy Dog", result);
    }

    [Fact]
    public void ToSingleLine_RemovesLineBreaksAndCollapsesSpaces()
    {
        var input = "Line 1\r\nLine 2\nLine 3\rLine 4   with   spaces";
        var result = TextTransforms.ToSingleLine(input);
        Assert.Equal("Line 1 Line 2 Line 3 Line 4 with spaces", result);
    }

    [Fact]
    public void Trim_TrimsLeadingAndTrailingWhitespace()
    {
        var input = "   Hello World   ";
        var result = TextTransforms.Trim(input);
        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void ToPlainText_NormalizesLineEndingsAndStripsControlChars()
    {
        var input = "Hello\x0007\x0008World\r\nSecond Line";
        var result = TextTransforms.ToPlainText(input);
        Assert.DoesNotContain("\x0007", result, StringComparison.Ordinal);
        Assert.DoesNotContain("\x0008", result, StringComparison.Ordinal);
        Assert.Contains("HelloWorld", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(
        "https://example.com/product?id=42&utm_source=twitter&utm_medium=social&utm_campaign=spring_sale",
        "https://example.com/product?id=42"
    )]
    [InlineData(
        "https://example.com/page?fbclid=IwAR123456789&gclid=EAIaIQobChMI12345&article=99#comments",
        "https://example.com/page?article=99#comments"
    )]
    [InlineData(
        "https://example.com/search?utm_term=shoes&utm_content=banner",
        "https://example.com/search"
    )]
    [InlineData(
        "Check this link https://example.com/post?id=1&utm_source=rss for more details!",
        "Check this link https://example.com/post?id=1 for more details!"
    )]
    public void StripTrackingParameters_RemovesUtmAndSocialTrackers(string input, string expected)
    {
        var result = TextTransforms.StripTrackingParameters(input);
        Assert.Equal(expected, result);
    }
}
