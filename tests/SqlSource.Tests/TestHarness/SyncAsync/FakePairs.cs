using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// The parameters a fake does not read give it the signature of the method the generator writes.
#pragma warning disable IDE0060

namespace SqlSource.Tests.TestHarness.SyncAsync;

// Hand-written method pairs shaped like the generator's, for the tests that find and call them by reflection.
internal static class FakePairs
{
    public sealed record Args(int Id);

    public static class Queries
    {
        public static int Get(
            DbConnection connection,
            Args parameters,
            DbTransaction? transaction = null,
            int? commandTimeout = null
        ) => parameters.Id + 1;

        public static int Get(
            DbConnection connection,
            IEnumerable<Args> parameters,
            DbTransaction? transaction = null,
            int? commandTimeout = null
        ) => parameters.Count();

        public static Task<int> GetAsync(
            DbConnection connection,
            Args parameters,
            DbTransaction? transaction = null,
            int? commandTimeout = null,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(parameters.Id + 1);

        public static Task<int> GetAsync(
            DbConnection connection,
            IEnumerable<Args> parameters,
            DbTransaction? transaction = null,
            int? commandTimeout = null,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(parameters.Count());

        public static void Fail(DbConnection connection, Args parameters) =>
            throw new InvalidOperationException("sync failure");

        public static async Task FailAsync(
            DbConnection connection,
            Args parameters,
            CancellationToken cancellationToken
        )
        {
            await Task.Yield();
            throw new InvalidOperationException("async failure");
        }

        // Has no asynchronous twin.
        public static void Orphan(DbConnection connection, Args parameters)
        {
            // Method intentionally left empty.
        }
    }

    public sealed class Repository(int answer)
    {
        public int Count(DbConnection connection, Args parameters) => answer;

        public Task<int> CountAsync(DbConnection connection, Args parameters, CancellationToken cancellationToken) =>
            Task.FromResult(answer);
    }
}

// Hand-written pairs of the connection they are given, for the tests of the runner: one runs a command naming whether
// it was given a transaction, one throws naming the connection's database, and one throws an exception of an item whose
// index differs between the two methods.
internal static class FakeConnectionPairs
{
    public static void Run(DbConnection connection, FakePairs.Args parameters, DbTransaction? transaction = null)
    {
        using var command = CommandOf(connection, transaction);
        _ = command.ExecuteNonQuery();
    }

    public static async Task RunAsync(
        DbConnection connection,
        FakePairs.Args parameters,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default
    )
    {
        await using var command = CommandOf(connection, transaction);
        _ = await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static void FailIn(DbConnection connection, FakePairs.Args parameters) =>
        throw new InvalidOperationException($"Failed in {connection.Database}.");

    public static Task FailInAsync(
        DbConnection connection,
        FakePairs.Args parameters,
        CancellationToken cancellationToken = default
    ) => Task.FromException(new InvalidOperationException($"Failed in {connection.Database}."));

    public static void FailAt(DbConnection connection, FakePairs.Args parameters) =>
        throw new ItemException(parameters.Id + 1);

    public static Task FailAtAsync(
        DbConnection connection,
        FakePairs.Args parameters,
        CancellationToken cancellationToken = default
    ) => Task.FromException(new ItemException(parameters.Id));

    private static DbCommand CommandOf(DbConnection connection, DbTransaction? transaction)
    {
        var command = connection.CreateCommand();
        if (transaction is null)
        {
            command.CommandText = "not given";
        }
        else
        {
            command.CommandText = "given";
        }

        command.Transaction = transaction;
        return command;
    }
}

// An exception of a collection method's item, which names the item by its ItemIndex as the generated methods' do.  Only
// the fakes throw it, with an item, so it has none of the usual constructors and stays internal.
#pragma warning disable CA1032, S3871
internal sealed class ItemException(int itemIndex) : Exception("An item failed.")
#pragma warning restore CA1032, S3871
{
    public int ItemIndex { get; } = itemIndex;
}
