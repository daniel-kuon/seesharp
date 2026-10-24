using SeeSharp.Content;

namespace SeeSharp.App.Tests;

public class CoursePathsTests
{
    private static readonly string Cwd = Path.GetFullPath(OperatingSystem.IsWindows() ? @"C:\work\tool" : "/work/tool");

    [Fact]
    public void Argument_wins_over_everything()
    {
        var path = CoursePaths.Resolve("course-arg", "course-env", "course-setting", Cwd, _ => true);
        Assert.Equal(Path.GetFullPath("course-arg", Cwd), path);
    }

    [Fact]
    public void Environment_then_setting_then_sibling()
    {
        Assert.Equal(Path.GetFullPath("course-env", Cwd), CoursePaths.Resolve(null, "course-env", "course-setting", Cwd, _ => true));
        Assert.Equal(Path.GetFullPath("course-setting", Cwd), CoursePaths.Resolve(null, "", "course-setting", Cwd, _ => true));
        Assert.Equal(Path.GetFullPath("../seesharp-course", Cwd), CoursePaths.Resolve(null, null, null, Cwd, _ => true));
    }

    [Fact]
    public void No_source_and_no_sibling_means_null()
    {
        Assert.Null(CoursePaths.Resolve(null, null, " ", Cwd, _ => false));
    }

    [Fact]
    public void Explicit_path_is_returned_even_when_missing()
    {
        // The settings page then says which path is wrong instead of silently falling back.
        Assert.Equal(Path.GetFullPath("nope", Cwd), CoursePaths.Resolve("nope", null, null, Cwd, _ => false));
    }

    [Fact]
    public void Book_defaults_next_to_the_course()
    {
        var course = Path.Combine(Cwd, "seesharp-course");
        var inCourse = Path.Combine(course, "book", "csharp14.pdf");
        Assert.Equal(inCourse, CoursePaths.BookPdf(null, course, p => p == inCourse));
        Assert.Equal(Path.Combine(Cwd, "csharp14.pdf"), CoursePaths.BookPdf(null, course, _ => false));
        Assert.Equal(Path.GetFullPath("book.pdf", Cwd), CoursePaths.BookPdf(Path.Combine(Cwd, "book.pdf"), course, _ => true));
    }

    [Fact]
    public void Translated_book_is_used_for_its_locale_when_present()
    {
        var pdf = Path.Combine(Cwd, "csharp14.pdf");
        var english = Path.Combine(Cwd, "csharp14.en.pdf");
        Assert.Equal(english, CoursePaths.LocalizedBook(pdf, "en", p => p == english));
        Assert.Equal(pdf, CoursePaths.LocalizedBook(pdf, "en", _ => false));
        Assert.Equal(pdf, CoursePaths.LocalizedBook(pdf, "de", _ => true));
    }

    [Fact]
    public void Book_link_uses_the_physical_page()
    {
        var reading = new Reading { Section = "5.4.3", PrintedPage = 372 };
        Assert.Equal("book.pdf#page=404", CourseState.BookHref("any.pdf", reading));
        Assert.Null(CourseState.BookHref(null, reading));
    }
}
