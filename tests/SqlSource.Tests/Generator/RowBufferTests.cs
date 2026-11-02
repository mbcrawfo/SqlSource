// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Generation.Emitting;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The buffer a many method reads its rows into when it returns an array, a list, an immutable array or one of their
// interfaces: written once for the project, in a file of its own, when a method it keeps uses it, and named from
// global:: in each method.  Each run is at CodeGen under postgres on Npgsql 10, and every method must compile without
// a warning.
public sealed class RowBufferTests
{
    private const string Struct = "struct SqlSourceRowBuffer";

    private const string NewBuffer = "new global::SqlSource.SqlSourceRowBuffer<";

    private const string Select = "SELECT id, name FROM users;\n";

    private const string APath = "/app/Repo/A.sql";

    private const string BPath = "/app/Repo/B.sql";

    // Two types of one namespace, each claiming a file of its own.
    private const string TwoTypesSource = """
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate(Path = "A.sql")]
            public partial class First;

            [SqlSourceGenerate(Path = "B.sql")]
            public partial class Second;
        }
        """;

    private static readonly SqlFile ListUsers = new(GeneratorHarness.UsersPath, "-- name: ListUsers\n" + Select);

    [Theory]
    [InlineData("Array", "ToArray")]
    [InlineData("IEnumerable", "ToArray")]
    [InlineData("IReadOnlyCollection", "ToArray")]
    [InlineData("IReadOnlyList", "ToArray")]
    [InlineData("List", "ToList")]
    [InlineData("IList", "ToList")]
    [InlineData("ICollection", "ToList")]
    [InlineData("ImmutableArray", "ToImmutableArray")]
    public void Run_ManyMethodOfEachPooledType_ReadsItsRowsIntoTheBuffer(string collection, string result)
    {
        var run = RunListUsers("CollectionType = GeneratorCollectionType." + collection);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("var rows = new global::SqlSource.SqlSourceRowBuffer<global::App.ListUsersDto>();\n");
        methods.ShouldContain("return rows." + result + "();\n");
        methods.ShouldNotContain(Struct);
        run.Sources[RowBuffer.HintName].ShouldBe(RowBuffer.Source);
    }

    [Theory]
    [InlineData("Array", "new global::System.Collections.Generic.List<global::App.ListUsersDto>()", "rows.ToArray()")]
    [InlineData(
        "IEnumerable",
        "new global::System.Collections.Generic.List<global::App.ListUsersDto>()",
        "rows.ToArray()"
    )]
    [InlineData(
        "IReadOnlyCollection",
        "new global::System.Collections.Generic.List<global::App.ListUsersDto>()",
        "rows.ToArray()"
    )]
    [InlineData(
        "IReadOnlyList",
        "new global::System.Collections.Generic.List<global::App.ListUsersDto>()",
        "rows.ToArray()"
    )]
    [InlineData("List", "new global::System.Collections.Generic.List<global::App.ListUsersDto>()", "rows")]
    [InlineData("IList", "new global::System.Collections.Generic.List<global::App.ListUsersDto>()", "rows")]
    [InlineData("ICollection", "new global::System.Collections.Generic.List<global::App.ListUsersDto>()", "rows")]
    [InlineData(
        "ImmutableArray",
        "global::System.Collections.Immutable.ImmutableArray.CreateBuilder<global::App.ListUsersDto>()",
        "rows.DrainToImmutable()"
    )]
    public void Run_ManyMethodOfEachPooledTypeUnderNoArrayPool_ReadsItsRowsAsBeforeThePool(
        string collection,
        string buffer,
        string result
    )
    {
        var run = RunListUsers(
            "CollectionType = GeneratorCollectionType." + collection + ", Parameters = \"no-array-pool\""
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("var rows = " + buffer + ";\n");
        methods.ShouldContain("return " + result + ";\n");
        methods.ShouldNotContain("SqlSourceRowBuffer");
        run.Sources.Keys.ShouldNotContain(RowBuffer.HintName);
    }

    // One of each pooled type in one file: the buffer is written once, and every member of it, ToList and
    // ToImmutableArray among them, compiles beside a method of each pooled type.
    [Fact]
    public void Run_MethodOfEveryPooledType_CompilesWithOneBufferAndNoWarning()
    {
        string[] collections =
        [
            "Array",
            "IEnumerable",
            "IReadOnlyCollection",
            "IReadOnlyList",
            "List",
            "IList",
            "ICollection",
            "ImmutableArray",
        ];
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            string.Join(
                "\n",
                collections.Select(collection =>
                    "-- name: List" + collection + "\n-- collection-type: " + collection + "\n" + Select
                )
            )
        );

        var run = MethodRuns.Run(file, [.. collections.Select(collection => Users(file, "List" + collection))]);

        run.ShouldBeClean();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldNotContain(Struct);
        Count(methods, NewBuffer).ShouldBe(collections.Length);
        run.Sources[RowBuffer.HintName].ShouldBe(RowBuffer.Source);
    }

