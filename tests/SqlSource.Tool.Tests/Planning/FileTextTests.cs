using System;
using Shouldly;
using SqlSource.Tool.Planning;
using Xunit;

namespace SqlSource.Tool.Tests.Planning;

public sealed class FileTextTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void Read_FileThatIsThere_GivesItsText()
    {
        var path = _folder.WriteFile("Q.sql", "SELECT 1;\n");

        FileText.Read(path).ShouldNotBeNull().ToString().ShouldBe("SELECT 1;\n");
    }

    [Fact]
    public void Read_FileThatIsGone_GivesNull() => FileText.Read(_folder.PathOf("Gone.sql")).ShouldBeNull();

    [Fact]
    public void Read_Folder_GivesNull() => FileText.Read(_folder.CreateFolder("Q.sql")).ShouldBeNull();
}
