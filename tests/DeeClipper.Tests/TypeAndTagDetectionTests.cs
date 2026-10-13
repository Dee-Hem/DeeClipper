using DeeClipper.Core.Detection;
using DeeClipper.Core.Models;
using Xunit;

namespace DeeClipper.Tests;

public class TypeAndTagDetectionTests
{
    [Theory]
    [InlineData("https://github.com", ClipboardItemType.Link)]
    [InlineData("http://example.com/test?id=123", ClipboardItemType.Link)]
    [InlineData("https://learn.microsoft.com/en-us/dotnet/csharp/", ClipboardItemType.Link)]
    [InlineData("Just plain text with no link", ClipboardItemType.Text)]
    [InlineData("Here is a sentence with https://google.com in it", ClipboardItemType.Text)]
    [InlineData("12345", ClipboardItemType.Text)]
    [InlineData("", ClipboardItemType.Text)]
    public void DetectType_ClassifiesLinkAndTextCorrectly(string input, ClipboardItemType expected)
    {
        var result = TypeDetector.DetectType(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("support.team+filter@company.co.uk")]
    [InlineData("john.doe_123@sub.domain.org")]
    public void DetectTags_IdentifiesEmail(string email)
    {
        var tags = TagDetector.DetectTags(email);
        Assert.Contains(DisplayTag.Email, tags);
    }

    [Theory]
    [InlineData("+1-555-123-4567")]
    [InlineData("(555) 234-5678")]
    [InlineData("+44 20 7946 0958")]
    [InlineData("555.345.6789")]
    [InlineData("08012345678")]
    public void DetectTags_IdentifiesPhone(string phone)
    {
        var tags = TagDetector.DetectTags(phone);
        Assert.Contains(DisplayTag.Phone, tags);
    }

    [Theory]
    [InlineData("#FFF")]
    [InlineData("#ffffff")]
    [InlineData("#0D9488")]
    [InlineData("#1E293BFF")]
    [InlineData("#1234")]
    public void DetectTags_IdentifiesColorHex(string color)
    {
        var tags = TagDetector.DetectTags(color);
        Assert.Contains(DisplayTag.Color, tags);
    }

    [Theory]
    [InlineData("42")]
    [InlineData("-17.5")]
    [InlineData("+3.14159")]
    [InlineData("1,000,000")]
    [InlineData("1,234.56")]
    [InlineData("1e-5")]
    public void DetectTags_IdentifiesNumber(string number)
    {
        var tags = TagDetector.DetectTags(number);
        Assert.Contains(DisplayTag.Number, tags);
    }

    [Theory]
    [InlineData("public class ClipboardService {\n    public void Save() {}\n}")]
    [InlineData("function calculateSum(a, b) {\n    return a + b;\n}")]
    [InlineData("SELECT id, name FROM users WHERE active = 1;")]
    [InlineData("import React from 'react';\nconst App = () => <div />;")]
    [InlineData("def process_items(data):\n    for item in data:\n        print(item)")]
    public void DetectTags_IdentifiesCodeHeuristic(string code)
    {
        var tags = TagDetector.DetectTags(code);
        Assert.Contains(DisplayTag.Code, tags);
    }

    [Fact]
    public void DetectTags_NormalSentence_DoesNotFlagSpecialTags()
    {
        var text = "Meeting rescheduled to tomorrow morning in conference room B.";
        var tags = TagDetector.DetectTags(text);
        Assert.Empty(tags);
    }
}
