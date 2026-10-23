namespace SeeSharp.App.Tests;

/// <summary>Tutor answers come from a model and are rendered as HTML: nothing in them may run script.</summary>
public class TutorRenderTests
{
    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("[klick](javascript:alert(1))")]
    [InlineData("[klick](JaVaScRiPt:alert(1))")]
    [InlineData("![bild](javascript:alert(1))")]
    [InlineData("[klick](data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==)")]
    [InlineData("Text{onclick=alert(1)}\n\n# Titel {onmouseover=alert(1)}")]
    public void Model_output_cannot_inject_script(string markdown)
    {
        var html = Tutor.Render(markdown).Value;

        // Escaped text such as "&lt;img … onerror=…&gt;" is harmless; only real tags and attributes matter.
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img src=\"x\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(@"(?i)<[^>]*\s(href|src)=""\s*(javascript|data|vbscript):", html);
        Assert.DoesNotMatch(@"(?i)<[^>]*\son\w+=", html);
    }

    [Fact]
    public void Ordinary_markdown_and_web_links_still_render()
    {
        var html = Tutor.Render("**fett** `code`\n\n[Doku](https://learn.microsoft.com/dotnet)").Value;

        Assert.Contains("<strong>fett</strong>", html);
        Assert.Contains("<code>code</code>", html);
        Assert.Contains("href=\"https://learn.microsoft.com/dotnet\"", html);
    }
}
