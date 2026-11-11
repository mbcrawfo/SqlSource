using System.Text;
using Shouldly;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Emitting.QueryMethods;
using SqlSource.Generation.Emitting.QueryMethods.CollectionMethods;
using SqlSource.Generation.Methods;
using SqlSource.Generation.Models;
using SqlSource.Parsing.Tokens;
using SqlSource.Settings;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;

namespace SqlSource.Tests.Generation.Emitting.QueryMethods;

// The collection method of a query: the overload that takes the items in place of the input, its documentation, its
// checks, what it returns, and its body, which runs the items in batches of BatchWriter.ChunkSize, in a transaction of
// its own unless the caller has one.  CollectionMethodTests of tests/SqlSource.Tests/Generator/ compile it.
public sealed class BatchMethodWriterTests
{
    // GetUser, a record of two columns in a list, as its collection method is planned.
    private static readonly QueryMethod Batch = QueryMethodWriterTests.GetUser with
    {
        Batch = new BatchCall(TransactionMode.On),
    };

    private static readonly MethodInput ListInput = new(
        "global::App.GetUserParams",
        TestSidecars.Of(
            new MethodParameter(
                "ids",
                "Ids",
                "int",
                true,
                false,
                Sequence: ParameterSequence.List,
                ExpansionPrefix: "ids_"
            )
        )
    );

    [Fact]
    public void Append_BatchMethod_HasTheOverloadsSignature() =>
        Write(Batch)
            .ShouldStartWith(
                GeneratedFile.Joined(
                    """
                    /// <summary>
                    /// The <c>GetUser</c> query from <c>Users.sql</c>.
                    /// Runs the query once for each item of <paramref name="parameters" /> and returns the rows of ↩
                    every item, in the order of the items.
                    /// </summary>
                    /// <remarks>
                    /// <code>
                    /// SELECT id, name FROM users WHERE id = @id ORDER BY {{orderBy}}
                    /// </code>
                    /// </remarks>
                    /// <param name="connection">The connection to run the query on.  It is opened when it is ↩
                    closed, and closed again when the method opened it.</param>
                    /// <param name="parameters">The items to run the query for, in order.</param>
                    /// <param name="orderBy">The text that replaces <c>{{orderBy}}</c>.</param>
                    /// <param name="transaction">The transaction to run every item in, or null for the method to ↩
                    begin one of its own, which it commits after the last item and rolls back when anything throws, ↩
                    unless a <c>System.Transactions</c> transaction is open.</param>
                    /// <param name="commandTimeout">The command's timeout in seconds, or null for the driver's.</param>
                    /// <param name="cancellationToken">Cancels the query.</param>
                    /// <returns>The row of each item that returned one, in the order of the items.</returns>
                    /// <exception cref="T:SqlSource.UnexpectedRowCountException">↩
                    An item returned more than one row.  Its <c>ItemIndex</c> is the item's position, ↩
                    counted from 0.</exception>
                    /// <exception cref="T:SqlSource.CollectionItemException">↩
                    The database raised an error for an item.  Its <c>InnerException</c> is the database's ↩
                    exception and its <c>ItemIndex</c> is the item's position, counted from 0.</exception>
                    public static async global::System.Threading.Tasks.Task<global::System.Collections.Generic.List<↩
                    global::App.GetUserDto>> GetUser(
                        this global::System.Data.Common.DbConnection connection,
                        global::System.Collections.Generic.IEnumerable<global::App.GetUserParams> parameters,
                        string orderBy,
                        global::System.Data.Common.DbTransaction? transaction = null,
                        int? commandTimeout = null,
                        global::System.Threading.CancellationToken cancellationToken = default)
                    {
                        global::System.ArgumentNullException.ThrowIfNull(connection);
                        global::System.ArgumentNullException.ThrowIfNull(parameters);
                        global::System.ArgumentException.ThrowIfNullOrWhiteSpace(orderBy);
                        using var items = parameters.GetEnumerator();

                    """
                )
            );

