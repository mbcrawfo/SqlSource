// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using System.Text.Json.Nodes;
using Shouldly;
using SqlSource.Settings;
using SqlSource.Testing;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generator;

// The name of the class that holds a query's members under the member location nested-class: the query's or the file
// header's -- nested-class:, else the nestedClass of the chain of the .sql file's folder, which these tests give in the
// settings file of that folder, else Sql.
public sealed class NestedClassTests
{
    // The settings file of the folder of the .sql file, GeneratorHarness.UsersPath, which is also the type's.
    private const string FilesFolderSettingsPath = "/app/Repo/sqlsource.json";

    [Fact]
    public void Run_NestedClassOfTheFilesFolder_NamesTheClass()
    {
        var run = Run(
            """
            using SqlSource;
            namespace App;
            [SqlSourceGenerate(Source = ".")]
            public partial class Sample
            {
                public static string Get() => Queries.GetUser;
            }
            """,
            "Queries"
        );

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources[GeneratorHarness.SampleFile].ShouldContain("private static partial class Queries\n");
        run.Sources[GeneratorHarness.SampleFile].ShouldNotContain("class Sql");
    }

    [Fact]
    public void Run_NestedClassMarker_WinsOverTheFilesFolder()
    {
        var run = Run(
            """
            using SqlSource;
            namespace App;
            [SqlSourceGenerate(Source = ".")]
            public partial class Sample
            {
                public static string Get() => Statements.GetUser;
            }
            """,
            "Queries",
            StatementsFile
        );

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources[GeneratorHarness.SampleFile].ShouldContain("private static partial class Statements\n");
    }

    [Fact]
    public void Run_NestedClassMarkerWithoutTheFilesFolder_NamesTheClass()
    {
        var run = Run(
            """
            using SqlSource;
            namespace App;
            [SqlSourceGenerate(Source = ".")]
            public partial class Sample
            {
                public static string Get() => Statements.GetUser;
            }
            """,
            null,
            StatementsFile
        );

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
    }

    // A folder-wide nestedClass beside a file whose members go on the type is normal.
    [Fact]
    public void Run_NestedClassAndClassLocation_IsIgnored()
    {
        var run = Run(
            """
            using SqlSource;
            namespace App;
            [SqlSourceGenerate(Source = ".")]
            public partial class Sample
            {
                public static string Get() => GetUser;
            }
            """,
            "Queries",
            new SqlFile(
                GeneratorHarness.UsersPath,
                "-- member-location: class\n-- nested-class: Statements\n\n" + SqlTexts.GetUser
            )
        );

        run.ShouldBeClean();
        run.Sources[GeneratorHarness.SampleFile].ShouldNotContain("static partial class");
        run.Sources[GeneratorHarness.SampleFile].ShouldContain("public const string GetUser = ");
    }

    // The member named Sql is no problem once the class has another name.
    [Fact]
    public void Run_MemberNamedSqlAndAnotherNestedClass_IsAllowed()
    {
        var run = Run(
            """
            using SqlSource;
            namespace App;
            [SqlSourceGenerate(Source = ".")]
            public partial class Sample
            {
                public static string Sql => Queries.GetUser;
            }
            """,
            "Queries"
        );

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("public partial class Sample { private int Queries; }", "Sample")]
    [InlineData("public partial class Sample<Queries> { }", "Sample")]
    [InlineData("public static partial class Queries { }", "Queries")]
    public void Run_NameOfTheFilesFolderTaken_IsAnErrorAtTheAttribute(string declaration, string name)
    {
        var run = Run(
            $$"""
            using SqlSource;
            namespace App;
            [SqlSourceGenerate(Source = ".")]
            {{declaration}}
            """,
            "Queries"
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC007 /app/Repo/Sample.cs(3,2)-(3,33): '{name}' already has a member named 'Queries', which a file "
                + "of it names as its nested class.  Rename the member, or set -- nested-class: or "
                + "-- member-location: in the file.",
        ]);
        run.Sources.Values.ShouldAllBe(text => !text.Contains("GetUser"));
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Run_NameOfTheMarkerTaken_IsAnErrorAtTheAttribute()
    {
        var run = Run(
            """
            using SqlSource;
            namespace App;
            [SqlSourceGenerate(Source = ".")]
            public partial class Sample
            {
                public void Statements() { }
            }
            """,
            null,
            StatementsFile
        );

        run.Diagnostics.ShouldBe([
            "SQLSRC007 /app/Repo/Sample.cs(3,2)-(3,33): 'Sample' already has a member named 'Statements', which a "
                + "file of it names as its nested class.  Rename the member, or set -- nested-class: or "
                + "-- member-location: in the file.",
        ]);
        run.Sources[GeneratorHarness.SampleFile].ShouldNotContain("GetUser");
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Run_QueryNamedLikeTheNestedClass_IsAnErrorAtTheQuery()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [
                new SourceFile(
                    GeneratorHarness.SourcePath,
                    """
                    using SqlSource;
                    namespace App;
                    [SqlSourceGenerate(Source = ".")]
                    public partial class Sample { }
                    """
                ),
            ],
            [new SqlFile(GeneratorHarness.UsersPath, "-- name: Queries\nSELECT 1;\n"), FilesFolder("Queries")],
            settings: GeneratorHarness.InNestedClass
        );

        run.Diagnostics.ShouldHaveSingleItem()
            .ShouldEndWith(
                "A query of 'Sample' cannot be named 'Queries', because its member would have the name of the type "
                    + "that contains it"
            );
    }

    // The marker's value is not a class name, so the file is left out, and the type keeps its empty class.
    [Theory]
    [InlineData("class")]
    [InlineData("My-Sql")]
    [InlineData("App.Sql")]
    [InlineData("1Sql")]
    public void Run_NestedClassMarkerThatIsNotValid_IsAnErrorAtTheMarker(string value)
    {
        var run = Run(
            """
            using SqlSource;
            namespace App;
            [SqlSourceGenerate(Source = ".")]
            public partial class Sample { }
            """,
            null,
            new SqlFile(GeneratorHarness.UsersPath, "-- nested-class: " + value + "\n\n" + SqlTexts.GetUser)
        );

        run.Diagnostics.ShouldHaveSingleItem()
            .ShouldStartWith($"SQLSRC111 {GeneratorHarness.UsersPath}(1,18)-(1,{18 + value.Length}): ");
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources[GeneratorHarness.SampleFile].ShouldContain("private static partial class Sql\n");
        run.Sources[GeneratorHarness.SampleFile].ShouldNotContain("GetUser");
    }

    // The file with -- nested-class: Statements in its header.
    private static readonly SqlFile StatementsFile = new(
        GeneratorHarness.UsersPath,
        "-- nested-class: Statements\n\n" + SqlTexts.GetUser
    );

    // The source with the file, Users unless another is given, and the nested class, or none, in the settings file of
    // the file's folder.  The members go in the nested class, the default.
    private static GeneratorRun Run(string source, string? nestedClass, SqlFile? file = null) =>
        GeneratorHarness.Run(
            OutputKind.Sql,
            [new SourceFile(GeneratorHarness.SourcePath, source)],
            nestedClass is null
                ? [file ?? GeneratorHarness.UsersFile]
                : [file ?? GeneratorHarness.UsersFile, FilesFolder(nestedClass)],
            settings: GeneratorHarness.InNestedClass
        );

    // The settings file of the file's folder, which names the nested class.
    private static SqlFile FilesFolder(string nestedClass) =>
        GeneratorHarness.SettingsFile(
            FilesFolderSettingsPath,
            GeneratorHarness.Json(new JsonObject { ["nestedClass"] = nestedClass })
        );
}
