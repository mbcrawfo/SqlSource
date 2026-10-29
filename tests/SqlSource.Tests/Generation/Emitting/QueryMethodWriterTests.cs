using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Methods;
using SqlSource.Parsing.Tokens;
using SqlSource.Settings;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;

namespace SqlSource.Tests.Generation.Emitting;

public sealed class QueryMethodWriterTests
{
    private const string Vector = "global::Microsoft.Data.SqlTypes.SqlVector<float>";

    private const string CompletedTransactionDocumentation =
        "/// <exception cref=\"global::System.InvalidOperationException\">"
        + "The transaction has completed: it was committed or rolled back before the query ran.</exception>\n";

    private const string ReaderLine =
        "var reader = await command.ExecuteReaderAsync("
        + "global::System.Data.CommandBehavior.Default, cancellationToken).ConfigureAwait(false);\n";

    // The row buffer as the methods of a file in the namespace App name it.
    private const string RowBufferType = "global::App.SqlSourceRowBuffer";

    // The type integer, as the entry of a parameter describes it.
    private static readonly PostgresType Integer = TestSidecars.Postgres(bindName: "integer");

    // GetUser -> one-optional: one parameter, one token, a record of two columns.  RowBufferTests derives
    // its method from it.
    internal static readonly QueryMethod GetUser = new(
        "GetUser",
        "GetUser",
        "The <c>GetUser</c> query from <c>Users.sql</c>.",
        ResultShape.OneOptional,
        CollectionKind.List,
        DatabaseEngine.PostgreSql,
        TestSidecars.Of(TestModels.Literal("SELECT id, name FROM users WHERE id = @id ORDER BY "), Token("orderBy")),
        true,
        true,
        new MethodInput(
            "global::App.GetUserParams",
            TestSidecars.Of(new MethodParameter("id", "Id", "int", true, false, Described: Integer))
        ),
        new MethodOutput(
            "global::App.GetUserDto",
            true,
            TestSidecars.Of(
                new MethodColumn("id", "Id", "int", false),
                new MethodColumn("name", "Name", "string", true)
            ),
            TestSidecars.Of(0, 1)
        ),
        TestModels.NameLocation("Users.sql")
    );

    // GetUser on SQL Server, without parameters, whose binding is SqlServerBindingTests'.
    private static readonly QueryMethod SqlServerGetUser = GetUser with
    {
        Engine = DatabaseEngine.SqlServer,
        Input = null,
    };

    // GetUser streaming its rows.
    private static readonly QueryMethod Stream = GetUser with
    {
        Shape = ResultShape.Many,
        Collection = CollectionKind.IAsyncEnumerable,
    };

    // GetUser streaming its rows on SQL Server, without parameters.
    private static readonly QueryMethod SqlServerStream = Stream with
    {
        Engine = DatabaseEngine.SqlServer,
        Input = null,
    };

