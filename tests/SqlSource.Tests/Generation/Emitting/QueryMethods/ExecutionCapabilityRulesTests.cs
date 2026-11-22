using System;
using Shouldly;
using SqlSource.Generation.Emitting.QueryMethods;
using SqlSource.Generation.Methods;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Generation.Emitting.QueryMethods;

public sealed class ExecutionCapabilityRulesTests
{
    private const ExecutionCapabilities SqlServerReadsRows =
        ExecutionCapabilities.RefusesCompletedTransaction
        | ExecutionCapabilities.DrainsLaterResults
        | ExecutionCapabilities.ChecksRollbackAtRead
        | ExecutionCapabilities.ChecksRollbackAfterDatabaseError;

    private const ExecutionCapabilities SqlServerBatchReadsNoRows =
        ExecutionCapabilities.RefusesCompletedTransaction | ExecutionCapabilities.RestoresSessionIsolation;

    private const ExecutionCapabilities SqlServerBatchReadsRows =
        SqlServerBatchReadsNoRows
        | ExecutionCapabilities.ChecksRollbackAtRead
        | ExecutionCapabilities.ChecksRollbackAfterDatabaseError
        | ExecutionCapabilities.ChecksResultSetsMatchItems;

    // PostgreSQL runs one statement and needs none of SQL Server's behaviours.  SQL Server refuses a completed
    // transaction on every method, drains and checks for a rollback after its own exception and after a database error
    // on one that reads rows, and drains in a finally on one that streams.
    [Theory]
    [InlineData(
        nameof(DatabaseEngine.PostgreSql),
        nameof(ResultShape.None),
        nameof(CollectionKind.List),
        (int)ExecutionCapabilities.None
    )]
    [InlineData(
        nameof(DatabaseEngine.PostgreSql),
        nameof(ResultShape.RowCount),
        nameof(CollectionKind.List),
        (int)ExecutionCapabilities.None
    )]
    [InlineData(
        nameof(DatabaseEngine.PostgreSql),
        nameof(ResultShape.One),
        nameof(CollectionKind.List),
        (int)ExecutionCapabilities.None
    )]
    [InlineData(
        nameof(DatabaseEngine.PostgreSql),
        nameof(ResultShape.OneOptional),
        nameof(CollectionKind.List),
        (int)ExecutionCapabilities.None
    )]
    [InlineData(
        nameof(DatabaseEngine.PostgreSql),
        nameof(ResultShape.Many),
        nameof(CollectionKind.List),
        (int)ExecutionCapabilities.None
    )]
    [InlineData(
        nameof(DatabaseEngine.PostgreSql),
        nameof(ResultShape.Many),
        nameof(CollectionKind.IAsyncEnumerable),
        (int)ExecutionCapabilities.None
    )]
    [InlineData(
        nameof(DatabaseEngine.SqlServer),
        nameof(ResultShape.None),
        nameof(CollectionKind.List),
        (int)ExecutionCapabilities.RefusesCompletedTransaction
    )]
    [InlineData(
        nameof(DatabaseEngine.SqlServer),
        nameof(ResultShape.RowCount),
        nameof(CollectionKind.List),
        (int)ExecutionCapabilities.RefusesCompletedTransaction
    )]
    [InlineData(
        nameof(DatabaseEngine.SqlServer),
        nameof(ResultShape.One),
        nameof(CollectionKind.List),
        (int)SqlServerReadsRows
    )]
    [InlineData(
        nameof(DatabaseEngine.SqlServer),
        nameof(ResultShape.OneOptional),
        nameof(CollectionKind.List),
        (int)SqlServerReadsRows
    )]
    [InlineData(
        nameof(DatabaseEngine.SqlServer),
        nameof(ResultShape.Many),
        nameof(CollectionKind.List),
        (int)SqlServerReadsRows
    )]
    [InlineData(
        nameof(DatabaseEngine.SqlServer),
        nameof(ResultShape.Many),
        nameof(CollectionKind.IAsyncEnumerable),
        (int)(SqlServerReadsRows | ExecutionCapabilities.DrainsWhenLeftAtARow)
    )]
    public void Of_EachEngineShapeAndStreaming_GivesItsFlags(
        string engineName,
        string shapeName,
        string collectionName,
        int expected
    )
    {
        var method = QueryMethodWriterTests.GetUser with
        {
            Engine = Enum.Parse<DatabaseEngine>(engineName),
            Shape = Enum.Parse<ResultShape>(shapeName),
            Collection = Enum.Parse<CollectionKind>(collectionName),
        };

        ExecutionCapabilityRules.Of(method).ShouldBe((ExecutionCapabilities)expected);
    }

