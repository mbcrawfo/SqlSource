using Shouldly;
using SqlSource.Tool.Projects;
using Xunit;

namespace SqlSource.Tool.Tests.Projects;

public sealed class ManifestReaderTests
{
    // The example of docs/superpowers/specs/2026-10-09-query-generation-phase-2-3-project-manifest-design.md in
    // version 2: the folders of the build, two files, two settings files and a second Compile item.
    private const string Example = """
        SqlSourceManifest=2
        Project=/work/src/App/App.csproj
        TargetFramework=net10.0
        LangVersion=14.0
        DefineConstants=TRACE;DEBUG;NET;NET10_0;NET8_0_OR_GREATER
        SqlSourceBuildFolders=|/work/src/App/obj/|/work/src/App/bin/
        File=/work/src/App/Queries/Users.sql
        File=/work/src/App/Queries/Orders.sql
        Settings=/work/src/sqlsource.json
        Settings=/work/src/App/sqlsource.json
        Compile=/work/src/App/UserRepository.cs
        Compile=/work/src/App/Program.cs

        """;

    private static ProjectManifest Read(string text)
    {
        var read = ManifestReader.Read(text);
        read.Succeeded.ShouldBeTrue(read.Failure);
        return read.Value.ShouldNotBeNull();
    }

    private static string ReasonOf(string text)
    {
        var read = ManifestReader.Read(text);
        read.Succeeded.ShouldBeFalse();
        return read.Failure.ShouldNotBeNull();
    }

    [Fact]
    public void Read_ExampleOfTheSpec_GivesEveryMember()
    {
        var manifest = Read(Example);

        manifest.ProjectPath.ShouldBe("/work/src/App/App.csproj");
        manifest.TargetFramework.ShouldBe("net10.0");
        manifest.LangVersion.ShouldBe("14.0");
        manifest.DefineConstants.ShouldBe(["TRACE", "DEBUG", "NET", "NET10_0", "NET8_0_OR_GREATER"]);
        manifest.BuildFolders.ShouldBe("|/work/src/App/obj/|/work/src/App/bin/");
        manifest.Files.ShouldBe(["/work/src/App/Queries/Users.sql", "/work/src/App/Queries/Orders.sql"]);
        manifest.SettingsFiles.ShouldBe(["/work/src/sqlsource.json", "/work/src/App/sqlsource.json"]);
        manifest.CompileFiles.ShouldBe(["/work/src/App/UserRepository.cs", "/work/src/App/Program.cs"]);
    }

    [Fact]
    public void Read_OnlyTheVersionAndTheProject_GivesEmptyMembers()
    {
        var manifest = Read("SqlSourceManifest=2\nProject=/work/App.csproj");

        manifest.TargetFramework.ShouldBeEmpty();
        manifest.LangVersion.ShouldBeEmpty();
        manifest.DefineConstants.ShouldBeEmpty();
        manifest.BuildFolders.ShouldBeEmpty();
        manifest.Files.ShouldBeEmpty();
        manifest.SettingsFiles.ShouldBeEmpty();
        manifest.CompileFiles.ShouldBeEmpty();
    }

    // A path is the rest of the line, as a File or a Compile line's is.
    [Fact]
    public void Read_SettingsLine_IsThePathAsItIs() =>
        Read("SqlSourceManifest=2\nProject=p\nSettings=/work/a=b/ sqlsource.json\nSettings=/work/sqlsource.json")
            .SettingsFiles.ShouldBe(["/work/a=b/ sqlsource.json", "/work/sqlsource.json"]);

    [Fact]
    public void Read_ValueThatHoldsAnEqualsSign_IsTheRestOfTheLine()
    {
        var manifest = Read("SqlSourceManifest=2\nProject=/work/a=b/App.csproj\nFile=/work/a=b/x=y.sql\nCompile==.cs");

        manifest.ProjectPath.ShouldBe("/work/a=b/App.csproj");
        manifest.Files.ShouldBe(["/work/a=b/x=y.sql"]);
        manifest.CompileFiles.ShouldBe(["=.cs"]);
    }

    [Fact]
    public void Read_ValueWithSpacesAroundIt_IsKeptAsItIs() =>
        Read("SqlSourceManifest=2\nProject= /work/App.csproj \nFile=/work/a .sql ").Files.ShouldBe(["/work/a .sql "]);

    [Theory]
    [InlineData("TRACE;DEBUG", new[] { "TRACE", "DEBUG" })]
    [InlineData("TRACE,DEBUG", new[] { "TRACE", "DEBUG" })]
    [InlineData(";TRACE;; DEBUG ,;", new[] { "TRACE", "DEBUG" })]
    // The compiler takes a space for a separator too, and so does a tab.
    [InlineData("TRACE DEBUG;A\tB ,C", new[] { "TRACE", "DEBUG", "A", "B", "C" })]
    [InlineData("", new string[0])]
    public void Read_Constants_AreSplitAtSemicolonsCommasAndWhiteSpace(string value, string[] expected) =>
        Read($"SqlSourceManifest=2\nProject=p\nDefineConstants={value}").DefineConstants.ShouldBe(expected);

