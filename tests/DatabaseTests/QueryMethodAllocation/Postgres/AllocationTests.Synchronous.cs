using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Data.Common;
using System.Linq;
using Npgsql;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.QueryMethodAllocation.Postgres;

// The synchronous pairs: each query of the pairs of AllocationTests.cs that is not a stream, through its copy
// <Query>Sync in the same file, the same query under method-type=sync by its own marker, against a hand-written
// synchronous ADO.NET method of the same calls, Open, ExecuteReader, Read and Close for their asynchronous twins and
// using for await using, under the same margin, the same Least...Saving constants and the same rules for moving them.
// A synchronous call makes its calls on the thread that calls it, so CallAllocations counts that thread alone.
public sealed partial class AllocationTests
{
    [Fact]
    public void GetAccountNameSync_ThroughThePool_AllocatesNoMoreThanByHand()
    {
        using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        using var connection = source.CreateConnection();
        Allocations.InState(connection, State);
        var parameters = new GetAccountNameArgs(1);
        connection.GetAccountNameSync(parameters).ShouldBe([new AccountName(1, "alice")]);
        GetAccountNamePooledByHand(connection, 1).ShouldBe([new AccountName(1, "alice")]);

        Allocations.ShouldAllocateNoMoreThanByHand(
            () => connection.GetAccountNameSync(parameters),
            () => GetAccountNamePooledByHand(connection, 1),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Fact]
    public void GetAccountNameSync_UnderArrayPoolOff_AllocatesNoMoreThanByHand()
    {
        using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        using var connection = source.CreateConnection();
        Allocations.InState(connection, State);
        var parameters = new GetAccountNameArgs(1);
        connection.GetAccountNameUnpooledSync(parameters).ShouldBe([new AccountName(1, "alice")]);
        GetAccountNameUnpooledByHand(connection, 1).ShouldBe([new AccountName(1, "alice")]);

        Allocations.ShouldAllocateNoMoreThanByHand(
            () => connection.GetAccountNameUnpooledSync(parameters),
            () => GetAccountNameUnpooledByHand(connection, 1),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Fact]
    public void ListAccountsSync_ThroughThePool_AllocatesNoMoreThanByHand()
    {
        using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        using var connection = source.CreateConnection();
        Allocations.InState(connection, State);
        var accounts = connection.ListAccountsSync();
        accounts.Count.ShouldBe(3);
        ListAccountsPooledByHand(connection).ShouldBe(accounts);

        Allocations.ShouldAllocateNoMoreThanByHand(
            () => connection.ListAccountsSync(),
            () => ListAccountsPooledByHand(connection),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Fact]
    public void ListAccountsSync_UnderArrayPoolOff_AllocatesNoMoreThanByHand()
    {
        using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        using var connection = source.CreateConnection();
        Allocations.InState(connection, State);
        var accounts = connection.ListAccountsUnpooledSync();
        accounts.Count.ShouldBe(3);
        ListAccountsUnpooledByHand(connection).ShouldBe(accounts);

        Allocations.ShouldAllocateNoMoreThanByHand(
            () => connection.ListAccountsUnpooledSync(),
            () => ListAccountsUnpooledByHand(connection),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Fact]
    public void ListClosedBeforeSync_ThroughThePool_AllocatesNoMoreThanByHand()
    {
        using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        using var connection = source.CreateConnection();
        Allocations.InState(connection, State);
        var parameters = new ListClosedBeforeArgs(null);
        connection.ListClosedBeforeSync(parameters).ShouldBe(ClosedBeforeRows);
        ListClosedBeforePooledByHand(connection, null).ShouldBe(ClosedBeforeRows);

        Allocations.ShouldAllocateNoMoreThanByHand(
            () => connection.ListClosedBeforeSync(parameters),
            () => ListClosedBeforePooledByHand(connection, null),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Fact]
    public void ListClosedBeforeSync_UnderArrayPoolOff_AllocatesNoMoreThanByHand()
    {
        using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        using var connection = source.CreateConnection();
        Allocations.InState(connection, State);
        var parameters = new ListClosedBeforeArgs(null);
        connection.ListClosedBeforeUnpooledSync(parameters).ShouldBe(ClosedBeforeRows);
        ListClosedBeforeUnpooledByHand(connection, null).ShouldBe(ClosedBeforeRows);

        Allocations.ShouldAllocateNoMoreThanByHand(
            () => connection.ListClosedBeforeUnpooledSync(parameters),
            () => ListClosedBeforeUnpooledByHand(connection, null),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Fact]
    public void NumbersAsListSync_ThreeRowsThroughThePool_AllocatesNoMoreThanByHand()
    {
        using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        using var connection = source.CreateConnection();
        Allocations.InState(connection, State);
        var parameters = new NumbersAsListArgs(CountedRows);
        connection.NumbersAsListSync(parameters).ShouldBe(CountedNumbers);
        NumbersAsListPooledByHand(connection, CountedRows).ShouldBe(CountedNumbers);

        Allocations.ShouldAllocateNoMoreThanByHand(
            () => connection.NumbersAsListSync(parameters),
            () => NumbersAsListPooledByHand(connection, CountedRows),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Fact]
    public void NumbersAsIListSync_ThreeRowsThroughThePool_AllocatesNoMoreThanByHand()
    {
        using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        using var connection = source.CreateConnection();
        Allocations.InState(connection, State);
        var parameters = new NumbersAsIListArgs(CountedRows);
        connection.NumbersAsIListSync(parameters).ShouldBe(CountedNumbers);
        NumbersAsListPooledByHand(connection, CountedRows).ShouldBe(CountedNumbers);

        Allocations.ShouldAllocateNoMoreThanByHand(
            () => connection.NumbersAsIListSync(parameters),
            () => NumbersAsListPooledByHand(connection, CountedRows),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Fact]
    public void NumbersAsICollectionSync_ThreeRowsThroughThePool_AllocatesNoMoreThanByHand()
    {
        using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        using var connection = source.CreateConnection();
        Allocations.InState(connection, State);
        var parameters = new NumbersAsICollectionArgs(CountedRows);
        connection.NumbersAsICollectionSync(parameters).ShouldBe(CountedNumbers);
        NumbersAsListPooledByHand(connection, CountedRows).ShouldBe(CountedNumbers);

        Allocations.ShouldAllocateNoMoreThanByHand(
            () => connection.NumbersAsICollectionSync(parameters),
            () => NumbersAsListPooledByHand(connection, CountedRows),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Fact]
    public void NumbersAsImmutableArraySync_ThreeRowsThroughThePool_AllocatesNoMoreThanByHand()
    {
        using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        using var connection = source.CreateConnection();
        Allocations.InState(connection, State);
        var parameters = new NumbersAsImmutableArrayArgs(CountedRows);
        connection.NumbersAsImmutableArraySync(parameters).ShouldBe(CountedNumbers);
        NumbersAsImmutableArrayPooledByHand(connection, CountedRows).ShouldBe(CountedNumbers);

        Allocations.ShouldAllocateNoMoreThanByHand(
            () => connection.NumbersAsImmutableArraySync(parameters),
            () => NumbersAsImmutableArrayPooledByHand(connection, CountedRows),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Fact]
    public void NumbersNoPoolAsListSync_ThreeRows_AllocatesNoMoreThanByHand()
    {
        using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        using var connection = source.CreateConnection();
        Allocations.InState(connection, State);
        var parameters = new NumbersNoPoolAsListArgs(CountedRows);
        connection.NumbersNoPoolAsListSync(parameters).ShouldBe(CountedNumbers);
        NumbersAsListUnpooledByHand(connection, CountedRows).ShouldBe(CountedNumbers);

        Allocations.ShouldAllocateNoMoreThanByHand(
            () => connection.NumbersNoPoolAsListSync(parameters),
            () => NumbersAsListUnpooledByHand(connection, CountedRows),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Fact]
    public void NumbersNoPoolAsIListSync_ThreeRows_AllocatesNoMoreThanByHand()
    {
        using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        using var connection = source.CreateConnection();
        Allocations.InState(connection, State);
        var parameters = new NumbersNoPoolAsIListArgs(CountedRows);
        connection.NumbersNoPoolAsIListSync(parameters).ShouldBe(CountedNumbers);
        NumbersAsListUnpooledByHand(connection, CountedRows).ShouldBe(CountedNumbers);

        Allocations.ShouldAllocateNoMoreThanByHand(
            () => connection.NumbersNoPoolAsIListSync(parameters),
            () => NumbersAsListUnpooledByHand(connection, CountedRows),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Fact]
    public void NumbersNoPoolAsICollectionSync_ThreeRows_AllocatesNoMoreThanByHand()
    {
        using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        using var connection = source.CreateConnection();
        Allocations.InState(connection, State);
        var parameters = new NumbersNoPoolAsICollectionArgs(CountedRows);
        connection.NumbersNoPoolAsICollectionSync(parameters).ShouldBe(CountedNumbers);
        NumbersAsListUnpooledByHand(connection, CountedRows).ShouldBe(CountedNumbers);

        Allocations.ShouldAllocateNoMoreThanByHand(
            () => connection.NumbersNoPoolAsICollectionSync(parameters),
            () => NumbersAsListUnpooledByHand(connection, CountedRows),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Fact]
    public void NumbersNoPoolAsImmutableArraySync_ThreeRows_AllocatesNoMoreThanByHand()
    {
        using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        using var connection = source.CreateConnection();
        Allocations.InState(connection, State);
        var parameters = new NumbersNoPoolAsImmutableArrayArgs(CountedRows);
        connection.NumbersNoPoolAsImmutableArraySync(parameters).ShouldBe(CountedNumbers);
        NumbersAsImmutableArrayUnpooledByHand(connection, CountedRows).ShouldBe(CountedNumbers);

        Allocations.ShouldAllocateNoMoreThanByHand(
            () => connection.NumbersNoPoolAsImmutableArraySync(parameters),
            () => NumbersAsImmutableArrayUnpooledByHand(connection, CountedRows),
            MarginInBytesPerCall,
            Settings
        );
    }

    // The few-hundred-rows tests, each against the hand-written baseline of its asynchronous twin's test.
    [Fact]
    public void NumbersAsArraySync_AFewHundredRows_AllocatesLessThanAListCopiedToAnArray()
    {
        using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        using var connection = source.CreateConnection();
        Allocations.InState(connection, State);
        var parameters = new NumbersAsArrayArgs(FewHundredRows);
        var expected = Enumerable.Range(1, FewHundredRows).Select(n => new CountedNumber(n)).ToArray();
        connection.NumbersAsArraySync(parameters).ShouldBe(expected);
        NumbersAsArrayUnpooledByHand(connection, FewHundredRows).ShouldBe(expected);

        Allocations.ShouldSaveAtLeast(
            () => connection.NumbersAsArraySync(parameters),
            () => NumbersAsArrayUnpooledByHand(connection, FewHundredRows),
            LeastSavingInBytesPerCall,
            Settings
        );
    }

    [Fact]
    public void NumbersAsListSync_AFewHundredRows_AllocatesLessThanAGrownList()
    {
        using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        using var connection = source.CreateConnection();
        Allocations.InState(connection, State);
        var parameters = new NumbersAsListArgs(FewHundredRows);
        var expected = Enumerable.Range(1, FewHundredRows).Select(n => new CountedNumber(n)).ToArray();
        connection.NumbersAsListSync(parameters).ShouldBe(expected);
        NumbersAsListUnpooledByHand(connection, FewHundredRows).ShouldBe(expected);

        Allocations.ShouldSaveAtLeast(
            () => connection.NumbersAsListSync(parameters),
            () => NumbersAsListUnpooledByHand(connection, FewHundredRows),
            LeastListSavingInBytesPerCall,
            Settings
        );
    }

    [Fact]
    public void NumbersAsImmutableArraySync_AFewHundredRows_AllocatesLessThanABuilder()
    {
        using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        using var connection = source.CreateConnection();
        Allocations.InState(connection, State);
        var parameters = new NumbersAsImmutableArrayArgs(FewHundredRows);
        var expected = Enumerable.Range(1, FewHundredRows).Select(n => new CountedNumber(n)).ToArray();
        connection.NumbersAsImmutableArraySync(parameters).ShouldBe(expected);
        NumbersAsImmutableArrayUnpooledByHand(connection, FewHundredRows).ShouldBe(expected);

        Allocations.ShouldSaveAtLeast(
            () => connection.NumbersAsImmutableArraySync(parameters),
            () => NumbersAsImmutableArrayUnpooledByHand(connection, FewHundredRows),
            LeastImmutableArraySavingInBytesPerCall,
            Settings
        );
    }

    // GetAccountNameSync of Queries/Accounts.sql, by hand: the rows into RowBufferByHand.
    private static AccountName[] GetAccountNamePooledByHand(
        DbConnection connection,
        int accountId,
        DbTransaction? transaction = null,
        int? commandTimeout = null
    )
    {
        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = GetAccountNameSql;
            command.Transaction = transaction;
            if (commandTimeout.HasValue)
            {
                command.CommandTimeout = commandTimeout.Value;
            }

            _ = command.Parameters.Add(
                new NpgsqlParameter<int>
                {
                    ParameterName = "accountId",
                    DataTypeName = "pg_catalog.int4",
                    TypedValue = accountId,
                }
            );

            using var reader = command.ExecuteReader(CommandBehavior.Default);
            var rows = new RowBufferByHand<AccountName>();
            while (reader.Read())
            {
                rows.Add(new AccountName(reader.GetFieldValue<int>(0), reader.GetFieldValue<string>(1)));
            }

            return rows.ToArray();
        }
        finally
        {
            if (wasClosed)
            {
                connection.Close();
            }
        }
    }

    // GetAccountNameUnpooledSync of Queries/Accounts.sql, by hand: the rows into a List, copied to an array.
    private static AccountName[] GetAccountNameUnpooledByHand(
        DbConnection connection,
        int accountId,
        DbTransaction? transaction = null,
        int? commandTimeout = null
    )
    {
        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = GetAccountNameSql;
            command.Transaction = transaction;
            if (commandTimeout.HasValue)
            {
                command.CommandTimeout = commandTimeout.Value;
            }

            _ = command.Parameters.Add(
                new NpgsqlParameter<int>
                {
                    ParameterName = "accountId",
                    DataTypeName = "pg_catalog.int4",
                    TypedValue = accountId,
                }
            );

            using var reader = command.ExecuteReader(CommandBehavior.Default);
            var rows = new List<AccountName>();
            while (reader.Read())
            {
                rows.Add(new AccountName(reader.GetFieldValue<int>(0), reader.GetFieldValue<string>(1)));
            }

            return [.. rows];
        }
        finally
        {
            if (wasClosed)
            {
                connection.Close();
            }
        }
    }

    // ListAccountsSync of Queries/Accounts.sql, by hand, the rows into RowBufferByHand: closed_at and name_length may
    // be NULL.
    private static AccountsDto[] ListAccountsPooledByHand(
        DbConnection connection,
        DbTransaction? transaction = null,
        int? commandTimeout = null
    )
    {
        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = ListAccountsSql;
            command.Transaction = transaction;
            if (commandTimeout.HasValue)
            {
                command.CommandTimeout = commandTimeout.Value;
            }

            using var reader = command.ExecuteReader(CommandBehavior.Default);
            var rows = new RowBufferByHand<AccountsDto>();
            while (reader.Read())
            {
                rows.Add(
                    new AccountsDto(
                        reader.GetFieldValue<int>(0),
                        reader.GetFieldValue<string>(1),
                        reader.GetFieldValue<string>(2),
                        reader.GetFieldValue<string>(3),
                        reader.GetFieldValue<DateTimeOffset>(4),
                        reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
                        reader.IsDBNull(6) ? null : reader.GetFieldValue<int>(6)
                    )
                );
            }

            return rows.ToArray();
        }
        finally
        {
            if (wasClosed)
            {
                connection.Close();
            }
        }
    }

    // ListAccountsUnpooledSync of Queries/Accounts.sql, by hand: closed_at and name_length may be NULL.
    private static AccountsDto[] ListAccountsUnpooledByHand(
        DbConnection connection,
        DbTransaction? transaction = null,
        int? commandTimeout = null
    )
    {
        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = ListAccountsSql;
            command.Transaction = transaction;
            if (commandTimeout.HasValue)
            {
                command.CommandTimeout = commandTimeout.Value;
            }

            using var reader = command.ExecuteReader(CommandBehavior.Default);
            var rows = new List<AccountsDto>();
            while (reader.Read())
            {
                rows.Add(
                    new AccountsDto(
                        reader.GetFieldValue<int>(0),
                        reader.GetFieldValue<string>(1),
                        reader.GetFieldValue<string>(2),
                        reader.GetFieldValue<string>(3),
                        reader.GetFieldValue<DateTimeOffset>(4),
                        reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
                        reader.IsDBNull(6) ? null : reader.GetFieldValue<int>(6)
                    )
                );
            }

            return [.. rows];
        }
        finally
        {
            if (wasClosed)
            {
                connection.Close();
            }
        }
    }

    // ListClosedBeforeSync of Queries/Accounts.sql, by hand, the rows into RowBufferByHand: a null date is the
    // parameter's own null, and closed_at may be NULL.
    private static ListClosedBeforeDto[] ListClosedBeforePooledByHand(
        DbConnection connection,
        DateTimeOffset? closedBefore,
        DbTransaction? transaction = null,
        int? commandTimeout = null
    )
    {
        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = ListClosedBeforeSql;
            command.Transaction = transaction;
            if (commandTimeout.HasValue)
            {
                command.CommandTimeout = commandTimeout.Value;
            }

            _ = command.Parameters.Add(
                new NpgsqlParameter<DateTimeOffset?>
                {
                    ParameterName = "closedBefore",
                    DataTypeName = "pg_catalog.timestamptz",
                    TypedValue = closedBefore,
                }
            );

            using var reader = command.ExecuteReader(CommandBehavior.Default);
            var rows = new RowBufferByHand<ListClosedBeforeDto>();
            while (reader.Read())
            {
                rows.Add(
                    new ListClosedBeforeDto(
                        reader.GetFieldValue<int>(0),
                        reader.GetFieldValue<string>(1),
                        reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2)
                    )
                );
            }

            return rows.ToArray();
        }
        finally
        {
            if (wasClosed)
            {
                connection.Close();
            }
        }
    }

    // ListClosedBeforeUnpooledSync of Queries/Accounts.sql, by hand: a null date is the parameter's own null, and
    // closed_at may be NULL.
    private static ListClosedBeforeDto[] ListClosedBeforeUnpooledByHand(
        DbConnection connection,
        DateTimeOffset? closedBefore,
        DbTransaction? transaction = null,
        int? commandTimeout = null
    )
    {
        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = ListClosedBeforeSql;
            command.Transaction = transaction;
            if (commandTimeout.HasValue)
            {
                command.CommandTimeout = commandTimeout.Value;
            }

            _ = command.Parameters.Add(
                new NpgsqlParameter<DateTimeOffset?>
                {
                    ParameterName = "closedBefore",
                    DataTypeName = "pg_catalog.timestamptz",
                    TypedValue = closedBefore,
                }
            );

            using var reader = command.ExecuteReader(CommandBehavior.Default);
            var rows = new List<ListClosedBeforeDto>();
            while (reader.Read())
            {
                rows.Add(
                    new ListClosedBeforeDto(
                        reader.GetFieldValue<int>(0),
                        reader.GetFieldValue<string>(1),
                        reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2)
                    )
                );
            }

            return [.. rows];
        }
        finally
        {
            if (wasClosed)
            {
                connection.Close();
            }
        }
    }

    // NumbersAsListSync of Queries/RowBuffers.sql, by hand, the rows into RowBufferByHand and copied to a List, as
    // NumbersAsIListSync and NumbersAsICollectionSync read them too.
    private static List<CountedNumber> NumbersAsListPooledByHand(
        DbConnection connection,
        int count,
        DbTransaction? transaction = null,
        int? commandTimeout = null
    )
    {
        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = NumbersSql;
            command.Transaction = transaction;
            if (commandTimeout.HasValue)
            {
                command.CommandTimeout = commandTimeout.Value;
            }

            _ = command.Parameters.Add(
                new NpgsqlParameter<int>
                {
                    ParameterName = "count",
                    DataTypeName = "pg_catalog.int4",
                    TypedValue = count,
                }
            );

            using var reader = command.ExecuteReader(CommandBehavior.Default);
            var rows = new RowBufferByHand<CountedNumber>();
            while (reader.Read())
            {
                rows.Add(new CountedNumber(reader.IsDBNull(0) ? null : reader.GetFieldValue<int>(0)));
            }

            return rows.ToList();
        }
        finally
        {
            if (wasClosed)
            {
                connection.Close();
            }
        }
    }

    // NumbersAsImmutableArraySync of Queries/RowBuffers.sql, by hand, the rows into RowBufferByHand and copied to an
    // ImmutableArray.
    private static ImmutableArray<CountedNumber> NumbersAsImmutableArrayPooledByHand(
        DbConnection connection,
        int count,
        DbTransaction? transaction = null,
        int? commandTimeout = null
    )
    {
        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = NumbersSql;
            command.Transaction = transaction;
            if (commandTimeout.HasValue)
            {
                command.CommandTimeout = commandTimeout.Value;
            }

            _ = command.Parameters.Add(
                new NpgsqlParameter<int>
                {
                    ParameterName = "count",
                    DataTypeName = "pg_catalog.int4",
                    TypedValue = count,
                }
            );

            using var reader = command.ExecuteReader(CommandBehavior.Default);
            var rows = new RowBufferByHand<CountedNumber>();
            while (reader.Read())
            {
                rows.Add(new CountedNumber(reader.IsDBNull(0) ? null : reader.GetFieldValue<int>(0)));
            }

            return rows.ToImmutableArray();
        }
        finally
        {
            if (wasClosed)
            {
                connection.Close();
            }
        }
    }

    // NumbersNoPoolAsListSync of Queries/RowBuffers.sql under array-pool=off, by hand, the rows into a List that is
    // returned, as NumbersNoPoolAsIListSync and NumbersNoPoolAsICollectionSync read them too, and the baseline of a
    // List grown by doubling that the pooled NumbersAsListSync is held against for a few hundred rows.
    private static List<CountedNumber> NumbersAsListUnpooledByHand(
        DbConnection connection,
        int count,
        DbTransaction? transaction = null,
        int? commandTimeout = null
    )
    {
        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = NumbersSql;
            command.Transaction = transaction;
            if (commandTimeout.HasValue)
            {
                command.CommandTimeout = commandTimeout.Value;
            }

            _ = command.Parameters.Add(
                new NpgsqlParameter<int>
                {
                    ParameterName = "count",
                    DataTypeName = "pg_catalog.int4",
                    TypedValue = count,
                }
            );

            using var reader = command.ExecuteReader(CommandBehavior.Default);
            var rows = new List<CountedNumber>();
            while (reader.Read())
            {
                rows.Add(new CountedNumber(reader.IsDBNull(0) ? null : reader.GetFieldValue<int>(0)));
            }

            return rows;
        }
        finally
        {
            if (wasClosed)
            {
                connection.Close();
            }
        }
    }

    // NumbersNoPoolAsImmutableArraySync of Queries/RowBuffers.sql under array-pool=off, by hand, the rows into a
    // builder that is drained.
    private static ImmutableArray<CountedNumber> NumbersAsImmutableArrayUnpooledByHand(
        DbConnection connection,
        int count,
        DbTransaction? transaction = null,
        int? commandTimeout = null
    )
    {
        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = NumbersSql;
            command.Transaction = transaction;
            if (commandTimeout.HasValue)
            {
                command.CommandTimeout = commandTimeout.Value;
            }

            _ = command.Parameters.Add(
                new NpgsqlParameter<int>
                {
                    ParameterName = "count",
                    DataTypeName = "pg_catalog.int4",
                    TypedValue = count,
                }
            );

            using var reader = command.ExecuteReader(CommandBehavior.Default);
            var rows = ImmutableArray.CreateBuilder<CountedNumber>();
            while (reader.Read())
            {
                rows.Add(new CountedNumber(reader.IsDBNull(0) ? null : reader.GetFieldValue<int>(0)));
            }

            return rows.DrainToImmutable();
        }
        finally
        {
            if (wasClosed)
            {
                connection.Close();
            }
        }
    }

    // NumbersAsArraySync of Queries/RowBuffers.sql, by hand, the rows into a List, copied to an array: the baseline
    // of List and ToArray() that the pooled NumbersAsArraySync is held against for a few hundred rows.
    private static CountedNumber[] NumbersAsArrayUnpooledByHand(
        DbConnection connection,
        int count,
        DbTransaction? transaction = null,
        int? commandTimeout = null
    )
    {
        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = NumbersSql;
            command.Transaction = transaction;
            if (commandTimeout.HasValue)
            {
                command.CommandTimeout = commandTimeout.Value;
            }

            _ = command.Parameters.Add(
                new NpgsqlParameter<int>
                {
                    ParameterName = "count",
                    DataTypeName = "pg_catalog.int4",
                    TypedValue = count,
                }
            );

            using var reader = command.ExecuteReader(CommandBehavior.Default);
            var rows = new List<CountedNumber>();
            while (reader.Read())
            {
                rows.Add(new CountedNumber(reader.IsDBNull(0) ? null : reader.GetFieldValue<int>(0)));
            }

            return [.. rows];
        }
        finally
        {
            if (wasClosed)
            {
                connection.Close();
            }
        }
    }
}