    // A repository's method takes neither a connection, a transaction nor a timeout, as its query method does not.
    [Fact]
    public void Append_BatchRepositoryMethod_TakesTheItemsAndTheTokensAndTheCancellation()
    {
        var text = Write(Repository(Batch));

        text.ShouldContain(
            "public async global::System.Threading.Tasks.Task<global::System.Collections.Generic.List<"
                + "global::App.GetUserDto>> GetUser(\n"
                + "    global::System.Collections.Generic.IEnumerable<global::App.GetUserParams> parameters,\n"
                + "    string orderBy,\n"
                + "    global::System.Threading.CancellationToken cancellationToken = default)\n"
                + "{\n"
                + "    global::System.ArgumentNullException.ThrowIfNull(parameters);\n"
                + "    global::System.ArgumentException.ThrowIfNullOrWhiteSpace(orderBy);\n"
                + "    using var items = parameters.GetEnumerator();\n"
        );
        text.ShouldNotContain("this global::System.Data.Common.DbConnection");
        text.ShouldNotContain("commandTimeout");
    }

    [Fact]
    public void AppendInterfaceMember_BatchRepositoryMethod_IsTheOverloadBesideTheQueryMethod()
    {
        var builder = new StringBuilder();

        QueryMethodWriter.AppendInterfaceMember(builder, string.Empty, Repository(Batch));

        var text = builder.ToString();
        text.ShouldContain("/// <param name=\"parameters\">The items to run the query for, in order.</param>\n");
        text.ShouldEndWith(
            "global::System.Threading.Tasks.Task<global::System.Collections.Generic.List<global::App.GetUserDto>> "
                + "GetUser(\n"
                + "    global::System.Collections.Generic.IEnumerable<global::App.GetUserParams> parameters,\n"
                + "    string orderBy,\n"
                + "    global::System.Threading.CancellationToken cancellationToken = default);\n"
        );
    }

    [Fact]
    public void Append_BatchMethod_DocumentsCollectionItemException()
    {
        var text = Write(Batch with { Shape = ResultShape.None, Output = null });

        text.ShouldContain(
            "/// <exception cref=\"T:SqlSource.CollectionItemException\">"
                + "The database raised an error for an item.  Its <c>InnerException</c> is the database's exception "
                + "and its <c>ItemIndex</c> is the item's position, counted from 0.</exception>\n"
        );
        Write(QueryMethodWriterTests.GetUser).ShouldNotContain("CollectionItemException");
    }

    // Each item's lists are counted as the item is added to its batch, which the body writes, so the header has no
    // count.
    [Fact]
    public void Append_BatchMethodWithAList_WritesNoCountInItsHeader()
    {
        var text = Write(
            Batch with
            {
                Segments = TestSidecars.Of(
                    TestModels.Literal("SELECT 1 WHERE id IN "),
                    new SqlSegment(SqlSegmentKind.List, "@ids")
                ),
                Input = ListInput,
            }
        );

        var header = text[..text.IndexOf("using var items", System.StringComparison.Ordinal)];
        header.ShouldNotContain("var ");
        header.ShouldContain("    global::System.ArgumentNullException.ThrowIfNull(parameters);\n");
    }

    // What each shape returns: the collection type of the row for the shapes that read rows, and what the query
    // method returns for the others.
    [Theory]
    [InlineData(nameof(ResultShape.Many), "Task<global::System.Collections.Generic.List<global::App.GetUserDto>>")]
    [InlineData(nameof(ResultShape.One), "Task<global::System.Collections.Generic.List<global::App.GetUserDto>>")]
    [InlineData(
        nameof(ResultShape.OneOptional),
        "Task<global::System.Collections.Generic.List<global::App.GetUserDto>>"
    )]
    [InlineData(nameof(ResultShape.RowCount), "Task<int>")]
    [InlineData(nameof(ResultShape.None), "Task")]
    public void Append_EachShape_ReturnsItsTask(string shapeName, string task)
    {
        var shape = System.Enum.Parse<ResultShape>(shapeName);
        var method = Batch with
        {
            Shape = shape,
            Output = shape is ResultShape.None or ResultShape.RowCount ? null : Batch.Output,
        };

        Write(method).ShouldContain(" async global::System.Threading.Tasks." + task + " GetUser(\n");
        var builder = new StringBuilder();
        QueryMethodWriter.AppendInterfaceMember(builder, string.Empty, Repository(method));
        builder.ToString().ShouldContain("\nglobal::System.Threading.Tasks." + task + " GetUser(\n");
    }

