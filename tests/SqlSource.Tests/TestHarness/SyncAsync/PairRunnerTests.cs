using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using Xunit;

namespace SqlSource.Tests.TestHarness.SyncAsync;

public sealed class PairRunnerTests
{
    // A command of a data source refuses a connection, so the runner runs Prepare and the tables' SELECTs on commands
    // of the call's connection.
    [Fact]
    public async Task RunAsync_PrepareAndTables_RunOnTheConnectionOfEachCall()
    {
        var database = new FakeDatabase();
        var pair = MethodPairs
            .Find(typeof(PairRunnerTests).Assembly)
            .Single(static pair =>
                pair.Host == typeof(FakePairs.Queries) && pair.Method == "Get" && pair.Kind == MethodKind.Query
            );
        var scenario = new Scenario(
            "Queries.Get: prepared, with a table",
            typeof(FakePairs.Queries),
            "Get",
            MethodKind.Query,
            ScenarioTag.Ordinary,
            new Setup(Tables: ["SELECT n"], Prepare: "SET n"),
            new FakePairs.Args(1)
        );

        var verdict = await PairRunner.RunAsync(database, pair, scenario, []);

        verdict.Agreement.ShouldBe(Agreement.Same);
        database.Commands.ShouldBe(["SET n", "SELECT n", "SET n", "SELECT n"]);
    }

    // A caller who begins a transaction and does not pass it: each call is given null, and the transaction is still
    // begun before it and rolled back after it.
    [Theory]
    [InlineData(true, "given")]
    [InlineData(false, "not given")]
    public async Task RunAsync_PassTransaction_GivesTheCallTheTransactionOrNull(bool passTransaction, string seen)
    {
        var database = new FakeDatabase();
        var pair = MethodPairs
            .Find(typeof(PairRunnerTests).Assembly)
            .Single(static pair => pair.Host == typeof(FakeConnectionPairs) && pair.Method == "Run");
        var scenario = new Scenario(
            "FakeConnectionPairs.Run: in a transaction",
            typeof(FakeConnectionPairs),
            "Run",
            MethodKind.Query,
            ScenarioTag.Ordinary,
            new Setup(Transaction: CallerTransaction.Begun, PassTransaction: passTransaction),
            new FakePairs.Args(1)
        );

        var verdict = await PairRunner.RunAsync(database, pair, scenario, []);

        verdict.Agreement.ShouldBe(Agreement.Same);
        database.Commands.ShouldBe(["BEGIN", seen, "ROLLBACK", "BEGIN", seen, "ROLLBACK"]);
    }

    // A caller whose transaction has completed before the call: the call is given it, and the runner's own rollback
    // after the call finds nothing left to roll back.
    [Fact]
    public async Task RunAsync_CompletedTransaction_IsRolledBackBeforeTheCall()
    {
        var database = new FakeDatabase();
        var pair = MethodPairs
            .Find(typeof(PairRunnerTests).Assembly)
            .Single(static pair => pair.Host == typeof(FakeConnectionPairs) && pair.Method == "Run");
        var scenario = new Scenario(
            "FakeConnectionPairs.Run: completed",
            typeof(FakeConnectionPairs),
            "Run",
            MethodKind.Query,
            ScenarioTag.Other,
            new Setup(Transaction: CallerTransaction.Completed),
            new FakePairs.Args(1)
        );

        var verdict = await PairRunner.RunAsync(database, pair, scenario, []);

        verdict.Agreement.ShouldBe(Agreement.Same);
        database.Commands.ShouldBe(["BEGIN", "ROLLBACK", "given", "BEGIN", "ROLLBACK", "given"]);
    }

    // Each run of a scenario that writes has a copy of the database of its own, whose name a server's message may hold.
    [Fact]
    public async Task RunAsync_MessageNamingTheFreshDatabase_IsTheSame()
    {
        var database = new FakeDatabase();
        var pair = MethodPairs
            .Find(typeof(PairRunnerTests).Assembly)
            .Single(static pair => pair.Host == typeof(FakeConnectionPairs) && pair.Method == "FailIn");
        var scenario = new Scenario(
            "FakeConnectionPairs.FailIn: on a copy",
            typeof(FakeConnectionPairs),
            "FailIn",
            MethodKind.Query,
            ScenarioTag.DatabaseError,
            new Setup(Writes: true),
            new FakePairs.Args(1)
        );

        var verdict = await PairRunner.RunAsync(database, pair, scenario, []);

        verdict.Agreement.ShouldBe(Agreement.Same);
    }

    // Each method of the pair runs once: a runner that ran the asynchronous method twice would find the two the same.
    [Fact]
    public async Task RunAsync_PairThatDiffers_IsDifferent()
    {
        var database = new FakeDatabase();
        var pair = PairOf(typeof(FakePairs.Queries), "Fail");
        var scenario = new Scenario(
            "Queries.Fail: each method its own message",
            typeof(FakePairs.Queries),
            "Fail",
            MethodKind.Query,
            ScenarioTag.Other,
            new Setup(),
            new FakePairs.Args(1)
        );

        var verdict = await PairRunner.RunAsync(database, pair, scenario, []);

        verdict.Agreement.ShouldBe(Agreement.Different);
        verdict.Report.ShouldContain("async failure");
        verdict.Report.ShouldContain("sync failure");
    }

