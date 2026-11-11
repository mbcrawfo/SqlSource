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

    // PostgreSQL runs one statement and needs none of SQL Server's behaviours.  SQL Server refuses a completed
    // transaction on every method, drains and checks for a rollback after its own exception and after a database error
    // on one that reads rows, and drains in a finally on one that streams.
    [Theory]
    [InlineData(nameof(DatabaseEngine.PostgreSql), nameof(ResultShape.None), nameof(CollectionKind.List), 0)]
    [InlineData(nameof(DatabaseEngine.PostgreSql), nameof(ResultShape.RowCount), nameof(CollectionKind.List), 0)]
    [InlineData(nameof(DatabaseEngine.PostgreSql), nameof(ResultShape.One), nameof(CollectionKind.List), 0)]
    [InlineData(nameof(DatabaseEngine.PostgreSql), nameof(ResultShape.OneOptional), nameof(CollectionKind.List), 0)]
    [InlineData(nameof(DatabaseEngine.PostgreSql), nameof(ResultShape.Many), nameof(CollectionKind.List), 0)]
    [InlineData(
        nameof(DatabaseEngine.PostgreSql),
        nameof(ResultShape.Many),
        nameof(CollectionKind.IAsyncEnumerable),
        0
    )]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(ResultShape.None), nameof(CollectionKind.List), 1)]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(ResultShape.RowCount), nameof(CollectionKind.List), 1)]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(ResultShape.One), nameof(CollectionKind.List), 23)]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(ResultShape.OneOptional), nameof(CollectionKind.List), 23)]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(ResultShape.Many), nameof(CollectionKind.List), 23)]
    [InlineData(
        nameof(DatabaseEngine.SqlServer),
        nameof(ResultShape.Many),
        nameof(CollectionKind.IAsyncEnumerable),
        31
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
    [InlineData(nameof(ResultShape.None), nameof(CollectionKind.List), 0)]
    [InlineData(nameof(ResultShape.RowCount), nameof(CollectionKind.List), 0)]
    [InlineData(nameof(ResultShape.One), nameof(CollectionKind.List), 2)]
    [InlineData(nameof(ResultShape.OneOptional), nameof(CollectionKind.List), 2)]
    [InlineData(nameof(ResultShape.Many), nameof(CollectionKind.List), 2)]
    [InlineData(nameof(ResultShape.Many), nameof(CollectionKind.IAsyncEnumerable), 10)]
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
    // which takes no transaction, only the second.  On PostgreSQL it finds the failed item by the first command that
    // did not complete, whatever its shape, and as a repository's method too.
    [Theory]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(ResultShape.Many), false, 85)]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(ResultShape.One), false, 85)]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(ResultShape.OneOptional), false, 85)]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(ResultShape.RowCount), false, 1)]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(ResultShape.None), false, 1)]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(ResultShape.Many), true, 64)]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(ResultShape.RowCount), true, 0)]
    [InlineData(nameof(DatabaseEngine.PostgreSql), nameof(ResultShape.Many), false, 32)]
    [InlineData(nameof(DatabaseEngine.PostgreSql), nameof(ResultShape.None), false, 32)]
    [InlineData(nameof(DatabaseEngine.PostgreSql), nameof(ResultShape.RowCount), false, 32)]
    [InlineData(nameof(DatabaseEngine.PostgreSql), nameof(ResultShape.One), true, 32)]
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

    // The values the theories above give as numbers.
    [Fact]
    public void Flags_AsNumbers_HaveTheValuesOfTheTheory()
    {
        ((int)ExecutionCapabilities.RefusesCompletedTransaction).ShouldBe(1);
        ((int)SqlServerReadsRows).ShouldBe(23);
        ((int)(SqlServerReadsRows | ExecutionCapabilities.DrainsWhenLeftAtARow)).ShouldBe(31);
        ((int)ExecutionCapabilities.DrainsLaterResults).ShouldBe(2);
        ((int)(ExecutionCapabilities.DrainsLaterResults | ExecutionCapabilities.DrainsWhenLeftAtARow)).ShouldBe(10);
        ((int)ExecutionCapabilities.FindsFailedItemByCompletion).ShouldBe(32);
        ((int)ExecutionCapabilities.ChecksResultSetsMatchItems).ShouldBe(64);
        (
            (int)(
                ExecutionCapabilities.RefusesCompletedTransaction
                | ExecutionCapabilities.ChecksRollbackAtRead
                | ExecutionCapabilities.ChecksRollbackAfterDatabaseError
                | ExecutionCapabilities.ChecksResultSetsMatchItems
            )
        ).ShouldBe(85);
    }

    [Theory]
    [InlineData(nameof(ExecutionCapabilities.RefusesCompletedTransaction))]
    [InlineData(nameof(ExecutionCapabilities.DrainsLaterResults))]
    [InlineData(nameof(ExecutionCapabilities.ChecksRollbackAtRead))]
    [InlineData(nameof(ExecutionCapabilities.DrainsWhenLeftAtARow))]
    [InlineData(nameof(ExecutionCapabilities.ChecksRollbackAfterDatabaseError))]
    [InlineData(nameof(ExecutionCapabilities.FindsFailedItemByCompletion))]
    [InlineData(nameof(ExecutionCapabilities.ChecksResultSetsMatchItems))]
    public void Has_EachFlag_IsTrueOnlyWhenSet(string flagName)
    {
        var flag = Enum.Parse<ExecutionCapabilities>(flagName);
        var all =
            SqlServerReadsRows
            | ExecutionCapabilities.DrainsWhenLeftAtARow
            | ExecutionCapabilities.FindsFailedItemByCompletion
            | ExecutionCapabilities.ChecksResultSetsMatchItems;

        flag.Has(flag).ShouldBeTrue();
        all.Has(flag).ShouldBeTrue();
        (all & ~flag).Has(flag).ShouldBeFalse();
        ExecutionCapabilities.None.Has(flag).ShouldBeFalse();
    }
}