    [Fact]
    public void Append_ArrayCollection_ReturnsTheArray() =>
        Write(Batch with { Shape = ResultShape.Many, Collection = CollectionKind.Array })
            .ShouldContain(" async global::System.Threading.Tasks.Task<global::App.GetUserDto[]> GetUser(\n");

    // A token cannot hide the item, which the bindings will read.
    [Fact]
    public void Locals_TokenNamedItem_LeavesTheItemAnotherName()
    {
        var method = Batch with
        {
            Segments = TestSidecars.Of(
                TestModels.Literal("SELECT 1 FROM "),
                new SqlSegment(SqlSegmentKind.Token, "item")
            ),
        };
        var tokens = MethodWriter.GetTokens(method.Segments);

        var locals = new QueryMethodLocals(
            MethodArguments.Of(method, tokens),
            method.Input,
            method.Output,
            streams: false,
            ExecutionCapabilities.None,
            source: null,
            takesItems: true
        );

        locals.Input.ShouldBe("item1");
        new QueryMethodLocals(
            MethodArguments.Of(QueryMethodWriterTests.GetUser, tokens),
            method.Input,
            method.Output,
            streams: false,
            ExecutionCapabilities.None,
            source: null
        ).Input.ShouldBe("parameters");
    }

    [Fact]
    public void ChunkSize_OfACollectionMethod_IsOneThousand() => BatchWriter.ChunkSize.ShouldBe(1000);

    // An empty collection returns before the connection is opened or a transaction begun.
    [Theory]
    [InlineData(nameof(ResultShape.Many), "return [];")]
    [InlineData(nameof(ResultShape.One), "return [];")]
    [InlineData(nameof(ResultShape.OneOptional), "return [];")]
    [InlineData(nameof(ResultShape.RowCount), "return 0;")]
    [InlineData(nameof(ResultShape.None), "return;")]
    public void Append_EmptyCollection_ReturnsBeforeTheConnectionIsOpened(string shapeName, string empty)
    {
        var text = Write(WithShape(System.Enum.Parse<ResultShape>(shapeName)));

        text.ShouldContain(
            "    using var items = parameters.GetEnumerator();\n"
                + "    if (!items.MoveNext())\n"
                + "    {\n"
                + "        "
                + empty
                + "\n"
                + "    }\n"
        );
        IndexOf(text, "if (!items.MoveNext())").ShouldBeLessThan(IndexOf(text, "OpenAsync("));
        IndexOf(text, "if (!items.MoveNext())").ShouldBeLessThan(IndexOf(text, "BeginTransactionAsync("));
    }

    // The method begins its own transaction unless the caller gave one or an ambient one is open, decided before the
    // connection is opened, and commits it after the last chunk, rolls it back when anything throws and disposes it.
    [Fact]
    public void Append_TransactionOn_BeginsCommitsAndRollsBackItsOwn()
    {
        var text = Write(Batch);

        text.ShouldContain(
            "    var ownsTransaction = transaction is null && "
                + "global::System.Transactions.Transaction.Current is null;\n"
        );
        IndexOf(text, "var ownsTransaction").ShouldBeLessThan(IndexOf(text, "OpenAsync("));
        text.ShouldContain(
            "        var ownTransaction = ownsTransaction\n"
                + "            ? await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)\n"
                + "            : null;\n"
        );
        text.ShouldContain("batch.Transaction = ownTransaction ?? transaction;\n");
        text.ShouldContain(
            "            if (ownTransaction is not null)\n"
                + "            {\n"
                + "                await ownTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);\n"
                + "            }\n"
        );
        text.ShouldContain(
            "        catch\n"
                + "        {\n"
                + "            if (ownTransaction?.Connection is not null)\n"
                + "            {\n"
                + "                try\n"
                + "                {\n"
                + "                    await ownTransaction.RollbackAsync("
                + "global::System.Threading.CancellationToken.None)"
                + ".ConfigureAwait(false);\n"
                + "                }\n"
                + "                catch\n"
                + "                {\n"
        );
        text.ShouldContain(
            "            throw;\n"
                + "        }\n"
                + "        finally\n"
                + "        {\n"
                + "            if (ownTransaction is not null)\n"
                + "            {\n"
                + "                await ownTransaction.DisposeAsync().ConfigureAwait(false);\n"
                + "            }\n"
                + "        }\n"
        );
    }

