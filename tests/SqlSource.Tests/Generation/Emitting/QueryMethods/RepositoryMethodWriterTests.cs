using System;
using System.Globalization;
using System.Text;
using Shouldly;
using SqlSource.Generation.Emitting.QueryMethods;
using SqlSource.Generation.Methods;
using SqlSource.Generation.Repositories;
using SqlSource.Parsing.Tokens;
using SqlSource.Settings;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;

namespace SqlSource.Tests.Generation.Emitting.QueryMethods;

// The methods of a repository's queries: instance methods on the type that take no connection, transaction or timeout
// and get their connection from their source, each kind by its own statement, disposed as the command is when the
// method made it.
public sealed class RepositoryMethodWriterTests
{
    // GetUser as a repository's method on the type, without its token, whose connection is the data source in the field
    // _dataSource.
    private static readonly QueryMethod Repository = QueryMethodWriterTests.GetUser with
    {
        Segments = TestSidecars.Of(TestModels.Literal("SELECT id, name FROM users WHERE id = @id")),
        Host = new MethodHost(Visibility.Public, MemberLocation.Class, null),
        Source = TestModels.Source(),
    };

    // The scope that disposes a repository method's own connection, as the command and the reader are disposed.
    private const string ConnectionScope =
        "    await using var connectionScope = "
        + "global::System.Threading.Tasks.TaskAsyncEnumerableExtensions.ConfigureAwait(connection, false);\n";

    // The null source's exception, as a repository method of GetUserRepository writes it for its member.
    private const string NullSource =
        "throw new global::System.InvalidOperationException("
        + "\"The connection source '_dataSource' of 'App.UserRepository' is null.\")";

    // A repository's method: an instance method that gets its connection from its source, opens it when it is closed
    // and disposes it when the method made it, and takes neither a connection, a transaction nor a timeout.
    [Fact]
    public void Append_RepositoryDataSource_IsTheWholeMethod() =>
        WriteAsPlanned(Repository)
            .ShouldBe(
                GeneratedFile.Joined(
                    """
                    /// <summary>
                    /// The <c>GetUser</c> query from <c>Users.sql</c>.
                    /// </summary>
                    /// <remarks>
                    /// <code>
                    /// SELECT id, name FROM users WHERE id = @id
                    /// </code>
                    /// </remarks>
                    /// <param name="parameters">The query's parameters.</param>
                    /// <param name="cancellationToken">Cancels the query.</param>
                    /// <returns>The row, or null when there is none.</returns>
                    /// <exception cref="T:SqlSource.UnexpectedRowCountException">↩
                    The query returned more than one row.</exception>
                    public async global::System.Threading.Tasks.Task<global::App.GetUserDto?> GetUser(
                        global::App.GetUserParams parameters,
                        global::System.Threading.CancellationToken cancellationToken = default)
                    {
                        global::System.ArgumentNullException.ThrowIfNull(parameters);
                        const string sql = "SELECT id, name FROM users WHERE id = @id";
                        var connection = await (this._dataSource ?? ↩
                    throw new global::System.InvalidOperationException(↩
                    "The connection source '_dataSource' of 'App.UserRepository' is null.")).↩
                    OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
                        await using var connectionScope = ↩
                    global::System.Threading.Tasks.TaskAsyncEnumerableExtensions.ConfigureAwait(connection, false);
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

                            {
                                var parameter = new global::Npgsql.NpgsqlParameter<int>();
                                parameter.ParameterName = "id";
                                parameter.DataTypeName = "pg_catalog.int4";
                                parameter.TypedValue = parameters.Id;
                                command.Parameters.Add(parameter);
                            }

                            var reader = await command.ExecuteReaderAsync(↩
                    global::System.Data.CommandBehavior.Default, cancellationToken).ConfigureAwait(false);
                            await using var readerScope = ↩
                    global::System.Threading.Tasks.TaskAsyncEnumerableExtensions.ConfigureAwait(reader, false);
                            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                            {
                                return null;
                            }

                            var row = new global::App.GetUserDto(reader.GetFieldValue<int>(0), ↩
                    reader.IsDBNull(1) ? default(string?) : reader.GetFieldValue<string>(1));
                            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                            {
                                throw new global::SqlSource.UnexpectedRowCountException("GetUser", true);
                            }

                            return row;
                        }
                        finally
                        {
                            if (wasClosed)
                            {
                                await connection.CloseAsync().ConfigureAwait(false);
                            }
                        }
                    }

                    """
                )
            );

