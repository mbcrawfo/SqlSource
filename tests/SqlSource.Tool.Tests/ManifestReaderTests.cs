using System.Linq;
using Shouldly;
using SqlSource.Tool.Projects;
using Xunit;

namespace SqlSource.Tool.Tests;

public class ManifestReaderTests
{
    // The example of the spec of sub-phase 2.3, with every line the target writes.
    private const string Example = """
        SqlSourceManifest=1
        Project=/work/src/App/App.csproj
        TargetFramework=net10.0
        LangVersion=14.0
        DefineConstants=TRACE;DEBUG;NET;NET10_0;NET8_0_OR_GREATER
        Property.SqlSourceDialect=postgres
        Property.SqlSourceDatabase=
        Property.SqlSourceOutput=
        Property.SqlSourceGeneratorParameters=sort-input no-token-validation
        Property.SqlSourceInputModelSuffix=
        Property.SqlSourceOutputModelSuffix=
        Property.SqlSourceModelNamespace=
        Property.SqlSourceInputModelType=
        Property.SqlSourceOutputModelType=
        Property.SqlSourceCollectionType=
        File=/work/src/App/Queries/Users.sql
        File.SqlSourceDialect=
        File.SqlSourceDatabase=billing
        File.SqlSourceOutput=
        File.SqlSourceGeneratorParameters=
        File.SqlSourceInputModelSuffix=
        File.SqlSourceOutputModelSuffix=
        File.SqlSourceModelNamespace=
        File.SqlSourceInputModelType=
        File.SqlSourceOutputModelType=
        File.SqlSourceCollectionType=
        File=/work/src/App/Queries/Orders.sql
        File.SqlSourceDialect=mysql, no-backslash-escapes
        File.SqlSourceDatabase=
        Compile=/work/src/App/UserRepository.cs
        Compile=/work/src/App/Program.cs

        """;

    private static ProjectManifest Read(string text)
    {
        var manifest = ManifestReader.Read(text, out var reason).ShouldNotBeNull(reason);
        reason.ShouldBeEmpty();
        return manifest;
    }

    private static string ReasonOf(string text)
    {
        ManifestReader.Read(text, out var reason).ShouldBeNull();
        return reason;
    }

    [Fact]
    public void Read_ExampleOfTheSpec_GivesEveryMember()
    {
        var manifest = Read(Example);

        manifest.ProjectPath.ShouldBe("/work/src/App/App.csproj");
        manifest.TargetFramework.ShouldBe("net10.0");
        manifest.LangVersion.ShouldBe("14.0");
        manifest.DefineConstants.ShouldBe(["TRACE", "DEBUG", "NET", "NET10_0", "NET8_0_OR_GREATER"]);
        manifest
            .Properties.OrderBy(pair => pair.Key)
            .Select(pair => (pair.Key, pair.Value))
            .ShouldBe([
                ("SqlSourceDialect", "postgres"),
                ("SqlSourceGeneratorParameters", "sort-input no-token-validation"),
            ]);
        manifest.Files.Length.ShouldBe(2);
        manifest.Files[0].Path.ShouldBe("/work/src/App/Queries/Users.sql");
        manifest.Files[0].Metadata.ShouldHaveSingleItem().ShouldBe(new("SqlSourceDatabase", "billing"));
        manifest.Files[1].Path.ShouldBe("/work/src/App/Queries/Orders.sql");
        manifest
            .Files[1]
            .Metadata.ShouldHaveSingleItem()
            .ShouldBe(new("SqlSourceDialect", "mysql, no-backslash-escapes"));
        manifest.CompileFiles.ShouldBe(["/work/src/App/UserRepository.cs", "/work/src/App/Program.cs"]);
    }

    [Fact]
    public void Read_OnlyTheVersionAndTheProject_GivesEmptyMembers()
    {
        var manifest = Read("SqlSourceManifest=1\nProject=/work/App.csproj");

        manifest.TargetFramework.ShouldBeEmpty();
        manifest.LangVersion.ShouldBeEmpty();
        manifest.DefineConstants.ShouldBeEmpty();
        manifest.Properties.ShouldBeEmpty();
        manifest.Files.ShouldBeEmpty();
        manifest.CompileFiles.ShouldBeEmpty();
    }

    [Fact]
    public void Read_ValueThatHoldsAnEqualsSign_IsTheRestOfTheLine()
    {
        var manifest = Read("SqlSourceManifest=1\nProject=/work/a=b/App.csproj\nFile=/work/a=b/x=y.sql\nCompile==.cs");

        manifest.ProjectPath.ShouldBe("/work/a=b/App.csproj");
        manifest.Files.ShouldHaveSingleItem().Path.ShouldBe("/work/a=b/x=y.sql");
        manifest.CompileFiles.ShouldBe(["=.cs"]);
    }

