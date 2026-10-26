using System.Threading;
using Shouldly;
using SqlSource.Generation.Chains;
using SqlSource.Settings;
using SqlSource.Settings.TypeOverrides;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generation.Chains;

public sealed class ListedSettingsFileTests
{
    private const string QueriesPath = "/app/Queries/sqlsource.json";

    [Fact]
    public void Read_FileNamedExactly_IsRead()
    {
        var listed = Read(
                QueriesPath, /*lang=json,strict*/
                """{"output": "sql"}"""
            )
            .ShouldNotBeNull();

        listed.Path.ShouldBe(QueriesPath);
        listed.NormalizedFolder.ShouldBe("app/Queries");
        listed.Depth.ShouldBe(2);
        listed.File.Level.Output.ShouldBe(OutputKind.Sql);
        listed.Diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("/app/Queries/SqlSource.json")]
    [InlineData("/app/Queries/SQLSOURCE.JSON")]
    public void Read_FileNamedInAnotherCase_IsNull(string path)
    {
        ListedSettingsFile.IsSettingsFile(path).ShouldBeFalse();
        Read(path, "{}").ShouldBeNull();

        // Only the name's case is wrong: the same folder's file named exactly is read.
        var exact = path[..(path.LastIndexOf('/') + 1)] + "sqlsource.json";
        Read(exact, "{}").ShouldNotBeNull().NormalizedFolder.ShouldBe("app/Queries");
    }

    [Theory]
    [InlineData("/app/Queries/sqlsource.json.bak")]
    [InlineData("/app/Queries/my-sqlsource.json")]
    [InlineData("/app/sqlsource.json/Queries.sql")]
    public void Read_FileNotNamedSettingsFile_IsNull(string path)
    {
        ListedSettingsFile.IsSettingsFile(path).ShouldBeFalse();
        Read(path, "{}").ShouldBeNull();
    }

    [Fact]
    public void Read_FileAtTheRoot_HasDepthZero()
    {
        var listed = Read("/sqlsource.json", "{}").ShouldNotBeNull();

        listed.NormalizedFolder.ShouldBe(string.Empty);
        listed.Depth.ShouldBe(0);
    }

    [Fact]
    public void Read_PathWithBackslashes_IsReadAtItsNormalisedFolder()
    {
        const string Path = @"C:\App\Queries\sqlsource.json";

        var listed = Read(Path, "{}").ShouldNotBeNull();

        ListedSettingsFile.IsSettingsFile(Path).ShouldBeTrue();
        listed.Path.ShouldBe(Path);
        listed.NormalizedFolder.ShouldBe("C:/App/Queries");
        listed.Depth.ShouldBe(3);
    }

    [Fact]
    public void Read_PathThatDoesNotNormalise_IsNull() => Read("../sqlsource.json", "{}").ShouldBeNull();

    [Fact]
    public void Read_TypeOverride_IsAtTheFilesPlace()
    {
        var listed = Read(
                QueriesPath, /*lang=json,strict*/
                """{"typeOverrides": {"int4": "long"}}"""
            )
            .ShouldNotBeNull();

        listed
            .File.Level.TypeOverrides.ShouldNotBeNull()
            .ShouldBe([new TypeOverride("int4", "long", SettingPlace.OfSettingsFile(QueriesPath, 2))]);
    }

    [Fact]
    public void Read_InvalidDatabase_IsNotReportedByTheBuild_AndIsByTheTool()
    {
        const string Text = /*lang=json,strict*/
            """{"database": " a b "}""";

        var build = Read(QueriesPath, Text, SettingsFileSide.Build);
        var tool = Read(QueriesPath, Text, SettingsFileSide.Tool);

        build.ShouldNotBeNull().Diagnostics.ShouldBeEmpty();
        tool.ShouldNotBeNull().Diagnostics.ShouldHaveSingleItem().Descriptor.Id.ShouldBe("SQLSRC019");
    }

    [Theory]
    [InlineData("dialect", "sqlserver2", true, true)]
    [InlineData("output", "bogus", true, true)]
    [InlineData("modelNamespace", "a..b", true, false)]
    [InlineData("collectionType", "lists", true, false)]
    public void Read_InvalidValue_IsReportedByTheSidesThatReadItsSetting(
        string key,
        string value,
        bool build,
        bool tool
    )
    {
        var text = $$"""{"{{key}}": "{{value}}"}""";

        var byBuild = Read(QueriesPath, text, SettingsFileSide.Build).ShouldNotBeNull();
        var byTool = Read(QueriesPath, text, SettingsFileSide.Tool).ShouldNotBeNull();

        byBuild.Diagnostics.Count.ShouldBe(build ? 1 : 0);
        byTool.Diagnostics.Count.ShouldBe(tool ? 1 : 0);
    }

    [Fact]
    public void Read_UnknownKey_IsReportedAtItsLineAndColumn()
    {
        const string Text = /*lang=json,strict*/
            "{\n  \"dialekt\": \"postgres\"\n}";

        var listed = Read(QueriesPath, Text);

        var diagnostic = listed.ShouldNotBeNull().Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.Id.ShouldBe("SQLSRC016");
        diagnostic.Location.Path.ShouldBe(QueriesPath);
        diagnostic.Location.LineSpan.Start.Line.ShouldBe(1);
        diagnostic.Location.LineSpan.Start.Character.ShouldBe(2);
    }

    [Fact]
    public void Read_TextTheCompilerCannotGive_IsInvalidJson()
    {
        var listed = ListedSettingsFile
            .Read(new InMemoryAdditionalText(QueriesPath, null), SettingsFileSide.Build, CancellationToken.None)
            .ShouldNotBeNull();

        listed.File.Root.ShouldBeNull();
        listed.Diagnostics.ShouldHaveSingleItem().Descriptor.Id.ShouldBe("SQLSRC015");
    }

    private static ListedSettingsFile? Read(string path, string text, SettingsFileSide side = SettingsFileSide.Build) =>
        ListedSettingsFile.Read(new InMemoryAdditionalText(path, text), side, CancellationToken.None);
}