    [Theory]
    [InlineData(nameof(TransactionMode.ReadUncommitted), "ReadUncommitted")]
    [InlineData(nameof(TransactionMode.ReadCommitted), "ReadCommitted")]
    [InlineData(nameof(TransactionMode.RepeatableRead), "RepeatableRead")]
    [InlineData(nameof(TransactionMode.Serializable), "Serializable")]
    [InlineData(nameof(TransactionMode.Snapshot), "Snapshot")]
    public void Append_IsolationLevel_BeginsTheTransactionAtIt(string modeName, string level) =>
        Write(Batch with { Batch = new BatchCall(System.Enum.Parse<TransactionMode>(modeName)) })
            .ShouldContain(
                "? await connection.BeginTransactionAsync(global::System.Data.IsolationLevel."
                    + level
                    + ", cancellationToken).ConfigureAwait(false)\n"
            );

    // Under transaction=off each batch runs in the caller's transaction, if any, and the method begins none.
    [Fact]
    public void Append_TransactionOff_BeginsNone()
    {
        var text = Write(Batch with { Batch = new BatchCall(TransactionMode.Off) });

        text.ShouldNotContain("BeginTransactionAsync");
        text.ShouldNotContain("CommitAsync");
        text.ShouldNotContain("RollbackAsync");
        text.ShouldNotContain("ownTransaction");
        text.ShouldContain("batch.Transaction = transaction;\n");
    }

    // A repository's method takes no transaction, so only an ambient one is the caller's.
    [Fact]
    public void Append_RepositoryMethod_OwnsTheTransactionUnlessAnAmbientOneIsOpen()
    {
        var text = Write(Repository(Batch));

        text.ShouldContain("    var ownsTransaction = global::System.Transactions.Transaction.Current is null;\n");
        text.ShouldContain("batch.Transaction = ownTransaction;\n");
        text.ShouldNotContain("Timeout");
        Write(Repository(Batch with { Batch = new BatchCall(TransactionMode.Off) })).ShouldNotContain("Transaction");
    }

    // Each chunk is a batch of the connection, its transaction and timeout set, filled with a command for each item, up
    // to the chunk's size, and disposed before the next.
    [Fact]
    public void Append_BatchMethod_FillsABatchForEachChunk()
    {
        var text = Write(Batch);

        text.ShouldContain(
            "                var first = itemIndex;\n"
                + "                var batch = connection.CreateBatch();\n"
                + "                await using var batchScope = "
                + "global::System.Threading.Tasks.TaskAsyncEnumerableExtensions"
                + ".ConfigureAwait(batch, false);\n"
                + "                batch.Transaction = ownTransaction ?? transaction;\n"
                + "                if (commandTimeout.HasValue)\n"
                + "                {\n"
                + "                    batch.Timeout = commandTimeout.Value;\n"
                + "                }\n"
        );
        text.ShouldContain("                    var command = batch.CreateBatchCommand();\n");
        text.ShouldContain("command.CommandText = sql;\n");
        text.ShouldContain(
            "                    batch.BatchCommands.Add(command);\n"
                + "                    itemIndex++;\n"
                + "                }\n"
                + "                while (itemIndex - first < 1000 && items.MoveNext());\n"
        );
        text.ShouldContain("                more = itemIndex - first == 1000 && items.MoveNext();\n");
        text.ShouldContain("            while (more);\n");
        // The parameter is bound into the batch command, from the item.
        text.ShouldContain("command.Parameters.Add(parameter);\n");
        text.ShouldContain("parameter.TypedValue = item.Id;\n");
    }