    // Each kind of source gives its connection by its own statement: a connection string a new driver connection, a
    // data source an opened one, a factory what it returns, awaited when it is asynchronous and given the token when
    // it takes one, each disposed as the command is, and a borrowed connection itself, which the method does not
    // dispose.
    [Theory]
    [InlineData(
        nameof(ConnectionSourceKind.ConnectionString),
        nameof(CandidateMember.Field),
        false,
        false,
        "var connection = new global::Npgsql.NpgsqlConnection(this._dataSource ?? {0});\n" + ConnectionScope
    )]
    [InlineData(
        nameof(ConnectionSourceKind.ConnectionString),
        nameof(CandidateMember.Method),
        false,
        false,
        "var connection = new global::Npgsql.NpgsqlConnection(this._dataSource() ?? {0});\n" + ConnectionScope
    )]
    [InlineData(
        nameof(ConnectionSourceKind.ConnectionString),
        nameof(CandidateMember.Method),
        false,
        true,
        "var connection = new global::Npgsql.NpgsqlConnection(this._dataSource(cancellationToken) ?? {0});\n"
            + ConnectionScope
    )]
    [InlineData(
        nameof(ConnectionSourceKind.DataSource),
        nameof(CandidateMember.Field),
        false,
        false,
        "var connection = await (this._dataSource ?? {0})"
            + ".OpenConnectionAsync(cancellationToken).ConfigureAwait(false);\n"
            + ConnectionScope
    )]
    [InlineData(
        nameof(ConnectionSourceKind.Factory),
        nameof(CandidateMember.Method),
        false,
        false,
        "var connection = this._dataSource() ?? {0};\n" + ConnectionScope
    )]
    [InlineData(
        nameof(ConnectionSourceKind.Factory),
        nameof(CandidateMember.Method),
        false,
        true,
        "var connection = this._dataSource(cancellationToken) ?? {0};\n" + ConnectionScope
    )]
    [InlineData(
        nameof(ConnectionSourceKind.Factory),
        nameof(CandidateMember.Method),
        true,
        false,
        "var connection = await this._dataSource().ConfigureAwait(false) ?? {0};\n" + ConnectionScope
    )]
    [InlineData(
        nameof(ConnectionSourceKind.Factory),
        nameof(CandidateMember.Method),
        true,
        true,
        "var connection = await this._dataSource(cancellationToken).ConfigureAwait(false) ?? {0};\n" + ConnectionScope
    )]
    [InlineData(
        nameof(ConnectionSourceKind.Borrowed),
        nameof(CandidateMember.Property),
        false,
        false,
        "var connection = this._dataSource ?? {0};\n"
    )]
    public void Append_RepositoryKind_AcquiresAsTheTableSays(
        string kindName,
        string memberName,
        bool isAsync,
        bool takesCancellationToken,
        string statement
    )
    {
        var source = TestModels.Source(
            Enum.Parse<ConnectionSourceKind>(kindName),
            Enum.Parse<CandidateMember>(memberName),
            isAsync: isAsync,
            takesCancellationToken: takesCancellationToken
        );

        WriteAsPlanned(Repository with { Source = source })
            .ShouldContain(
                "    const string sql = \"SELECT id, name FROM users WHERE id = @id\";\n"
                    + "    "
                    + string.Format(CultureInfo.InvariantCulture, statement, NullSource)
                    + "    var wasClosed = connection.State == global::System.Data.ConnectionState.Closed;\n"
            );
    }

    // A connection string is the engine's driver's connection.
    [Fact]
    public void Append_RepositoryConnectionStringOnSqlServer_MakesASqlConnection() =>
        WriteAsPlanned(
                Repository with
                {
                    Engine = DatabaseEngine.SqlServer,
                    Input = null,
                    Source = TestModels.Source(ConnectionSourceKind.ConnectionString),
                }
            )
            .ShouldContain(
                "    var connection = new global::Microsoft.Data.SqlClient.SqlConnection("
                    + "this._dataSource ?? "
                    + NullSource
                    + ");\n"
            );

