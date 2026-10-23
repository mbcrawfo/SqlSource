// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Linq;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Settings;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generator;

// The names of the generated files, which the compiler compares ignoring case and which must be legal file names.
public sealed class GeneratedFileNameTests
{
    [Fact]
    public void Run_TypesThatDifferByNamespaceOrArity_GetTheirOwnFiles()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate]
                public partial class Sample;

                [SqlSourceGenerate]
                public partial class Sample<T>;
            }

            namespace Other
            {
                [SqlSourceGenerate]
                public partial class Sample;
            }
            """,
            GeneratorHarness.UsersFile
        );

        run.ShouldBeClean();
        run.Sources.Keys.ShouldBe(
            ["SqlSourceGenerateAttribute.g.cs", "App.Sample.g.cs", "App.Sample-1.g.cs", "Other.Sample.g.cs"],
            ignoreOrder: true
        );
    }

    [Fact]
    public void Run_TypesAndNamespacesThatDifferOnlyByCase_GetFilesWhoseNamesDifferByMoreThanCase()
    {
        // The compiler compares the names of generated files ignoring case, and refuses the second of two that are
        // equal that way with an exception that costs every type its generated code.
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate]
                public partial class Sample { public static string Value => Sql.GetUser; }

                [SqlSourceGenerate]
                public partial class sample { public static string Value => Sql.GetUser; }

                [SqlSourceGenerate]
                public partial class Other { public static string Value => Sql.GetUser; }
            }

            namespace APP
            {
                [SqlSourceGenerate]
                public partial class Sample { public static string Value => Sql.GetUser; }
            }
            """,
            GeneratorHarness.UsersFile
        );

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources.Count.ShouldBe(5);
        run.Sources.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count().ShouldBe(5);
        run.Sources.Keys.ShouldContain("App.Other.g.cs");
        run.Sources.Keys.ShouldNotContain("App.Sample.g.cs");
        run.Sources.Values.Count(source => source.Contains("public const string GetUser", StringComparison.Ordinal))
            .ShouldBe(4);
    }

    [Fact]
    public void Run_TypeWhoseFileWouldBeNamedLikeTheAttributes_GetsAnotherName()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            [SqlSource.SqlSourceGenerate]
            public partial class SqlSourceGenerateAttribute
            {
                public static string Value => Sql.GetUser;
            }
            """,
            GeneratorHarness.UsersFile
        );

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources.Count.ShouldBe(2);
        run.Sources["SqlSourceGenerateAttribute.g.cs"].ShouldBe(AttributeSource.Text);
    }

    [Fact]
    public void Run_TypeNameWrittenWithAUnicodeEscape_GetsAFileNamedByTheCharacter()
    {
        // "\u0053ample" is the identifier Sample.  The escape is legal in code and not in the name of a file.
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            namespace App;

            [SqlSource.SqlSourceGenerate]
            public partial class \u0053ample
            {
                public static string Value => Sql.GetUser;
            }
            """,
            GeneratorHarness.UsersFile
        );

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources.Keys.ShouldContain("App.Sample.g.cs");
    }

    [Fact]
    public void Run_AttributeOnTwoPartialDeclarations_IsLeftToTheCompilerAndTheTypeGetsOneFile()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [
                new SourceFile("/app/Repo/Sample.cs", "[SqlSource.SqlSourceGenerate]\npublic partial class Sample;\n"),
                new SourceFile("/app/Other/Sample.cs", "[SqlSource.SqlSourceGenerate]\npublic partial class Sample;\n"),
            ],
            [GeneratorHarness.UsersFile, new SqlFile("/app/Other/Orders.sql", "SELECT 2;\n")]
        );

        run.Diagnostics.ShouldBeEmpty();
        run.Sources.Keys.ShouldBe(["SqlSourceGenerateAttribute.g.cs", "Sample.g.cs"], ignoreOrder: true);
        run.Sources["Sample.g.cs"].ShouldContain("public const string GetUser");
        run.CompilationErrors.ShouldHaveSingleItem().ShouldStartWith("CS0579 /app/Other/Sample.cs(1,2)");
    }
}