    // The SQL of a query without a list is the same for every item, so it is built once, before the loop.
    [Fact]
    public void Append_QueryWithoutAList_BuildsItsSqlOnce()
    {
        var text = Write(Batch);

        IndexOf(text, "var sql = string.Create(").ShouldBeLessThan(IndexOf(text, "var ownsTransaction"));
        CountOf(text, "var sql = ").ShouldBe(1);
    }

    [Fact]
    public void Append_NullItem_ThrowsArgumentNullExceptionWithItsPosition() =>
        Write(Batch)
            .ShouldContain(
                "                    var item = items.Current;\n"
                    + "                    if (item is null)\n"
                    + "                    {\n"
                    + "                        throw new global::System.ArgumentNullException(\n"
                    + "                            \"parameters\",\n"
                    + "                            \"Item \" + itemIndex.ToString("
                    + "global::System.Globalization.CultureInfo"
                    + ".InvariantCulture) + \" of the collection is null.\"\n"
                    + "                        );\n"
                    + "                    }\n"
            );

    // A database error at an item is thrown as CollectionItemException, with the item's position: the chunk's first
    // and the failed command's place in the batch.
    [Fact]
    public void Append_DatabaseError_IsThrownAsCollectionItemException() =>
        Write(Batch)
            .ShouldContain(
                "                catch (global::System.Data.Common.DbException error) when (\n"
                    + "                    error is not global::SqlSource.CollectionItemException\n"
                    + "                    && error.BatchCommand is { } failed\n"
                    + "                    && batch.BatchCommands.Contains(failed)\n"
                    + "                )\n"
                    + "                {\n"
                    + "                    throw new global::SqlSource.CollectionItemException(\n"
                    + "                        \"GetUser\",\n"
                    + "                        first + batch.BatchCommands.IndexOf(failed),\n"
                    + "                        error\n"
                    + "                    );\n"
                    + "                }\n"
            );

    // On PostgreSQL a database error that names no command of the batch, as Npgsql 10's names none unless the
    // connection string asks it to, is the item of the first command that did not complete, whose statement type
    // Npgsql has not set; an error after every command completed is thrown as it is, and so is one of a batch whose
    // commands are not Npgsql's, as a connection that wraps Npgsql's makes.  It follows the catch of a named command,
    // for every shape, a rowcount's that runs without a reader included.
    [Theory]
    [InlineData(nameof(ResultShape.Many))]
    [InlineData(nameof(ResultShape.RowCount))]
    public void Append_PostgreSqlErrorThatNamesNoCommand_IsTheFirstCommandThatDidNotComplete(string shapeName)
    {
        var text = Write(WithShape(System.Enum.Parse<ResultShape>(shapeName)));

        text.ShouldContain(
            "                    );\n"
                + "                }\n"
                + "                catch (global::Npgsql.PostgresException error) when ("
                + "batch.BatchCommands[0] is global::Npgsql.NpgsqlBatchCommand)\n"
                + "                {\n"
                + "                    var failed = 0;\n"
                + "                    while (\n"
                + "                        failed < batch.BatchCommands.Count\n"
                + "                        && ((global::Npgsql.NpgsqlBatchCommand)batch.BatchCommands[failed])"
                + ".StatementType\n"
                + "                            != global::Npgsql.StatementType.Unknown\n"
                + "                    )\n"
                + "                    {\n"
                + "                        failed++;\n"
                + "                    }\n"
                + "\n"
                + "                    if (failed == batch.BatchCommands.Count)\n"
                + "                    {\n"
                + "                        throw;\n"
                + "                    }\n"
                + "\n"
                + "                    throw new global::SqlSource.CollectionItemException("
                + "\"GetUser\", first + failed, error);\n"
                + "                }\n"
        );
        IndexOf(text, "catch (global::Npgsql.PostgresException error) when (")
            .ShouldBeGreaterThan(IndexOf(text, "catch (global::System.Data.Common.DbException error) when ("));
    }