    // The iterator reaches the type's member, so it is no static local function, and it gets the connection when the
    // loop starts, so that the connection lives until the loop ends.
    [Fact]
    public void Append_RepositoryStream_AcquiresInsideANonStaticIterator()
    {
        var text = WriteAsPlanned(
            Repository with
            {
                Shape = ResultShape.Many,
                Collection = CollectionKind.IAsyncEnumerable,
            }
        );
        var call = text.IndexOf("return ReadRows(", StringComparison.Ordinal);

        call.ShouldBeGreaterThan(0);
        text.ShouldContain(
            "public global::System.Collections.Generic.IAsyncEnumerable<global::App.GetUserDto> GetUser(\n"
                + "    global::App.GetUserParams parameters,\n"
                + "    global::System.Threading.CancellationToken cancellationToken = default)\n"
        );
        text.ShouldContain(
            "    return ReadRows(parameters, cancellationToken);\n\n"
                + "    async global::System.Collections.Generic.IAsyncEnumerable<global::App.GetUserDto> ReadRows(\n"
                + "        global::App.GetUserParams parameters,\n"
                + "        [global::System.Runtime.CompilerServices.EnumeratorCancellation] "
                + "global::System.Threading.CancellationToken cancellationToken)\n"
                + "    {\n"
                + "        const string sql = \"SELECT id, name FROM users WHERE id = @id\";\n"
                + "        var connection = await (this._dataSource ?? "
                + NullSource
                + ").OpenConnectionAsync(cancellationToken).ConfigureAwait(false);\n"
        );
        text.ShouldNotContain("static");
        text[..call].ShouldNotContain("connection");
    }

    // A struct's local function reaches neither this nor a primary-constructor parameter, so a struct repository's
    // stream reads its source's value before the iterator, or copies the struct for an instance method, which the
    // iterator calls on the copy so that overloads resolve as written; the iterator captures the local.  A static
    // method is reached through its type.
    [Theory]
    [InlineData(
        nameof(ConnectionSourceKind.DataSource),
        nameof(CandidateMember.Field),
        "this._dataSource",
        "    var source = this._dataSource;\n",
        "var connection = await (source ?? "
    )]
    [InlineData(
        nameof(ConnectionSourceKind.Factory),
        nameof(CandidateMember.Method),
        "this._dataSource",
        "    var self = this;\n",
        "var connection = self._dataSource() ?? "
    )]
    [InlineData(
        nameof(ConnectionSourceKind.ConnectionString),
        nameof(CandidateMember.Method),
        "this._dataSource",
        "    var self = this;\n",
        "var connection = new global::Npgsql.NpgsqlConnection(self._dataSource() ?? "
    )]
    [InlineData(
        nameof(ConnectionSourceKind.Factory),
        nameof(CandidateMember.Method),
        "Repo._dataSource",
        "",
        "var connection = Repo._dataSource() ?? "
    )]
    public void Append_StructRepositoryStream_ReadsTheSourceBeforeTheIterator(
        string kindName,
        string memberName,
        string access,
        string read,
        string acquire
    )
    {
        var source = TestModels.Source(
            Enum.Parse<ConnectionSourceKind>(kindName),
            Enum.Parse<CandidateMember>(memberName),
            access: access,
            inStruct: true
        );

        var text = WriteAsPlanned(
            Repository with
            {
                Shape = ResultShape.Many,
                Collection = CollectionKind.IAsyncEnumerable,
                Source = source,
            }
        );

        text.ShouldContain(
            "    global::System.ArgumentNullException.ThrowIfNull(parameters);\n\n"
                + read
                + "    return ReadRows(parameters, cancellationToken);\n\n"
                + "    async global::System.Collections.Generic.IAsyncEnumerable<global::App.GetUserDto> ReadRows(\n"
        );
        text.ShouldContain("        " + acquire);
        text[text.IndexOf("ReadRows(\n", StringComparison.Ordinal)..].ShouldNotContain("this.");
    }

    // A struct repository's method that does not stream reaches its source where it is: an asynchronous method of a
    // struct may read this.
    [Fact]
    public void Append_StructRepositoryNotStreaming_ReadsTheSourceWhereItIs() =>
        WriteAsPlanned(Repository with { Source = TestModels.Source(inStruct: true) })
            .ShouldContain("    var connection = await (this._dataSource ?? ");

