using System;
using System.IO;
using System.Text;
using Shouldly;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Planning;

public sealed class FileTextTests : IDisposable
{
    private const string Query = "SELECT 'café';";

    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void Read_FileThatIsThere_GivesItsText()
    {
        var path = _folder.WriteFile("Q.sql", "SELECT 1;\n");

        FileText.Read(path).ShouldNotBeNull().Text.ToString().ShouldBe("SELECT 1;\n");
    }

    [Fact]
    public void Read_FileThatIsGone_GivesNull() => FileText.Read(_folder.PathOf("Gone.sql")).ShouldBeNull();

    [Fact]
    public void Read_Folder_GivesNull() => FileText.Read(_folder.CreateFolder("Q.sql")).ShouldBeNull();

    [Fact]
    public void Read_Utf8WithoutMark_IsTheTextAndTheCompilerDoesNotFallBack()
    {
        var path = WriteBytes("Q.sql", Encoding.UTF8.GetBytes(Query));

        var contents = FileText.Read(path).ShouldNotBeNull();

        contents.Text.ToString().ShouldBe(Query);
        contents.CompilerFallsBack.ShouldBeFalse();
    }

    [Fact]
    public void Read_Utf16WithMark_IsTheTextAndTheCompilerDoesNotFallBack()
    {
        var path = WriteBytes("Q.sql", [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(Query)]);

        var contents = FileText.Read(path).ShouldNotBeNull();

        contents.Text.ToString().ShouldBe(Query);
        contents.CompilerFallsBack.ShouldBeFalse();
    }

    [Fact]
    public void Read_Utf8MarkThenAByteThatIsNotUtf8_TheCompilerDoesNotFallBack()
    {
        var path = WriteBytes("Q.sql", [0xEF, 0xBB, 0xBF, .. Encoding.Latin1.GetBytes(Query)]);

        FileText.Read(path).ShouldNotBeNull().CompilerFallsBack.ShouldBeFalse();
    }

    [Fact]
    public void Read_Windows1252_HasAReplacementCharacterAndTheCompilerFallsBack()
    {
        var path = WriteBytes("Q.sql", Encoding.Latin1.GetBytes(Query));

        var contents = FileText.Read(path).ShouldNotBeNull();

        contents.Text.ToString().ShouldBe("SELECT 'caf�';");
        contents.CompilerFallsBack.ShouldBeTrue();
    }

    [Fact]
    public void SqlFileText_FileChangedAfterTheFirstRead_GivesTheFirstText()
    {
        var path = _folder.WriteFile("Q.sql", "SELECT 1;\n");
        var file = new SqlFileText(path);

        file.GetText(TestContext.Current.CancellationToken).ShouldNotBeNull().ToString().ShouldBe("SELECT 1;\n");
        File.WriteAllText(path, "SELECT 2;\n");

        file.GetText(TestContext.Current.CancellationToken).ShouldNotBeNull().ToString().ShouldBe("SELECT 1;\n");
    }

    private string WriteBytes(string relativePath, byte[] bytes)
    {
        var path = _folder.PathOf(relativePath);
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