    // SqlClient names the failed command of every error, so SQL Server's method has no search.
    [Fact]
    public void Append_SqlServer_HasNoSearchForTheFailedCommand() =>
        Write(WithShape(ResultShape.Many) with { Engine = DatabaseEngine.SqlServer }).ShouldNotContain("StatementType");

    // rowcount sums each command's count, leaving out the -1 of a statement that counts nothing; none runs each chunk.
    [Fact]
    public void Append_RowCount_SumsEachCommandsRecordsAffected()
    {
        var text = Write(WithShape(ResultShape.RowCount));

        text.ShouldContain("            var affected = 0;\n");
        text.ShouldContain(
            "                    await batch.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);\n"
                + "                    for (var result = 0; result < itemIndex - first; result++)\n"
                + "                    {\n"
                + "                        affected += global::System.Math.Max("
                + "batch.BatchCommands[result].RecordsAffected, 0);\n"
                + "                    }\n"
        );
        text.ShouldContain("            return affected;\n");
        var none = Write(WithShape(ResultShape.None));
        none.ShouldContain("await batch.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);\n");
        none.ShouldNotContain("RecordsAffected");
        none.ShouldNotContain("return ");
        none.ShouldContain("        return;\n");
    }

    // Each command's result set is read in order: one row for one, which throws for none or a second, with the item's
    // position.
    [Fact]
    public void Append_One_ReadsOneRowOfEachResultSet() =>
        Write(WithShape(ResultShape.One))
            .ShouldContain(
                "                    for (var result = 0; result < itemIndex - first; result++)\n"
                    + "                    {\n"
                    + "                        if (result > 0)\n"
                    + "                        {\n"
                    + "                            await reader.NextResultAsync(cancellationToken)"
                    + ".ConfigureAwait(false);\n"
                    + "                        }\n"
                    + "\n"
                    + "                        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))\n"
                    + "                        {\n"
                    + "                            throw new global::SqlSource.UnexpectedRowCountException("
                    + "\"GetUser\", false, first + result);\n"
                    + "                        }\n"
                    + "\n"
                    + "                        rows.Add(new global::App.GetUserDto(reader.GetFieldValue<int>(0), "
                    + "reader.IsDBNull(1) ? default(string?) : reader.GetFieldValue<string>(1)));\n"
                    + "                        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))\n"
                    + "                        {\n"
                    + "                            throw new global::SqlSource.UnexpectedRowCountException("
                    + "\"GetUser\", true, first + result);\n"
                    + "                        }\n"
                    + "                    }\n"
            );

    [Fact]
    public void Append_OneOptional_AddsTheRowWhenThereIsOne() =>
        Write(Batch)
            .ShouldContain(
                "                        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))\n"
                    + "                        {\n"
                    + "                            rows.Add(new global::App.GetUserDto(reader.GetFieldValue<int>(0), "
                    + "reader.IsDBNull(1) ? default(string?) : reader.GetFieldValue<string>(1)));\n"
                    + "                            if (await reader.ReadAsync(cancellationToken)"
                    + ".ConfigureAwait(false))\n"
                    + "                            {\n"
                    + "                                throw new global::SqlSource.UnexpectedRowCountException("
                    + "\"GetUser\", true, first + result);\n"
                    + "                            }\n"
                    + "                        }\n"
            );

    [Fact]
    public void Append_Many_AddsEveryRowOfEachResultSet()
    {
        var text = Write(WithShape(ResultShape.Many));

        text.ShouldContain(
            "                        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))\n"
                + "                        {\n"
                + "                            rows.Add(new global::App.GetUserDto("
        );
        text.ShouldContain("            var rows = new " + RowBuffer.TypeText + "<global::App.GetUserDto>();\n");
        text.ShouldContain("            return rows.ToList();\n");
    }

