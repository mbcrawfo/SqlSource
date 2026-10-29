// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using Shouldly;
using SqlSource.Settings;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generator;

// Names and SQL that C# or XML cannot hold as written, escaped in the generated file or kept as they are.
public sealed class GeneratedSourceEscapingTests
{
    [Fact]
    public void Run_TypeInTheGlobalNamespaceNamedWithAKeyword_HasNoNamespaceBlockAndKeepsTheEscape()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            [SqlSource.SqlSourceGenerate(SqlLocation = SqlSource.SqlLocation.Direct)]
            public partial struct @class
            {
                public static string Value => GetUser;
            }
            """,
            GeneratorHarness.UsersFile
        );

        run.ShouldBeClean();
        run.Sources["class.g.cs"]
            .ShouldBe(
                $$"""
                {{GeneratedFile.Header}}
                partial struct @class
                {
                    /// <summary>
                    /// The <c>GetUser</c> query from <c>Users.sql</c>.
                    /// </summary>
                    /// <remarks>
                    /// <code>
                    /// SELECT 1;
                    /// </code>
                    /// </remarks>
                    public const string GetUser = "SELECT 1;";
                }

                """
            );
    }

    [Fact]
    public void Run_NamespaceWithAnEscapedKeyword_KeepsTheEscapeInCode()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;

            namespace App.@event
            {
                namespace Data
                {
                    [SqlSourceGenerate]
                    public partial class Sample
                    {
                        public static string Value => Sql.GetUser;
                    }
                }
            }
            """,
            GeneratorHarness.UsersFile
        );

        run.ShouldBeClean();
        run.Sources["App.event.Data.Sample.g.cs"].ShouldContain("namespace App.@event.Data\n{\n");
    }

    [Fact]
    public void Run_SqlWithATokenThatNeedsEscaping_CompilesAndDocumentsIt()
    {
        // The literal text around a token is escaped like a constant's, in the string and in the comment.
        const string Sql = "SELECT \"a\", 'b\\c', '\t', '<&>', '\u2028\u0085' FROM {{table}} WHERE x < 1;";

        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;

            namespace App;

            /// <summary>Queries.</summary>
            [SqlSourceGenerate(SqlLocation = SqlLocation.Direct)]
            public partial class Sample;
            """,
            new SqlFile(GeneratorHarness.UsersPath, Sql + "\n")
        );

        run.ShouldBeClean();
        var source = run.Sources[GeneratorHarness.SampleFile];
        source.ShouldContain("\"SELECT \\\"a\\\", 'b\\\\c', '\\t', '<&>', '\\u2028\\u0085' FROM \".CopyTo(span);\n");
        source.ShouldContain("/// ' FROM {{table}} WHERE x &lt; 1;\n");
    }

    [Fact]
    public void Run_SqlThatNeedsEscaping_CompilesAndKeepsItsValue()
    {
        // A quoted region is copied as written, so it can hold a quote, a backslash, a tab, characters XML reserves,
        // and a character that C# treats as a line terminator.
        const string Sql = "SELECT \"a\", 'b\\c', '\t', '<&>', '\u2028\u0085' WHERE x < 1;";

        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            GeneratorHarness.SampleSource(null),
            new SqlFile(GeneratorHarness.UsersPath, "-- summary: a < b & c\n" + Sql + "\n")
        );

        run.ShouldBeClean();
        var source = run.Sources[GeneratorHarness.SampleFile];
        source.ShouldContain("/// a &lt; b &amp; c\n");
        source.ShouldContain(
            "public const string Users = \"SELECT \\\"a\\\", 'b\\\\c', '\\t', '<&>', '\\u2028\\u0085' WHERE x < 1;\";\n"
        );
    }

    [Fact]
    public void Run_SqlWithCharactersXmlCannotHold_ReplacesThemInTheCommentAndKeepsTheValue()
    {
        // A form feed and a Ctrl-Z, which old tools leave at the end of a file, are legal in SQL text and in a C#
        // string, and are not legal anywhere in an XML document.
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            GeneratorHarness.SampleSource(null),
            new SqlFile(GeneratorHarness.UsersPath, "-- summary: a\fb\nSELECT '\f\u001A';\n")
        );

        run.ShouldBeClean();
        run.Sources[GeneratorHarness.SampleFile]
            .ShouldContain("public const string Users = \"SELECT '\\f\\u001A';\";\n");
    }

    [Fact]
    public void Run_NonAsciiNames_AreKept()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [
                new SourceFile(
                    "/app/Dépôt/Sample.cs",
                    "namespace Données;\n[SqlSource.SqlSourceGenerate]\n"
                        + "public partial class Requêtes { public string V => Sql.Größe; }\n"
                ),
            ],
            [new SqlFile("/app/Dépôt/Größe.sql", "SELECT 'ñ 😀';\n")]
        );

        run.ShouldBeClean();
        run.Sources["Données.Requêtes.g.cs"].ShouldContain("public const string Größe = \"SELECT 'ñ 😀';\";");
    }
}
