using System;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Projects;
using SqlSource.Tool.Reporting;
using Xunit;

namespace SqlSource.Tool.Tests;

// The tool's reader of [SqlSourceGenerate] works on syntax: it has no compilation and follows no name.
public sealed class AttributeReaderTests : IDisposable
{
    private const string PathPrefix = "[SqlSourceGenerate(Path = ";
    private const string OutputPrefix = "[SqlSourceGenerate(Output = ";

    private readonly TempFolder _folder = new();

    private string Source => _folder.PathOf("App/Repo/Queries.cs");

    public void Dispose() => _folder.Dispose();

    private ProjectManifest Manifest(string langVersion, string[] constants, params string[] compile) =>
        new()
        {
            ProjectPath = _folder.PathOf("App/App.csproj"),
            TargetFramework = "net10.0",
            LangVersion = langVersion,
            DefineConstants = [.. constants],
            Properties = [],
            Files = [],
            CompileFiles = [.. compile],
        };

    private ProjectClaims Read(string source, string langVersion = "", params string[] constants)
    {
        _ = _folder.WriteFile("App/Repo/Queries.cs", source);
        return AttributeReader.Read(Manifest(langVersion, constants, Source));
    }

    // The attribute lists start on the third line, in its first column.
    private static string OnAClass(string attributes) =>
        $"using SqlSource;\n\n{attributes}\ninternal static partial class Queries;\n";

    [Theory]
    [InlineData("[SqlSourceGenerate]")]
    [InlineData("[SqlSourceGenerateAttribute]")]
    [InlineData("[SqlSourceGenerate()]")]
    [InlineData("[SqlSource.SqlSourceGenerate]")]
    [InlineData("[global::SqlSource.SqlSourceGenerateAttribute]")]
    [InlineData("[type: SqlSourceGenerate]")]
    [InlineData("[System.Serializable, SqlSourceGenerate]")]
    [InlineData("[System.Serializable]\n[SqlSourceGenerate]")]
    public void Read_AttributeHoweverItIsNamed_IsAClaimThatSetsNothing(string attributes)
    {
        var claims = Read(OnAClass(attributes));

        claims.Errors.ShouldBeEmpty();
        var claim = claims.Claims.ShouldHaveSingleItem();
        claim.SourcePath.ShouldBe(Source);
        claim.Path.ShouldBeNull();
        claim.Output.ShouldBeNull();
    }

    [Fact]
    public void Read_Attribute_GivesWhereItStarts() =>
        Read(OnAClass("[System.Serializable, SqlSourceGenerate(Path = \"Q\")]"))
            .Claims.ShouldHaveSingleItem()
            .Position.ShouldBe(new LinePosition(2, "[System.Serializable, ".Length));

    [Theory]
    [InlineData("internal partial class Queries { }")]
    [InlineData("internal partial struct Queries { }")]
    [InlineData("internal partial record Queries;")]
    [InlineData("internal partial record struct Queries;")]
    [InlineData("internal readonly partial record struct Queries(int Id);")]
    public void Read_ClassStructOrRecord_IsAClaim(string declaration) =>
        Read("using SqlSource;\n[SqlSourceGenerate]\n" + declaration + "\n").Claims.Length.ShouldBe(1);

    [Fact]
    public void Read_NestedType_IsAClaim() =>
        Read(
            """
            namespace App
            {
                internal partial class Outer
                {
                    [SqlSource.SqlSourceGenerate(Path = "Inner")]
                    private partial class Inner;
                }
            }
            """
        )
            .Claims.ShouldHaveSingleItem()
            .Path.ShouldBe("Inner");

