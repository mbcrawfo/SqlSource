using System.Text;
using Shouldly;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Emitting.QueryMethods;
using SqlSource.Generation.Methods;
using SqlSource.Generation.Models;
using SqlSource.Parsing.Tokens;
using SqlSource.Settings;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;

namespace SqlSource.Tests.Generation.Emitting.QueryMethods;

// The collection method of a query: the overload that takes the items in place of the input, its documentation, its
// checks and what it returns.  Its body is written by the tests of the batch run, not these.
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
            .ShouldBe(
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
                    /// <param name="transaction">The transaction to run the query in, or null.</param>
                    /// <param name="commandTimeout">The command's timeout in seconds, or null for the driver's.</param>
                    /// <param name="cancellationToken">Cancels the query.</param>
                    /// <returns>The row of each item that returned one, in the order of the items.</returns>
                    /// <exception cref="T:SqlSource.UnexpectedRowCountException">↩
                    The query returned more than one row.</exception>
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
                        await global::System.Threading.Tasks.Task.CompletedTask.ConfigureAwait(false);
                        throw new global::System.NotImplementedException();
                    }

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
                + "    await global::System.Threading.Tasks.Task.CompletedTask.ConfigureAwait(false);\n"
                + "    throw new global::System.NotImplementedException();\n"
                + "}\n"
        );
        text.ShouldNotContain("connection");
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
    // count and no helper.
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

        text.ShouldNotContain("var ids");
        text.ShouldNotContain("var ");
        text.ShouldContain("    global::System.ArgumentNullException.ThrowIfNull(parameters);\n");
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

    private static QueryMethod Repository(QueryMethod method) =>
        method with
        {
            Host = RepositoryMethodWriterTests.Repository.Host,
            Source = RepositoryMethodWriterTests.Repository.Source,
        };

    private static string Write(QueryMethod method)
    {
        var builder = new StringBuilder();
        QueryMethodWriter.Append(builder, string.Empty, method);
        return builder.ToString();
    }
}
