// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Settings;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generator;

// A query with tokens, which is a method that builds its SQL from its arguments.
public sealed class GeneratedSourceMethodTests
{
    [Fact]
    public void Run_QueryWithTokens_IsAMethodBesideTheConstantsOfItsFile()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;

            namespace App;

            [SqlSourceGenerate(SqlLocation = SqlLocation.Direct)]
            public partial class Sample
            {
                public static string List() => ListFrom("users", "1 = 1") + GetUser;
            }
            """,
            new SqlFile(
                GeneratorHarness.UsersPath,
                """
                -- name: GetUser
                SELECT 1;

                -- name: ListFrom
                -- summary: Lists the rows of a table.
                SELECT id -- the key
                FROM {{ table }} WHERE {{filter}} ORDER BY {{table}}.id;
                """
            )
        );

        run.ShouldBeClean();
        run.Sources["App.Sample.g.cs"]
            .ShouldBe(
                $$$"""
                {{{GeneratedFile.Header}}}
                namespace App
                {
                    partial class Sample
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

                        /// <summary>
                        /// Lists the rows of a table.
                        /// </summary>
                        /// <remarks>
                        /// <code>
                        /// SELECT id
                        /// FROM {{table}} WHERE {{filter}} ORDER BY {{table}}.id;
                        /// </code>
                        /// </remarks>
                        /// <param name="table">The text that replaces <c>{{table}}</c>.</param>
                        /// <param name="filter">The text that replaces <c>{{filter}}</c>.</param>
                        /// <returns>The SQL with each token replaced by its argument.</returns>
                        public static string ListFrom(string table, string filter)
                        {
                            global::System.ArgumentException.ThrowIfNullOrWhiteSpace(table);
                            global::System.ArgumentException.ThrowIfNullOrWhiteSpace(filter);
                            return string.Create(
                                checked(36 + table.Length * 2 + filter.Length),
                                (table, filter),
                                static (span, state) =>
                                {
                                    "SELECT id\nFROM ".CopyTo(span);
                                    span = span.Slice(15);
                                    state.Item1.CopyTo(span);
                                    span = span.Slice(state.Item1.Length);
                                    " WHERE ".CopyTo(span);
                                    span = span.Slice(7);
                                    state.Item2.CopyTo(span);
                                    span = span.Slice(state.Item2.Length);
                                    " ORDER BY ".CopyTo(span);
                                    span = span.Slice(10);
                                    state.Item1.CopyTo(span);
                                    span = span.Slice(state.Item1.Length);
                                    ".id;".CopyTo(span);
                                }
                            );
                        }
                    }
                }

                """
            );
    }

    [Fact]
    public void Run_QueryWithTokensOnAGenericStructInNestedLocation_IsAMethodOfTheSqlClass()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;

            namespace App;

            /// <summary>Queries.</summary>
            [SqlSourceGenerate]
            public partial struct Sample<T>
            {
                /// <summary>Uses the query.</summary>
                public static string List() => Sql.ListFrom("users");
            }
            """,
            new SqlFile(GeneratorHarness.UsersPath, "-- name: ListFrom\nSELECT * FROM {{table}};\n")
        );

        run.ShouldBeClean();
        run.Sources["App.Sample-1.g.cs"]
            .ShouldContain(
                "        private static class Sql\n        {\n            /// <summary>\n"
                    + "            /// The <c>ListFrom</c> query from <c>Users.sql</c>.\n"
            );
        run.Sources["App.Sample-1.g.cs"].ShouldContain("            public static string ListFrom(string table)\n");
    }

    [Fact]
    public void Run_QueryThatIsOnlyATokenOrHasEightOfThem_Compiles()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;

            namespace App;

            [SqlSourceGenerate]
            public partial class Sample
            {
                public static string Whole() => Sql.Whole("SELECT 1;");

                public static string Eight() => Sql.Eight("1", "2", "3", "4", "5", "6", "7", "8");
            }
            """,
            new SqlFile(
                GeneratorHarness.UsersPath,
                "-- name: Whole\n{{sql}}\n"
                    + "-- name: Eight\nSELECT {{a}}{{b}}, {{c}}, {{d}}, {{e}}, {{f}}, {{g}}, {{h}}, {{a}};\n"
            )
        );

        run.ShouldBeClean();
    }

    // Any identifier that is not a reserved keyword can be a token name.  Each of these is a name the generated method
    // uses itself, a name of something in scope, or a contextual keyword.
    [Theory]
    [InlineData("span")]
    [InlineData("state")]
    [InlineData("span1")]
    [InlineData("Item1")]
    [InlineData("Item2")]
    [InlineData("Rest")]
    [InlineData("System")]
    [InlineData("String")]
    [InlineData("ArgumentException")]
    [InlineData("Sql")]
    [InlineData("Sample")]
    [InlineData("Probe")]
    [InlineData("T")]
    [InlineData("var")]
    [InlineData("dynamic")]
    [InlineData("await")]
    [InlineData("nameof")]
    [InlineData("global")]
    [InlineData("value")]
    [InlineData("scoped")]
    [InlineData("field")]
    [InlineData("_")]
    [InlineData("from")]
    [InlineData("sql")]
    [InlineData("command")]
    [InlineData("reader")]
    [InlineData("rows")]
    [InlineData("row")]
    [InlineData("wasClosed")]
    [InlineData("parameter")]
    [InlineData("App")]
    [InlineData("commandScope")]
    [InlineData("readerScope")]
    [InlineData("value0")]
    public void Run_TokenWithAnAwkwardName_Compiles(string name)
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;

            namespace App;

            [SqlSourceGenerate]
            public partial class Sample<T>
            {
                public static string One() => Sql.Probe("a");

                public static string Two() => Sql.Pair("a", "b");
            }
            """,
            new SqlFile(
                GeneratorHarness.UsersPath,
                "-- name: Probe\nSELECT {{"
                    + name
                    + "}}, {{"
                    + name
                    + "}};\n"
                    + "-- name: Pair\nSELECT {{"
                    + name
                    + "}} FROM {{second}};\n"
            )
        );

        run.ShouldBeClean();
    }

    // The same names as the token of a query's method, beside its parameters, its connection and its locals, under
    // CodeGen: one method reads many rows, the other one, and each binds a parameter and reads a column through a
    // converter, so that value names the pattern variable of the binding too.
    [Theory]
    [InlineData("span")]
    [InlineData("state")]
    [InlineData("span1")]
    [InlineData("Item1")]
    [InlineData("Item2")]
    [InlineData("Rest")]
    [InlineData("System")]
    [InlineData("String")]
    [InlineData("ArgumentException")]
    [InlineData("Sql")]
    [InlineData("Sample")]
    [InlineData("Probe")]
    [InlineData("T")]
    [InlineData("var")]
    [InlineData("dynamic")]
    [InlineData("await")]
    [InlineData("nameof")]
    [InlineData("global")]
    [InlineData("value")]
    [InlineData("scoped")]
    [InlineData("field")]
    [InlineData("_")]
    [InlineData("from")]
    [InlineData("sql")]
    [InlineData("command")]
    [InlineData("reader")]
    [InlineData("rows")]
    [InlineData("row")]
    [InlineData("wasClosed")]
    [InlineData("parameter")]
    [InlineData("App")]
    [InlineData("commandScope")]
    [InlineData("readerScope")]
    [InlineData("value0")]
    public void Run_TokenWithAnAwkwardNameInAQueryMethod_Compiles(string name)
    {
        var run = MethodRuns.RunTokenNamed(name, LanguageVersion.CSharp12);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(" ProbeOne(\n");
        methods.ShouldContain("(object)global::App.UserId.ToDatabase(");
        methods.ShouldContain("global::App.UserId.FromDatabase(");
    }

    [Fact]
    public void Run_TokenWithADefault_GeneratesTheMethodItGeneratesWithout()
    {
        var source = GeneratorHarness.SampleSource(null);

        var plain = GeneratorHarness.Run(
            OutputKind.Sql,
            source,
            new SqlFile("/app/Repo/Q.sql", "SELECT * FROM {{table}} {{filter}};\n")
        );
        var withDefaults = GeneratorHarness.Run(
            OutputKind.Sql,
            source,
            new SqlFile(
                "/app/Repo/Q.sql",
                "-- token: {{filter:WHERE x = 1}}\nSELECT * FROM {{table:users}} {{filter}};\n"
            )
        );

        withDefaults.Diagnostics.ShouldBeEmpty();
        withDefaults.GeneratedCodeWarnings.ShouldBeEmpty();
        withDefaults.Sources["App.Sample.g.cs"].ShouldBe(plain.Sources["App.Sample.g.cs"]);
    }
}