    [Fact]
    public void Append_OneOptionalWithParametersAndAToken_IsTheWholeMethod()
    {
        var builder = new StringBuilder("before\n");

        QueryMethodWriter.Append(builder, "    ", GetUser, "public static", isExtension: true, RowBufferType);

        builder
            .ToString()
            .ShouldBe(
                GeneratedFile.Joined(
                    """
                    before
                        /// <summary>
                        /// The <c>GetUser</c> query from <c>Users.sql</c>.
                        /// </summary>
                        /// <remarks>
                        /// <code>
                        /// SELECT id, name FROM users WHERE id = @id ORDER BY {{orderBy}}
                        /// </code>
                        /// </remarks>
                        /// <param name="connection">The connection to run the query on.  ↩
                    It is opened when it is closed, and closed again when the method opened it.</param>
                        /// <param name="parameters">The query's parameters.</param>
                        /// <param name="orderBy">The text that replaces <c>{{orderBy}}</c>.</param>
                        /// <param name="transaction">The transaction to run the query in, or null.</param>
                        /// <param name="commandTimeout">↩
                    The command's timeout in seconds, or null for the driver's.</param>
                        /// <param name="cancellationToken">Cancels the query.</param>
                        /// <returns>The row, or null when there is none.</returns>
                        /// <exception cref="global::SqlSource.UnexpectedRowCountException">↩
                    The query returned more than one row.</exception>
                        public static async global::System.Threading.Tasks.Task<global::App.GetUserDto?> GetUser(
                            this global::System.Data.Common.DbConnection connection,
                            global::App.GetUserParams parameters,
                            string orderBy,
                            global::System.Data.Common.DbTransaction? transaction = null,
                            int? commandTimeout = null,
                            global::System.Threading.CancellationToken cancellationToken = default)
                        {
                            global::System.ArgumentNullException.ThrowIfNull(connection);
                            global::System.ArgumentNullException.ThrowIfNull(parameters);
                            global::System.ArgumentException.ThrowIfNullOrWhiteSpace(orderBy);
                            var sql = string.Create(
                                checked(51 + orderBy.Length),
                                orderBy,
                                static (span, state) =>
                                {
                                    "SELECT id, name FROM users WHERE id = @id ORDER BY ".CopyTo(span);
                                    span = span.Slice(51);
                                    state.CopyTo(span);
                                }
                            );
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
                                #pragma warning disable CA2100 // The text of a token is the caller's own choice.
                                command.CommandText = sql;
                                #pragma warning restore CA2100
                                command.Transaction = transaction;
                                if (commandTimeout.HasValue)
                                {
                                    command.CommandTimeout = commandTimeout.Value;
                                }

                                {
                                    var parameter = new global::Npgsql.NpgsqlParameter<int>();
                                    parameter.ParameterName = "id";
                                    parameter.DataTypeName = "integer";
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
    }

    [Theory]
    [InlineData(
        nameof(ResultShape.Many),
        "global::System.Threading.Tasks.Task<global::System.Collections.Generic.List<global::App.GetUserDto>>",
        ReaderLine,
        "<returns>The rows the query returned.</returns>",
        true
    )]
    [InlineData(
        nameof(ResultShape.One),
        "global::System.Threading.Tasks.Task<global::App.GetUserDto>",
        ReaderLine,
        "<returns>The row.</returns>",
        true
    )]
    [InlineData(
        nameof(ResultShape.OneOptional),
        "global::System.Threading.Tasks.Task<global::App.GetUserDto?>",
        ReaderLine,
        "<returns>The row, or null when there is none.</returns>",
        true
    )]
    [InlineData(
        nameof(ResultShape.None),
        "global::System.Threading.Tasks.Task",
        "        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);\n    }\n",
        "<returns>A task that completes when the query has run.</returns>",
        false
    )]
    [InlineData(
        nameof(ResultShape.RowCount),
        "global::System.Threading.Tasks.Task<int>",
        "        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);\n    }\n",
        "<returns>The number of rows the query affected.</returns>",
        false
    )]
    public void Append_EachShape_ReturnsAndExecutesAsTheShapeSays(
        string shapeName,
        string returns,
        string execute,
        string returnsXml,
        bool readsRows
    )
    {
        var text = Write(GetUser with { Shape = Enum.Parse<ResultShape>(shapeName) });

        text.ShouldContain("public static async " + returns + " GetUser(\n");
        text.ShouldContain(execute);
        text.ShouldContain("/// " + returnsXml + "\n");
        text.Contains("ExecuteReaderAsync", StringComparison.Ordinal).ShouldBe(readsRows);
        text.Contains("ExecuteNonQueryAsync", StringComparison.Ordinal).ShouldBe(!readsRows);
    }

    // One row exactly: no row and more than one are each an UnexpectedRowCountException, which the summary names.
    [Fact]
    public void Append_ShapeOne_ThrowsOnNoRowAndOnMoreThanOne()
    {
        var text = Write(GetUser with { Shape = ResultShape.One });

        text.ShouldContain(
            "/// <exception cref=\"global::SqlSource.UnexpectedRowCountException\">"
                + "The query returned no rows, or more than one.</exception>\n"
        );
        text.ShouldNotContain("return null;");
        text.ShouldContain(
            "            throw new global::SqlSource.UnexpectedRowCountException(\"GetUser\", false);\n"
        );
        text.ShouldContain("            throw new global::SqlSource.UnexpectedRowCountException(\"GetUser\", true);\n");
    }

    // At most one row: no row is null, and more than one an UnexpectedRowCountException, which the summary names.
    [Fact]
    public void Append_ShapeOneOptional_ReturnsNullForNoRowAndThrowsOnMoreThanOne()
    {
        var text = Write(GetUser with { Shape = ResultShape.OneOptional });

        text.ShouldContain(
            "/// <exception cref=\"global::SqlSource.UnexpectedRowCountException\">"
                + "The query returned more than one row.</exception>\n"
        );
        text.ShouldContain("            return null;\n");
        text.ShouldNotContain("UnexpectedRowCountException(\"GetUser\", false)");
        text.ShouldContain("            throw new global::SqlSource.UnexpectedRowCountException(\"GetUser\", true);\n");
    }

    // A shape that expects no count of rows throws no UnexpectedRowCountException and documents none.
    [Theory]
    [InlineData(nameof(ResultShape.Many))]
    [InlineData(nameof(ResultShape.None))]
    [InlineData(nameof(ResultShape.RowCount))]
    public void Append_ShapeWithoutAnExpectedCount_ThrowsNoUnexpectedRowCount(string shapeName)
    {
        var text = Write(GetUser with { Shape = Enum.Parse<ResultShape>(shapeName) });

        text.ShouldNotContain("UnexpectedRowCountException");
        text.ShouldNotContain("<exception");
    }

    [Theory]
    [InlineData(nameof(CollectionKind.Array), "global::App.GetUserDto[]", "RowBuffer", "rows.ToArray()")]
    [InlineData(
        nameof(CollectionKind.IEnumerable),
        "global::System.Collections.Generic.IEnumerable<global::App.GetUserDto>",
        "RowBuffer",
        "rows.ToArray()"
    )]
    [InlineData(
        nameof(CollectionKind.IReadOnlyCollection),
        "global::System.Collections.Generic.IReadOnlyCollection<global::App.GetUserDto>",
        "RowBuffer",
        "rows.ToArray()"
    )]
    [InlineData(
        nameof(CollectionKind.IReadOnlyList),
        "global::System.Collections.Generic.IReadOnlyList<global::App.GetUserDto>",
        "RowBuffer",
        "rows.ToArray()"
    )]
    [InlineData(
        nameof(CollectionKind.List),
        "global::System.Collections.Generic.List<global::App.GetUserDto>",
        "RowBuffer",
        "rows.ToList()"
    )]
    [InlineData(
        nameof(CollectionKind.IList),
        "global::System.Collections.Generic.IList<global::App.GetUserDto>",
        "RowBuffer",
        "rows.ToList()"
    )]
    [InlineData(
        nameof(CollectionKind.ICollection),
        "global::System.Collections.Generic.ICollection<global::App.GetUserDto>",
        "RowBuffer",
        "rows.ToList()"
    )]
    [InlineData(
        nameof(CollectionKind.ImmutableArray),
        "global::System.Collections.Immutable.ImmutableArray<global::App.GetUserDto>",
        "RowBuffer",
        "rows.ToImmutableArray()"
    )]
    [InlineData(
        nameof(CollectionKind.ImmutableList),
        "global::System.Collections.Immutable.ImmutableList<global::App.GetUserDto>",
        "ImmutableList",
        "rows.ToImmutable()"
    )]
    [InlineData(
        nameof(CollectionKind.IImmutableList),
        "global::System.Collections.Immutable.IImmutableList<global::App.GetUserDto>",
        "ImmutableList",
        "rows.ToImmutable()"
    )]
    public void Append_EachCollectionType_BuffersAndMaterialisesAsDecided(
        string collectionName,
        string returns,
        string buffer,
        string result
    )
    {
        var text = Write(
            GetUser with
            {
                Shape = ResultShape.Many,
                Collection = Enum.Parse<CollectionKind>(collectionName),
            }
        );

        text.ShouldContain("public static async global::System.Threading.Tasks.Task<" + returns + "> GetUser(\n");
        text.ShouldContain(
            "        var rows = "
                + buffer switch
                {
                    "List" => "new global::System.Collections.Generic.List<global::App.GetUserDto>()",
                    "RowBuffer" => "new global::App.SqlSourceRowBuffer<global::App.GetUserDto>()",
                    _ => "global::System.Collections.Immutable." + buffer + ".CreateBuilder<global::App.GetUserDto>()",
                }
                + ";\n"
                + "        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))\n"
                + "        {\n"
                + "            rows.Add(new global::App.GetUserDto(reader.GetFieldValue<int>(0), "
                + "reader.IsDBNull(1) ? default(string?) : reader.GetFieldValue<string>(1)));\n"
                + "        }\n"
                + "\n"
                + "        return "
                + result
                + ";\n"
                + "    }\n"
                + "    finally\n"
        );
    }

    // On SQL Server the later results are read before the method returns, so that an error a later statement raises
    // is thrown rather than lost when the reader is disposed.
    [Fact]
    public void Append_SqlServerShapeMany_DrainsTheLaterResultsBeforeItReturns() =>
        Write(SqlServerGetUser with { Shape = ResultShape.Many, Collection = CollectionKind.Array })
            .ShouldContain(
                "            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))\n"
                    + "            {\n"
                    + "                rows.Add(new global::App.GetUserDto(reader.GetFieldValue<int>(0), "
                    + "reader.IsDBNull(1) ? default(string?) : reader.GetFieldValue<string>(1)));\n"
                    + "            }\n"
                    + "\n"
                    + Drain("            ")
                    + "\n"
                    + "            return rows.ToArray();\n"
                    + "        }\n"
                    + TransactionCheck("        ")
                    + "    }\n"
                    + "    finally\n"
            );

    // The method's own exception, for no row or for a second, is thrown as it is: only the return drains.
    [Fact]
    public void Append_SqlServerShapeOne_DrainsAfterTheSecondRead()
    {
        var text = Write(SqlServerGetUser with { Shape = ResultShape.One });

        text.ShouldContain(
            "            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))\n"
                + "            {\n"
                + "                throw new global::SqlSource.UnexpectedRowCountException(\"GetUser\", false);\n"
                + "            }\n"
        );
        text.ShouldContain(
            "            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))\n"
                + "            {\n"
                + "                throw new global::SqlSource.UnexpectedRowCountException(\"GetUser\", true);\n"
                + "            }\n"
                + "\n"
                + Drain("            ")
                + "\n"
                + "            return row;\n"
                + "        }\n"
                + TransactionCheck("        ")
        );
        Regex.Count(text, "NextResultAsync", RegexOptions.None, TimeSpan.FromSeconds(1)).ShouldBe(1);
    }

    // No row drains too, so that a later error is thrown rather than hidden behind the null.
    [Fact]
    public void Append_SqlServerShapeOneOptional_DrainsBeforeEachReturn()
    {
        var text = Write(SqlServerGetUser with { Shape = ResultShape.OneOptional });

        text.ShouldContain(
            "            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))\n"
                + "            {\n"
                + Drain("                ")
                + "\n"
                + "                return null;\n"
                + "            }\n"
        );
        text.ShouldContain(
            "                throw new global::SqlSource.UnexpectedRowCountException(\"GetUser\", true);\n"
                + "            }\n"
                + "\n"
                + Drain("            ")
                + "\n"
                + "            return row;\n"
                + "        }\n"
                + TransactionCheck("        ")
        );
        Regex.Count(text, "NextResultAsync", RegexOptions.None, TimeSpan.FromSeconds(1)).ShouldBe(2);
    }

    // On SQL Server an error the rest of the batch raises, read when the reader is disposed after the method's own
    // exception, can roll back the transaction unannounced, so the whole read is in a try whose catch disposes the
    // reader and, when the transaction is gone, throws an exception that says so.  The database's error and the
    // caller's cancellation are thrown as they are, and a method given no transaction catches nothing.
    [Theory]
    [InlineData(nameof(ResultShape.Many))]
    [InlineData(nameof(ResultShape.One))]
    [InlineData(nameof(ResultShape.OneOptional))]
    public void Append_SqlServerShapeReadingRows_ChecksTheTransactionWhenTheReadFails(string shapeName)
    {
        var text = Write(
            SqlServerGetUser with
            {
                Shape = Enum.Parse<ResultShape>(shapeName),
                Collection = CollectionKind.Array,
            }
        );
        const string Scope =
            "        await using var readerScope = "
            + "global::System.Threading.Tasks.TaskAsyncEnumerableExtensions.ConfigureAwait(reader, false);\n";
        const string TryStart = "        try\n        {\n";
        var end = "        }\n" + TransactionCheck("        ") + "    }\n    finally\n";

        text.ShouldContain(Scope + TryStart);
        text.ShouldContain(end);
        var start = text.IndexOf(Scope + TryStart, StringComparison.Ordinal) + Scope.Length + TryStart.Length;
        var read = text[start..text.IndexOf(end, StringComparison.Ordinal)].Split('\n')[..^1];
        read.ShouldAllBe(line => line.Length == 0 || line.StartsWith("            ", StringComparison.Ordinal));
        read[^1].ShouldStartWith("            return ");
        Regex.Count(text, "catch \\(", RegexOptions.None, TimeSpan.FromSeconds(1)).ShouldBe(1);
    }

    // Each SQL Server shape that reads rows says when it throws the exception of a rolled-back transaction.
    [Theory]
    [InlineData(nameof(ResultShape.Many), nameof(CollectionKind.Array))]
    [InlineData(nameof(ResultShape.One), nameof(CollectionKind.Array))]
    [InlineData(nameof(ResultShape.OneOptional), nameof(CollectionKind.Array))]
    [InlineData(nameof(ResultShape.Many), nameof(CollectionKind.IAsyncEnumerable))]
    public void Append_SqlServerShapeReadingRows_DocumentsTheRolledBackTransaction(
        string shapeName,
        string collectionName
    ) =>
        Write(
                SqlServerGetUser with
                {
                    Shape = Enum.Parse<ResultShape>(shapeName),
                    Collection = Enum.Parse<CollectionKind>(collectionName),
                }
            )
            .ShouldContain(
                "/// <exception cref=\"global::SqlSource.TransactionRolledBackException\">"
                    + "The query threw an exception of its own, its inner exception, and an error that the rest of "
                    + "the query raised rolled back the transaction.</exception>\n"
            );

    // A PostgreSQL query is one statement, so no error is read after the method's own exception.
    [Theory]
    [InlineData(nameof(ResultShape.Many), nameof(CollectionKind.Array))]
    [InlineData(nameof(ResultShape.One), nameof(CollectionKind.Array))]
    [InlineData(nameof(ResultShape.OneOptional), nameof(CollectionKind.Array))]
    [InlineData(nameof(ResultShape.Many), nameof(CollectionKind.IAsyncEnumerable))]
    public void Append_PostgresShapeReadingRows_DoesNotCheckTheTransaction(string shapeName, string collectionName)
    {
        var text = Write(
            GetUser with
            {
                Shape = Enum.Parse<ResultShape>(shapeName),
                Collection = Enum.Parse<CollectionKind>(collectionName),
            }
        );

        text.ShouldNotContain("TransactionRolledBackException");
        text.ShouldNotContain("catch (");
    }

    // A SQL Server method that reads no rows throws no exception of its own after the query has run.
    [Theory]
    [InlineData(nameof(ResultShape.None))]
    [InlineData(nameof(ResultShape.RowCount))]
    public void Append_SqlServerShapeReadingNoRows_DoesNotCheckTheTransaction(string shapeName)
    {
        var text = Write(SqlServerGetUser with { Shape = Enum.Parse<ResultShape>(shapeName) });

        text.ShouldNotContain("TransactionRolledBackException");
        text.ShouldNotContain("catch (");
    }

    // A PostgreSQL query is one statement, so its reader never has a later result to read.
    [Theory]
    [InlineData(nameof(ResultShape.Many))]
    [InlineData(nameof(ResultShape.One))]
    [InlineData(nameof(ResultShape.OneOptional))]
    public void Append_PostgresShapeReadingRows_DoesNotDrain(string shapeName) =>
        Write(GetUser with { Shape = Enum.Parse<ResultShape>(shapeName) }).ShouldNotContain("NextResultAsync");

    // A transaction that has completed is refused before anything runs, since a command given it would run outside
    // any transaction, on SQL Server, before the SQL is built and the connection opened.
    [Theory]
    [InlineData(nameof(ResultShape.None))]
    [InlineData(nameof(ResultShape.RowCount))]
    [InlineData(nameof(ResultShape.One))]
    [InlineData(nameof(ResultShape.OneOptional))]
    [InlineData(nameof(ResultShape.Many))]
    public void Append_SqlServerEachShape_RefusesACompletedTransactionBeforeItOpensTheConnection(string shapeName)
    {
        var text = Write(
            SqlServerGetUser with
            {
                Shape = Enum.Parse<ResultShape>(shapeName),
                Collection = CollectionKind.Array,
            }
        );

        text.ShouldContain(
            "    global::System.ArgumentException.ThrowIfNullOrWhiteSpace(orderBy);\n"
                + CompletedTransactionCheck("    ")
                + "\n"
                + "    var sql = string.Create(\n"
        );
        Regex
            .Count(text, "\\.Connection is null\\)\n", RegexOptions.None, TimeSpan.FromSeconds(1))
            .ShouldBe(shapeName is nameof(ResultShape.None) or nameof(ResultShape.RowCount) ? 1 : 2);
        text.IndexOf(CompletedTransactionCheck("    "), StringComparison.Ordinal)
            .ShouldBeLessThan(text.IndexOf("var wasClosed", StringComparison.Ordinal));
    }

    // A streaming method checks when its loop starts, inside the iterator, so a transaction that completes between the
    // call and the loop is refused too.
    [Fact]
    public void Append_SqlServerStreams_RefusesACompletedTransactionInsideTheIterator()
    {
        var text = Write(SqlServerStream);

        text.ShouldContain(
            "[global::System.Runtime.CompilerServices.EnumeratorCancellation] "
                + "global::System.Threading.CancellationToken cancellationToken)\n"
                + "    {\n"
                + CompletedTransactionCheck("        ")
                + "\n"
                + "        var sql = string.Create(\n"
        );
        text.IndexOf(CompletedTransactionCheck("        "), StringComparison.Ordinal)
            .ShouldBeGreaterThan(text.IndexOf("static async ", StringComparison.Ordinal));
        text.IndexOf(CompletedTransactionCheck("        "), StringComparison.Ordinal)
            .ShouldBeLessThan(text.IndexOf("var wasClosed", StringComparison.Ordinal));
    }

    // Each SQL Server method says that it refuses a transaction that has completed.
    [Theory]
    [InlineData(nameof(ResultShape.None), nameof(CollectionKind.Array))]
    [InlineData(nameof(ResultShape.RowCount), nameof(CollectionKind.Array))]
    [InlineData(nameof(ResultShape.One), nameof(CollectionKind.Array))]
    [InlineData(nameof(ResultShape.OneOptional), nameof(CollectionKind.Array))]
    [InlineData(nameof(ResultShape.Many), nameof(CollectionKind.Array))]
    [InlineData(nameof(ResultShape.Many), nameof(CollectionKind.IAsyncEnumerable))]
    public void Append_SqlServerEachShape_DocumentsTheCompletedTransaction(string shapeName, string collectionName) =>
        Write(
                SqlServerGetUser with
                {
                    Shape = Enum.Parse<ResultShape>(shapeName),
                    Collection = Enum.Parse<CollectionKind>(collectionName),
                }
            )
            .ShouldContain(CompletedTransactionDocumentation);

    // The refusal is SQL Server's alone, whose driver runs a command given a completed transaction outside any
    // transaction, so a PostgreSQL method neither checks nor documents it.
    [Theory]
    [InlineData(nameof(ResultShape.None), nameof(CollectionKind.Array))]
    [InlineData(nameof(ResultShape.RowCount), nameof(CollectionKind.Array))]
    [InlineData(nameof(ResultShape.One), nameof(CollectionKind.Array))]
    [InlineData(nameof(ResultShape.OneOptional), nameof(CollectionKind.Array))]
    [InlineData(nameof(ResultShape.Many), nameof(CollectionKind.Array))]
    [InlineData(nameof(ResultShape.Many), nameof(CollectionKind.IAsyncEnumerable))]
    public void Append_PostgresEachShape_DoesNotRefuseACompletedTransaction(string shapeName, string collectionName)
    {
        var text = Write(
            GetUser with
            {
                Shape = Enum.Parse<ResultShape>(shapeName),
                Collection = Enum.Parse<CollectionKind>(collectionName),
            }
        );

        text.ShouldNotContain("transaction.Connection");
        text.ShouldNotContain("global::System.InvalidOperationException");
    }

    // Under no-array-pool the types the buffer serves read their rows as before the buffer.
    [Theory]
    [InlineData(nameof(CollectionKind.Array), "new global::System.Collections.Generic.List<", "rows.ToArray()")]
    [InlineData(nameof(CollectionKind.IEnumerable), "new global::System.Collections.Generic.List<", "rows.ToArray()")]
    [InlineData(
        nameof(CollectionKind.IReadOnlyCollection),
        "new global::System.Collections.Generic.List<",
        "rows.ToArray()"
    )]
    [InlineData(nameof(CollectionKind.IReadOnlyList), "new global::System.Collections.Generic.List<", "rows.ToArray()")]
    [InlineData(nameof(CollectionKind.List), "new global::System.Collections.Generic.List<", "rows")]
    [InlineData(nameof(CollectionKind.IList), "new global::System.Collections.Generic.List<", "rows")]
    [InlineData(nameof(CollectionKind.ICollection), "new global::System.Collections.Generic.List<", "rows")]
    [InlineData(
        nameof(CollectionKind.ImmutableArray),
        "global::System.Collections.Immutable.ImmutableArray.CreateBuilder<",
        "rows.DrainToImmutable()"
    )]
    public void Append_EachPooledCollectionTypeUnderNoArrayPool_ReadsItsRowsAsBeforeThePool(
        string collectionName,
        string buffer,
        string result
    )
    {
        var text = Write(
            GetUser with
            {
                Shape = ResultShape.Many,
                Collection = Enum.Parse<CollectionKind>(collectionName),
                UseArrayPool = false,
            }
        );

        text.ShouldContain("        var rows = " + buffer);
        text.ShouldContain("        return " + result + ";\n");
        text.ShouldNotContain("SqlSourceRowBuffer");
    }

    [Theory]
    [InlineData("public static")]
    [InlineData("internal static")]
    [InlineData("private static")]
    public void Append_TypePlacement_TakesTheConnectionWithoutThis(string modifiers)
    {
        var text = Write(GetUser, modifiers, isExtension: false);

        text.ShouldContain(
            modifiers
                + " async global::System.Threading.Tasks.Task<global::App.GetUserDto?> GetUser(\n"
                + "    global::System.Data.Common.DbConnection connection,\n"
        );
        text.ShouldNotContain("this ");
    }

    [Fact]
    public void Append_NoParameters_HasNoParametersArgumentNorBinding()
    {
        var text = Write(GetUser with { Input = null });

        text.ShouldNotContain("parameters");
        text.ShouldNotContain("CreateParameter");
        text.ShouldContain("    this global::System.Data.Common.DbConnection connection,\n    string orderBy,\n");
        text.ShouldContain("        }\n\n        var reader = await command.ExecuteReaderAsync(", Case.Sensitive);
    }

    // CA2100 flags a command whose SQL is not a constant, which the tokens make it: the rule is off around that one
    // assignment, and back on for the rest of the method.
    [Fact]
    public void Append_Tokens_TurnsCA2100OffAroundTheAssignmentOfTheSqlAlone()
    {
        var text = Write(GetUser);

        text.ShouldContain(
            "        var command = connection.CreateCommand();\n"
                + "        await using var commandScope = "
                + "global::System.Threading.Tasks.TaskAsyncEnumerableExtensions.ConfigureAwait(command, false);\n"
                + "        #pragma warning disable CA2100 // The text of a token is the caller's own choice.\n"
                + "        command.CommandText = sql;\n"
                + "        #pragma warning restore CA2100\n"
                + "        command.Transaction = transaction;\n"
        );
        Regex.Count(text, "#pragma", RegexOptions.None, TimeSpan.FromSeconds(1)).ShouldBe(2);
    }

    [Fact]
    public void Append_NoTokens_HoldsTheSqlInAConstant()
    {
        var text = Write(
            GetUser with
            {
                Segments = TestSidecars.Of(TestModels.Literal("SELECT id, name\nFROM users;")),
            }
        );

        text.ShouldContain("/// <code>\n/// SELECT id, name\n/// FROM users;\n/// </code>\n");
        text.ShouldNotContain("<param name=\"orderBy\"");
        text.ShouldContain(
            "    global::App.GetUserParams parameters,\n"
                + "    global::System.Data.Common.DbTransaction? transaction = null,\n"
        );
        text.ShouldContain(
            "    global::System.ArgumentNullException.ThrowIfNull(parameters);\n"
                + "    const string sql = \"SELECT id, name\\nFROM users;\";\n"
                + "    var wasClosed = "
        );
        text.ShouldNotContain("ThrowIfNullOrWhiteSpace");
        text.ShouldNotContain("string.Create");
        text.ShouldNotContain("#pragma");
        text.ShouldContain("        command.CommandText = sql;\n        command.Transaction = transaction;\n");
    }

    [Fact]
    public void Append_NoTokenValidation_DoesNotCheckTheTokens()
    {
        var validated = Write(GetUser);
        var unvalidated = Write(GetUser with { ValidateTokens = false });

        unvalidated.ShouldNotContain("ThrowIfNullOrWhiteSpace");
        validated.ShouldBe(
            unvalidated.Replace(
                "ThrowIfNull(parameters);\n",
                "ThrowIfNull(parameters);\n    global::System.ArgumentException.ThrowIfNullOrWhiteSpace(orderBy);\n",
                StringComparison.Ordinal
            )
        );
    }

    [Fact]
    public void Append_RecordOutput_ReadsByThePositionalConstructor() =>
        Write(GetUser)
            .ShouldContain(
                "        var row = new global::App.GetUserDto(reader.GetFieldValue<int>(0), "
                    + "reader.IsDBNull(1) ? default(string?) : reader.GetFieldValue<string>(1));\n"
            );

    [Fact]
    public void Append_ClassOutput_ReadsByAnInitializer() =>
        Write(GetUser with { Output = GetUser.Output! with { IsRecord = false } })
            .ShouldContain(
                "        var row = new global::App.GetUserDto { Id = reader.GetFieldValue<int>(0), "
                    + "Name = reader.IsDBNull(1) ? default(string?) : reader.GetFieldValue<string>(1) };\n"
            );

    // Sorted, the model's properties are Email, Id, Name, and the constructor takes the columns in that order.
    [Fact]
    public void Append_SortedRecordOutput_PassesEachColumnToItsProperty() =>
        Write(
                GetUser with
                {
                    Output = new MethodOutput(
                        "global::App.GetUserDto",
                        true,
                        TestSidecars.Of(
                            new MethodColumn("name", "Name", "string", false),
                            new MethodColumn("id", "Id", "int", false),
                            new MethodColumn("email", "Email", "string", true)
                        ),
                        TestSidecars.Of(2, 1, 0)
                    ),
                }
            )
            .ShouldContain(
                "        var row = new global::App.GetUserDto("
                    + "reader.IsDBNull(2) ? default(string?) : reader.GetFieldValue<string>(2), "
                    + "reader.GetFieldValue<int>(1), "
                    + "reader.GetFieldValue<string>(0));\n"
            );

    [Fact]
    public void Append_NullableAndNotNullColumns_ReadWithIsDBNullOnlyWhenNullable()
    {
        var text = Write(
            GetUser with
            {
                Output = GetUser.Output! with
                {
                    IsRecord = false,
                    Columns = TestSidecars.Of(
                        new MethodColumn("a", "A", "int", false),
                        new MethodColumn("b", "B", "int", true),
                        new MethodColumn("c", "C", "string", false),
                        new MethodColumn("d", "D", "string", true)
                    ),
                    ConstructorOrder = TestSidecars.Of(0, 1, 2, 3),
                },
            }
        );

        text.ShouldContain(
            "{ A = reader.GetFieldValue<int>(0), "
                + "B = reader.IsDBNull(1) ? default(int?) : reader.GetFieldValue<int>(1), "
                + "C = reader.GetFieldValue<string>(2), "
                + "D = reader.IsDBNull(3) ? default(string?) : reader.GetFieldValue<string>(3) };\n"
        );
        text.ShouldNotContain("GetFieldValue<int?>");
        text.ShouldNotContain("GetFieldValue<string?>");
    }

    [Fact]
    public void Append_NotNullVectorColumn_ChecksIsNullAndThrowsWithTheColumnsName() =>
        Write(GetUser with { Output = VectorOutput(isNullable: false) })
            .ShouldContain(
                "        var value1 = reader.GetFieldValue<"
                    + Vector
                    + ">(1);\n"
                    + "        if (value1.IsNull)\n"
                    + "        {\n"
                    + "            throw new global::System.InvalidCastException(\"Column 'embedding' is null.\");\n"
                    + "        }\n"
                    + "\n"
                    + "        var row = new global::App.GetUserDto(reader.GetFieldValue<int>(0), value1);\n"
            );

    [Fact]
    public void Append_NullableVectorColumn_HasNoIsNullCheck()
    {
        var text = Write(GetUser with { Output = VectorOutput(isNullable: true) });

        text.ShouldNotContain("IsNull)");
        text.ShouldNotContain("InvalidCastException");
        text.ShouldContain(
            "        var row = new global::App.GetUserDto(reader.GetFieldValue<int>(0), "
                + "reader.IsDBNull(1) ? default("
                + Vector
                + "?) : reader.GetFieldValue<"
                + Vector
                + ">(1));\n"
        );
    }

    // A null of a reference type is bound as the null of the parameter's type, as the property holds it.
    [Fact]
    public void Append_NotNullReferenceParameter_BindsItsValueAsItIs() =>
        Write(
                GetUser with
                {
                    Input = new MethodInput(
                        "global::App.GetUserParams",
                        TestSidecars.Of(
                            new MethodParameter("id", "Id", "int", true, false, Described: Integer),
                            new MethodParameter(
                                "name",
                                "Name",
                                "string",
                                false,
                                false,
                                Described: TestSidecars.Postgres(bindName: "text")
                            )
                        )
                    ),
                }
            )
            .ShouldContain(
                "            parameter.TypedValue = parameters.Id;\n"
                    + "            command.Parameters.Add(parameter);\n"
                    + "        }\n"
                    + "\n"
                    + "        {\n"
                    + "            var parameter = new global::Npgsql.NpgsqlParameter<string>();\n"
                    + "            parameter.ParameterName = \"name\";\n"
                    + "            parameter.DataTypeName = \"text\";\n"
                    + "            parameter.TypedValue = parameters.Name;\n"
                    + "            command.Parameters.Add(parameter);\n"
                    + "        }\n"
                    + "\n"
            );

    [Fact]
    public void Append_TokenNamedLikeALocal_RenamesTheLocal()
    {
        var text = Write(
            GetUser with
            {
                Segments = TestSidecars.Of(TestModels.Literal("SELECT 1 FROM "), Token("sql"), Token("span")),
            }
        );

        text.ShouldContain("    var sql1 = string.Create(\n");
        text.ShouldContain("        static (span1, state) =>\n");
        text.ShouldContain(
            "        #pragma warning disable CA2100 // The text of a token is the caller's own choice.\n"
                + "        command.CommandText = sql1;\n"
                + "        #pragma warning restore CA2100\n"
        );
    }

    // An asynchronous method cannot use await as an identifier, so the argument is verbatim wherever the method uses
    // it; its documentation keeps the name.
    [Fact]
    public void Append_TokenNamedAwait_WritesItVerbatimWhereItIsAnIdentifier()
    {
        var text = Write(
            GetUser with
            {
                Segments = TestSidecars.Of(TestModels.Literal("SELECT 1 FROM "), Token("await"), Token("b")),
            }
        );

        text.ShouldContain("/// <param name=\"await\">The text that replaces <c>{{await}}</c>.</param>\n");
        text.ShouldContain("    string @await,\n    string b,\n");
        text.ShouldContain("    global::System.ArgumentException.ThrowIfNullOrWhiteSpace(@await);\n");
        text.ShouldContain("        checked(14 + @await.Length + b.Length),\n        (@await, b),\n");
        text.ShouldNotContain("string await");
        text.ShouldNotContain("(await)");
        text.ShouldNotContain(" await.Length");
        text.ShouldNotContain("(await, b)");
    }

    // A conversion by element reads the base map's array, with nullable elements when they are of a value type, and
    // converts each element; ReadText is the array it reads.
    [Theory]
    [InlineData(
        "string[]",
        false,
        "string[]",
        "global::System.Array.ConvertAll<string, global::App.Email>(reader.GetFieldValue<string[]>(0), "
    )]
    [InlineData(
        "long[]",
        true,
        "long?[]",
        "global::System.Array.ConvertAll<long?, global::App.Email>(reader.GetFieldValue<long?[]>(0), "
    )]
    public void Append_ColumnConvertedByElement_ReadsTheDatabasesArray(
        string databaseText,
        bool databaseIsValueType,
        string readText,
        string read
    )
    {
        var column = new MethodColumn(
            "emails",
            "Emails",
            "global::App.Email[]",
            false,
            new Conversion(
                "global::App.Email.FromDatabase",
                databaseText,
                databaseIsValueType,
                true,
                "global::App.Email",
                true
            )
        );

        var text = Write(
            GetUser with
            {
                Output = GetUser.Output! with
                {
                    Columns = TestSidecars.Of(column),
                    ConstructorOrder = TestSidecars.Of(0),
                },
            }
        );

        column.ReadText.ShouldBe(readText);
        text.ShouldContain(
            "new global::App.GetUserDto("
                + read
                + "static element => element is { } item ? global::App.Email.FromDatabase(item) : "
                + "throw new global::System.InvalidCastException(\"Column 'emails' holds a null element.\")))"
        );
    }

    // An element the property writes nullable keeps a NULL element as null, the converter not called for it.
    [Fact]
    public void Append_ColumnOfANullableElementConvertedByElement_KeepsANullElement()
    {
        var column = new MethodColumn(
            "ids",
            "Ids",
            "global::App.UserId?[]",
            false,
            new Conversion("global::App.UserId.FromDatabase", "long[]", true, true, "global::App.UserId?", true)
        );

        var text = Write(
            GetUser with
            {
                Output = GetUser.Output! with
                {
                    Columns = TestSidecars.Of(column),
                    ConstructorOrder = TestSidecars.Of(0),
                },
            }
        );

        column.ReadText.ShouldBe("long?[]");
        text.ShouldContain(
            "new global::App.GetUserDto(global::System.Array.ConvertAll<long?, global::App.UserId?>("
                + "reader.GetFieldValue<long?[]>(0), "
                + "static element => element is { } item ? global::App.UserId.FromDatabase(item) : null))"
        );
    }

    // The outer method checks the arguments and returns the iterator, a static local function that takes every
    // argument and holds the rest of the body, yielding each row as it reads it.
    [Fact]
    public void Append_IAsyncEnumerable_IsTheWholeMethod() =>
        Write(Stream)
            .ShouldBe(
                GeneratedFile.Joined(
                    """
                    /// <summary>
                    /// The <c>GetUser</c> query from <c>Users.sql</c>.
                    /// </summary>
                    /// <remarks>
                    /// <code>
                    /// SELECT id, name FROM users WHERE id = @id ORDER BY {{orderBy}}
                    /// </code>
                    /// </remarks>
                    /// <param name="connection">The connection to run the query on.  ↩
                    When it is closed, it is opened when the loop starts and closed again when the loop ends.</param>
                    /// <param name="parameters">The query's parameters.</param>
                    /// <param name="orderBy">The text that replaces <c>{{orderBy}}</c>.</param>
                    /// <param name="transaction">The transaction to run the query in, or null.</param>
                    /// <param name="commandTimeout">↩
                    The command's timeout in seconds, or null for the driver's.</param>
                    /// <param name="cancellationToken">Cancels the query.</param>
                    /// <returns>The rows the query returns, each read as the loop asks for it.</returns>
                    public static global::System.Collections.Generic.IAsyncEnumerable<global::App.GetUserDto> GetUser(
                        this global::System.Data.Common.DbConnection connection,
                        global::App.GetUserParams parameters,
                        string orderBy,
                        global::System.Data.Common.DbTransaction? transaction = null,
                        int? commandTimeout = null,
                        global::System.Threading.CancellationToken cancellationToken = default)
                    {
                        global::System.ArgumentNullException.ThrowIfNull(connection);
                        global::System.ArgumentNullException.ThrowIfNull(parameters);
                        global::System.ArgumentException.ThrowIfNullOrWhiteSpace(orderBy);

                        return ReadRows(↩
                    connection, parameters, orderBy, transaction, commandTimeout, cancellationToken);

                        static async ↩
                    global::System.Collections.Generic.IAsyncEnumerable<global::App.GetUserDto> ReadRows(
                            global::System.Data.Common.DbConnection connection,
                            global::App.GetUserParams parameters,
                            string orderBy,
                            global::System.Data.Common.DbTransaction? transaction,
                            int? commandTimeout,
                            [global::System.Runtime.CompilerServices.EnumeratorCancellation] ↩
                    global::System.Threading.CancellationToken cancellationToken)
                        {
                            var sql = string.Create(
                                checked(51 + orderBy.Length),
                                orderBy,
                                static (span, state) =>
                                {
                                    "SELECT id, name FROM users WHERE id = @id ORDER BY ".CopyTo(span);
                                    span = span.Slice(51);
                                    state.CopyTo(span);
                                }
                            );
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
                                #pragma warning disable CA2100 // The text of a token is the caller's own choice.
                                command.CommandText = sql;
                                #pragma warning restore CA2100
                                command.Transaction = transaction;
                                if (commandTimeout.HasValue)
                                {
                                    command.CommandTimeout = commandTimeout.Value;
                                }

                                {
                                    var parameter = new global::Npgsql.NpgsqlParameter<int>();
                                    parameter.ParameterName = "id";
                                    parameter.DataTypeName = "integer";
                                    parameter.TypedValue = parameters.Id;
                                    command.Parameters.Add(parameter);
                                }

                                var reader = await command.ExecuteReaderAsync(↩
                    global::System.Data.CommandBehavior.Default, cancellationToken).ConfigureAwait(false);
                                await using var readerScope = ↩
                    global::System.Threading.Tasks.TaskAsyncEnumerableExtensions.ConfigureAwait(reader, false);
                                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                                {
                                    yield return new global::App.GetUserDto(reader.GetFieldValue<int>(0), ↩
                    reader.IsDBNull(1) ? default(string?) : reader.GetFieldValue<string>(1));
                                }
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

                    """
                )
            );

    // The call checks its arguments at once; the iterator runs nothing until the loop asks for a row.
    [Fact]
    public void Append_IAsyncEnumerable_ChecksTheArgumentsInTheOuterMethod()
    {
        var text = Write(Stream);
        var call = text.IndexOf("return ReadRows(", StringComparison.Ordinal);

        call.ShouldBeGreaterThan(0);
        var outer = text[..call];
        var iterator = text[call..];
        outer.ShouldContain("ThrowIfNull(connection)");
        outer.ShouldContain("ThrowIfNull(parameters)");
        outer.ShouldContain("ThrowIfNullOrWhiteSpace(orderBy)");
        iterator.ShouldNotContain("ThrowIfNull(connection)");
        iterator.ShouldNotContain("ThrowIfNull(parameters)");
        iterator.ShouldNotContain("ThrowIfNullOrWhiteSpace(orderBy)");
    }

    [Fact]
    public void Append_IAsyncEnumerableWithoutParameters_PassesNoParameters()
    {
        var text = Write(Stream with { Input = null });

        text.ShouldContain(
            "    return ReadRows(connection, orderBy, transaction, commandTimeout, cancellationToken);\n"
        );
        text.ShouldNotContain("parameters");
    }

    [Fact]
    public void Append_IAsyncEnumerableTokenNamedReadRows_RenamesTheLocalFunction()
    {
        var text = Write(
            Stream with
            {
                Segments = TestSidecars.Of(TestModels.Literal("SELECT 1 FROM "), Token("ReadRows")),
            }
        );

        text.ShouldContain(
            "    return ReadRows1(connection, parameters, ReadRows, transaction, commandTimeout, cancellationToken);\n"
        );
        text.ShouldContain("> ReadRows1(\n");
        text.ShouldContain("\n    global::App.GetUserParams parameters,\n    string ReadRows,\n");
        text.ShouldContain("\n        global::App.GetUserParams parameters,\n        string ReadRows,\n");
    }

    // As in the method of any other type: an asynchronous local function cannot use await as an identifier either.
    [Fact]
    public void Append_IAsyncEnumerableTokenNamedAwait_WritesItVerbatim()
    {
        var text = Write(
            Stream with
            {
                Segments = TestSidecars.Of(TestModels.Literal("SELECT 1 FROM "), Token("await"), Token("b")),
            }
        );

        text.ShouldContain("/// <param name=\"await\">The text that replaces <c>{{await}}</c>.</param>\n");
        text.ShouldContain(
            "    return ReadRows(connection, parameters, @await, b, transaction, commandTimeout, cancellationToken);\n"
        );
        text.ShouldContain("\n    string @await,\n    string b,\n");
        text.ShouldContain("\n        string @await,\n        string b,\n");
        text.ShouldContain("    global::System.ArgumentException.ThrowIfNullOrWhiteSpace(@await);\n");
        text.ShouldContain("            checked(14 + @await.Length + b.Length),\n            (@await, b),\n");
        text.ShouldNotContain("string await");
        text.ShouldNotContain("(await)");
        text.ShouldNotContain(" await.Length");
        text.ShouldNotContain("(await, b)");
        text.ShouldNotContain(" await,");
    }

    // After the last row the iterator reads the later results inside its try, so that the move that ends the loop
    // throws an error a later statement raises; PostgreSQL's has no later result.
    [Fact]
    public void Append_SqlServerStreams_DrainsAfterTheReadLoop()
    {
        Write(SqlServerStream)
            .ShouldContain(
                "                    atRow = false;\n"
                    + "                }\n"
                    + "\n"
                    + Drain("                ")
                    + "            }\n"
                    + "            finally\n"
            );
        Write(Stream).ShouldNotContain("NextResultAsync");
    }

    // A loop left at a row, by a break, a throw in its body or a disposal, disposes the iterator at its yield, and the
    // finally reads the later results then, so that an error the rest of the batch raises is thrown.  It reads them
    // without the token, as the reader's disposal would, so a token the caller cancelled neither throws nor cancels the
    // command.  The row is built before the flag is set, so an exception of the iterator's own, from a read, a check or
    // a converter, reads none.
    [Fact]
    public void Append_SqlServerStreams_DrainsInAFinallyWhenLeftAtARow()
    {
        Write(SqlServerStream with { Output = VectorOutput(isNullable: false) })
            .ShouldContain(
                "            await using var readerScope = "
                    + "global::System.Threading.Tasks.TaskAsyncEnumerableExtensions.ConfigureAwait(reader, false);\n"
                    + "            var atRow = false;\n"
                    + "            try\n"
                    + "            {\n"
                    + "                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))\n"
                    + "                {\n"
                    + "                    global::App.GetUserDto row;\n"
                    + "                    try\n"
                    + "                    {\n"
                    + "                        var value1 = reader.GetFieldValue<"
                    + Vector
                    + ">(1);\n"
                    + "                        if (value1.IsNull)\n"
                    + "                        {\n"
                    + "                            throw new global::System.InvalidCastException("
                    + "\"Column 'embedding' is null.\");\n"
                    + "                        }\n"
                    + "\n"
                    + "                        row = new global::App.GetUserDto("
                    + "reader.GetFieldValue<int>(0), value1);\n"
                    + "                    }\n"
                    + TransactionCheck("                    ")
                    + "\n"
                    + "                    atRow = true;\n"
                    + "                    yield return row;\n"
                    + "                    atRow = false;\n"
                    + "                }\n"
                    + "\n"
                    + Drain("                ")
                    + "            }\n"
                    + "            finally\n"
                    + "            {\n"
                    + "                if (atRow)\n"
                    + "                {\n"
                    + Drain("                    ", "global::System.Threading.CancellationToken.None")
                    + "                }\n"
                    + "            }\n"
                    + "        }\n"
                    + "        finally\n"
            );
        Write(Stream).ShouldNotContain("atRow");
    }

    // C# allows no catch on a try that holds a yield return, so the iterator builds each row in a try of its own, whose
    // catch disposes the reader and, when the transaction is gone, throws the exception that says so in place of the
    // iterator's own.
    [Fact]
    public void Append_SqlServerStreams_ChecksTheTransactionWhenARowFails() =>
        Write(SqlServerStream with { Output = VectorOutput(isNullable: false) })
            .ShouldContain(
                "                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))\n"
                    + "                {\n"
                    + "                    global::App.GetUserDto row;\n"
                    + "                    try\n"
                    + "                    {\n"
                    + "                        var value1 = reader.GetFieldValue<"
                    + Vector
                    + ">(1);\n"
                    + "                        if (value1.IsNull)\n"
                    + "                        {\n"
                    + "                            throw new global::System.InvalidCastException("
                    + "\"Column 'embedding' is null.\");\n"
                    + "                        }\n"
                    + "\n"
                    + "                        row = new global::App.GetUserDto("
                    + "reader.GetFieldValue<int>(0), value1);\n"
                    + "                    }\n"
                    + TransactionCheck("                    ")
                    + "\n"
                    + "                    atRow = true;\n"
            );

    // A token named as the flag renames the flag, which no argument may hide.
    [Fact]
    public void Append_SqlServerStreamsTokenNamedAtRow_RenamesTheFlag()
    {
        var text = Write(
            SqlServerStream with
            {
                Segments = TestSidecars.Of(TestModels.Literal("SELECT 1 FROM "), Token("atRow")),
            }
        );

        text.ShouldContain("var atRow1 = false;\n");
        text.ShouldContain("if (atRow1)\n");
    }

    [Fact]
    public void Append_IAsyncEnumerableNotNullVectorColumn_ChecksBeforeYielding() =>
        Write(Stream with { Output = VectorOutput(isNullable: false) })
            .ShouldContain(
                "            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))\n"
                    + "            {\n"
                    + "                var value1 = reader.GetFieldValue<"
                    + Vector
                    + ">(1);\n"
                    + "                if (value1.IsNull)\n"
                    + "                {\n"
                    + "                    throw new global::System.InvalidCastException("
                    + "\"Column 'embedding' is null.\");\n"
                    + "                }\n"
                    + "\n"
                    + "                yield return new global::App.GetUserDto(reader.GetFieldValue<int>(0), value1);\n"
                    + "            }\n"
            );

    [Theory]
    [InlineData("public static")]
    [InlineData("internal static")]
    [InlineData("private static")]
    public void Append_IAsyncEnumerableTypePlacement_TakesTheConnectionWithoutThis(string modifiers)
    {
        var text = Write(Stream, modifiers, isExtension: false);

        text.ShouldContain(
            "\n"
                + modifiers
                + " global::System.Collections.Generic.IAsyncEnumerable<global::App.GetUserDto> GetUser(\n"
                + "    global::System.Data.Common.DbConnection connection,\n"
        );
        text.ShouldNotContain("this ");
    }

    // The collection type reads only a many query, so any other shape's method does not stream.
    [Theory]
    [InlineData(nameof(ResultShape.One))]
    [InlineData(nameof(ResultShape.OneOptional))]
    [InlineData(nameof(ResultShape.None))]
    [InlineData(nameof(ResultShape.RowCount))]
    public void Append_IAsyncEnumerableOfANonManyShape_IsTheOrdinaryMethod(string shapeName)
    {
        var method = GetUser with { Shape = Enum.Parse<ResultShape>(shapeName) };

        Write(method with { Collection = CollectionKind.IAsyncEnumerable }).ShouldBe(Write(method));
    }

    // Nothing is buffered, so no-array-pool has nothing to change.
    [Fact]
    public void Append_IAsyncEnumerableUnderNoArrayPool_IsTheSameMethod() =>
        Write(Stream with { UseArrayPool = false }).ShouldBe(Write(Stream));

    [Fact]
    public void Append_SummaryOfTheQuery_IsTheMethodsSummary() =>
        Write(GetUser with { SummaryXml = "Finds a user by <c>id</c>,\nor none." })
            .ShouldStartWith("/// <summary>\n/// Finds a user by <c>id</c>,\n/// or none.\n/// </summary>\n");

    // Not required of this writer's tests, which compare text: the driver tests compile it.  Every shape, collection
    // type, placement and engine, with names that could hide what the method writes, compiled together against the
    // real System.Data, Npgsql's parameter and SqlClient's SqlVector.
    [Fact]
    public void Append_EveryShapeAndCollectionType_Compiles()
    {
        var awkward = TestSidecars.Of(
            TestModels.Literal("SELECT 1 FROM "),
            Token("sql"),
            Token("command"),
            Token("reader"),
            Token("row"),
            Token("rows"),
            Token("value2"),
            Token("span"),
            Token("state"),
            Token("wasClosed"),
            Token("parameter"),
            Token("global"),
            Token("var"),
            Token("System"),
            Token("App"),
            Token("await"),
            Token("ReadRows"),
            Token("atRow"),
            Token("exception"),
            Token("yield")
        );
        var withVector = GetUser.Output! with
        {
            Columns = TestSidecars.Of(
                new MethodColumn("id", "Id", "int", false),
                new MethodColumn("name", "Name", "string", true),
                new MethodColumn("embedding", "Embedding", Vector, false),
                new MethodColumn("other", "Other", Vector, true)
            ),
            ConstructorOrder = TestSidecars.Of(0, 1, 2, 3),
        };
        var asClass = withVector with { TypeText = "global::App.GetUserClass", IsRecord = false };
        var builder = new StringBuilder("namespace App\n{\n    internal static class Queries\n    {\n");
        var index = 0;
        foreach (var shape in Enum.GetValues<ResultShape>())
        {
            foreach (var collection in Enum.GetValues<CollectionKind>())
            {
                foreach (var output in new[] { withVector, asClass })
                {
                    foreach (var engine in new[] { DatabaseEngine.PostgreSql, DatabaseEngine.SqlServer })
                    {
                        var method = GetUser with
                        {
                            Name = "Q" + index++,
                            Shape = shape,
                            Collection = collection,
                            Engine = engine,
                            Output = output,
                            Segments = index % 4 < 2 ? awkward : GetUser.Segments,
                            Input = index % 3 == 0 ? null : GetUser.Input,
                        };
                        // Every awkward method once by each placement and engine: the engine goes by ones, the
                        // segments by twos and the placement by eights.
                        QueryMethodWriter.Append(
                            builder,
                            "        ",
                            method,
                            "public static",
                            isExtension: index % 8 < 4,
                            RowBufferType
                        );
                    }
                }
            }
        }

        _ = builder.Append(
            """
                }

                internal sealed record GetUserParams(int Id);

                internal sealed record GetUserDto(
                    int Id,
                    string? Name,
                    global::Microsoft.Data.SqlTypes.SqlVector<float> Embedding,
                    global::Microsoft.Data.SqlTypes.SqlVector<float>? Other
                );

                internal sealed class GetUserClass
                {
                    public int Id { get; init; }

                    public string? Name { get; init; }

                    public global::Microsoft.Data.SqlTypes.SqlVector<float> Embedding { get; init; }

                    public global::Microsoft.Data.SqlTypes.SqlVector<float>? Other { get; init; }
                }
            }

            namespace SqlSource
            {
                internal sealed class UnexpectedRowCountException : global::System.InvalidOperationException
                {
                    public UnexpectedRowCountException(string queryName, bool moreThanOne) { }
                }

                internal sealed class TransactionRolledBackException : global::System.InvalidOperationException
                {
                    public TransactionRolledBackException(string queryName, global::System.Exception innerException) { }
                }
            }

            namespace App
            {

            """
        );
        RowBuffer.Append(builder, "    ", RowBuffer.Name);
        _ = builder.Append("}\n");

        var compilation = GeneratorHarness.CreateCompilation(
            [new SourceFile("/app/Queries.cs", "#nullable enable\n" + builder)],
            references: [NpgsqlReferences.Npgsql10, SqlClientReferences.SqlClient7]
        );

        compilation
            .GetDiagnostics(TestContext.Current.CancellationToken)
            // CS1701, SqlClient's System.Data.Common unified with the runtime's, has no place in the source.
            .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning && diagnostic.Location.IsInSource)
            .Select(diagnostic => diagnostic.ToString())
            .ShouldBeEmpty();
    }

    private static MethodOutput VectorOutput(bool isNullable) =>
        GetUser.Output! with
        {
            Columns = TestSidecars.Of(
                new MethodColumn("id", "Id", "int", false),
                new MethodColumn("embedding", "Embedding", Vector, isNullable)
            ),
        };

    private static SqlSegment Token(string name) => new(SqlSegmentKind.Token, name);

    // The loop that reads the results after the rows, at an indent, with the method's token unless another is given.
    private static string Drain(string indent, string token = "cancellationToken") =>
        indent
        + "while (await reader.NextResultAsync("
        + token
        + ").ConfigureAwait(false))\n"
        + indent
        + "{\n"
        + indent
        + "}\n";

    // The catch of a SQL Server method that reads rows, at an indent, with the exception's name unless another is
    // given.
    private static string TransactionCheck(string indent, string exception = "exception") =>
        indent
        + "catch (global::System.Exception "
        + exception
        + ") when (transaction is not null && "
        + exception
        + " is not (global::System.Data.Common.DbException or global::System.OperationCanceledException))\n"
        + indent
        + "{\n"
        + indent
        + "    await reader.DisposeAsync().ConfigureAwait(false);\n"
        + indent
        + "    if (transaction.Connection is null)\n"
        + indent
        + "    {\n"
        + indent
        + "        throw new global::SqlSource.TransactionRolledBackException(\"GetUser\", "
        + exception
        + ");\n"
        + indent
        + "    }\n"
        + "\n"
        + indent
        + "    throw;\n"
        + indent
        + "}\n";

    // The refusal of a completed transaction by a SQL Server method of GetUser, at an indent.
    private static string CompletedTransactionCheck(string indent) =>
        indent
        + "if (transaction is not null && transaction.Connection is null)\n"
        + indent
        + "{\n"
        + indent
        + "    throw new global::System.InvalidOperationException("
        + "\"The transaction given to the query 'GetUser' has completed; it is no longer usable.\");\n"
        + indent
        + "}\n";

    private static string Write(QueryMethod method, string modifiers = "public static", bool isExtension = true)
    {
        var builder = new StringBuilder();
        QueryMethodWriter.Append(builder, string.Empty, method, modifiers, isExtension, RowBufferType);
        return builder.ToString();
    }
}
