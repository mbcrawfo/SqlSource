using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.Tool.Tests.Realistic;

// The call of a case's generated method by reflection: a method of the generated signature passes the check and one of
// any other fails naming what differs, a call passes the method what it is given and gives back every row, and an
// exception of the method surfaces as itself, whether it is thrown at once or after an await.  The column that held a
// NULL where its property cannot is named with that property.  The fakes stand for generated methods and touch neither
// the connection nor the transaction.
public sealed class RealisticCallsTests
{
    [Theory]
    [InlineData(nameof(Fakes.WithInput), typeof(Args))]
    [InlineData(nameof(Fakes.WithoutInput), null)]
    public void CheckSignature_GeneratedSignature_Passes(string name, Type? input) =>
        Should.NotThrow(() => RealisticCalls.CheckSignature(MethodOf(name), typeof(Row), input));

    [Fact]
    public void CheckSignature_OtherReturnType_ThrowsNamingIt()
    {
        var thrown = Should.Throw<InvalidOperationException>(() =>
            RealisticCalls.CheckSignature(MethodOf(nameof(Fakes.ReturnsList)), typeof(Row), null)
        );

        thrown.Message.ShouldContain(nameof(Fakes.ReturnsList));
        thrown.Message.ShouldContain("Task<List<Row>>");
    }

    [Fact]
    public void CheckSignature_MissingParameters_Throws()
    {
        var thrown = Should.Throw<InvalidOperationException>(() =>
            RealisticCalls.CheckSignature(MethodOf(nameof(Fakes.WithoutInput)), typeof(Row), typeof(Args))
        );

        thrown.Message.ShouldContain(nameof(Fakes.WithoutInput));
        thrown.Message.ShouldContain("Args parameters");
    }

    [Fact]
    public async Task InvokeAsync_Method_ReturnsEveryRow()
    {
        await using var connection = new StubConnection();
        await using var transaction = new StubTransaction();

        var read = await RealisticCalls.InvokeAsync(
            MethodOf(nameof(Fakes.WithoutInput)),
            connection,
            null,
            transaction,
            TestContext.Current.CancellationToken
        );

        read.ShouldBe([new Row(1, "one"), new Row(2, null)]);
    }

    [Fact]
    public async Task InvokeAsync_Method_PassesConnectionInputTransactionAndToken()
    {
        await using var connection = new StubConnection();
        await using var transaction = new StubTransaction();
        using var cancellation = new CancellationTokenSource();
        var input = new Args();

        _ = await RealisticCalls.InvokeAsync(
            MethodOf(nameof(Fakes.WithInput)),
            connection,
            input,
            transaction,
            cancellation.Token
        );

        input.Received.ShouldBe(new Call(connection, transaction, null, cancellation.Token));
    }

    [Fact]
    public async Task InvokeAsync_MethodThatThrowsAtOnce_ThrowsItsException()
    {
        await using var connection = new StubConnection();
        await using var transaction = new StubTransaction();

        var thrown = await Should.ThrowAsync<InvalidCastException>(() =>
            RealisticCalls.InvokeAsync(
                MethodOf(nameof(Fakes.ThrowsAtOnce)),
                connection,
                null,
                transaction,
                TestContext.Current.CancellationToken
            )
        );

        thrown.Message.ShouldBe(nameof(Fakes.ThrowsAtOnce));
    }

    [Fact]
    public async Task InvokeAsync_MethodThatThrowsLater_ThrowsItsException()
    {
        await using var connection = new StubConnection();
        await using var transaction = new StubTransaction();

        var thrown = await Should.ThrowAsync<InvalidCastException>(() =>
            RealisticCalls.InvokeAsync(
                MethodOf(nameof(Fakes.ThrowsLater)),
                connection,
                null,
                transaction,
                TestContext.Current.CancellationToken
            )
        );

        thrown.Message.ShouldBe(nameof(Fakes.ThrowsLater));
    }

    [Fact]
    public void NullColumnsOf_NotNullColumnThatHeldNull_NamesColumnAndProperty() =>
        RealisticCalls
            .NullColumnsOf(RealisticTestCases.Keyed(), typeof(Row), ["id", "name"], [true, false])
            .ShouldBe(
                "realistic/keyed column 0 id is the property Row.Id of Int32, which is not nullable, and held a NULL "
                    + "over the seed"
            );

    // Both columns held a NULL, and the second's property can hold null, so only the first is named.
    [Fact]
    public void NullColumnsOf_NullableColumnThatHeldNull_IsNotNamed() =>
        RealisticCalls
            .NullColumnsOf(RealisticTestCases.Keyed(), typeof(Row), ["id", "name"], [true, true])
            .ShouldBe(
                "realistic/keyed column 0 id is the property Row.Id of Int32, which is not nullable, and held a NULL "
                    + "over the seed"
            );

