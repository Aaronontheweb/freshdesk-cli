using FreshdeskCLI.Services;

namespace FreshdeskCLI.Tests.Services;

public class MarkdownConversionTests
{
    [Fact]
    public void ConvertsBoldMarkdownToHtml()
    {
        var html = FreshdeskApiClient.ConvertMarkdownToHtml("This is **important** text.");

        Assert.Contains("<strong>important</strong>", html);
    }

    [Fact]
    public void ConvertsListsToHtml()
    {
        var html = FreshdeskApiClient.ConvertMarkdownToHtml("- first\n- second");

        Assert.Contains("<ul>", html);
        Assert.Contains("<li>first", html);
        Assert.Contains("<li>second", html);
    }

    [Fact]
    public void ConvertsParagraphsToFreshdeskSafeDivs()
    {
        var html = FreshdeskApiClient.ConvertMarkdownToHtml("First paragraph.\n\nSecond paragraph.");

        Assert.Contains("<div>First paragraph.<br><br></div>", html);
        Assert.Contains("<div>Second paragraph.<br><br></div>", html);
        Assert.DoesNotContain("<p>", html);
        Assert.DoesNotContain("</p>", html);
    }

    [Fact]
    public void ConvertsLinksToHtml()
    {
        var html = FreshdeskApiClient.ConvertMarkdownToHtml("[docs](https://example.com)");

        Assert.Contains("<a href=\"https://example.com\">docs</a>", html);
    }

    [Fact]
    public void ConvertsCodeBlocksToHtml()
    {
        var html = FreshdeskApiClient.ConvertMarkdownToHtml("```\nvar x = 1;\n```");

        Assert.Contains("<pre><code>", html);
    }

    [Theory]
    [InlineData("[docs](https://example.com)", "href=\"https://example.com\"")]
    [InlineData("[docs](http://example.com)", "href=\"http://example.com\"")]
    [InlineData("[mail](mailto:support@example.com)", "href=\"mailto:support@example.com\"")]
    [InlineData("[relative](docs/page.html)", "href=\"docs/page.html\"")]
    public void PreservesLinks(string markdown, string expectedFragment)
    {
        var html = FreshdeskApiClient.ConvertMarkdownToHtml(markdown);

        Assert.Contains(expectedFragment, html);
    }

    // DisableHtml escapes raw HTML in the input rather than passing it through.
    // This keeps example payloads (e.g. when explaining a security issue to a
    // customer) intact as readable text, and lets Freshdesk's own server-side
    // sanitizer be the single authority on what HTML reaches the recipient.
    [Theory]
    [InlineData("<img src=x onerror=alert(1)>", "<img")]
    [InlineData("<a href=javascript:alert(1)>x</a>", "<a href")]
    [InlineData("<script >alert(1)</script >", "<script")]
    [InlineData("<script>alert(1)", "<script")]
    [InlineData("<iframe src=\"https://evil.example\"></iframe>", "<iframe")]
    [InlineData("<svg/onload=alert(1)>", "<svg")]
    public void EscapesRawHtmlInMarkdownInput(string payload, string forbiddenRawTag)
    {
        var html = FreshdeskApiClient.ConvertMarkdownToHtml(payload);

        Assert.DoesNotContain(forbiddenRawTag, html);
        Assert.Contains("&lt;", html);
    }

    [Fact]
    public void EscapesRawHtmlButStillRendersSurroundingMarkdown()
    {
        var html = FreshdeskApiClient.ConvertMarkdownToHtml("Hello **world** <script>alert(1)</script>");

        Assert.Contains("<strong>world</strong>", html);
        Assert.DoesNotContain("<script", html);
        Assert.Contains("&lt;script", html);
    }
}