    [Fact]
    public void Read_ValueWithSpacesAroundIt_IsKeptAsItIs() =>
        Read("SqlSourceManifest=1\nProject= /work/App.csproj \nFile=/work/a .sql ")
            .Files.ShouldHaveSingleItem()
            .Path.ShouldBe("/work/a .sql ");

    [Theory]
    [InlineData("TRACE;DEBUG", new[] { "TRACE", "DEBUG" })]
    [InlineData("TRACE,DEBUG", new[] { "TRACE", "DEBUG" })]
    [InlineData(";TRACE;; DEBUG ,;", new[] { "TRACE", "DEBUG" })]
    [InlineData("", new string[0])]
    public void Read_Constants_AreSplitAtSemicolonsAndCommas(string value, string[] expected) =>
        Read($"SqlSourceManifest=1\nProject=p\nDefineConstants={value}").DefineConstants.ShouldBe(expected);

    [Fact]
    public void Read_KeyThatIsNotKnown_IsPassedOver()
    {
        var manifest = Read(
            "SqlSourceManifest=1\nProject=p\nAssemblyName=App\nFile=a.sql\nFile.SqlSourceLater=x\n"
                + "Property.SqlSourceLater=y"
        );

        // A name under a prefix is kept: a later sub-phase reads the ones it knows.
        manifest.Files.ShouldHaveSingleItem().Metadata["SqlSourceLater"].ShouldBe("x");
        manifest.Properties["SqlSourceLater"].ShouldBe("y");
    }

    [Fact]
    public void Read_EmptyLines_ArePassedOver() =>
        Read("SqlSourceManifest=1\n\nProject=p\n\n\nCompile=a.cs\n").CompileFiles.ShouldBe(["a.cs"]);

    [Fact]
    public void Read_LinesThatEndWithACarriageReturn_AreReadWithoutIt()
    {
        var manifest = Read("SqlSourceManifest=1\r\nProject=p\r\nFile=a.sql\r\nFile.SqlSourceDialect=mssql\r\n");

        manifest.ProjectPath.ShouldBe("p");
        manifest.Files.ShouldHaveSingleItem().Metadata["SqlSourceDialect"].ShouldBe("mssql");
    }

    [Fact]
    public void Read_ByteOrderMark_IsPassedOver() => Read("﻿SqlSourceManifest=1\nProject=p").ProjectPath.ShouldBe("p");

    [Fact]
    public void Read_KeyGivenTwice_TakesTheLast() =>
        Read("SqlSourceManifest=1\nProject=p\nProject=q\nProperty.SqlSourceDialect=a\nProperty.SqlSourceDialect=b")
            .ShouldSatisfyAllConditions(
                manifest => manifest.ProjectPath.ShouldBe("q"),
                manifest => manifest.Properties["SqlSourceDialect"].ShouldBe("b")
            );

    [Theory]
    [InlineData("")]
    [InlineData("﻿")]
    public void Read_NoText_IsEmpty(string text) => ReasonOf(text).ShouldBe("it is empty");

    [Theory]
    [InlineData("Project=p\nSqlSourceManifest=1")]
    [InlineData("\nSqlSourceManifest=1\nProject=p")]
    [InlineData("sqlsourcemanifest=1\nProject=p")]
    [InlineData( /*lang=json,strict*/
        "{ \"SqlSourceManifest\": 1 }"
    )]
    public void Read_FirstLineThatIsNotTheVersion_SaysSo(string text) =>
        ReasonOf(text).ShouldBe("its first line is not 'SqlSourceManifest=1'");

    [Theory]
    [InlineData("2")]
    [InlineData("")]
    [InlineData("1.0")]
    [InlineData(" 1")]
    public void Read_AnotherVersion_SaysThatThePackageAndTheToolAreOutOfStep(string version) =>
        ReasonOf($"SqlSourceManifest={version}\nProject=p\nthis line has no equals sign")
            .ShouldBe(
                $"it has version '{version}' of the format and this tool reads version 1.  "
                    + "The SqlSource package and the sqlsource tool are out of step: update the older one"
            );

    [Fact]
    public void Read_LineWithoutAnEqualsSign_NamesTheLine() =>
        ReasonOf("SqlSourceManifest=1\nProject=p\n\nCompile").ShouldBe("line 4 has no '='");

    [Fact]
    public void Read_MetadataBeforeAnyFile_NamesTheLine() =>
        ReasonOf("SqlSourceManifest=1\nProject=p\nFile.SqlSourceDialect=mssql\nFile=a.sql")
            .ShouldBe("line 3 gives metadata of a file before any file");

    [Theory]
    [InlineData("SqlSourceManifest=1")]
    [InlineData("SqlSourceManifest=1\nProject=")]
    [InlineData("SqlSourceManifest=1\nFile=a.sql")]
    public void Read_NoProject_SaysSo(string text) => ReasonOf(text).ShouldBe("it names no project");
}