    // A repository's method takes no transaction, so it neither refuses nor checks one; it still drains the later
    // results, and drains in a finally when it streams.
    [Theory]
    [InlineData(nameof(ResultShape.None), nameof(CollectionKind.List), (int)ExecutionCapabilities.None)]
    [InlineData(nameof(ResultShape.RowCount), nameof(CollectionKind.List), (int)ExecutionCapabilities.None)]
    [InlineData(nameof(ResultShape.One), nameof(CollectionKind.List), (int)ExecutionCapabilities.DrainsLaterResults)]
    [InlineData(
        nameof(ResultShape.OneOptional),
        nameof(CollectionKind.List),
        (int)ExecutionCapabilities.DrainsLaterResults
    )]
    [InlineData(nameof(ResultShape.Many), nameof(CollectionKind.List), (int)ExecutionCapabilities.DrainsLaterResults)]
    [InlineData(
        nameof(ResultShape.Many),
        nameof(CollectionKind.IAsyncEnumerable),
        (int)(ExecutionCapabilities.DrainsLaterResults | ExecutionCapabilities.DrainsWhenLeftAtARow)
    )]
    public void Of_RepositoryOnSqlServer_KeepsOnlyTheDrains(string shapeName, string collectionName, int expected)
    {
        var method = QueryMethodWriterTests.GetUser with
        {
            Engine = DatabaseEngine.SqlServer,
            Shape = Enum.Parse<ResultShape>(shapeName),
            Collection = Enum.Parse<CollectionKind>(collectionName),
            Source = TestModels.Source(),
        };

        ExecutionCapabilityRules.Of(method).ShouldBe((ExecutionCapabilities)expected);
    }

