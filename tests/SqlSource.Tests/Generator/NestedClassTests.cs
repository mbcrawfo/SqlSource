// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System.Collections.Generic;
using Shouldly;
using SqlSource.Settings;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generator;

// The name of the class that holds a type's members under SqlLocation.Nested: the attribute's NestedClass, else the
// nestedClass of the chain of the folder of the type's C# file, which SettingsFileTests holds, else the project's
// SqlSourceNestedClass, else Sql.
public sealed class NestedClassTests
{
    private const string AttributeOnly = "SqlSourceGenerateAttribute.g.cs";

    [Fact]
    public void Run_NestedClassProperty_NamesTheClass()
    {
        var run = Run(
            """
            using SqlSource;
            namespace App;
            [SqlSourceGenerate]
            public partial class Sample
            {
                public static string Get() => Queries.GetUser;
            }
            """,
            "Queries"
        );

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources["App.Sample.g.cs"].ShouldContain("private static class Queries\n");
        run.Sources["App.Sample.g.cs"].ShouldNotContain("class Sql");
    }

    [Fact]
    public void Run_NestedClassOnTheAttribute_WinsOverTheProperty()
    {
        var run = Run(
            """
            using SqlSource;
            namespace App;
            [SqlSourceGenerate(NestedClass = "Statements")]
            public partial class Sample
            {
                public static string Get() => Statements.GetUser;
            }
            """,
            "Queries"
        );

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources["App.Sample.g.cs"].ShouldContain("private static class Statements\n");
    }

    [Fact]
    public void Run_NestedClassOnTheAttributeWithoutTheProperty_NamesTheClass()
    {
        var run = Run(
            """
            using SqlSource;
            namespace App;
            [SqlSourceGenerate(NestedClass = "Statements")]
            public partial class Sample
            {
                public static string Get() => Statements.GetUser;
            }
            """,
            null
        );

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Run_NestedClassAndDirectLocation_IsIgnored()
    {
        var run = Run(
            """
            using SqlSource;
            namespace App;
            [SqlSourceGenerate(SqlLocation = SqlLocation.Direct, NestedClass = "Statements")]
            public partial class Sample
            {
                public static string Get() => GetUser;
            }
            """,
            "Queries"
        );

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources["App.Sample.g.cs"].ShouldNotContain("static class");
    }

    // The member named Sql is no problem once the class has another name.
    [Fact]
    public void Run_MemberNamedSqlAndAnotherNestedClass_IsAllowed()
    {
        var run = Run(
            """
            using SqlSource;
            namespace App;
            [SqlSourceGenerate]
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
    public void Run_NameOfThePropertyTaken_IsAnErrorAtTheAttribute(string declaration, string name)
    {
        var run = Run(
            $$"""
            using SqlSource;
            namespace App;
            [SqlSourceGenerate]
            {{declaration}}
            """,
            "Queries"
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC007 /app/Repo/Sample.cs(3,2)-(3,19): '{name}' already has a member named 'Queries'.  Rename it, "
                + "or use SqlLocation.Direct.",
        ]);
        run.Sources.Keys.ShouldBe([AttributeOnly]);
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Run_NameOfTheAttributeTaken_IsAnErrorAtTheAttribute()
    {
        var run = Run(
            """
            using SqlSource;
            namespace App;
            [SqlSourceGenerate(NestedClass = "Statements")]
            public partial class Sample
            {
                public void Statements() { }
            }
            """,
            null
        );

        run.Diagnostics.ShouldBe([
            "SQLSRC007 /app/Repo/Sample.cs(3,2)-(3,47): 'Sample' already has a member named 'Statements'.  Rename "
                + "it, or use SqlLocation.Direct.",
        ]);
        run.Sources.Keys.ShouldBe([AttributeOnly]);
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
                    [SqlSourceGenerate]
                    public partial class Sample { }
                    """
                ),
            ],
            [new SqlFile(GeneratorHarness.UsersPath, "-- name: Queries\nSELECT 1;\n")],
            properties: new Dictionary<string, string?> { ["SqlSourceNestedClass"] = "Queries" }
        );

        run.Diagnostics.ShouldHaveSingleItem()
            .ShouldEndWith(
                "A query of 'Sample' cannot be named 'Queries', because its member would have the name of the type "
                    + "that contains it"
            );
    }

    [Theory]
    [InlineData("class")]
    [InlineData("My-Sql")]
    [InlineData("App.Sql")]
    [InlineData("1Sql")]
    public void Run_NestedClassOnTheAttributeThatIsNotValid_IsAnErrorAtTheAttribute(string value)
    {
        var run = Run(
            $$"""
            using SqlSource;
            namespace App;
            [SqlSourceGenerate(NestedClass = "{{value}}")]
            public partial class Sample { }
            """,
            null
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC006 /app/Repo/Sample.cs(3,2)-(3,{37 + value.Length}): '{value}' is not a valid value of "
                + "NestedClass",
        ]);
        run.Sources.Keys.ShouldBe([AttributeOnly]);
    }

    // A value that is not valid is not set, so the class keeps its default name.  A test of the MSBuild path: the
    // output is the project's property too.
    [Theory]
    [InlineData("class")]
    [InlineData("My-Sql")]
    [InlineData("App.Sql")]
    public void Run_NestedClassPropertyThatIsNotValid_IsReportedWithoutAPosition(string value)
    {
        var run = RunOnMSBuild(
            """
            using SqlSource;
            namespace App;
            [SqlSourceGenerate]
            public partial class Sample
            {
                public static string Get() => Sql.GetUser;
            }
            """,
            value
        );

        run.Diagnostics.ShouldBe([$"SQLSRC014 (1,1)-(1,1): '{value}' is not a valid value of SqlSourceNestedClass"]);
        run.CompilationErrors.ShouldBeEmpty();
    }

    private static GeneratorRun Run(string source, string? nestedClass) =>
        GeneratorHarness.Run(
            OutputKind.Sql,
            [new SourceFile(GeneratorHarness.SourcePath, source)],
            [GeneratorHarness.UsersFile],
            properties: new Dictionary<string, string?> { ["SqlSourceNestedClass"] = nestedClass }
        );

    // The same with the output sql as the project's property, for a test of the MSBuild path.
    private static GeneratorRun RunOnMSBuild(string source, string nestedClass) =>
        GeneratorHarness.Run(
            null,
            [new SourceFile(GeneratorHarness.SourcePath, source)],
            [GeneratorHarness.UsersFile],
            properties: new Dictionary<string, string?>
            {
                ["SqlSourceOutput"] = "Sql",
                ["SqlSourceNestedClass"] = nestedClass,
            }
        );
}