    // The item an exception names is compared, so a synchronous method that fails at another item differs.
    [Fact]
    public async Task RunAsync_ExceptionWithItemIndex_RecordsIt()
    {
        var database = new FakeDatabase();
        var pair = PairOf(typeof(FakeConnectionPairs), "FailAt");
        var scenario = new Scenario(
            "FakeConnectionPairs.FailAt: each method its own item",
            typeof(FakeConnectionPairs),
            "FailAt",
            MethodKind.Query,
            ScenarioTag.LaterChunkFailure,
            new Setup(),
            new FakePairs.Args(2)
        );

        var verdict = await PairRunner.RunAsync(database, pair, scenario, []);

        verdict.Agreement.ShouldBe(Agreement.Different);
        verdict.Report.ShouldContain("item 2)");
        verdict.Report.ShouldContain("item 3)");
    }

    // What the call left in its transaction is read in that transaction, before the runner rolls it back.
    [Fact]
    public async Task RunAsync_Tables_ReadOnTheCallsConnectionInItsTransaction()
    {
        var database = new FakeDatabase();
        var pair = PairOf(typeof(FakeConnectionPairs), "Run");
        var scenario = new Scenario(
            "FakeConnectionPairs.Run: in a transaction, with a table",
            typeof(FakeConnectionPairs),
            "Run",
            MethodKind.Query,
            ScenarioTag.Ordinary,
            new Setup(Transaction: CallerTransaction.Begun, Tables: ["SELECT n"]),
            new FakePairs.Args(1)
        );

        _ = await PairRunner.RunAsync(database, pair, scenario, []);

        database
            .Executions.Select(static execution => execution.Text)
            .ShouldBe(["given", "SELECT n", "given", "SELECT n"]);
        foreach (var (call, table) in new[] { (0, 1), (2, 3) })
        {
            _ = database.Executions[call].Transaction.ShouldNotBeNull();
            database.Executions[table].Connection.ShouldBeSameAs(database.Executions[call].Connection);
            database.Executions[table].Transaction.ShouldBeSameAs(database.Executions[call].Transaction);
        }
    }

    // The returned value and each table's values are written through the engine's Render, which renders the values of
    // the libraries the harness does not reference.
    [Fact]
    public async Task RunAsync_ResultAndTables_RenderedThroughTheEngine()
    {
        var rendered = new List<object>();
        var database = new FakeDatabase
        {
            Renderer = value =>
            {
                rendered.Add(value);
                return null;
            },
        };
        var pair = PairOf(typeof(FakePairs.Queries), "Get");
        var scenario = new Scenario(
            "Queries.Get: with a table",
            typeof(FakePairs.Queries),
            "Get",
            MethodKind.Query,
            ScenarioTag.Ordinary,
            new Setup(Tables: ["SELECT n"]),
            new FakePairs.Args(41)
        );

        _ = await PairRunner.RunAsync(database, pair, scenario, []);

        rendered.ShouldBe([42, 1, 42, 1]);
    }

    // A pair whose two methods differ in one part of what they do alone, with the database set up for it: the runner
    // must record that part and Verdicts compare it.
    private static (FakeDatabase Database, string Method, Setup Setup) DifferingIn(string part) =>
        part switch
        {
            "Result" => (new FakeDatabase(), "Answer", new Setup()),
            "ConnectionState" => (new FakeDatabase(), "Close", new Setup()),
            "Tables" => (
                new FakeDatabase { TableValue = static read => read + 1 },
                "Run",
                new Setup(Tables: ["SELECT n"])
            ),
            "TransactionState" => (
                new FakeDatabase { TransactionState = static call => call == 0 ? "active" : "aborted" },
                "Run",
                new Setup(Transaction: CallerTransaction.Begun)
            ),
            "Code" => (new FakeDatabase(), "FailWithCode", new Setup()),
            "InnerType" => (new FakeDatabase(), "FailWithInner", new Setup()),
            _ => throw new ArgumentException($"No pair differs in '{part}'.", nameof(part)),
        };

    [Theory]
    [InlineData("Result", "Result: async = 2", "; sync = 1")]
    [InlineData("ConnectionState", "ConnectionState: async = Open", "; sync = Closed")]
    [InlineData("Tables", "Tables[0]: async = n\n1", "; sync = n\n2")]
    [InlineData("TransactionState", "TransactionState: async = active", "; sync = aborted")]
    [InlineData("Code", "code 23503", "code 23505")]
    [InlineData("InnerType", "inner System.ArgumentException", "inner System.FormatException")]
    public async Task RunAsync_PairThatDiffersInOnePart_IsDifferent(string part, string asyncText, string syncText)
    {
        var (database, method, setup) = DifferingIn(part);
        var pair = PairOf(typeof(FakeConnectionPairs), method);
        var scenario = new Scenario(
            $"FakeConnectionPairs.{method}: differs in {part}",
            typeof(FakeConnectionPairs),
            method,
            MethodKind.Query,
            ScenarioTag.Other,
            setup,
            new FakePairs.Args(1)
        );

        var verdict = await PairRunner.RunAsync(database, pair, scenario, []);

        verdict.Agreement.ShouldBe(Agreement.Different);
        verdict.Report.ShouldContain(asyncText);
        verdict.Report.ShouldContain(syncText);
    }

    private static MethodPair PairOf(Type host, string method) =>
        MethodPairs
            .Find(typeof(PairRunnerTests).Assembly)
            .Single(pair => pair.Host == host && pair.Method == method && pair.Kind == MethodKind.Query);
}