    // Two types, each with a pooled method in a file of methods of its own, share the project's one buffer.
    [Fact]
    public void Run_PooledMethodsInSeveralFiles_WriteOneBufferFile()
    {
        var a = new SqlFile(APath, "-- name: ListFirst\n" + Select);
        var b = new SqlFile(BPath, "-- name: ListSecond\n-- collection-type: List\n" + Select);

        var run = MethodRuns.Run(
            TwoTypesSource,
            a,
            Sidecars.Of(a, Users(a, "ListFirst")),
            b,
            Sidecars.Of(b, Users(b, "ListSecond"))
        );

        run.ShouldBeClean();
        run.Sources.Keys.Count(key => key == RowBuffer.HintName).ShouldBe(1);
        run.Sources[RowBuffer.HintName].ShouldBe(RowBuffer.Source);
        foreach (var name in new[] { "App.First-Methods.g.cs", "App.Second-Methods.g.cs" })
        {
            run.Sources[name].ShouldNotContain(Struct);
            Count(run.Sources[name], NewBuffer).ShouldBe(1);
        }
    }

    [Fact]
    public void Run_ProjectWithNoPooledMethod_HasNoBufferFile()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser -> one\n"
                + Select
                + "\n-- name: RenameUsers -> rowcount\nUPDATE users SET name = name;\n"
                + "\n-- name: ListUsers\n-- collection-type: ImmutableList\n"
                + Select
        );

        var run = MethodRuns.Run(
            file,
            [Users(file, "GetUser"), Entry(file, "RenameUsers", [], null), Users(file, "ListUsers")]
        );

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile].ShouldNotContain("SqlSourceRowBuffer");
        run.Sources.Keys.ShouldNotContain(RowBuffer.HintName);
    }

    // A many method of IAsyncEnumerable yields its rows one at a time, so it uses no buffer.
    [Fact]
    public void Run_EveryManyMethodStreams_WritesNoRowBuffer()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: StreamUsers\n-- collection-type: IAsyncEnumerable\n" + Select
        );

        var run = MethodRuns.Run(file, [Users(file, "StreamUsers")]);

        run.ShouldBeClean();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        run.Sources[MethodRuns.MethodsFile].ShouldNotContain("SqlSourceRowBuffer");
        run.Sources.Keys.ShouldNotContain(RowBuffer.HintName);
    }

    [Fact]
    public void Run_StreamingBesideAnArrayMethod_WritesTheRowBufferOnce()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: StreamUsers\n-- collection-type: IAsyncEnumerable\n" + Select + "\n-- name: ListUsers\n" + Select
        );

        var run = MethodRuns.Run(file, [Users(file, "StreamUsers"), Users(file, "ListUsers")]);

        run.ShouldBeClean();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        var methods = run.Sources[MethodRuns.MethodsFile];
        run.Sources.Keys.ShouldContain(RowBuffer.HintName);
        Count(methods, NewBuffer).ShouldBe(1);
        // The one buffer is read by the array method, which follows the streaming method in the file.
        var streaming = methods.IndexOf(" StreamUsers(\n", StringComparison.Ordinal);
        var array = methods.IndexOf(" ListUsers(\n", StringComparison.Ordinal);
        streaming.ShouldBeGreaterThan(-1);
        array.ShouldBeGreaterThan(streaming);
        methods[streaming..array].ShouldNotContain("SqlSourceRowBuffer");
        methods[array..].ShouldContain(NewBuffer);
    }

    [Fact]
    public void Run_FileMixingPooledAndNoArrayPoolMethods_PoolsOnlyThePooled()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: ListUsers\n" + Select + "\n-- name: ListOthers\n-- generator: no-array-pool\n" + Select
        );

        var run = MethodRuns.Run(file, [Users(file, "ListUsers"), Users(file, "ListOthers")]);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        run.Sources.Keys.ShouldContain(RowBuffer.HintName);
        Count(methods, "var rows = " + NewBuffer).ShouldBe(1);
        Count(methods, "var rows = new global::System.Collections.Generic.List<").ShouldBe(1);
    }

    // Under no-array-pool the method reads its rows into a list, as before the buffer, and the project has no buffer.
    [Fact]
    public void Run_ArrayMethodUnderNoArrayPool_WritesTheMethodWithAListAndNoBuffer()
    {
        var run = MethodRuns.Run(
            ListUsers,
            [Users(ListUsers, "ListUsers")],
            MethodRuns.SampleSource("Parameters = \"no-array-pool\"")
        );

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile]
            .ShouldBe(ListUsersMethods("new global::System.Collections.Generic.List<global::App.ListUsersDto>()"));
        run.Sources.Keys.ShouldNotContain(RowBuffer.HintName);
    }

    // The one pooled method clashes with the second type's and is dropped, so no method the project keeps uses the
    // buffer, though the first type keeps a method that reads one row.
    [Fact]
    public void Run_PooledMethodDroppedForAConflict_IsNoReasonForTheBuffer()
    {
        var a = new SqlFile(APath, "-- name: Get\n" + Select + "\n-- name: Find -> one\n" + Select);
        var b = new SqlFile(BPath, "-- name: Get -> one\n" + Select);

        var run = MethodRuns.Run(
            TwoTypesSource,
            a,
            Sidecars.Of(a, Users(a, "Get"), Users(a, "Find")),
            b,
            Sidecars.Of(b, Users(b, "Get"))
        );

        run.Problems().Count(problem => problem.StartsWith("SQLSRC274 ", StringComparison.Ordinal)).ShouldBe(2);
        run.Sources["App.First-Methods.g.cs"].ShouldContain(" Find(\n");
        run.Sources["App.First-Methods.g.cs"].ShouldNotContain(" Get(\n");
        run.Sources.Keys.ShouldNotContain(RowBuffer.HintName);
    }

    // A project on a framework older than .NET 8, or a language older than C# 12, gets no type's methods, so its pooled
    // many query is no reason for the buffer.
    [Theory]
    [InlineData(false, LanguageVersion.CSharp12, "SQLSRC003")]
    [InlineData(true, LanguageVersion.CSharp11, "SQLSRC012")]
    public void Run_UnsupportedFrameworkWithAPooledManyQuery_HasNoBufferFile(
        bool supportedFramework,
        LanguageVersion languageVersion,
        string id
    )
    {
        var run = GeneratorHarness.Run(
            OutputKind.CodeGen,
            [new SourceFile(GeneratorHarness.SourcePath, MethodRuns.Source)],
            [ListUsers, Sidecars.Of(ListUsers, Users(ListUsers, "ListUsers"))],
            supportedFramework: supportedFramework,
            languageVersion: languageVersion,
            references: [NpgsqlReferences.Npgsql10],
            dialect: "postgres"
        );

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith(id + " ");
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
        run.Sources.Keys.ShouldNotContain(RowBuffer.HintName);
    }

    [Theory]
    [InlineData("ExtensionClass", false)]
    [InlineData("Public", false)]
    [InlineData("Internal", false)]
    [InlineData("Private", false)]
    [InlineData("ExtensionClass", true)]
    [InlineData("Public", true)]
    [InlineData("Internal", true)]
    [InlineData("Private", true)]
    public void Run_EachPlacement_CompilesWithTheBuffer(string placement, bool nested)
    {
        var attribute = "[SqlSourceGenerate(MethodLocation = MethodLocation." + placement + ")]";
        var source = nested
            ? "using SqlSource;\n\nnamespace App\n{\n    public partial class Outer<TKey>\n    {\n        "
                + attribute
                + "\n        public partial class Sample;\n    }\n}\n"
            : "using SqlSource;\n\nnamespace App\n{\n    " + attribute + "\n    public partial class Sample;\n}\n";

        var run = MethodRuns.Run(ListUsers, [Users(ListUsers, "ListUsers")], source);

        run.ShouldBeClean();
        MethodsOf(run).ShouldContain(NewBuffer);
        run.Sources.Keys.ShouldContain(RowBuffer.HintName);
    }

    // The buffer is in the namespace SqlSource, so a type of the user's with its name, the methods on it, is no clash.
    [Fact]
    public void Run_TypeNamedLikeTheBuffer_CompilesBesideIt()
    {
        var source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(MethodLocation = MethodLocation.Public)]
                public partial class SqlSourceRowBuffer<T>;
            }
            """;

        var run = MethodRuns.Run(ListUsers, [Users(ListUsers, "ListUsers")], source);

        run.ShouldBeClean();
        MethodsOf(run).ShouldContain(NewBuffer);
        run.Sources.Keys.ShouldContain(RowBuffer.HintName);
    }

    // The global namespace, one under an escaped keyword, and one of the user's named SqlSource, below App, which would
    // hide the buffer's from a name that did not start at global::.
    [Theory]
    [InlineData("")]
    [InlineData("namespace @class.Rows;\n\n")]
    [InlineData("namespace App.SqlSource;\n\n")]
    public void Run_TypeInAnyNamespace_NamesTheBufferFromGlobal(string namespaceLine)
    {
        var source = "using SqlSource;\n\n" + namespaceLine + "[SqlSourceGenerate]\npublic partial class Sample;\n";

        var run = MethodRuns.Run(ListUsers, [Users(ListUsers, "ListUsers")], source);

        run.ShouldBeClean();
        MethodsOf(run).ShouldContain(NewBuffer);
    }

    // The file of ListUsers's methods on App.Sample, its rows read into the given buffer.
    private static string ListUsersMethods(string buffer) =>
        GeneratedFile.Joined(
            $$$"""
            {{{GeneratedFile.ModelHeader}}}
            namespace App
            {
                /// <summary>
                /// The query methods of <c>App.Sample</c>, which extend the connection they run on.
                /// </summary>
                public static partial class SampleExtensions
                {
                    /// <summary>
                    /// The <c>ListUsers</c> query from <c>Users.sql</c>.
                    /// </summary>
                    /// <remarks>
                    /// <code>
                    /// SELECT id, name FROM users;
                    /// </code>
                    /// </remarks>
                    /// <param name="connection">The connection to run the query on.  ↩
                It is opened when it is closed, and closed again when the method opened it.</param>
                    /// <param name="transaction">The transaction to run the query in, or null.</param>
                    /// <param name="commandTimeout">↩
                The command's timeout in seconds, or null for the driver's.</param>
                    /// <param name="cancellationToken">Cancels the query.</param>
                    /// <returns>The rows the query returned.</returns>
                    public static async global::System.Threading.Tasks.Task<global::App.ListUsersDto[]> ↩
                ListUsers(
                        this global::System.Data.Common.DbConnection connection,
                        global::System.Data.Common.DbTransaction? transaction = null,
                        int? commandTimeout = null,
                        global::System.Threading.CancellationToken cancellationToken = default)
                    {
                        global::System.ArgumentNullException.ThrowIfNull(connection);
                        const string sql = "SELECT id, name FROM users;";
                        var wasClosed = connection.State == global::System.Data.ConnectionState.Closed;
                        if (wasClosed)
                        {
                            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                        }

                        try
                        {
                            var command = connection.CreateCommand();
                            await using var commandScope = ↩
                global::System.Threading.Tasks.TaskAsyncEnumerableExtensions.ConfigureAwait(command, false);
                            command.CommandText = sql;
                            command.Transaction = transaction;
                            if (commandTimeout.HasValue)
                            {
                                command.CommandTimeout = commandTimeout.Value;
                            }

                            var reader = await command.ExecuteReaderAsync(↩
                global::System.Data.CommandBehavior.Default, cancellationToken).ConfigureAwait(false);
                            await using var readerScope = ↩
                global::System.Threading.Tasks.TaskAsyncEnumerableExtensions.ConfigureAwait(reader, false);
                            var rows = {{{buffer}}};
                            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                            {
                                rows.Add(new global::App.ListUsersDto(reader.GetFieldValue<int>(0), ↩
                reader.IsDBNull(1) ? default(string?) : reader.GetFieldValue<string>(1)));
                            }

                            return rows.ToArray();
                        }
                        finally
                        {
                            if (wasClosed)
                            {
                                await connection.CloseAsync().ConfigureAwait(false);
                            }
                        }
                    }
                }
            }

            """
        );

    // ListUsers on App.Sample with the attribute's arguments.
    private static GeneratorRun RunListUsers(string arguments) =>
        MethodRuns.Run(ListUsers, [Users(ListUsers, "ListUsers")], MethodRuns.SampleSource(arguments));

    // The entry of a query of the rows id and name.
    private static SidecarEntry Users(SqlFile file, string name) =>
        Entry(file, name, [], [Column("id"), Column("name", Text, nullable: true)]);

    // The one file of methods of the run.
    private static string MethodsOf(GeneratorRun run) =>
        run.Sources.Single(source => source.Key.EndsWith("-Methods.g.cs", StringComparison.Ordinal)).Value;

    private static int Count(string text, string part) =>
        Regex.Count(text, Regex.Escape(part), RegexOptions.None, TimeSpan.FromSeconds(1));
}
