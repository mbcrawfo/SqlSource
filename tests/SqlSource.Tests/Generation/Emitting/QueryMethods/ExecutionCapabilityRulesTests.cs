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

    // A collection method on SQL Server refuses the caller's completed transaction and nothing else: each result set of
    // its batch is an item's, so it drains none, and it never streams.  On PostgreSQL, or as a repository's method,
    // which takes no transaction, it has no flag.
    [Theory]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(ResultShape.Many), false, 1)]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(ResultShape.One), false, 1)]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(ResultShape.RowCount), false, 1)]
    [InlineData(nameof(DatabaseEngine.SqlServer), nameof(ResultShape.Many), true, 0)]
    [InlineData(nameof(DatabaseEngine.PostgreSql), nameof(ResultShape.Many), false, 0)]
    [InlineData(nameof(DatabaseEngine.PostgreSql), nameof(ResultShape.None), false, 0)]
    public void Of_CollectionMethod_RefusesOnlyACompletedTransactionOnSqlServer(
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
    }

    [Theory]
    [InlineData(nameof(ExecutionCapabilities.RefusesCompletedTransaction))]
    [InlineData(nameof(ExecutionCapabilities.DrainsLaterResults))]
    [InlineData(nameof(ExecutionCapabilities.ChecksRollbackAtRead))]
    [InlineData(nameof(ExecutionCapabilities.DrainsWhenLeftAtARow))]
    [InlineData(nameof(ExecutionCapabilities.ChecksRollbackAfterDatabaseError))]
    public void Has_EachFlag_IsTrueOnlyWhenSet(string flagName)
    {
        var flag = Enum.Parse<ExecutionCapabilities>(flagName);
        var all = SqlServerReadsRows | ExecutionCapabilities.DrainsWhenLeftAtARow;

        flag.Has(flag).ShouldBeTrue();
        all.Has(flag).ShouldBeTrue();
        (all & ~flag).Has(flag).ShouldBeFalse();
        ExecutionCapabilities.None.Has(flag).ShouldBeFalse();
    }
}