    // A token named as the local of the source's value takes no name from it.
    [Fact]
    public void Append_StructRepositoryStreamTokenNamedSource_RenamesTheLocal()
    {
        var text = WriteAsPlanned(
            Repository with
            {
                Shape = ResultShape.Many,
                Collection = CollectionKind.IAsyncEnumerable,
                Segments = TestSidecars.Of(TestModels.Literal("SELECT id, name FROM "), Token("source")),
                Source = TestModels.Source(inStruct: true),
            }
        );

        text.ShouldContain("    var source1 = this._dataSource;\n");
        text.ShouldContain("        var connection = await (source1 ?? ");
    }

    // A stream with nothing to check returns its iterator at once, with no blank line before.
    [Fact]
    public void Append_RepositoryStreamWithoutChecks_ReturnsTheIteratorFirst() =>
        WriteAsPlanned(
                Repository with
                {
                    Shape = ResultShape.Many,
                    Collection = CollectionKind.IAsyncEnumerable,
                    Input = null,
                }
            )
            .ShouldContain(
                "    global::System.Threading.CancellationToken cancellationToken = default)\n"
                    + "{\n"
                    + "    return ReadRows(cancellationToken);\n"
            );

    // A repository's method takes no transaction, so it neither refuses a completed one nor checks for a rollback, and
    // it still reads the later results, which can hold an error the caller is to see.
    [Fact]
    public void Append_RepositoryOnSqlServer_WritesNoTransactionCheck()
    {
        var text = WriteAsPlanned(
            Repository with
            {
                Engine = DatabaseEngine.SqlServer,
                Input = null,
                Shape = ResultShape.Many,
            }
        );

        text.ShouldNotContain("transaction");
        text.ShouldNotContain("TransactionRolledBackException");
        text.ShouldNotContain("commandTimeout");
        text.ShouldNotContain("CommandTimeout");
        text.ShouldContain(Drain("        "));
    }

    // A primary-constructor parameter named as the connection's local is reached by its name, so the local takes
    // another.
    [Fact]
    public void Append_RepositorySourceParameterNamedConnection_RenamesTheLocal()
    {
        var source = TestModels.Source(
            member: CandidateMember.Parameter,
            name: "connection",
            access: "connection",
            leadingName: "connection"
        );

        var text = WriteAsPlanned(Repository with { Source = source });

        text.ShouldContain(
            "    var connection1 = await (connection ?? throw new global::System.InvalidOperationException("
                + "\"The connection source 'connection' of 'App.UserRepository' is null.\"))"
                + ".OpenConnectionAsync(cancellationToken).ConfigureAwait(false);\n"
                + "    await using var connectionScope = "
                + "global::System.Threading.Tasks.TaskAsyncEnumerableExtensions.ConfigureAwait(connection1, false);\n"
                + "    var wasClosed = connection1.State == global::System.Data.ConnectionState.Closed;\n"
        );
        text.ShouldContain("        var command = connection1.CreateCommand();\n");
    }

    // The visibility is the placement's, and a repository's method is an instance method whatever it is.
    [Theory]
    [InlineData(nameof(Visibility.Public), "public async ")]
    [InlineData(nameof(Visibility.Internal), "internal async ")]
    [InlineData(nameof(Visibility.Protected), "protected async ")]
    [InlineData(nameof(Visibility.Private), "private async ")]
    public void Append_RepositoryVisibility_IsAnInstanceMethodOfIt(string visibilityName, string modifiers)
    {
        var text = WriteAsPlanned(Repository with { Host = OnType(visibilityName) });

        text.ShouldContain(
            "\n"
                + modifiers
                + "global::System.Threading.Tasks.Task<global::App.GetUserDto?> GetUser(\n"
                + "    global::App.GetUserParams parameters,\n"
        );
        text.ShouldNotContain("static");
    }

    private static SqlSegment Token(string name) => new(SqlSegmentKind.Token, name);

    // The loop that reads the results after the rows, at an indent.
    private static string Drain(string indent) =>
        indent
        + "while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))\n"
        + indent
        + "{\n"
        + indent
        + "}\n";

    // The method on the host it has, as a repository's method is planned.
    private static string WriteAsPlanned(QueryMethod method)
    {
        var builder = new StringBuilder();
        QueryMethodWriter.Append(builder, string.Empty, method);
        return builder.ToString();
    }

    // A method on the type, of the visibility.
    private static MethodHost OnType(string visibilityName) =>
        new(Enum.Parse<Visibility>(visibilityName), MemberLocation.Class, null);
}