    // A run of more columns than the model has properties says so, and names the columns that have a property.
    [Fact]
    public void NullColumnsOf_MoreColumnsThanProperties_NamesTheMismatch() =>
        RealisticCalls
            .NullColumnsOf(RealisticTestCases.Keyed(), typeof(Row), ["id", "name", "extra"], [true, false, true])
            .ShouldBe(
                "realistic/keyed read 3 columns over the seed, and Row has 2 properties\n"
                    + "realistic/keyed column 0 id is the property Row.Id of Int32, which is not nullable, and held a "
                    + "NULL over the seed"
            );

    [Fact]
    public void NullColumnsOf_NoNotNullColumnHeldNull_SaysNone() =>
        RealisticCalls
            .NullColumnsOf(RealisticTestCases.Keyed(), typeof(Row), ["id", "name"], [false, true])
            .ShouldBe("realistic/keyed: the oracle saw no NULL in a column whose property is not nullable");

    private static MethodInfo MethodOf(string name) =>
        typeof(Fakes).GetMethod(name, BindingFlags.Static | BindingFlags.Public)
        ?? throw new InvalidOperationException($"No fake {name}");

    // A row of a fake's result: a column that cannot be null and one that can.
    private sealed record Row(int Id, string? Name);

    // What a call gave the fake that records it, beside the input itself.
    private sealed record Call(
        DbConnection Connection,
        DbTransaction? Transaction,
        int? CommandTimeout,
        CancellationToken CancellationToken
    );

    // The input model of a fake, which keeps what the fake that records it was given.
    private sealed class Args
    {
        public Call? Received { get; set; }
    }

    // Methods of the generated signature, or of another, as the generator would write them on an extension class.
    [SuppressMessage(
        "Style",
        "IDE0060:Remove unused parameter",
        Justification = "A fake has the parameters of the generated signature, which it does not read."
    )]
    [SuppressMessage(
        "Major Code Smell",
        "S1172:Unused method parameters should be removed",
        Justification = "A fake has the parameters of the generated signature, which it does not read."
    )]
    private static class Fakes
    {
        private static readonly IReadOnlyList<Row> Rows = [new Row(1, "one"), new Row(2, null)];

        public static async Task<IReadOnlyList<Row>> WithInput(
            DbConnection connection,
            Args parameters,
            DbTransaction? transaction = null,
            int? commandTimeout = null,
            CancellationToken cancellationToken = default
        )
        {
            await Task.Yield();
            parameters.Received = new Call(connection, transaction, commandTimeout, cancellationToken);
            return Rows;
        }

        public static async Task<IReadOnlyList<Row>> WithoutInput(
            DbConnection connection,
            DbTransaction? transaction = null,
            int? commandTimeout = null,
            CancellationToken cancellationToken = default
        )
        {
            await Task.Yield();
            return Rows;
        }

        public static async Task<List<Row>> ReturnsList(
            DbConnection connection,
            DbTransaction? transaction = null,
            int? commandTimeout = null,
            CancellationToken cancellationToken = default
        )
        {
            await Task.Yield();
            return [.. Rows];
        }

        // Not async, so that the exception is thrown before a task is made, as one before the first await of a method
        // that checks its arguments would be.
        public static Task<IReadOnlyList<Row>> ThrowsAtOnce(
            DbConnection connection,
            DbTransaction? transaction = null,
            int? commandTimeout = null,
            CancellationToken cancellationToken = default
        ) => throw new InvalidCastException(nameof(ThrowsAtOnce));

        public static async Task<IReadOnlyList<Row>> ThrowsLater(
            DbConnection connection,
            DbTransaction? transaction = null,
            int? commandTimeout = null,
            CancellationToken cancellationToken = default
        )
        {
            await Task.Yield();
            throw new InvalidCastException(nameof(ThrowsLater));
        }
    }

    // A connection that the fakes are given and never use.
    private sealed class StubConnection : DbConnection
    {
        [AllowNull]
        public override string ConnectionString
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override string Database => throw new NotSupportedException();

        public override string DataSource => throw new NotSupportedException();

        public override string ServerVersion => throw new NotSupportedException();

        public override ConnectionState State => throw new NotSupportedException();

        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

        public override void Close() => throw new NotSupportedException();

        public override void Open() => throw new NotSupportedException();

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            throw new NotSupportedException();

        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }

    // A transaction that the fakes are given and never use.
    private sealed class StubTransaction : DbTransaction
    {
        public override IsolationLevel IsolationLevel => throw new NotSupportedException();

        protected override DbConnection DbConnection => throw new NotSupportedException();

        public override void Commit() => throw new NotSupportedException();

        public override void Rollback() => throw new NotSupportedException();
    }
}
