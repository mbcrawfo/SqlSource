// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Settings;
using SqlSource.Testing;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generator;

// The problems of the attributed type.  A type with any of them gets no generated file, but for a member named like a
// nested class of its queries (SQLSRC007), which leaves out the files that use that class.
public sealed class TypeDiagnosticsTests
{
    // The files every project gets, and a type with a problem adds none.
    private static readonly string[] AttributeAndExceptions =
    [
        GeneratedFile.EmbeddedAttributeName,
        "SqlSourceGenerateAttribute.g.cs",
        "SqlSource-Exceptions.g.cs",
    ];

    [Fact]
    public void Run_TypeThatIsNotPartial_IsAnErrorAtItsName()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;
            namespace App;
            [SqlSourceGenerate(Source = ".")]
            public class Sample { }
            """,
            GeneratorHarness.UsersFile
        );

        run.Diagnostics.ShouldBe([
            "SQLSRC001 /app/Repo/Sample.cs(4,14)-(4,20): 'Sample' must be declared partial so that SqlSource can add "
                + "members to it",
        ]);
        run.Sources.Keys.ShouldBe(AttributeAndExceptions, ignoreOrder: true);
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Run_ContainingTypesThatAreNotPartial_AreEachAnError()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;
            namespace App;
            public class Outer
            {
                public partial struct Middle
                {
                    public record @class
                    {
                        [SqlSourceGenerate(Source = ".")]
                        public partial class Sample { }
                    }
                }
            }
            """,
            GeneratorHarness.UsersFile
        );

        run.Diagnostics.ShouldBe([
            "SQLSRC001 /app/Repo/Sample.cs(3,14)-(3,19): 'Outer' must be declared partial so that SqlSource can add "
                + "members to it",
            "SQLSRC001 /app/Repo/Sample.cs(7,23)-(7,29): 'class' must be declared partial so that SqlSource can add "
                + "members to it",
        ]);
        run.Sources.Keys.ShouldBe(AttributeAndExceptions, ignoreOrder: true);
    }

    [Fact]
    public void Run_FileLocalType_IsAnErrorAtItsName()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;
            namespace App;
            [SqlSourceGenerate(Source = ".")]
            file partial class Sample { }
            """,
            GeneratorHarness.UsersFile
        );

        run.Diagnostics.ShouldBe([
            "SQLSRC002 /app/Repo/Sample.cs(4,20)-(4,26): 'Sample' is a file-local type, and SqlSource cannot add "
                + "members to it",
        ]);
        run.Sources.Keys.ShouldBe(AttributeAndExceptions, ignoreOrder: true);
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Run_TypeNestedInAFileLocalType_IsAnErrorAtTheFileLocalType()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;
            namespace App;
            file static partial class Outer
            {
                [SqlSourceGenerate(Source = ".")]
                public partial class Sample { }
            }
            """,
            GeneratorHarness.UsersFile
        );

        run.Diagnostics.ShouldBe([
            "SQLSRC002 /app/Repo/Sample.cs(3,27)-(3,32): 'Outer' is a file-local type, and SqlSource cannot add "
                + "members to it",
        ]);
        run.Sources.Keys.ShouldBe(AttributeAndExceptions, ignoreOrder: true);
    }

    [Fact]
    public void Run_ProjectThatTargetsAFrameworkOlderThanNet8_IsAnErrorAtEachAttribute()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [
                new SourceFile(
                    GeneratorHarness.SourcePath,
                    """
                    using SqlSource;
                    namespace App;
                    [SqlSourceGenerate]
                    public partial class First { }
                    [SqlSourceGenerate]
                    public partial class Second { }
                    """
                ),
            ],
            // With no references the compiler cannot read a string argument, so the types take the files of their
            // names.
            [
                new SqlFile("/app/Repo/First.sql", SqlTexts.GetUser),
                new SqlFile("/app/Repo/Second.sql", SqlTexts.GetUser),
            ],
            supportedFramework: false
        );

        run.Diagnostics.ShouldBe(
            [
                "SQLSRC003 /app/Repo/Sample.cs(3,2)-(3,19): SqlSource generates code for .NET 8 and later, and this "
                    + "project targets an older framework",
                "SQLSRC003 /app/Repo/Sample.cs(5,2)-(5,19): SqlSource generates code for .NET 8 and later, and this "
                    + "project targets an older framework",
            ],
            ignoreOrder: true
        );
        run.Sources.Keys.ShouldBe(AttributeAndExceptions, ignoreOrder: true);
    }

    [Theory]
    [InlineData(LanguageVersion.CSharp7_3, "7.3")]
    [InlineData(LanguageVersion.CSharp8, "8.0")]
    [InlineData(LanguageVersion.CSharp9, "9.0")]
    [InlineData(LanguageVersion.CSharp11, "11.0")]
    public void Run_ProjectOnALanguageVersionOlderThanCSharp12_IsAnErrorAtEachAttribute(
        LanguageVersion languageVersion,
        string version
    )
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [
                new SourceFile(
                    GeneratorHarness.SourcePath,
                    """
                    using SqlSource;
                    namespace App
                    {
                        [SqlSourceGenerate(Source = ".")]
                        public partial class First { }
                        [SqlSourceGenerate(Source = "Users.sql")]
                        public partial class Second { }
                    }
                    """
                ),
            ],
            [GeneratorHarness.UsersFile],
            languageVersion: languageVersion
        );

        run.Diagnostics.ShouldBe(
            [
                "SQLSRC012 /app/Repo/Sample.cs(4,6)-(4,37): SqlSource generates C# 12 code, and this project's "
                    + $"language version is {version}",
                "SQLSRC012 /app/Repo/Sample.cs(6,6)-(6,45): SqlSource generates C# 12 code, and this project's "
                    + $"language version is {version}",
            ],
            ignoreOrder: true
        );
        run.Sources.Keys.ShouldBe(AttributeAndExceptions, ignoreOrder: true);

        // Nothing was generated for the types, so the compiler has no generated code to reject.
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Run_ProjectOnAnOlderFrameworkAndLanguageVersion_ReportsTheFrameworkOnly()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [
                new SourceFile(
                    GeneratorHarness.SourcePath,
                    """
                    using SqlSource;
                    [SqlSourceGenerate]
                    public partial class Sample { }
                    """
                ),
            ],
            // With no references the compiler cannot read a string argument, so the type takes the file of its name.
            [new SqlFile("/app/Repo/Sample.sql", SqlTexts.GetUser)],
            supportedFramework: false,
            languageVersion: LanguageVersion.CSharp7_3
        );

        run.Diagnostics.ShouldBe([
            "SQLSRC003 /app/Repo/Sample.cs(2,2)-(2,19): SqlSource generates code for .NET 8 and later, and this "
                + "project targets an older framework",
        ]);
        run.Sources.Keys.ShouldBe(AttributeAndExceptions, ignoreOrder: true);
    }

    [Theory]
    [InlineData("Missing.sql")]
    [InlineData("Missing")]
    [InlineData("Sub")]
    [InlineData("../../../Users.sql")]
    [InlineData(GeneratorHarness.UsersPath)]
    public void Run_SourceThatMatchesNothing_IsAnErrorAtTheAttribute(string path)
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            $$"""
            using SqlSource;
            namespace App;
            [SqlSourceGenerate(Source = "{{path}}")]
            public partial class Sample { }
            """,
            GeneratorHarness.UsersFile,
            new SqlFile("/app/Repo/Sub/Deeper/Orders.sql", "SELECT 2;\n")
        );

        run.Diagnostics.ShouldHaveSingleItem()
            .ShouldBe(
                $"SQLSRC004 /app/Repo/Sample.cs(3,2)-(3,{32 + path.Length}): Source '{path}' matches no .sql file.  It "
                    + "is relative to the folder of this file; a value that ends in .sql is one file, and any other "
                    + "value is a folder."
            );
        run.Sources.Keys.ShouldBe(AttributeAndExceptions, ignoreOrder: true);
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Run_NoSourceAndNoFileNamedForTheType_IsAnErrorAtTheAttribute()
    {
        // A file of another name beside it, and the file of its name elsewhere, are not the type's.
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;
            namespace App;
            [SqlSourceGenerate]
            public partial class Sample { }
            """,
            new SqlFile("/app/Repo/Users.sql", "SELECT 1;\n"),
            new SqlFile("/app/Repo/Sub/Sample.sql", "SELECT 2;\n"),
            new SqlFile("/app/Sample.sql", "SELECT 3;\n"),
            new SqlFile("/app/Repo/Sample.txt", "SELECT 4;\n")
        );

        run.Diagnostics.ShouldBe([
            "SQLSRC005 /app/Repo/Sample.cs(3,2)-(3,19): No 'Sample.sql' beside this file.  Add it, or set Source.",
        ]);
        run.Sources.Keys.ShouldBe(AttributeAndExceptions, ignoreOrder: true);
    }

    [Fact]
    public void Run_NoSource_TakesTheFileNamedForTheTypeAlone()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;
            namespace App;
            [SqlSourceGenerate]
            public partial class Sample { }
            """,
            new SqlFile("/app/Repo/sample.SQL", "-- name: First\nSELECT 1;\n"),
            new SqlFile("/app/Repo/Users.sql", "-- name: Second\nSELECT 2;\n")
        );

        run.Diagnostics.ShouldBeEmpty();
        run.Sources[GeneratorHarness.SampleFile].ShouldContain("First");
        run.Sources[GeneratorHarness.SampleFile].ShouldNotContain("Second");
    }

    [Fact]
    public void Run_SourceDotAndNoSqlFileInTheFolder_IsAnErrorAtTheAttribute()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;
            namespace App;
            [SqlSourceGenerate(Source = ".")]
            public partial class Sample { }
            """,
            new SqlFile("/app/Repo/Sub/Users.sql", "SELECT 1;\n")
        );

        run.Diagnostics.ShouldBe([
            "SQLSRC004 /app/Repo/Sample.cs(3,2)-(3,33): Source '.' matches no .sql file.  It is relative to the folder "
                + "of this file; a value that ends in .sql is one file, and any other value is a folder.",
        ]);
    }

    [Theory]
    [InlineData("private string Sql { get; set; } = string.Empty;")]
    [InlineData("private const int Sql = 1;")]
    [InlineData("public static void Sql() { }")]
    [InlineData("private static class Sql { }")]
    public void Run_NestedLocationAndAMemberNamedSql_IsAnErrorAtTheAttribute(string member)
    {
        var run = GeneratorHarness.RunInNestedClass(
            OutputKind.Sql,
            $$"""
            using SqlSource;
            namespace App;
            [SqlSourceGenerate(Source = ".")]
            public partial class Sample
            {
                {{member}}
            }
            """,
            GeneratorHarness.UsersFile
        );

        run.Diagnostics.ShouldBe([NestedClassTaken("Sample")]);
        run.Sources.Values.ShouldAllBe(text => !text.Contains("GetUser"));
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("public static partial class Sql", "Sql")]
    [InlineData("public partial class Sample<Sql>", "Sample")]
    [InlineData("public partial struct Sample<T, Sql>", "Sample")]
    public void Run_NestedLocationAndATypeOrTypeParameterNamedSql_IsAnErrorAtTheAttribute(
        string declaration,
        string name
    )
    {
        // A nested class cannot have the name of the type that contains it, or of one of its type parameters.
        var run = GeneratorHarness.RunInNestedClass(
            OutputKind.Sql,
            $$"""
            using SqlSource;
            namespace App;
            [SqlSourceGenerate(Source = ".")]
            {{declaration}} { }
            """,
            GeneratorHarness.UsersFile
        );

        run.Diagnostics.ShouldBe([NestedClassTaken(name)]);
        run.Sources.Values.ShouldAllBe(text => !text.Contains("GetUser"));
        run.CompilationErrors.ShouldBeEmpty();
    }

    // The run's settings put the members on the type.
    [Fact]
    public void Run_ClassLocationAndATypeNamedSql_IsAllowed()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;
            namespace App;
            [SqlSourceGenerate(Source = ".")]
            public static partial class Sql { }
            public static class Consumer { public const string Value = Sql.GetUser; }
            """,
            GeneratorHarness.UsersFile
        );

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Run_ClassLocationAndAMemberNamedSql_IsAllowed()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;
            namespace App;
            [SqlSourceGenerate(Source = ".")]
            public partial class Sample
            {
                public static string Sql => GetUser;
            }
            """,
            GeneratorHarness.UsersFile
        );

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources.Keys.ShouldContain(GeneratorHarness.SampleFile);
    }

    [Fact]
    public void Run_TypeWithSeveralProblems_ReportsThemAll()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;
            namespace App;
            [SqlSourceGenerate(Source = "Missing")]
            public class Sample { }
            """,
            GeneratorHarness.UsersFile
        );

        run.Diagnostics.Count.ShouldBe(2);
        run.Diagnostics[0].ShouldStartWith("SQLSRC001 /app/Repo/Sample.cs(4,14)-(4,20)");
        run.Diagnostics[1].ShouldStartWith("SQLSRC004 /app/Repo/Sample.cs(3,2)-(3,39)");
        run.Sources.Keys.ShouldBe(AttributeAndExceptions, ignoreOrder: true);
    }

    [Fact]
    public void Run_TypeWithAProblem_StillReportsTheErrorsOfItsSqlFiles()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;
            namespace App;
            [SqlSourceGenerate(Source = ".")]
            public class Sample { }
            """,
            new SqlFile(GeneratorHarness.UsersPath, "-- name: 1x\nSELECT 1;\n")
        );

        run.Diagnostics.Count.ShouldBe(2);
        run.Diagnostics.ShouldContain(diagnostic => diagnostic.StartsWith("SQLSRC001 /app/Repo/Sample.cs"));
        run.Diagnostics.ShouldContain(diagnostic =>
            diagnostic.StartsWith($"SQLSRC103 {GeneratorHarness.UsersPath}(1,10)-(1,12)")
        );
    }

    [Fact]
    public void Run_AttributeOnAnInterfaceOrAnEnum_IsLeftToTheCompiler()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;
            namespace App;
            [SqlSourceGenerate(Source = ".")]
            public partial interface ISample { }
            [SqlSourceGenerate(Source = ".")]
            public enum Kind { None }
            """,
            GeneratorHarness.UsersFile
        );

        run.Diagnostics.ShouldBeEmpty();
        run.Sources.Keys.ShouldBe(AttributeAndExceptions, ignoreOrder: true);
        run.CompilationErrors.Count.ShouldBe(2);
        run.CompilationErrors.ShouldAllBe(error => error.StartsWith("CS0592 "));
    }

    // SQLSRC007 at the attribute of the source, for a member Sql of the type of that name.
    private static string NestedClassTaken(string name) =>
        $"SQLSRC007 /app/Repo/Sample.cs(3,2)-(3,33): '{name}' already has a member named 'Sql', which a file of it "
        + "names as its nested class.  Rename the member, or set -- nested-class: or -- member-location: in the file.";
}
