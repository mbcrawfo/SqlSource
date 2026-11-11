using System;
using System.Collections.Generic;
using System.Text;
using Shouldly;
using SqlSource.Generation.Methods;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Generation.Methods;

public sealed class MethodArgumentsTests
{
    private const string StreamConnectionXml =
        "The connection to run the query on.  When it is closed, it is opened when the loop starts and closed again "
        + "when the loop ends.";

    private const string ConnectionXml =
        "The connection to run the query on.  It is opened when it is closed, and closed again when the method "
        + "opened it.";

    private static readonly MethodInput Input = new("global::App.P", EquatableArray<MethodParameter>.Empty);

    [Fact]
    public void GetEnumerator_WithInputAndTokens_GivesEachArgumentInOrder()
    {
        var arguments = Enumerate(Of());

        arguments
            .ConvertAll(static argument => argument.Role)
            .ShouldBe([
                ArgumentRole.Receiver,
                ArgumentRole.Input,
                ArgumentRole.Token,
                ArgumentRole.Token,
                ArgumentRole.Transaction,
                ArgumentRole.CommandTimeout,
                ArgumentRole.Cancellation,
            ]);
        arguments
            .ConvertAll(static argument => argument.Name)
            .ShouldBe([
                "connection",
                "parameters",
                "orderBy",
                "await",
                "transaction",
                "commandTimeout",
                "cancellationToken",
            ]);
        arguments[1].TypeText.ShouldBe("global::App.P");
    }

    [Fact]
    public void GetEnumerator_WithoutInput_LeavesTheInputOut() =>
        Enumerate(new MethodArguments(null, ["orderBy", "await"], streams: false, takesConnection: true))
            .ConvertAll(static argument => argument.Role)
            .ShouldBe([
                ArgumentRole.Receiver,
                ArgumentRole.Token,
                ArgumentRole.Token,
                ArgumentRole.Transaction,
                ArgumentRole.CommandTimeout,
                ArgumentRole.Cancellation,
            ]);

    // A repository's method gets its own connection, so it takes no connection, transaction or command timeout.
    [Fact]
    public void Of_Repository_IsInputTokensAndCancellation()
    {
        var method = Emitting.QueryMethods.QueryMethodWriterTests.GetUser with { Source = TestModels.Source() };

        var arguments = Enumerate(MethodArguments.Of(method, ["orderBy", "await"]));

        arguments
            .ConvertAll(static argument => argument.Role)
            .ShouldBe([ArgumentRole.Input, ArgumentRole.Token, ArgumentRole.Token, ArgumentRole.Cancellation]);
        arguments
            .ConvertAll(static argument => argument.Name)
            .ShouldBe(["parameters", "orderBy", "await", "cancellationToken"]);
        MethodArguments.Of(method, []).TakesConnection.ShouldBeFalse();
    }

    // A collection method takes the items in the place of the input, with the input's name, and no new name
    // is reserved.
    [Fact]
    public void Of_BatchMethod_TakesTheItemsAsParameters()
    {
        var method = Emitting.QueryMethods.QueryMethodWriterTests.GetUser with
        {
            Batch = new BatchCall(TransactionMode.On),
        };

        var arguments = Enumerate(MethodArguments.Of(method, ["orderBy"]));

        arguments
            .ConvertAll(static argument => argument.Role)
            .ShouldBe([
                ArgumentRole.Receiver,
                ArgumentRole.Input,
                ArgumentRole.Token,
                ArgumentRole.Transaction,
                ArgumentRole.CommandTimeout,
                ArgumentRole.Cancellation,
            ]);
        arguments[1].Name.ShouldBe("parameters");
        arguments[1].TypeText.ShouldBe("global::System.Collections.Generic.IEnumerable<global::App.GetUserParams>");
        arguments[1].DocumentationXml.ShouldBe("The items to run the query for, in order.");
        Enumerate(MethodArguments.Of(method with { Source = TestModels.Source() }, ["orderBy"]))
            .ConvertAll(static argument => argument.Role)
            .ShouldBe([ArgumentRole.Input, ArgumentRole.Token, ArgumentRole.Cancellation]);
    }

