// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;

namespace SqlSource.Tests.Generator;

// The buffer a many method reads its rows into when it returns an array, a list, an immutable array or one of their
// interfaces: written once at the end of a file of methods that uses it, at the depth of the namespace, and named from
// global:: in each method.  Each run is at CodeGen under postgres on Npgsql 10, and every method must compile without
// a warning.
public sealed class RowBufferTests
{
    private const string Struct = "file struct SqlSourceRowBuffer<T>\n";

    private const string Select = "SELECT id, name FROM users;\n";

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
        methods.ShouldContain("var rows = new global::App.SqlSourceRowBuffer<global::App.ListUsersDto>();\n");
        methods.ShouldContain("return rows." + result + "();\n");
        Count(methods, Struct).ShouldBe(1);
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
    }

    // One of each pooled type in one file: the struct is written once, and its members all compile for every row type.
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
        Count(methods, Struct).ShouldBe(1);
        Count(methods, "new global::App.SqlSourceRowBuffer<").ShouldBe(collections.Length);
    }

    [Fact]
    public void Run_SeveralPooledMethods_WriteTheBufferOnce()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: ListUsers\n-- collection-type: Array\n"
                + Select
                + "\n-- name: ListOthers\n-- collection-type: IEnumerable\n"
                + Select
                + "\n-- name: ListMore\n-- collection-type: IReadOnlyList\n"
                + Select
        );

        var run = MethodRuns.Run(file, [Users(file, "ListUsers"), Users(file, "ListOthers"), Users(file, "ListMore")]);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        Count(methods, Struct).ShouldBe(1);
        Count(methods, "new global::App.SqlSourceRowBuffer<").ShouldBe(3);
    }

    [Fact]
    public void Run_FileWithNoPooledMethod_HasNoBuffer()
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
        Count(methods, Struct).ShouldBe(1);
        Count(methods, "var rows = new global::App.SqlSourceRowBuffer<").ShouldBe(1);
        Count(methods, "var rows = new global::System.Collections.Generic.List<").ShouldBe(1);
    }

    [Fact]
    public void Run_PooledMethod_WritesTheBufferAfterTheClassInTheNamespace()
    {
        var run = MethodRuns.Run(ListUsers, [Users(ListUsers, "ListUsers")]);

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile]
            .ShouldBe(
                ListUsersMethods(
                    "new global::App.SqlSourceRowBuffer<global::App.ListUsersDto>()",
                    "\n" + GeneratedFile.RowBuffer + "}\n"
                )
            );
    }

    // Under no-array-pool the method reads its rows into a list, as before the buffer, and the file has no buffer.
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
            .ShouldBe(
                ListUsersMethods("new global::System.Collections.Generic.List<global::App.ListUsersDto>()", "}\n")
            );
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
        var methods = MethodsOf(run);
        methods.ShouldContain("\n    " + Struct);
        methods.ShouldContain("new global::App.SqlSourceRowBuffer<");
    }

    [Fact]
    public void Run_TypeNamedLikeTheBuffer_NamesTheBufferOtherwise()
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
        var methods = MethodsOf(run);
        methods.ShouldContain("file struct SqlSourceRowBufferOfRows<T>\n");
        methods.ShouldContain("new global::App.SqlSourceRowBufferOfRows<");
    }

    [Theory]
    [InlineData("", "global::SqlSourceRowBuffer<")]
    [InlineData("namespace @class.Rows;\n\n", "global::@class.Rows.SqlSourceRowBuffer<")]
    public void Run_TypeInTheGlobalNamespaceOrUnderAnEscapedKeyword_NamesTheBufferFromGlobal(
        string namespaceLine,
        string buffer
    )
    {
        var source = "using SqlSource;\n\n" + namespaceLine + "[SqlSourceGenerate]\npublic partial class Sample;\n";

        var run = MethodRuns.Run(ListUsers, [Users(ListUsers, "ListUsers")], source);

        run.ShouldBeClean();
        MethodsOf(run).ShouldContain(buffer);
    }

    // The file of ListUsers's methods on App.Sample, its rows read into the buffer, and the end after the extension
    // class: the buffer and the namespace's brace, or the brace alone.
    private static string ListUsersMethods(string buffer, string end) =>
        Joined(
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
            {{{end}}}
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

    // An expected text with each line that ends in the mark joined to the next, whose indentation goes.
    private static string Joined(string text) =>
        Regex.Replace(text, "↩\n *", string.Empty, RegexOptions.None, TimeSpan.FromSeconds(1));
}