    [Theory]
    [InlineData("[assembly: SqlSource.SqlSourceGenerate]\ninternal partial class Queries;")]
    [InlineData("[SqlSource.SqlSourceGenerate]\ninternal partial interface IQueries;")]
    [InlineData("[SqlSource.SqlSourceGenerate]\ninternal enum Kind { One }")]
    [InlineData("[SqlSource.SqlSourceGenerate]\ninternal delegate void Run();")]
    [InlineData("internal partial class Queries { [SqlSource.SqlSourceGenerate] private void Run() { } }")]
    [InlineData("[method: SqlSource.SqlSourceGenerate]\ninternal partial class Queries;")]
    [InlineData("[SqlSource.Generate]\ninternal partial class Queries; // SqlSourceGenerate")]
    public void Read_AttributeElsewhereOrOfAnotherName_IsNoClaim(string source)
    {
        var claims = Read(source);

        claims.Claims.ShouldBeEmpty();
        claims.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Read_TwoAttributesOnOneDeclaration_TakesTheFirst() =>
        Read(OnAClass("[SqlSourceGenerate(Path = \"A\")]\n[SqlSourceGenerate(Path = \"B\")]"))
            .Claims.ShouldHaveSingleItem()
            .Path.ShouldBe("A");

    [Theory]
    [InlineData("\"Queries\"", "Queries")]
    [InlineData("@\"Queries\\Users.sql\"", "Queries\\Users.sql")]
    [InlineData("\"\"\"Queries/Users.sql\"\"\"", "Queries/Users.sql")]
    [InlineData("\"Qu\\u0065ries\"", "Queries")]
    [InlineData("\" \"", " ")]
    [InlineData("\"\"", null)]
    [InlineData("null", null)]
    [InlineData("default", null)]
    public void Read_PathAsALiteral_IsItsValue(string expression, string? expected)
    {
        var claims = Read(OnAClass(PathPrefix + expression + ")]"));

        claims.Errors.ShouldBeEmpty();
        claims.Claims.ShouldHaveSingleItem().Path.ShouldBe(expected);
    }

    [Theory]
    [InlineData("GeneratorOutput.Sql", "Sql")]
    [InlineData("GeneratorOutput.Models", "Models")]
    [InlineData("GeneratorOutput.CodeGen", "CodeGen")]
    [InlineData("SqlSource.GeneratorOutput.Models", "Models")]
    [InlineData("global::SqlSource.GeneratorOutput.CodeGen", "CodeGen")]
    // The compiler rejects a member that does not exist.
    [InlineData("GeneratorOutput.Other", null)]
    public void Read_OutputAsAMember_IsThatMember(string expression, string? expected)
    {
        var claims = Read(OnAClass(OutputPrefix + expression + ")]"));

        claims.Errors.ShouldBeEmpty();
        var output = claims.Claims.ShouldHaveSingleItem().Output;
        // Not "output?.ToString().ShouldBe": that would assert nothing for an output that is not set.
        (output?.ToString()).ShouldBe(expected);
    }

    [Theory]
    [InlineData("Folder")]
    [InlineData("nameof(Queries)")]
    [InlineData("\"a\" + \"b\"")]
    [InlineData("$\"Queries\"")]
    [InlineData("(\"Queries\")")]
    [InlineData("default(string)")]
    public void Read_PathThatIsNoLiteral_IsSqlsrc208AtTheExpressionAndNoClaim(string expression)
    {
        var claims = Read(OnAClass(PathPrefix + expression + ")]"));

        claims.Claims.ShouldBeEmpty();
        claims.Errors.ShouldBe([
            ToolDiagnostic.At(
                ToolDiagnostics.AttributeArgumentNotLiteral,
                Source,
                new LinePosition(2, PathPrefix.Length),
                "Path"
            ),
        ]);
    }

    [Theory]
    [InlineData("(GeneratorOutput)1")]
    [InlineData("Level")]
    [InlineData("default")]
    [InlineData("GeneratorOutput.Models | GeneratorOutput.CodeGen")]
    [InlineData("Other.Models")]
    public void Read_OutputThatIsNoMember_IsSqlsrc208AtTheExpressionAndNoClaim(string expression)
    {
        var claims = Read(OnAClass(OutputPrefix + expression + ")]"));

        claims.Claims.ShouldBeEmpty();
        claims.Errors.ShouldBe([
            ToolDiagnostic.At(
                ToolDiagnostics.AttributeArgumentNotLiteral,
                Source,
                new LinePosition(2, OutputPrefix.Length),
                "Output"
            ),
        ]);
    }

    [Fact]
    public void Read_BothArgumentsNoLiterals_IsSqlsrc208ForEach() =>
        Read(OnAClass("[SqlSourceGenerate(Path = Folder, Output = Level)]")).Errors.Length.ShouldBe(2);

    [Fact]
    public void Read_OtherArgumentsOfTheAttribute_AreNotRead() =>
        Read(OnAClass("[SqlSourceGenerate(Parameters = Words, ModelNamespace = nameof(App), Path = \"Q\")]"))
            .Errors.ShouldBeEmpty();

    // Review focus 2.
    [Fact]
    public void Read_ArgumentsTheReaderDoesNotExpect_AreSteppedOverAndTheFirstPathIsRead()
    {
        var claims = Read(OnAClass("[SqlSourceGenerate(\"positional\", Path = \"A\", Path = \"B\", Unknown = 3)]"));

        claims.Errors.ShouldBeEmpty();
        claims.Claims.ShouldHaveSingleItem().Path.ShouldBe("A");
    }

    // Review focus 1.
    [Fact]
    public void Read_FileThatDoesNotParse_GivesTheClaimsItCanRead() =>
        Read("[SqlSource.SqlSourceGenerate(Path = \"Q\")]\ninternal partial class Queries {\n    void Broken( {\n")
            .Claims.ShouldHaveSingleItem()
            .Path.ShouldBe("Q");

    // The text is searched for the word before anything is parsed, so a spelling that hides it is not found.
    [Fact]
    public void Read_FileWithoutTheWord_IsNotParsed() =>
        Read(OnAClass("[SqlSource\\u0047enerate]")).Claims.ShouldBeEmpty();

    [Fact]
    public void Read_FileThatIsNotOnTheDisk_IsSteppedOver()
    {
        var claims = AttributeReader.Read(Manifest("", [], _folder.PathOf("App/Gone.cs")));

        claims.Claims.ShouldBeEmpty();
        claims.Errors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(new[] { "FEATURE" }, 1)]
    [InlineData(new[] { "OTHER" }, 0)]
    public void Read_AttributeInsideIf_IsReadWhenTheManifestHasTheConstant(string[] constants, int expected) =>
        Read("#if FEATURE\n[SqlSource.SqlSourceGenerate]\n#endif\ninternal partial class Queries;\n", "", constants)
            .Claims.Length.ShouldBe(expected);

    // The constants of a framework are in a manifest because its target depends on the SDK's target that adds them.
    [Fact]
    public void Read_AttributeInsideIfOfAFramework_IsReadWithTheConstantsOfTheManifest() =>
        Read(
            "#if NET8_0_OR_GREATER\n[SqlSource.SqlSourceGenerate]\n#endif\ninternal partial class Queries;\n",
            "",
            "TRACE",
            "NET",
            "NET10_0",
            "NET8_0_OR_GREATER"
        )
            .Claims.Length.ShouldBe(1);

    [Theory]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("preview")]
    [InlineData("default")]
    [InlineData("12.0")]
    [InlineData("no version")]
    public void Read_AnyLangVersion_IsRead(string langVersion) =>
        Read(OnAClass("[SqlSourceGenerate]"), langVersion).Claims.Length.ShouldBe(1);

    [Fact]
    public void Read_FileOfTheNewestCSharp_IsRead() =>
        Read(
            """
            [SqlSource.SqlSourceGenerate(Path = "Queries")]
            internal partial class Repository(int size)
            {
                private readonly int[] _sizes = [1, 2, .. new[] { 3 }];

                public int Size
                {
                    get => field;
                    set => field = value < 0 ? size : value;
                }
            }
            """
        )
            .Claims.ShouldHaveSingleItem()
            .Path.ShouldBe("Queries");
}
