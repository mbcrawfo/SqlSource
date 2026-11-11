// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

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

            [SqlSourceGenerate(Source = ".")]
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
        run.Sources[GeneratorHarness.SampleFile]
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
        var run = GeneratorHarness.RunInNestedClass(
            OutputKind.Sql,
            """
            using SqlSource;

            namespace App;

            /// <summary>Queries.</summary>
            [SqlSourceGenerate(Source = ".")]
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
                "        private static partial class Sql\n        {\n            /// <summary>\n"
                    + "            /// The <c>ListFrom</c> query from <c>Users.sql</c>.\n"
            );
        run.Sources["App.Sample-1.g.cs"].ShouldContain("            public static string ListFrom(string table)\n");
    }

    [Fact]
    public void Run_QueryThatIsOnlyATokenOrHasEightOfThem_Compiles()
    {
        var run = GeneratorHarness.RunInNestedClass(
            OutputKind.Sql,
            """
            using SqlSource;

            namespace App;

            [SqlSourceGenerate(Source = ".")]
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
    [InlineData("element")]
    [InlineData("item")]
    [InlineData("atRow")]
    [InlineData("exception")]
    [InlineData("raisedLater")]
    [InlineData("hasRow")]
    [InlineData("list0")]
    [InlineData("count0")]
    [InlineData("size0")]
    [InlineData("padded0")]
    [InlineData("total")]
    [InlineData("index")]
    [InlineData("end")]
    [InlineData("written")]
    [InlineData("CountOf")]
    [InlineData("PaddedSize")]
    [InlineData("DigitsUpTo")]
    [InlineData("TElement")]
    [InlineData("values")]
    [InlineData("collection")]
    [InlineData("array")]
    [InlineData("size")]
    [InlineData("digits")]
    [InlineData("power")]
    [InlineData("items")]
    [InlineData("itemIndex")]
    [InlineData("first")]
    [InlineData("more")]
    [InlineData("batch")]
    [InlineData("batchScope")]
    [InlineData("result")]
    [InlineData("affected")]
    [InlineData("ownsTransaction")]
    [InlineData("ownTransaction")]
    [InlineData("error")]
    [InlineData("failed")]
    [InlineData("isolationLevel")]
    [InlineData("isolationCommand")]
    [InlineData("isolationScope")]
    public void Run_TokenWithAnAwkwardName_Compiles(string name)
    {
        var run = GeneratorHarness.RunInNestedClass(
            OutputKind.Sql,
            """
            using SqlSource;

            namespace App;

            [SqlSourceGenerate(Source = ".")]
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
    // Methods: one method reads many rows, the other one, and each binds a parameter and reads a column through a
    // converter, and an array element by element, so that value names the pattern variable of the binding and element
    // and item the static lambdas' parameter and pattern variable too.  C# lets a lambda's names hide the token's
    // argument, so the text is checked for them as well.  Each runs under method-type=async and again under sync, whose
    // synchronous methods have the same locals.
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
    [InlineData("element")]
    [InlineData("item")]
    [InlineData("ReadRows")]
    [InlineData("yield")]
    [InlineData("atRow")]
    [InlineData("exception")]
    [InlineData("raisedLater")]
    [InlineData("hasRow")]
    [InlineData("list0")]
    [InlineData("count0")]
    [InlineData("size0")]
    [InlineData("padded0")]
    [InlineData("total")]
    [InlineData("index")]
    [InlineData("end")]
    [InlineData("written")]
    [InlineData("CountOf")]
    [InlineData("PaddedSize")]
    [InlineData("DigitsUpTo")]
    [InlineData("TElement")]
    [InlineData("values")]
    [InlineData("collection")]
    [InlineData("array")]
    [InlineData("size")]
    [InlineData("digits")]
    [InlineData("power")]
    [InlineData("items")]
    [InlineData("itemIndex")]
    [InlineData("first")]
    [InlineData("more")]
    [InlineData("batch")]
    [InlineData("batchScope")]
    [InlineData("result")]
    [InlineData("affected")]
    [InlineData("ownsTransaction")]
    [InlineData("ownTransaction")]
    [InlineData("error")]
    [InlineData("failed")]
    [InlineData("isolationLevel")]
    [InlineData("isolationCommand")]
    [InlineData("isolationScope")]
    public void Run_TokenWithAnAwkwardNameInAQueryMethod_Compiles(string name)
    {
        ShouldCompileWithTokenNamed(name, "async");
        ShouldCompileWithTokenNamed(name, "sync");
    }

    // The runs of RunTokenNamed and RunSqlServerTokenNamed under the method-type, with the names the token leaves the
    // locals.
    private static void ShouldCompileWithTokenNamed(string name, string methodType)
    {
        var run = MethodRuns.RunTokenNamed(name, LanguageVersion.CSharp12, methodType);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(" ProbeOne(\n");
        methods.ShouldContain(" ProbeStream(\n");
        methods.ShouldContain("yield return ");
        // The iterator takes the name ReadRows unless a token has it, and then ReadRows1.
        methods.ShouldContain(name == "ReadRows" ? "return ReadRows1(" : "return ReadRows(");
        methods.ShouldContain(" ? global::App.UserId.ToDatabase(");
        methods.ShouldContain("global::App.UserId.FromDatabase(");
        methods.ShouldContain("global::System.Array.ConvertAll<int?, global::App.UserId>(");
        methods.ShouldContain("global::System.Array.ConvertAll<global::App.UserId, int>(");
        // C# is case-sensitive, and a collection method's pattern variable is item1, which Item1 does not hide.
        methods.ShouldNotContain("static " + name + " =>", Case.Sensitive);
        methods.ShouldNotContain("is { } " + name + " ?", Case.Sensitive);
        // A list's local takes the name list0 unless a token has it, and then list01.
        methods.ShouldContain(name == "list0" ? "var list01 = parameters.Members;" : "var list0 = parameters.Members;");
        methods.ShouldContain(".ParameterName = \"members_\" + ");
        // Each query has its collection method, whose enumerator takes the name items unless a token has it, and then
        // items1.
        methods.ShouldContain(" ProbeCount(\n");
        methods.Split(".CreateBatch();\n").Length.ShouldBe(5);
        var items = name == "items" ? "items1" : "items";
        methods.ShouldContain("using var " + items + " = parameters.GetEnumerator();");

        var sqlServer = MethodRuns.RunSqlServerTokenNamed(name, LanguageVersion.CSharp12, methodType);

        sqlServer.ShouldBeClean();
        // The flag of a row being yielded takes the name atRow unless a token has it, and then atRow1.
        sqlServer
            .Sources[MethodRuns.MethodsFile]
            .ShouldContain(name == "atRow" ? "var atRow1 = false;" : "var atRow = false;");
        // The exception the check catches takes the name exception unless a token has it, and then exception1.
        sqlServer
            .Sources[MethodRuns.MethodsFile]
            .ShouldContain(
                name == "exception"
                    ? "catch (global::System.Exception exception1) when (transaction is not null && exception1 is not"
                    : "catch (global::System.Exception exception) when (transaction is not null && exception is not"
            );
        // The flag of a later error takes the name raisedLater unless a token has it, and then raisedLater1.
        sqlServer
            .Sources[MethodRuns.MethodsFile]
            .ShouldContain(name == "raisedLater" ? "var raisedLater1 = false;" : "var raisedLater = false;");
        // The local of a read takes the name hasRow unless a token has it, and then hasRow1.
        sqlServer.Sources[MethodRuns.MethodsFile].ShouldContain(name == "hasRow" ? "bool hasRow1;" : "bool hasRow;");
        // The collection method at a level keeps the session's level in isolationLevel, and reads and restores it by
        // isolationCommand in isolationScope, each unless a token has the name, and then with a 1.
        sqlServer
            .Sources[MethodRuns.MethodsFile]
            .ShouldContain(name == "isolationLevel" ? "var isolationLevel1 = 0;" : "var isolationLevel = 0;");
        sqlServer
            .Sources[MethodRuns.MethodsFile]
            .ShouldContain(
                name == "isolationCommand"
                    ? "var isolationCommand1 = connection.CreateCommand();"
                    : "var isolationCommand = connection.CreateCommand();"
            );
        sqlServer
            .Sources[MethodRuns.MethodsFile]
            .ShouldContain(
                name == "isolationScope" ? "await using var isolationScope1 =" : "await using var isolationScope ="
            );
        // Under sync each query method but the stream is synchronous, and every collection method asynchronous.
        var isSynchronous = methodType == "sync";
        methods.Contains(" static int ProbeCount(\n", System.StringComparison.Ordinal).ShouldBe(isSynchronous);
        sqlServer
            .Sources[MethodRuns.MethodsFile]
            .Contains(" static int ProbeLevel(\n", System.StringComparison.Ordinal)
            .ShouldBe(isSynchronous);
    }

    // A list's use is a segment of its own, and the token method of a type writes it as the SQL wrote it, in the
    // literal around it.
    [Fact]
    public void TokenMethod_QueryWithAListAndAToken_KeepsTheListAsWritten()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: ListByIds\n-- output: models\nSELECT id FROM users WHERE id IN @ids ORDER BY {{order:id}}\n"
        );
        var run = ModelRuns.Run(
            file,
            ModelRuns.Entry(file, "ListByIds", [ModelRuns.Parameter("ids")], [ModelRuns.Column("id")])
        );

        run.ShouldBeClean();
        run.Sources[GeneratorHarness.SampleFile]
            .ShouldContain(
                """
                        /// <code>
                        /// SELECT id FROM users WHERE id IN @ids ORDER BY {{order}}
                        /// </code>
                        /// </remarks>
                        /// <param name="order">The text that replaces <c>{{order}}</c>.</param>
                        /// <returns>The SQL with each token replaced by its argument.</returns>
                        public static string ListByIds(string order)
                        {
                            global::System.ArgumentException.ThrowIfNullOrWhiteSpace(order);
                            return string.Create(
                                checked(47 + order.Length),
                                order,
                                static (span, state) =>
                                {
                                    "SELECT id FROM users WHERE id IN @ids ORDER BY ".CopyTo(span);
                                    span = span.Slice(47);
                                    state.CopyTo(span);
                                }
                            );
                        }

                """
            );
    }

    // The kept form's list use is split at the kept form's places, and its token method keeps the comment before it in
    // the literal around it.
    [Fact]
    public void TokenMethod_QueryWithAListThatKeepsComments_KeepsTheCommentAndTheList()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: ListByIds\n-- output: models\n-- generator: keep-comments\n"
                + "SELECT id FROM users WHERE id IN /* c */ @ids ORDER BY {{order:id}}\n"
        );
        var run = ModelRuns.Run(
            file,
            ModelRuns.Entry(file, "ListByIds", [ModelRuns.Parameter("ids")], [ModelRuns.Column("id")])
        );

        run.ShouldBeClean();
        run.Sources[GeneratorHarness.SampleFile]
            .ShouldContain(
                """
                                checked(55 + order.Length),
                                order,
                                static (span, state) =>
                                {
                                    "SELECT id FROM users WHERE id IN /* c */ @ids ORDER BY ".CopyTo(span);
                                    span = span.Slice(55);
                                    state.CopyTo(span);
                                }

                """
            );
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

        withDefaults.ShouldBeClean();
        withDefaults.Sources[GeneratorHarness.SampleFile].ShouldBe(plain.Sources[GeneratorHarness.SampleFile]);
    }
}