    // The rows of every shape that reads them go into the buffer a many query method reads into: the pool's under
    // array-pool, else a list.
    [Theory]
    [InlineData(nameof(ResultShape.Many))]
    [InlineData(nameof(ResultShape.One))]
    [InlineData(nameof(ResultShape.OneOptional))]
    public void Append_EachShapeThatReadsRows_ReadsIntoTheBufferOfAManyMethod(string shapeName)
    {
        var method = WithShape(System.Enum.Parse<ResultShape>(shapeName)) with { Collection = CollectionKind.Array };

        Write(method).ShouldContain("var rows = new " + RowBuffer.TypeText + "<global::App.GetUserDto>();\n");
        Write(method).ShouldContain("return rows.ToArray();\n");
        Write(method with { UseArrayPool = false })
            .ShouldContain("var rows = new global::System.Collections.Generic.List<global::App.GetUserDto>();\n");
    }

    // Each item's lists are counted, checked against the limit and written into its SQL as the item is added.
    [Fact]
    public void Append_BatchMethodWithAList_CountsAndWritesEachItemsList()
    {
        var text = Write(
            Batch with
            {
                Segments = TestSidecars.Of(
                    TestModels.Literal("SELECT 1 WHERE id IN "),
                    new SqlSegment(SqlSegmentKind.List, "@ids")
                ),
                Input = ListInput,
            }
        );

        text.ShouldContain("                    var list0 = item.Ids;\n");
        text.ShouldContain("                    var count0 = CountOf(ref list0);\n");
        IndexOf(text, "var list0 = item.Ids;").ShouldBeGreaterThan(IndexOf(text, "var item = items.Current;"));
        IndexOf(text, "var sql = string.Create(").ShouldBeGreaterThan(IndexOf(text, "var list0 = item.Ids;"));
        IndexOf(text, "var sql = string.Create(").ShouldBeLessThan(IndexOf(text, "batch.CreateBatchCommand()"));
        text.ShouldContain("    static int CountOf<TElement>(");
    }

    // On SQL Server the caller's completed transaction is refused before anything runs, and no drain reads later
    // results, since each result set is an item's.
    [Fact]
    public void Append_SqlServer_RefusesACompletedTransactionAndDrainsNothing()
    {
        var text = Write(WithShape(ResultShape.Many) with { Engine = DatabaseEngine.SqlServer });

        IndexOf(text, "if (transaction is not null && transaction.Connection is null)")
            .ShouldBeLessThan(IndexOf(text, "using var items"));
        text.ShouldNotContain("while (await reader.NextResultAsync(");
        text.ShouldNotContain("TransactionRolledBackException");
    }

    private static QueryMethod Repository(QueryMethod method) =>
        method with
        {
            Host = RepositoryMethodWriterTests.Repository.Host,
            Source = RepositoryMethodWriterTests.Repository.Source,
        };

    // The collection method of GetUser of the shape, with its output for a shape that reads rows.
    private static QueryMethod WithShape(ResultShape shape) =>
        Batch with
        {
            Shape = shape,
            Output = shape is ResultShape.None or ResultShape.RowCount ? null : Batch.Output,
        };

    private static int IndexOf(string text, string part)
    {
        var index = text.IndexOf(part, System.StringComparison.Ordinal);
        index.ShouldBeGreaterThanOrEqualTo(0, part);
        return index;
    }

    private static int CountOf(string text, string part)
    {
        var count = 0;
        for (var at = text.IndexOf(part, System.StringComparison.Ordinal); at >= 0; )
        {
            count++;
            at = text.IndexOf(part, at + part.Length, System.StringComparison.Ordinal);
        }

        return count;
    }

    private static string Write(QueryMethod method)
    {
        var builder = new StringBuilder();
        QueryMethodWriter.Append(builder, string.Empty, method);
        return builder.ToString();
    }
}