    // A collection method's transaction says what null does under its mode: under on or an isolation level the method
    // begins its own, unless an ambient one is open, and under off it runs in none.  A query method's is unchanged.
    [Theory]
    [InlineData(
        nameof(TransactionMode.On),
        "The transaction to run every item in, or null for the method to begin one of its own, which it commits after "
            + "the last item and rolls back when anything throws, unless a <c>System.Transactions</c> transaction is "
            + "open."
    )]
    [InlineData(
        nameof(TransactionMode.Serializable),
        "The transaction to run every item in, or null for the method to begin one of its own, which it commits after "
            + "the last item and rolls back when anything throws, unless a <c>System.Transactions</c> transaction is "
            + "open."
    )]
    [InlineData(nameof(TransactionMode.Off), "The transaction to run every item in, or null to run them in none.")]
    public void Of_BatchMethod_DocumentsTheTransactionByItsMode(string modeName, string expected)
    {
        var method = Emitting.QueryMethods.QueryMethodWriterTests.GetUser with
        {
            Batch = new BatchCall(Enum.Parse<TransactionMode>(modeName)),
        };

        Enumerate(MethodArguments.Of(method, ["orderBy"]))
            .Find(static argument => argument.Role == ArgumentRole.Transaction)
            .DocumentationXml.ShouldBe(expected);
        Enumerate(MethodArguments.Of(Emitting.QueryMethods.QueryMethodWriterTests.GetUser, ["orderBy"]))
            .Find(static argument => argument.Role == ArgumentRole.Transaction)
            .DocumentationXml.ShouldBe("The transaction to run the query in, or null.");
    }

    [Fact]
    public void Of_RepositoryWithoutInputOrTokens_IsTheCancellationAlone() =>
        Enumerate(new MethodArguments(null, [], streams: true, takesConnection: false))
            .ConvertAll(static argument => argument.Role)
            .ShouldBe([ArgumentRole.Cancellation]);

    // A synchronous method takes the asynchronous method's arguments without the cancellation token.
    [Fact]
    public void Of_SynchronousMethod_TakesNoCancellationToken()
    {
        var method = Emitting.QueryMethods.QueryMethodWriterTests.GetUser with
        {
            Synchrony = MethodSynchrony.Synchronous,
        };

        Enumerate(MethodArguments.Of(method, ["orderBy", "await"]))
            .ConvertAll(static argument => argument.Role)
            .ShouldBe([
                ArgumentRole.Receiver,
                ArgumentRole.Input,
                ArgumentRole.Token,
                ArgumentRole.Token,
                ArgumentRole.Transaction,
                ArgumentRole.CommandTimeout,
            ]);
        Enumerate(MethodArguments.Of(method with { Source = TestModels.Source() }, ["orderBy"]))
            .ConvertAll(static argument => argument.Role)
            .ShouldBe([ArgumentRole.Input, ArgumentRole.Token]);
    }

    [Fact]
    public void AppendDeclaration_SynchronousMethod_EndsAfterTheCommandTimeout()
    {
        var builder = new StringBuilder();

        Synchronous().AppendDeclaration(builder, "    ", default);

        builder
            .ToString()
            .ShouldBe(
                "        this global::System.Data.Common.DbConnection connection,\n"
                    + "        global::App.P parameters,\n"
                    + "        string orderBy,\n"
                    + "        string @await,\n"
                    + "        global::System.Data.Common.DbTransaction? transaction = null,\n"
                    + "        int? commandTimeout = null)\n"
            );
    }

    // A synchronous repository's method has no argument after its tokens, so the last token ends the member.
    [Fact]
    public void AppendInterfaceDeclaration_SynchronousRepositoryMethod_EndsAfterTheLastToken()
    {
        var builder = new StringBuilder();

        new MethodArguments(
            Input,
            ["orderBy", "await"],
            false,
            false,
            synchrony: MethodSynchrony.Synchronous
        ).AppendInterfaceDeclaration(builder, "    ");

        builder
            .ToString()
            .ShouldBe("        global::App.P parameters,\n        string orderBy,\n        string @await);\n");
    }

    [Fact]
    public void GetEnumerator_Streams_DocumentsTheReceiverForTheLoop()
    {
        Enumerate(Of(streams: true))[0].DocumentationXml.ShouldBe(StreamConnectionXml);
        Enumerate(Of())[0].DocumentationXml.ShouldBe(ConnectionXml);
    }

