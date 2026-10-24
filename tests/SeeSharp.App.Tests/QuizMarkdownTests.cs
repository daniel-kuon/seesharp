namespace SeeSharp.App.Tests;

/// <summary>Quiz prompts may hold code listings; options use inline code.</summary>
public class QuizMarkdownTests
{
    [Fact]
    public void Prompt_renders_code_blocks()
    {
        var html = CourseState.MarkdownText("Was gibt das aus?\n\n```csharp\nint x = 1;\n```").Value;
        Assert.Contains("<pre><code", html);
        Assert.Contains("int x = 1;", html);
    }

    [Fact]
    public void Option_renders_inline_without_paragraph()
    {
        Assert.Equal("<code>var e = [1, 2, 3];</code> kompiliert",
            CourseState.MarkdownInline("`var e = [1, 2, 3];` kompiliert").Value);
    }
}
