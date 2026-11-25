using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Projects;
using SqlSource.Tool.Reporting;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Planning;

// The tool's reader of [SqlSourceGenerate] works on syntax: it has no compilation, and follows a name only through
// a using alias.
public sealed class AttributeReaderTests : IDisposable
{
    private const string SourcePrefix = "[SqlSourceGenerate(Source = ";

    private readonly TempFolder _folder = new();

    private string Source => _folder.PathOf("App/Repo/Queries.cs");

    public void Dispose() => _folder.Dispose();

    private ProjectManifest Manifest(string langVersion, string[] constants, params string[] compile) =>
        Manifest(langVersion, constants, compile, []);

    private ProjectManifest Manifest(
        string langVersion,
        string[] constants,
        string[] compile,
        UsingAlias[] usingAliases
    ) =>
        new()
        {
            ProjectPath = _folder.PathOf("App/App.csproj"),
            TargetFramework = "net10.0",
            LangVersion = langVersion,
            DefineConstants = [.. constants],
            BuildFolders = "",
            Files = [],
            SettingsFiles = [],
            CompileFiles = [.. compile],
            UsingAliases = [.. usingAliases],
        };

    private ProjectClaims Read(string source, string langVersion = "", params string[] constants)
    {
        _ = _folder.WriteFile("App/Repo/Queries.cs", source);
        return AttributeReader.Read(Manifest(langVersion, constants, Source), TestContext.Current.CancellationToken);
    }

    // The file is written in Windows-1252, which is not UTF-8 once it holds a letter outside ASCII.
    private ProjectClaims ReadLatin1(string source)
    {
        _ = _folder.CreateFolder("App/Repo");
        File.WriteAllBytes(Source, Encoding.Latin1.GetBytes(source));
        return AttributeReader.Read(Manifest("", [], Source), TestContext.Current.CancellationToken);
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
        claim.Source.ShouldBeNull();
    }