    [Fact]
    public void Read_KeyThatIsNotKnown_IsPassedOver() =>
        Read("SqlSourceManifest=2\nProject=p\nAssemblyName=App\nFile=a.sql").Files.ShouldBe(["a.sql"]);

    // Version 1 gave the settings of the project and of each file as Property. and File. lines.  The tool reads the
    // settings from the settings files, and such a line, which a later package does not write, is a key it does not
    // know.  A File. line before any File line is no error either.
    [Fact]
    public void Read_LinesOfVersion1Settings_ArePassedOver()
    {
        var manifest = Read(
            "SqlSourceManifest=2\nProject=/p.csproj\nProperty.SqlSourceDialect=postgres\nFile.SqlSourceDialect=y\n"
                + "File=/a.sql\nFile.SqlSourceDialect=x\nSqlSourceBuildFolders=|/p/obj/"
        );

        manifest.Files.ShouldBe(["/a.sql"]);
        manifest.BuildFolders.ShouldBe("|/p/obj/");
    }

    [Fact]
    public void Read_EmptyLines_ArePassedOver() =>
        Read("SqlSourceManifest=2\n\nProject=p\n\n\nCompile=a.cs\n").CompileFiles.ShouldBe(["a.cs"]);

    [Fact]
    public void Read_LinesThatEndWithACarriageReturn_AreReadWithoutIt()
    {
        var manifest = Read("SqlSourceManifest=2\r\nProject=p\r\nFile=a.sql\r\nSqlSourceBuildFolders=|/o/\r\n");

        manifest.ProjectPath.ShouldBe("p");
        manifest.Files.ShouldBe(["a.sql"]);
        manifest.BuildFolders.ShouldBe("|/o/");
    }

    [Fact]
    public void Read_ByteOrderMark_IsPassedOver() => Read("﻿SqlSourceManifest=2\nProject=p").ProjectPath.ShouldBe("p");

    [Fact]
    public void Read_KeyGivenTwice_TakesTheLast() =>
        Read("SqlSourceManifest=2\nProject=p\nProject=q\nSqlSourceBuildFolders=|/a/\nSqlSourceBuildFolders=|/b/")
            .ShouldSatisfyAllConditions(
                manifest => manifest.ProjectPath.ShouldBe("q"),
                manifest => manifest.BuildFolders.ShouldBe("|/b/")
            );

    [Theory]
    [InlineData("")]
    [InlineData("﻿")]
    public void Read_NoText_IsEmpty(string text) => ReasonOf(text).ShouldBe("it is empty");

    [Theory]
    [InlineData("Project=p\nSqlSourceManifest=2")]
    [InlineData("\nSqlSourceManifest=2\nProject=p")]
    [InlineData("sqlsourcemanifest=2\nProject=p")]
    [InlineData( /*lang=json,strict*/
        "{ \"SqlSourceManifest\": 2 }"
    )]
    public void Read_FirstLineThatIsNotTheVersion_SaysSo(string text) =>
        ReasonOf(text).ShouldBe("its first line is not 'SqlSourceManifest=2'");

    [Theory]
    [InlineData("3")]
    [InlineData("")]
    [InlineData("2.0")]
    [InlineData(" 2")]
    public void Read_AnotherVersion_SaysThatThePackageAndTheToolAreOutOfStep(string version) =>
        ReasonOf($"SqlSourceManifest={version}\nProject=p\nthis line has no equals sign")
            .ShouldBe(
                $"it has version '{version}' of the format and this tool reads version 2.  "
                    + "The SqlSource package and the sqlsource tool are out of step: update the older one"
            );

    // A package of version 1 writes the settings into the manifest, which this tool would read as setting nothing.
    [Fact]
    public void Read_ManifestOfVersion1_IsRefusedAsOutOfStep() =>
        ReasonOf("SqlSourceManifest=1\nProject=/p.csproj")
            .ShouldSatisfyAllConditions(
                reason => reason.ShouldContain("version '1'"),
                reason => reason.ShouldContain("out of step")
            );

    [Fact]
    public void Read_LineWithoutAnEqualsSign_NamesTheLine() =>
        ReasonOf("SqlSourceManifest=2\nProject=p\n\nCompile").ShouldBe("line 4 has no '='");

    [Theory]
    [InlineData("SqlSourceManifest=2")]
    [InlineData("SqlSourceManifest=2\nProject=")]
    [InlineData("SqlSourceManifest=2\nFile=a.sql")]
    public void Read_NoProject_SaysSo(string text) => ReasonOf(text).ShouldBe("it names no project");
}