    [Theory]
    [InlineData(nameof(MemberLocation.ExtensionClass), "this ")]
    [InlineData(nameof(MemberLocation.Class), "")]
    [InlineData(nameof(MemberLocation.NestedClass), "")]
    public void AppendDeclaration_TokenNamedAwait_IsVerbatim(string locationName, string receiverPrefix)
    {
        var builder = new StringBuilder();

        Of()
            .AppendDeclaration(
                builder,
                "    ",
                new MethodHost(Visibility.Public, Enum.Parse<MemberLocation>(locationName), null)
            );

        builder
            .ToString()
            .ShouldBe(
                "        "
                    + receiverPrefix
                    + "global::System.Data.Common.DbConnection connection,\n"
                    + "        global::App.P parameters,\n"
                    + "        string orderBy,\n"
                    + "        string @await,\n"
                    + "        global::System.Data.Common.DbTransaction? transaction = null,\n"
                    + "        int? commandTimeout = null,\n"
                    + "        global::System.Threading.CancellationToken cancellationToken = default)\n"
            );
    }

    [Fact]
    public void AppendIteratorDeclaration_OfTheArguments_TakesNoDefaultsAndMarksTheToken()
    {
        var builder = new StringBuilder();

        Of().AppendIteratorDeclaration(builder, "    ");

        builder
            .ToString()
            .ShouldBe(
                "        global::System.Data.Common.DbConnection connection,\n"
                    + "        global::App.P parameters,\n"
                    + "        string orderBy,\n"
                    + "        string @await,\n"
                    + "        global::System.Data.Common.DbTransaction? transaction,\n"
                    + "        int? commandTimeout,\n"
                    + "        [global::System.Runtime.CompilerServices.EnumeratorCancellation] "
                    + "global::System.Threading.CancellationToken cancellationToken)\n"
            );
    }

    [Fact]
    public void AppendCall_OfTheArguments_GivesEachNameInOrder()
    {
        var builder = new StringBuilder();

        Of().AppendCall(builder);

        builder
            .ToString()
            .ShouldBe("connection, parameters, orderBy, @await, transaction, commandTimeout, cancellationToken");
    }

    [Theory]
    [InlineData("connection")]
    [InlineData("parameters")]
    [InlineData("transaction")]
    [InlineData("commandTimeout")]
    [InlineData("cancellationToken")]
    public void IsReserved_FixedName_IsTrue(string name) => MethodArguments.IsReserved(name).ShouldBeTrue();

    [Theory]
    [InlineData("Connection")]
    [InlineData("orderBy")]
    public void IsReserved_NameDifferingByCase_IsFalse(string name) => MethodArguments.IsReserved(name).ShouldBeFalse();

    [Fact]
    public void GetEnumerator_AndEachForm_AllocatesNothing()
    {
        var arguments = Of();
        var withoutConnection = new MethodArguments(Input, ["orderBy", "await"], false, false);
        var synchronous = Synchronous();
        var builder = new StringBuilder(4096);
        var roles = 0;
        for (var round = 0; round < 10; round++)
        {
            roles += Round(arguments, builder) + Round(withoutConnection, builder) + Round(synchronous, builder);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var round = 0; round < 1_000; round++)
        {
            roles += Round(arguments, builder) + Round(withoutConnection, builder) + Round(synchronous, builder);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.ShouldBe(0);
        roles.ShouldBe(1_010 * (7 + 4 + 6));
    }

    private static MethodArguments Of(bool streams = false) => new(Input, ["orderBy", "await"], streams, true);

    private static MethodArguments Synchronous() =>
        new(Input, ["orderBy", "await"], false, true, synchrony: MethodSynchrony.Synchronous);

    private static List<MethodArgument> Enumerate(MethodArguments arguments)
    {
        var list = new List<MethodArgument>();
        foreach (var argument in arguments)
        {
            list.Add(argument);
        }

        return list;
    }

    // One round of the allocation test: a walk of the arguments and each form, into the cleared builder.  It gives the
    // number of arguments walked, so that the walk is not left out.
    private static int Round(MethodArguments arguments, StringBuilder builder)
    {
        var count = 0;
        foreach (var argument in arguments)
        {
            count += argument.Name.Length > 0 ? 1 : 0;
        }

        _ = builder.Clear();
        arguments.AppendDeclaration(builder, "    ", default);
        arguments.AppendInterfaceDeclaration(builder, "    ");
        arguments.AppendIteratorDeclaration(builder, "    ");
        arguments.AppendCall(builder);
        return count;
    }
}