    [Fact]
    public void Read_Attribute_GivesWhereItStarts() =>
        Read(OnAClass("[System.Serializable, SqlSourceGenerate(Source = \"Q\")]"))
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
                    [SqlSource.SqlSourceGenerate(Source = "Inner")]
                    private partial class Inner;
                }
            }
            """
        )
            .Claims.ShouldHaveSingleItem()
            .Source.ShouldBe("Inner");

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

    // Several files of App/, in the order given, with the project's Using items that have an alias.
    private ProjectClaims ReadFiles(UsingAlias[] usingAliases, params (string Name, string Text)[] files)
    {
        var paths = files.Select(file => _folder.WriteFile("App/" + file.Name, file.Text)).ToArray();
        return AttributeReader.Read(Manifest("", [], paths, usingAliases), TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("using Gen = SqlSource.SqlSourceGenerateAttribute;", "[Gen(Source = \"Q\")]")]
    [InlineData("using Gen = global::SqlSource.SqlSourceGenerateAttribute;", "[Gen(Source = \"Q\")]")]
    [InlineData("using Gen = SqlSourceGenerateAttribute;", "[Gen(Source = \"Q\")]")]
    [InlineData("using GenAttribute = SqlSource.SqlSourceGenerateAttribute;", "[Gen(Source = \"Q\")]")]
    [InlineData("using GenAttribute = SqlSource.SqlSourceGenerateAttribute;", "[GenAttribute(Source = \"Q\")]")]
    [InlineData("using Gen = SqlSource.SqlSourceGenerateAttribute;", "[System.Serializable, Gen(Source = \"Q\")]")]
    [InlineData("using Gen = SqlSource.SqlSourceGenerateAttribute;", "[type: Gen(Source = \"Q\")]")]
    public void Read_AliasOfTheAttributeInTheFile_IsAClaim(string directive, string attributes) =>
        Read($"{directive}\n\n{attributes}\ninternal static partial class Queries;\n")
            .Claims.ShouldHaveSingleItem()
            .Source.ShouldBe("Q");

    // An alias of another type is no claim, and [GenAttribute] does not name the alias Gen.
    [Theory]
    [InlineData("using Gen = System.SerializableAttribute; // SqlSourceGenerate", "[Gen]")]
    [InlineData("using Gen = SqlSource.SqlSourceGenerateAttribute;", "[GenAttribute]")]
    [InlineData("using Gen = SqlSource.SqlSourceGenerateAttribute<int>;", "[Gen]")]
    [InlineData("using Gen = SqlSource.SqlSourceGenerateAttribute;", "[Other.Gen]")]
    [InlineData("using Gen = SqlSource.SqlSourceGenerateAttribute;", "[method: Gen]")]
    public void Read_AliasOfAnotherTypeOrNameOfAnother_IsNoClaim(string directive, string attributes)
    {
        var claims = Read($"{directive}\n\n{attributes}\ninternal static partial class Queries;\n");

        claims.Claims.ShouldBeEmpty();
        claims.Errors.ShouldBeEmpty();
    }

    // An alias in a namespace reaches the types of that namespace alone, and one of the same name there hides the
    // file's.
    [Fact]
    public void Read_AliasInANamespace_ReachesThatNamespaceAndHidesTheFilesAlias() =>
        Read(
            """
            using Gen = SqlSource.SqlSourceGenerateAttribute;

            namespace Inside
            {
                using Own = SqlSource.SqlSourceGenerateAttribute;

                [Own(Source = "Inside")]
                internal partial class InsideQueries;

                namespace Deeper
                {
                    [Own(Source = "Deeper")]
                    internal partial class DeeperQueries;
                }
            }

            namespace Hidden
            {
                using Gen = System.SerializableAttribute;

                [Gen(Source = "Hidden")]
                internal partial class HiddenQueries;
            }

            namespace Outside
            {
                [Own(Source = "Outside")]
                internal partial class OutsideQueries;

                [Gen(Source = "File")]
                internal partial class FileQueries;
            }
            """
        )
            .Claims.Select(static claim => claim.Source)
            .ShouldBe(["Inside", "Deeper", "File"]);

    [Fact]
    public void Read_AliasInAFileScopedNamespace_IsAClaim() =>
        Read(
            """
            namespace App;

            using Gen = SqlSource.SqlSourceGenerateAttribute;

            [Gen(Source = "Q")]
            internal partial class Queries;
            """
        )
            .Claims.ShouldHaveSingleItem()
            .Source.ShouldBe("Q");

    // A global using of one file reaches every file of the project, one that never holds the word too, and the claims
    // stay in the order of the files.
    [Fact]
    public void Read_GlobalAliasOfAnotherFile_ReachesEveryFileInTheirOrder() =>
        ReadFiles(
            [],
            ("First.cs", "[Gen(Source = \"First\")]\ninternal partial class First;\n"),
            (
                "Usings.cs",
                "global using Gen = SqlSource.SqlSourceGenerateAttribute;\n\n"
                    + "[SqlSource.SqlSourceGenerate(Source = \"Second\")]\ninternal partial class Second;\n"
            ),
            ("Third.cs", "[GenAttribute(Source = \"Third\")]\ninternal partial class Third;\n"),
            ("Fourth.cs", "[Other(Source = \"Fourth\")]\ninternal partial class Fourth;\n")
        )
            .Claims.Select(static claim => claim.Source)
            .ShouldBe(["First", "Second"]);

    // A global using named GenAttribute is written [Gen], so a file that holds only "Gen" is read.
    [Fact]
    public void Read_GlobalAliasWithTheSuffix_ReachesAFileThatWritesItWithout() =>
        ReadFiles(
            [],
            ("Usings.cs", "global using GenAttribute = SqlSource.SqlSourceGenerateAttribute;\n"),
            ("Queries.cs", "[Gen(Source = \"Q\")]\ninternal partial class Queries;\n")
        )
            .Claims.ShouldHaveSingleItem()
            .Source.ShouldBe("Q");

    [Fact]
    public void Read_GlobalAliasInsideAnIfThatDoesNotHold_IsNoClaim() =>
        ReadFiles(
            [],
            ("Usings.cs", "#if FEATURE\nglobal using Gen = SqlSource.SqlSourceGenerateAttribute;\n#endif\n"),
            ("Queries.cs", "[Gen(Source = \"Q\")]\ninternal partial class Queries;\n")
        )
            .Claims.ShouldBeEmpty();

    // A Using item of the project with an alias is a global using that no Compile file holds.
    [Theory]
    [InlineData("SqlSource.SqlSourceGenerateAttribute", 1)]
    [InlineData("global::SqlSource.SqlSourceGenerateAttribute", 1)]
    [InlineData("System.SerializableAttribute", 0)]
    [InlineData("not a name", 0)]
    public void Read_UsingItemWithAnAlias_IsAnAliasOfEveryFile(string target, int expected) =>
        ReadFiles([new UsingAlias("Gen", target)], ("Queries.cs", "[Gen]\ninternal partial class Queries;\n"))
            .Claims.Length.ShouldBe(expected);

    // The compiler rejects the second attribute of a type, and the generator reads the first, in the order of the
    // files.
    [Fact]
    public void Read_AttributeOnTwoPartialDeclarations_IsReadAtTheFirst() =>
        ReadFiles(
            [],
            ("A.cs", "namespace App.Data;\n[SqlSource.SqlSourceGenerate(Source = \"A\")]\ninternal partial class Q;\n"),
            (
                "B.cs",
                "namespace App { namespace Data {\n"
                    + "[SqlSource.SqlSourceGenerate(Source = \"B\")]\ninternal partial class Q; } }\n"
            )
        )
            .Claims.ShouldHaveSingleItem()
            .Source.ShouldBe("A");

    // A declaration of the type with no attribute, first in the order of the files, takes nothing from the one after
    // it.
    [Fact]
    public void Read_PartialDeclarationWithoutTheAttributeFirst_IsNoClaimAndTheNextIs() =>
        ReadFiles(
            [],
            ("A.cs", "// SqlSourceGenerate\ninternal partial class Q;\n"),
            ("B.cs", "[SqlSource.SqlSourceGenerate(Source = \"B\")]\ninternal partial class Q;\n")
        )
            .Claims.ShouldHaveSingleItem()
            .Source.ShouldBe("B");

    // An alias of the file reaches that file alone, though the file is read for the project's aliases.
    [Fact]
    public void Read_AliasOfAFile_DoesNotReachAnotherFile() =>
        ReadFiles(
            [],
            (
                "A.cs",
                "using Gen = SqlSource.SqlSourceGenerateAttribute;\n[Gen(Source = \"A\")]\ninternal partial class A;\n"
            ),
            ("B.cs", "[Gen(Source = \"B\")]\ninternal partial class B;\n")
        )
            .Claims.ShouldHaveSingleItem()
            .Source.ShouldBe("A");

    // The second is not read at all, so a first that is not read leaves the type with no claim.
    [Fact]
    public void Read_AttributeOnTwoPartialDeclarationsTheFirstNotALiteral_IsSqlsrc208AndNoClaim()
    {
        var claims = ReadFiles(
            [],
            ("A.cs", "[SqlSource.SqlSourceGenerate(Source = nameof(Q))]\ninternal partial class Q;\n"),
            ("B.cs", "[SqlSource.SqlSourceGenerate(Source = \"B\")]\ninternal partial class Q;\n")
        );

        claims.Claims.ShouldBeEmpty();
        claims.Errors.ShouldHaveSingleItem().Descriptor.ShouldBe(ToolDiagnostics.AttributeArgumentNotLiteral);
    }

    // Types that differ in a namespace, an arity, the type around them, or a file of their own, are not one type.
    [Theory]
    [InlineData("namespace A;\n", "namespace B;\n", "", "", "")]
    [InlineData("", "", "", "<T>", "")]
    [InlineData("", "", "", "", "internal partial class Outer {\n")]
    [InlineData("", "", "file ", "", "")]
    public void Read_AttributeOnTwoTypesOfOneName_IsAClaimOfEach(
        string firstNamespace,
        string secondNamespace,
        string modifier,
        string typeParameters,
        string outer
    ) =>
        ReadFiles(
            [],
            (
                "A.cs",
                firstNamespace
                    + $"[SqlSource.SqlSourceGenerate(Source = \"A\")]\n{modifier}partial class Q{typeParameters};\n"
            ),
            (
                "B.cs",
                secondNamespace
                    + outer
                    + $"[SqlSource.SqlSourceGenerate(Source = \"B\")]\n{modifier}partial class Q;\n"
                    + (outer.Length > 0 ? "}\n" : "")
            )
        )
            .Claims.Select(static claim => claim.Source)
            .ShouldBe(["A", "B"]);

    [Fact]
    public void Read_TwoAttributesOnOneDeclaration_TakesTheFirst() =>
        Read(OnAClass("[SqlSourceGenerate(Source = \"A\")]\n[SqlSourceGenerate(Source = \"B\")]"))
            .Claims.ShouldHaveSingleItem()
            .Source.ShouldBe("A");

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
        var claims = Read(OnAClass(SourcePrefix + expression + ")]"));

        claims.Errors.ShouldBeEmpty();
        claims.Claims.ShouldHaveSingleItem().Source.ShouldBe(expected);
    }

    [Theory]
    [InlineData("\"Requêtes\"")]
    [InlineData("@\"Requêtes\"")]
    [InlineData("\"\"\"Requêtes\"\"\"")]
    public void Read_PathOutsideAsciiInAFileThatIsNotUtf8_IsSqlsrc271AtThePathAndNoClaim(string expression)
    {
        var claims = ReadLatin1(OnAClass(SourcePrefix + expression + ")]"));

        claims.Claims.ShouldBeEmpty();
        claims.Errors.ShouldBe([
            ToolDiagnostic.At(ToolDiagnostics.FileNotUtf8, Source, new LinePosition(2, SourcePrefix.Length), Source),
        ]);
    }

    [Theory]
    [InlineData("internal static partial class Queries;", "Queries")]
    [InlineData("internal partial class Repo<T>;", "Repo")]
    [InlineData("internal partial class @event;", "event")]
    [InlineData("internal partial class \\u0041pp;", "App")]
    [InlineData("internal partial record struct Point;", "Point")]
    public void Read_Type_GivesItsName(string declaration, string expected) =>
        Read($"using SqlSource;\n\n[SqlSourceGenerate]\n{declaration}\n")
            .Claims.ShouldHaveSingleItem()
            .TypeName.ShouldBe(expected);

    [Fact]
    public void Read_NestedType_GivesItsOwnName() =>
        Read(
            "using SqlSource;\n\ninternal partial class Outer\n{\n    [SqlSourceGenerate]\n"
                + "    private partial class Inner;\n}\n"
        )
            .Claims.ShouldHaveSingleItem()
            .TypeName.ShouldBe("Inner");

    // On Windows the compiler reads the name in the system's code page, so the file named for it would not be the one
    // the build looks for.
    [Fact]
    public void Read_NameOutsideAsciiWithNoSourceInAFileThatIsNotUtf8_IsSqlsrc271AtTheNameAndNoClaim()
    {
        var claims = ReadLatin1("using SqlSource;\n\n[SqlSourceGenerate]\ninternal partial class Café;\n");

        claims.Claims.ShouldBeEmpty();
        claims.Errors.ShouldBe([
            ToolDiagnostic.At(ToolDiagnostics.FileNotUtf8, Source, new LinePosition(3, 23), Source),
        ]);
    }

    // The name wherever it stands: after a record's class or struct, after a comment, or with the letter outside ASCII
    // first.  Each position is the name's first character.
    [Theory]
    [InlineData("internal partial record class Café;", 30)]
    [InlineData("internal partial record struct Café;", 31)]
    [InlineData("internal partial class /* x */ Café;", 31)]
    [InlineData("internal partial class Écrit;", 23)]
    [InlineData("internal partial class Caf\u00e9<T>;", 23)]
    public void Read_NameOutsideAsciiWrittenAnyWayInAFileThatIsNotUtf8_IsSqlsrc271AtTheName(
        string declaration,
        int column
    )
    {
        var claims = ReadLatin1($"using SqlSource;\n\n[SqlSourceGenerate]\n{declaration}\n");

        claims.Claims.ShouldBeEmpty();
        claims.Errors.ShouldBe([
            ToolDiagnostic.At(ToolDiagnostics.FileNotUtf8, Source, new LinePosition(3, column), Source),
        ]);
    }

    // A name whose first letter is outside ASCII and that is not on the keyword's line, or that follows an @, leaves no
    // identifier at all: the error is at that letter.
    [Theory]
    [InlineData("internal partial class\n    Écrit;", 4, 4)]
    [InlineData("internal partial class\r\n    Écrit;", 4, 4)]
    [InlineData("internal partial class // c\n    Écrit;", 4, 4)]
    [InlineData("internal partial class @Écrit;", 3, 24)]
    [InlineData("internal partial record class\n    Écrit;", 4, 4)]
    public void Read_NameThatLeavesNoIdentifierInAFileThatIsNotUtf8_IsSqlsrc271AtItsFirstLetterOutsideAscii(
        string declaration,
        int line,
        int column
    )
    {
        var claims = ReadLatin1($"using SqlSource;\n\n[SqlSourceGenerate]\n{declaration}\n");

        claims.Claims.ShouldBeEmpty();
        claims.Errors.ShouldBe([
            ToolDiagnostic.At(ToolDiagnostics.FileNotUtf8, Source, new LinePosition(line, column), Source),
        ]);
    }

    // A declaration with no name at all, and nothing outside ASCII, is the compiler's error, not this one.
    [Fact]
    public void Read_DeclarationWithNoNameInAFileThatIsNotUtf8_IsNotSqlsrc271()
    {
        var claims = ReadLatin1("// café\nusing SqlSource;\n\n[SqlSourceGenerate]\ninternal partial class\n{\n}\n");

        claims.Errors.ShouldBeEmpty();
    }

    // What follows the name is not the name, a comment outside ASCII included.
    [Theory]
    [InlineData("internal partial class Queries/*é*/;")]
    [InlineData("internal partial class Queries /* é */ { }")]
    [InlineData("internal partial class Queries<T> where T : struct { /* é */ }")]
    public void Read_AsciiNameFollowedByTextOutsideAsciiInAFileThatIsNotUtf8_IsAClaim(string declaration)
    {
        var claims = ReadLatin1($"using SqlSource;\n\n[SqlSourceGenerate]\n{declaration}\n");

        claims.Errors.ShouldBeEmpty();
        claims.Claims.ShouldHaveSingleItem().TypeName.ShouldBe("Queries");
    }

    [Fact]
    public void Read_NameOutsideAsciiWithASourceInAFileThatIsNotUtf8_IsAClaim()
    {
        var claims = ReadLatin1(
            "using SqlSource;\n\n[SqlSourceGenerate(Source = \".\")]\ninternal partial class Café;\n"
        );

        claims.Errors.ShouldBeEmpty();
        claims.Claims.ShouldHaveSingleItem().Source.ShouldBe(".");
    }

    [Fact]
    public void Read_NameEscapedInAFileThatIsNotUtf8_IsAClaimWithThatName()
    {
        var claims = ReadLatin1(
            "// café\nusing SqlSource;\n\n[SqlSourceGenerate]\ninternal partial class Caf\\u00e9;\n"
        );

        claims.Errors.ShouldBeEmpty();
        claims.Claims.ShouldHaveSingleItem().TypeName.ShouldBe("Café");
    }

    [Fact]
    public void Read_AsciiPathInAFileThatIsNotUtf8_IsAClaim()
    {
        var claims = ReadLatin1("// café\n" + OnAClass("[SqlSourceGenerate(Source = \"Q\")]"));

        claims.Errors.ShouldBeEmpty();
        claims.Claims.ShouldHaveSingleItem().Source.ShouldBe("Q");
    }

    [Fact]
    public void Read_PathEscapedInAFileThatIsNotUtf8_IsAClaimWithThatPath()
    {
        var claims = ReadLatin1("// café\n" + OnAClass(SourcePrefix + "\"Requ\\u00eates\")]"));

        claims.Errors.ShouldBeEmpty();
        claims.Claims.ShouldHaveSingleItem().Source.ShouldBe("Requêtes");
    }

    [Fact]
    public void Read_PathOutsideAsciiInUtf8_IsAClaimWithThatPath()
    {
        var claims = Read(OnAClass(SourcePrefix + "\"Requêtes\")]"));

        claims.Errors.ShouldBeEmpty();
        claims.Claims.ShouldHaveSingleItem().Source.ShouldBe("Requêtes");
    }

    [Fact]
    public void Read_NameOutsideAsciiInUtf8_IsAClaimWithThatName()
    {
        var claims = Read("using SqlSource;\n\n[SqlSourceGenerate]\ninternal partial class Café;\n");

        claims.Errors.ShouldBeEmpty();
        claims.Claims.ShouldHaveSingleItem().TypeName.ShouldBe("Café");
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
        var claims = Read(OnAClass(SourcePrefix + expression + ")]"));

        claims.Claims.ShouldBeEmpty();
        claims.Errors.ShouldBe([
            ToolDiagnostic.At(
                ToolDiagnostics.AttributeArgumentNotLiteral,
                Source,
                new LinePosition(2, SourcePrefix.Length),
                "Source"
            ),
        ]);
    }

    // The attribute has no Output, and the compiler rejects one: the reader steps over it however it is written.
    [Theory]
    [InlineData("Output = GeneratorOutput.Sql")]
    [InlineData("Output = (GeneratorOutput)1")]
    public void Read_OutputArgument_IsIgnoredWithoutError(string argument)
    {
        var claims = Read(OnAClass("[SqlSourceGenerate(Source = \"a.sql\", " + argument + ")]"));

        claims.Errors.ShouldBeEmpty();
        claims.Claims.ShouldHaveSingleItem().Source.ShouldBe("a.sql");
    }

    [Fact]
    public void Read_PathThatIsNoLiteralBesideAnOutput_IsSqlsrc208OfThePathAlone() =>
        Read(OnAClass("[SqlSourceGenerate(Source = Folder, Output = Level)]"))
            .Errors.ShouldBe([
                ToolDiagnostic.At(
                    ToolDiagnostics.AttributeArgumentNotLiteral,
                    Source,
                    new LinePosition(2, SourcePrefix.Length),
                    "Source"
                ),
            ]);

    [Fact]
    public void Read_OtherArgumentsOfTheAttribute_AreNotRead() =>
        Read(OnAClass("[SqlSourceGenerate(Parameters = Words, ModelNamespace = nameof(App), Source = \"Q\")]"))
            .Errors.ShouldBeEmpty();

    [Fact]
    public void Read_ArgumentsTheReaderDoesNotExpect_AreSteppedOverAndTheFirstSourceIsRead()
    {
        var claims = Read(OnAClass("[SqlSourceGenerate(\"positional\", Source = \"A\", Source = \"B\", Unknown = 3)]"));

        claims.Errors.ShouldBeEmpty();
        claims.Claims.ShouldHaveSingleItem().Source.ShouldBe("A");
    }

    [Fact]
    public void Read_FileThatDoesNotParse_GivesTheClaimsItCanRead() =>
        Read("[SqlSource.SqlSourceGenerate(Source = \"Q\")]\ninternal partial class Queries {\n    void Broken( {\n")
            .Claims.ShouldHaveSingleItem()
            .Source.ShouldBe("Q");

    // The text is searched for the word before anything is parsed, so a spelling that hides it is not found.
    [Fact]
    public void Read_FileWithoutTheWord_IsNotParsed() =>
        Read(OnAClass("[SqlSource\\u0047enerate]")).Claims.ShouldBeEmpty();

    [Fact]
    public void Read_FileThatIsNotOnTheDisk_IsSteppedOver()
    {
        var claims = AttributeReader.Read(
            Manifest("", [], _folder.PathOf("App/Gone.cs")),
            TestContext.Current.CancellationToken
        );

        claims.Claims.ShouldBeEmpty();
        claims.Errors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(new[] { "FEATURE" }, 1)]
    [InlineData(new[] { "OTHER" }, 0)]
    public void Read_AttributeInsideIf_IsReadWhenTheManifestHasTheConstant(string[] constants, int expected) =>
        Read("#if FEATURE\n[SqlSource.SqlSourceGenerate]\n#endif\ninternal partial class Queries;\n", "", constants)
            .Claims.Length.ShouldBe(expected);

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
            [SqlSource.SqlSourceGenerate(Source = "Queries")]
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
            .Source.ShouldBe("Queries");
}
