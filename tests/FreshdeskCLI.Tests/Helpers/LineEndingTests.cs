using Xunit;

namespace FreshdeskCLI.Tests.Helpers;

public class LineEndingTests
{
    [Theory]
    [InlineData("Line 1\r\nLine 2\r\nLine 3", "Line 1\nLine 2\nLine 3")]
    [InlineData("Line 1\rLine 2\rLine 3", "Line 1\nLine 2\nLine 3")]
    [InlineData("Line 1\nLine 2\nLine 3", "Line 1\nLine 2\nLine 3")]
    [InlineData("Mixed\r\nLine\rEndings\nHere", "Mixed\nLine\nEndings\nHere")]
    [InlineData("", "")]
    [InlineData("Single line with no endings", "Single line with no endings")]
    [InlineData("\r\n\r\n", "\n\n")]
    public void NormalizeLineEndings_ConvertsToUnixLineEndings(string input, string expected)
    {
        var result = NormalizeLineEndings(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NormalizeLineEndings_PreservesContent()
    {
        var markdownContent = "# Bug Fix\r\n\r\n## Changes\r\n- Item 1\r\n- Item 2\r\n\r\n```csharp\r\nvar x = 1;\r\n```";
        var expected = "# Bug Fix\n\n## Changes\n- Item 1\n- Item 2\n\n```csharp\nvar x = 1;\n```";

        var result = NormalizeLineEndings(markdownContent);

        Assert.Equal(expected, result);
    }

    private static string NormalizeLineEndings(string text)
    {
        return text.Replace("\r\n", "\n").Replace("\r", "\n");
    }
}