    // A collection method on SQL Server refuses the caller's completed transaction, and drains nothing, since each
    // result set of its batch is an item's and it never streams; one that reads rows checks for a rollback of the
    // caller's transaction as a query method does, and that each result set is an item's, and as a repository's method,
    // which takes no transaction, only the second.  Under on, the mode of these rows, it begins a transaction of its
    // own and restores the session's level after it, a repository's method too.  On PostgreSQL it finds the failed item
    // by the first command that did not complete, whatever its shape, and as a repository's method too.
    [Theory]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(ResultShape.Many), false, (int)SqlServerBatchReadsRows)]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(ResultShape.One), false, (int)SqlServerBatchReadsRows)]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(ResultShape.OneOptional), false, (int)SqlServerBatchReadsRows)]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(ResultShape.RowCount), false, (int)SqlServerBatchReadsNoRows)]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(ResultShape.None), false, (int)SqlServerBatchReadsNoRows)]
    [InlineData(
        nameof(DatabaseEngine.SqlServer),
        nameof(ResultShape.Many),
        true,
        (int)(ExecutionCapabilities.ChecksResultSetsMatchItems | ExecutionCapabilities.RestoresSessionIsolation)
    )]
    [InlineData(
        nameof(DatabaseEngine.SqlServer),
        nameof(ResultShape.RowCount),
        true,
        (int)ExecutionCapabilities.RestoresSessionIsolation
    )]
    [InlineData(
        nameof(DatabaseEngine.PostgreSql),
        nameof(ResultShape.Many),
        false,
        (int)ExecutionCapabilities.FindsFailedItemByCompletion
    )]
    [InlineData(
        nameof(DatabaseEngine.PostgreSql),
        nameof(ResultShape.None),
        false,
        (int)ExecutionCapabilities.FindsFailedItemByCompletion
    )]
    [InlineData(
        nameof(DatabaseEngine.PostgreSql),
        nameof(ResultShape.RowCount),
        false,
        (int)ExecutionCapabilities.FindsFailedItemByCompletion
    )]
    [InlineData(
        nameof(DatabaseEngine.PostgreSql),
        nameof(ResultShape.One),
        true,
        (int)ExecutionCapabilities.FindsFailedItemByCompletion
    )]
    public void Of_CollectionMethod_GivesItsEnginesFlags(
        string engineName,
        string shapeName,
        bool isRepository,
        int expected
    )
    {
        var method = QueryMethodWriterTests.GetUser with
        {
            Engine = Enum.Parse<DatabaseEngine>(engineName),
            Shape = Enum.Parse<ResultShape>(shapeName),
            Collection = CollectionKind.Array,
            Source = isRepository ? TestModels.Source() : null,
            Batch = new BatchCall(TransactionMode.On),
        };

        ExecutionCapabilityRules.Of(method).ShouldBe((ExecutionCapabilities)expected);
    }

    // On SQL Server a collection method whose mode begins a transaction of its own, at a level it names or under on,
    // restores the session's level after it, a repository's method too, since it begins its own; under off, which
    // begins none, it restores nothing, and on PostgreSQL, whose level is the transaction's alone, at no mode.
    [Theory]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(TransactionMode.ReadUncommitted), false, true)]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(TransactionMode.ReadCommitted), false, true)]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(TransactionMode.RepeatableRead), false, true)]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(TransactionMode.Serializable), false, true)]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(TransactionMode.Snapshot), false, true)]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(TransactionMode.Serializable), true, true)]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(TransactionMode.ReadUncommitted), true, true)]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(TransactionMode.On), false, true)]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(TransactionMode.Off), false, false)]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(TransactionMode.On), true, true)]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(TransactionMode.Off), true, false)]
    [InlineData(nameof(DatabaseEngine.PostgreSql), nameof(TransactionMode.ReadCommitted), false, false)]
    [InlineData(nameof(DatabaseEngine.PostgreSql), nameof(TransactionMode.RepeatableRead), false, false)]
    [InlineData(nameof(DatabaseEngine.PostgreSql), nameof(TransactionMode.Serializable), true, false)]
    [InlineData(nameof(DatabaseEngine.PostgreSql), nameof(TransactionMode.On), false, false)]
    public void Of_CollectionMethodAtAMode_RestoresTheSessionsIsolationOnlyWithItsOwnTransactionOnSqlServer(
        string engineName,
        string modeName,
        bool isRepository,
        bool restores
    )
    {
        var method = QueryMethodWriterTests.GetUser with
        {
            Engine = Enum.Parse<DatabaseEngine>(engineName),
            Collection = CollectionKind.Array,
            Source = isRepository ? TestModels.Source() : null,
            Batch = new BatchCall(Enum.Parse<TransactionMode>(modeName)),
        };

        ExecutionCapabilityRules.Of(method).Has(ExecutionCapabilities.RestoresSessionIsolation).ShouldBe(restores);
    }

    [Theory]
    [InlineData(nameof(ExecutionCapabilities.RefusesCompletedTransaction))]
    [InlineData(nameof(ExecutionCapabilities.DrainsLaterResults))]
    [InlineData(nameof(ExecutionCapabilities.ChecksRollbackAtRead))]
    [InlineData(nameof(ExecutionCapabilities.DrainsWhenLeftAtARow))]
    [InlineData(nameof(ExecutionCapabilities.ChecksRollbackAfterDatabaseError))]
    [InlineData(nameof(ExecutionCapabilities.FindsFailedItemByCompletion))]
    [InlineData(nameof(ExecutionCapabilities.ChecksResultSetsMatchItems))]
    [InlineData(nameof(ExecutionCapabilities.RestoresSessionIsolation))]
    public void Has_EachFlag_IsTrueOnlyWhenSet(string flagName)
    {
        var flag = Enum.Parse<ExecutionCapabilities>(flagName);
        var all =
            SqlServerReadsRows
            | ExecutionCapabilities.DrainsWhenLeftAtARow
            | ExecutionCapabilities.FindsFailedItemByCompletion
            | ExecutionCapabilities.ChecksResultSetsMatchItems
            | ExecutionCapabilities.RestoresSessionIsolation;

        flag.Has(flag).ShouldBeTrue();
        all.Has(flag).ShouldBeTrue();
        (all & ~flag).Has(flag).ShouldBeFalse();
        ExecutionCapabilities.None.Has(flag).ShouldBeFalse();
    }
}
