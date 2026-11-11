using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Data.Common;
using System.Linq;
using Microsoft.Data.SqlClient;
using NodaTime;
using Shouldly;
using SqlSource.DatabaseTests.SqlServer.EndToEnd.Shapes;
using Xunit;

namespace SqlSource.DatabaseTests.QueryMethodAllocation.SqlServer;

// The synchronous pairs: each query of the pairs of AllocationTests.cs that is not a stream, through its copy
// <Query>Sync in the same file, the same query under method-type=sync by its own marker, against a hand-written
// synchronous ADO.NET method of the same calls, Open, ExecuteReader, Read, NextResult and ExecuteNonQuery for their
// asynchronous twins and using and Dispose for await using and DisposeAsync, on both connections, under the same
// margin, the same Least...Saving constants and the same rules for moving them.  A synchronous call makes its calls on
// the thread that calls it, so CallAllocations counts that thread alone.
public sealed partial class AllocationTests
{
    [Theory]
    [InlineData(ConnectionState.Closed)]
    [InlineData(ConnectionState.Open)]
    public void SearchAccountsSync_ThroughThePool_AllocatesNoMoreThanByHand(ConnectionState state)
    {
        using var connection = new SqlConnection(sqlServer.ConnectionString);
        Allocations.InState(connection, state);

        var generatedRows = ShapeQueries.SearchAccountsSync(connection, Filter);
        generatedRows.ShouldBe(Rows);
        SearchAccountsPooledByHand(connection, Filter).ShouldBe(generatedRows);

        Allocations.ShouldAllocateNoMoreThanByHand(
            () => ShapeQueries.SearchAccountsSync(connection, Filter),
            () => SearchAccountsPooledByHand(connection, Filter),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Theory]
    [InlineData(ConnectionState.Closed)]
    [InlineData(ConnectionState.Open)]
    public void SearchAccountsUnpooledSync_UnderArrayPoolOff_AllocatesNoMoreThanByHand(ConnectionState state)
    {
        using var connection = new SqlConnection(sqlServer.ConnectionString);
        Allocations.InState(connection, state);

        var generatedRows = ShapeQueries.SearchAccountsUnpooledSync(connection, Filter);
        generatedRows.ShouldBe(Rows);
        SearchAccountsUnpooledByHand(connection, Filter).ShouldBe(generatedRows);

        Allocations.ShouldAllocateNoMoreThanByHand(
            () => ShapeQueries.SearchAccountsUnpooledSync(connection, Filter),
            () => SearchAccountsUnpooledByHand(connection, Filter),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Theory]
    [InlineData(ConnectionState.Closed)]
    [InlineData(ConnectionState.Open)]
    public void NumbersAsListSync_ThreeRowsThroughThePool_AllocatesNoMoreThanByHand(ConnectionState state)
    {
        using var connection = new SqlConnection(sqlServer.ConnectionString);
        Allocations.InState(connection, state);

        var parameters = new NumbersCount(CountedRows);
        connection.NumbersAsListSync(parameters).ShouldBe(CountedNumbers);
        NumbersAsListPooledByHand(connection, parameters).ShouldBe(CountedNumbers);

        Allocations.ShouldAllocateNoMoreThanByHand(
            () => connection.NumbersAsListSync(parameters),
            () => NumbersAsListPooledByHand(connection, parameters),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Theory]
    [InlineData(ConnectionState.Closed)]
    [InlineData(ConnectionState.Open)]
    public void NumbersAsIListSync_ThreeRowsThroughThePool_AllocatesNoMoreThanByHand(ConnectionState state)
    {
        using var connection = new SqlConnection(sqlServer.ConnectionString);
        Allocations.InState(connection, state);

        var parameters = new NumbersCount(CountedRows);
        connection.NumbersAsIListSync(parameters).ShouldBe(CountedNumbers);
        NumbersAsListPooledByHand(connection, parameters).ShouldBe(CountedNumbers);

        Allocations.ShouldAllocateNoMoreThanByHand(
            () => connection.NumbersAsIListSync(parameters),
            () => NumbersAsListPooledByHand(connection, parameters),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Theory]
    [InlineData(ConnectionState.Closed)]
    [InlineData(ConnectionState.Open)]
    public void NumbersAsICollectionSync_ThreeRowsThroughThePool_AllocatesNoMoreThanByHand(ConnectionState state)
    {
        using var connection = new SqlConnection(sqlServer.ConnectionString);
        Allocations.InState(connection, state);

        var parameters = new NumbersCount(CountedRows);
        connection.NumbersAsICollectionSync(parameters).ShouldBe(CountedNumbers);
        NumbersAsListPooledByHand(connection, parameters).ShouldBe(CountedNumbers);

        Allocations.ShouldAllocateNoMoreThanByHand(
            () => connection.NumbersAsICollectionSync(parameters),
            () => NumbersAsListPooledByHand(connection, parameters),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Theory]
    [InlineData(ConnectionState.Closed)]
    [InlineData(ConnectionState.Open)]
    public void NumbersAsImmutableArraySync_ThreeRowsThroughThePool_AllocatesNoMoreThanByHand(ConnectionState state)
    {
        using var connection = new SqlConnection(sqlServer.ConnectionString);
        Allocations.InState(connection, state);

        var parameters = new NumbersCount(CountedRows);
        connection.NumbersAsImmutableArraySync(parameters).ShouldBe(CountedNumbers);
        NumbersAsImmutableArrayPooledByHand(connection, parameters).ShouldBe(CountedNumbers);

        Allocations.ShouldAllocateNoMoreThanByHand(
            () => connection.NumbersAsImmutableArraySync(parameters),
            () => NumbersAsImmutableArrayPooledByHand(connection, parameters),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Theory]
    [InlineData(ConnectionState.Closed)]
    [InlineData(ConnectionState.Open)]
    public void NumbersNoPoolAsListSync_ThreeRows_AllocatesNoMoreThanByHand(ConnectionState state)
    {
        using var connection = new SqlConnection(sqlServer.ConnectionString);
        Allocations.InState(connection, state);

        var parameters = new NumbersCount(CountedRows);
        connection.NumbersNoPoolAsListSync(parameters).ShouldBe(CountedNumbers);
        NumbersAsListUnpooledByHand(connection, parameters).ShouldBe(CountedNumbers);

        Allocations.ShouldAllocateNoMoreThanByHand(
            () => connection.NumbersNoPoolAsListSync(parameters),
            () => NumbersAsListUnpooledByHand(connection, parameters),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Theory]
    [InlineData(ConnectionState.Closed)]
    [InlineData(ConnectionState.Open)]
    public void NumbersNoPoolAsIListSync_ThreeRows_AllocatesNoMoreThanByHand(ConnectionState state)
    {
        using var connection = new SqlConnection(sqlServer.ConnectionString);
        Allocations.InState(connection, state);

        var parameters = new NumbersCount(CountedRows);
        connection.NumbersNoPoolAsIListSync(parameters).ShouldBe(CountedNumbers);
        NumbersAsListUnpooledByHand(connection, parameters).ShouldBe(CountedNumbers);

        Allocations.ShouldAllocateNoMoreThanByHand(
            () => connection.NumbersNoPoolAsIListSync(parameters),
            () => NumbersAsListUnpooledByHand(connection, parameters),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Theory]
    [InlineData(ConnectionState.Closed)]
    [InlineData(ConnectionState.Open)]
    public void NumbersNoPoolAsICollectionSync_ThreeRows_AllocatesNoMoreThanByHand(ConnectionState state)
    {
        using var connection = new SqlConnection(sqlServer.ConnectionString);
        Allocations.InState(connection, state);

        var parameters = new NumbersCount(CountedRows);
        connection.NumbersNoPoolAsICollectionSync(parameters).ShouldBe(CountedNumbers);
        NumbersAsListUnpooledByHand(connection, parameters).ShouldBe(CountedNumbers);

        Allocations.ShouldAllocateNoMoreThanByHand(
            () => connection.NumbersNoPoolAsICollectionSync(parameters),
            () => NumbersAsListUnpooledByHand(connection, parameters),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Theory]
    [InlineData(ConnectionState.Closed)]
    [InlineData(ConnectionState.Open)]
    public void NumbersNoPoolAsImmutableArraySync_ThreeRows_AllocatesNoMoreThanByHand(ConnectionState state)
    {
        using var connection = new SqlConnection(sqlServer.ConnectionString);
        Allocations.InState(connection, state);

        var parameters = new NumbersCount(CountedRows);
        connection.NumbersNoPoolAsImmutableArraySync(parameters).ShouldBe(CountedNumbers);
        NumbersAsImmutableArrayUnpooledByHand(connection, parameters).ShouldBe(CountedNumbers);

        Allocations.ShouldAllocateNoMoreThanByHand(
            () => connection.NumbersNoPoolAsImmutableArraySync(parameters),
            () => NumbersAsImmutableArrayUnpooledByHand(connection, parameters),
            MarginInBytesPerCall,
            Settings
        );
    }

    // The few-hundred-rows tests, each against the hand-written baseline of its asynchronous twin's test.
    [Theory]
    [InlineData(ConnectionState.Closed)]
    [InlineData(ConnectionState.Open)]
    public void NumbersAsArraySync_AFewHundredRows_AllocatesLessThanAListCopiedToAnArray(ConnectionState state)
    {
        using var connection = new SqlConnection(sqlServer.ConnectionString);
        Allocations.InState(connection, state);

        var parameters = new NumbersCount(FewHundredRows);
        var expected = Enumerable.Range(1, FewHundredRows).Select(n => new CountedNumber(n)).ToArray();
        connection.NumbersAsArraySync(parameters).ShouldBe(expected);
        NumbersAsArrayUnpooledByHand(connection, parameters).ShouldBe(expected);

        Allocations.ShouldSaveAtLeast(
            () => connection.NumbersAsArraySync(parameters),
            () => NumbersAsArrayUnpooledByHand(connection, parameters),
            LeastSavingInBytesPerCall,
            Settings
        );
    }

    [Theory]
    [InlineData(ConnectionState.Closed)]
    [InlineData(ConnectionState.Open)]
    public void NumbersAsListSync_AFewHundredRows_AllocatesLessThanAGrownList(ConnectionState state)
    {
        using var connection = new SqlConnection(sqlServer.ConnectionString);
        Allocations.InState(connection, state);

        var parameters = new NumbersCount(FewHundredRows);
        var expected = Enumerable.Range(1, FewHundredRows).Select(n => new CountedNumber(n)).ToArray();
        connection.NumbersAsListSync(parameters).ShouldBe(expected);
        NumbersAsListUnpooledByHand(connection, parameters).ShouldBe(expected);

        Allocations.ShouldSaveAtLeast(
            () => connection.NumbersAsListSync(parameters),
            () => NumbersAsListUnpooledByHand(connection, parameters),
            LeastListSavingInBytesPerCall,
            Settings
        );
    }

    [Theory]
    [InlineData(ConnectionState.Closed)]
    [InlineData(ConnectionState.Open)]
    public void NumbersAsImmutableArraySync_AFewHundredRows_AllocatesLessThanABuilder(ConnectionState state)
    {
        using var connection = new SqlConnection(sqlServer.ConnectionString);
        Allocations.InState(connection, state);

        var parameters = new NumbersCount(FewHundredRows);
        var expected = Enumerable.Range(1, FewHundredRows).Select(n => new CountedNumber(n)).ToArray();
        connection.NumbersAsImmutableArraySync(parameters).ShouldBe(expected);
        NumbersAsImmutableArrayUnpooledByHand(connection, parameters).ShouldBe(expected);

        Allocations.ShouldSaveAtLeast(
            () => connection.NumbersAsImmutableArraySync(parameters),
            () => NumbersAsImmutableArrayUnpooledByHand(connection, parameters),
            LeastImmutableArraySavingInBytesPerCall,
            Settings
        );
    }

    // The insert, each call writing a row at an id of its own, from a table that the test empties first.
    [Theory]
    [InlineData(ConnectionState.Closed)]
    [InlineData(ConnectionState.Open)]
    public void InsertNodaValuesSync_EveryColumn_AllocatesNoMoreThanByHand(ConnectionState state)
    {
        DeleteNodaValues();
        using var connection = new SqlConnection(sqlServer.ConnectionString);
        Allocations.InState(connection, state);

        var id = 0;
        connection.InsertNodaValuesSync(NodaValues with { Id = ++id }).ShouldBe(1);
        InsertNodaValuesByHand(connection, NodaValues with { Id = ++id }).ShouldBe(1);

        Allocations.ShouldAllocateNoMoreThanByHand(
            () => connection.InsertNodaValuesSync(NodaValues with { Id = ++id }),
            () => InsertNodaValuesByHand(connection, NodaValues with { Id = ++id }),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Theory]
    [InlineData(ConnectionState.Closed)]
    [InlineData(ConnectionState.Open)]
    public void GetNodaValuesSync_EveryColumn_AllocatesNoMoreThanByHand(ConnectionState state)
    {
        DeleteNodaValues();
        using var connection = new SqlConnection(sqlServer.ConnectionString);
        Allocations.InState(connection, state);

        var row = NodaValues with { Id = 1 };
        _ = connection.InsertNodaValuesSync(row);
        var parameters = new GetNodaValuesArgs(row.Id);

        var generatedRow = connection.GetNodaValuesSync(parameters);
        generatedRow.ShouldBe(
            new GetNodaValuesDto(
                row.Id,
                row.DateValue,
                row.TimeValue,
                row.Datetime2Value,
                row.DatetimeValue,
                row.SmalldatetimeValue,
                row.DatetimeoffsetValue
            )
        );
        GetNodaValuesByHand(connection, parameters).ShouldBe(generatedRow);

        Allocations.ShouldAllocateNoMoreThanByHand(
            () => connection.GetNodaValuesSync(parameters),
            () => GetNodaValuesByHand(connection, parameters),
            MarginInBytesPerCall,
            Settings
        );
    }

    // SearchAccountsSync by hand: the arguments of the generated method, and its calls, the rows into RowBufferByHand.
    private static SearchAccountsView[] SearchAccountsPooledByHand(
        DbConnection connection,
        SearchAccountsFilter parameters,
        DbTransaction? transaction = null,
        int? commandTimeout = null
    )
    {
        // The generated method's refusal of a transaction that has completed.
        if (transaction is not null && transaction.Connection is null)
        {
            throw new InvalidOperationException(
                "The transaction given to the query 'SearchAccountsSync' has completed; it is no longer usable."
            );
        }

        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = SearchAccountsSql;
            command.Transaction = transaction;
            if (commandTimeout.HasValue)
            {
                command.CommandTimeout = commandTimeout.Value;
            }

            // nvarchar(4000), or nvarchar(max) for a longer value, and DBNull for null, as the generated method binds.
            var name = (SqlParameter)command.CreateParameter();
            name.ParameterName = "name";
            name.SqlDbType = SqlDbType.NVarChar;
            name.Size = 4000;
            name.Value = (object?)parameters.Name ?? DBNull.Value;
            if (name.Value is string { Length: > 4000 })
            {
                name.Size = -1;
            }

            _ = command.Parameters.Add(name);

            var accountId = (SqlParameter)command.CreateParameter();
            accountId.ParameterName = "accountId";
            accountId.SqlDbType = SqlDbType.Int;
            accountId.Value = parameters.AccountId;
            _ = command.Parameters.Add(accountId);

            using var reader = command.ExecuteReader(CommandBehavior.Default);
            try
            {
                var rows = new RowBufferByHand<SearchAccountsView>();
                while (reader.Read())
                {
                    rows.Add(new SearchAccountsView(reader.GetFieldValue<int>(0), reader.GetFieldValue<string>(1)));
                }

                while (reader.NextResult())
                {
                    // The generated method's drain: the results after the rows, whose error SqlClient raises only here.
                }

                // The array behind the IReadOnlyList.
                return rows.ToArray();
            }
            catch (DbException exception) when (transaction is not null)
            {
                // The generated method's check after a database error: the rest of the batch is read, to see whether a
                // later error rolled the transaction back.
                var raisedLater = false;
                try
                {
                    while (reader.NextResult())
                    {
                        // The generated method's drain.
                    }
                }
                catch (DbException)
                {
                    raisedLater = true;
                }
#pragma warning disable CA1031, RCS1075
                catch (Exception)
#pragma warning restore CA1031, RCS1075
                {
                    // Empty as the generated method's is: it throws its first error, never the drain's.
                }

                reader.Dispose();
                if (raisedLater && transaction.Connection is null)
                {
                    throw new TransactionRolledBackException("SearchAccountsSync", exception);
                }

                throw;
            }
            catch (Exception exception)
                when (transaction is not null && exception is not (DbException or OperationCanceledException))
            {
                // The generated method's check of the transaction after an exception of its own.
                reader.Dispose();
                if (transaction.Connection is null)
                {
                    throw new TransactionRolledBackException("SearchAccountsSync", exception);
                }

                throw;
            }
        }
        finally
        {
            if (wasClosed)
            {
                connection.Close();
            }
        }
    }

    // SearchAccountsUnpooledSync by hand: the arguments of the generated method, and its calls, the rows into a List
    // copied to an array.
    private static SearchAccountsView[] SearchAccountsUnpooledByHand(
        DbConnection connection,
        SearchAccountsFilter parameters,
        DbTransaction? transaction = null,
        int? commandTimeout = null
    )
    {
        // The generated method's refusal of a transaction that has completed.
        if (transaction is not null && transaction.Connection is null)
        {
            throw new InvalidOperationException(
                "The transaction given to the query 'SearchAccountsUnpooledSync' has completed; it is no longer usable."
            );
        }

        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = SearchAccountsSql;
            command.Transaction = transaction;
            if (commandTimeout.HasValue)
            {
                command.CommandTimeout = commandTimeout.Value;
            }

            // nvarchar(4000), or nvarchar(max) for a longer value, and DBNull for null, as the generated method binds.
            var name = (SqlParameter)command.CreateParameter();
            name.ParameterName = "name";
            name.SqlDbType = SqlDbType.NVarChar;
            name.Size = 4000;
            name.Value = (object?)parameters.Name ?? DBNull.Value;
            if (name.Value is string { Length: > 4000 })
            {
                name.Size = -1;
            }

            _ = command.Parameters.Add(name);

            var accountId = (SqlParameter)command.CreateParameter();
            accountId.ParameterName = "accountId";
            accountId.SqlDbType = SqlDbType.Int;
            accountId.Value = parameters.AccountId;
            _ = command.Parameters.Add(accountId);

            using var reader = command.ExecuteReader(CommandBehavior.Default);
            try
            {
                var rows = new List<SearchAccountsView>();
                while (reader.Read())
                {
                    rows.Add(new SearchAccountsView(reader.GetFieldValue<int>(0), reader.GetFieldValue<string>(1)));
                }

                while (reader.NextResult())
                {
                    // The generated method's drain: the results after the rows, whose error SqlClient raises only here.
                }

                // The array behind the IReadOnlyList; a collection expression would wrap it in another object.
#pragma warning disable IDE0305
                return rows.ToArray();
#pragma warning restore IDE0305
            }
            catch (DbException exception) when (transaction is not null)
            {
                // The generated method's check after a database error: the rest of the batch is read, to see whether a
                // later error rolled the transaction back.
                var raisedLater = false;
                try
                {
                    while (reader.NextResult())
                    {
                        // The generated method's drain.
                    }
                }
                catch (DbException)
                {
                    raisedLater = true;
                }
#pragma warning disable CA1031, RCS1075
                catch (Exception)
#pragma warning restore CA1031, RCS1075
                {
                    // Empty as the generated method's is: it throws its first error, never the drain's.
                }

                reader.Dispose();
                if (raisedLater && transaction.Connection is null)
                {
                    throw new TransactionRolledBackException("SearchAccountsUnpooledSync", exception);
                }

                throw;
            }
            catch (Exception exception)
                when (transaction is not null && exception is not (DbException or OperationCanceledException))
            {
                // The generated method's check of the transaction after an exception of its own.
                reader.Dispose();
                if (transaction.Connection is null)
                {
                    throw new TransactionRolledBackException("SearchAccountsUnpooledSync", exception);
                }

                throw;
            }
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
        NumbersCount parameters,
        DbTransaction? transaction = null,
        int? commandTimeout = null
    )
    {
        // The generated method's refusal of a transaction that has completed.
        if (transaction is not null && transaction.Connection is null)
        {
            throw new InvalidOperationException(
                "The transaction given to the query 'NumbersAsListSync' has completed; it is no longer usable."
            );
        }

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

            // int, as the generated method binds.
            var countParameter = (SqlParameter)command.CreateParameter();
            countParameter.ParameterName = "count";
            countParameter.SqlDbType = SqlDbType.Int;
            countParameter.Value = parameters.Count;
            _ = command.Parameters.Add(countParameter);

            using var reader = command.ExecuteReader(CommandBehavior.Default);
            try
            {
                var rows = new RowBufferByHand<CountedNumber>();
                while (reader.Read())
                {
                    rows.Add(new CountedNumber(reader.IsDBNull(0) ? null : reader.GetFieldValue<int>(0)));
                }

                while (reader.NextResult())
                {
                    // The generated method's drain: the results after the rows, whose error SqlClient raises only here.
                }

                return rows.ToList();
            }
            catch (DbException exception) when (transaction is not null)
            {
                // The generated method's check after a database error: the rest of the batch is read, to see whether a
                // later error rolled the transaction back.
                var raisedLater = false;
                try
                {
                    while (reader.NextResult())
                    {
                        // The generated method's drain.
                    }
                }
                catch (DbException)
                {
                    raisedLater = true;
                }
#pragma warning disable CA1031, RCS1075
                catch (Exception)
#pragma warning restore CA1031, RCS1075
                {
                    // Empty as the generated method's is: it throws its first error, never the drain's.
                }

                reader.Dispose();
                if (raisedLater && transaction.Connection is null)
                {
                    throw new TransactionRolledBackException("NumbersAsListSync", exception);
                }

                throw;
            }
            catch (Exception exception)
                when (transaction is not null && exception is not (DbException or OperationCanceledException))
            {
                // The generated method's check of the transaction after an exception of its own.
                reader.Dispose();
                if (transaction.Connection is null)
                {
                    throw new TransactionRolledBackException("NumbersAsListSync", exception);
                }

                throw;
            }
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
        NumbersCount parameters,
        DbTransaction? transaction = null,
        int? commandTimeout = null
    )
    {
        // The generated method's refusal of a transaction that has completed.
        if (transaction is not null && transaction.Connection is null)
        {
            throw new InvalidOperationException(
                "The transaction given to the query 'NumbersAsImmutableArraySync' has completed; "
                    + "it is no longer usable."
            );
        }

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

            // int, as the generated method binds.
            var countParameter = (SqlParameter)command.CreateParameter();
            countParameter.ParameterName = "count";
            countParameter.SqlDbType = SqlDbType.Int;
            countParameter.Value = parameters.Count;
            _ = command.Parameters.Add(countParameter);

            using var reader = command.ExecuteReader(CommandBehavior.Default);
            try
            {
                var rows = new RowBufferByHand<CountedNumber>();
                while (reader.Read())
                {
                    rows.Add(new CountedNumber(reader.IsDBNull(0) ? null : reader.GetFieldValue<int>(0)));
                }

                while (reader.NextResult())
                {
                    // The generated method's drain: the results after the rows, whose error SqlClient raises only here.
                }

                return rows.ToImmutableArray();
            }
            catch (DbException exception) when (transaction is not null)
            {
                // The generated method's check after a database error: the rest of the batch is read, to see whether a
                // later error rolled the transaction back.
                var raisedLater = false;
                try
                {
                    while (reader.NextResult())
                    {
                        // The generated method's drain.
                    }
                }
                catch (DbException)
                {
                    raisedLater = true;
                }
#pragma warning disable CA1031, RCS1075
                catch (Exception)
#pragma warning restore CA1031, RCS1075
                {
                    // Empty as the generated method's is: it throws its first error, never the drain's.
                }

                reader.Dispose();
                if (raisedLater && transaction.Connection is null)
                {
                    throw new TransactionRolledBackException("NumbersAsImmutableArraySync", exception);
                }

                throw;
            }
            catch (Exception exception)
                when (transaction is not null && exception is not (DbException or OperationCanceledException))
            {
                // The generated method's check of the transaction after an exception of its own.
                reader.Dispose();
                if (transaction.Connection is null)
                {
                    throw new TransactionRolledBackException("NumbersAsImmutableArraySync", exception);
                }

                throw;
            }
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
        NumbersCount parameters,
        DbTransaction? transaction = null,
        int? commandTimeout = null
    )
    {
        // The generated method's refusal of a transaction that has completed.
        if (transaction is not null && transaction.Connection is null)
        {
            throw new InvalidOperationException(
                "The transaction given to the query 'NumbersNoPoolAsListSync' has completed; it is no longer usable."
            );
        }

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

            // int, as the generated method binds.
            var countParameter = (SqlParameter)command.CreateParameter();
            countParameter.ParameterName = "count";
            countParameter.SqlDbType = SqlDbType.Int;
            countParameter.Value = parameters.Count;
            _ = command.Parameters.Add(countParameter);

            using var reader = command.ExecuteReader(CommandBehavior.Default);
            try
            {
                var rows = new List<CountedNumber>();
                while (reader.Read())
                {
                    rows.Add(new CountedNumber(reader.IsDBNull(0) ? null : reader.GetFieldValue<int>(0)));
                }

                while (reader.NextResult())
                {
                    // The generated method's drain: the results after the rows, whose error SqlClient raises only here.
                }

                return rows;
            }
            catch (DbException exception) when (transaction is not null)
            {
                // The generated method's check after a database error: the rest of the batch is read, to see whether a
                // later error rolled the transaction back.
                var raisedLater = false;
                try
                {
                    while (reader.NextResult())
                    {
                        // The generated method's drain.
                    }
                }
                catch (DbException)
                {
                    raisedLater = true;
                }
#pragma warning disable CA1031, RCS1075
                catch (Exception)
#pragma warning restore CA1031, RCS1075
                {
                    // Empty as the generated method's is: it throws its first error, never the drain's.
                }

                reader.Dispose();
                if (raisedLater && transaction.Connection is null)
                {
                    throw new TransactionRolledBackException("NumbersNoPoolAsListSync", exception);
                }

                throw;
            }
            catch (Exception exception)
                when (transaction is not null && exception is not (DbException or OperationCanceledException))
            {
                // The generated method's check of the transaction after an exception of its own.
                reader.Dispose();
                if (transaction.Connection is null)
                {
                    throw new TransactionRolledBackException("NumbersNoPoolAsListSync", exception);
                }

                throw;
            }
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
        NumbersCount parameters,
        DbTransaction? transaction = null,
        int? commandTimeout = null
    )
    {
        // The generated method's refusal of a transaction that has completed.
        if (transaction is not null && transaction.Connection is null)
        {
            throw new InvalidOperationException(
                "The transaction given to the query 'NumbersNoPoolAsImmutableArraySync' has completed; "
                    + "it is no longer usable."
            );
        }

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

            // int, as the generated method binds.
            var countParameter = (SqlParameter)command.CreateParameter();
            countParameter.ParameterName = "count";
            countParameter.SqlDbType = SqlDbType.Int;
            countParameter.Value = parameters.Count;
            _ = command.Parameters.Add(countParameter);

            using var reader = command.ExecuteReader(CommandBehavior.Default);
            try
            {
                var rows = ImmutableArray.CreateBuilder<CountedNumber>();
                while (reader.Read())
                {
                    rows.Add(new CountedNumber(reader.IsDBNull(0) ? null : reader.GetFieldValue<int>(0)));
                }

                while (reader.NextResult())
                {
                    // The generated method's drain: the results after the rows, whose error SqlClient raises only here.
                }

                return rows.DrainToImmutable();
            }
            catch (DbException exception) when (transaction is not null)
            {
                // The generated method's check after a database error: the rest of the batch is read, to see whether a
                // later error rolled the transaction back.
                var raisedLater = false;
                try
                {
                    while (reader.NextResult())
                    {
                        // The generated method's drain.
                    }
                }
                catch (DbException)
                {
                    raisedLater = true;
                }
#pragma warning disable CA1031, RCS1075
                catch (Exception)
#pragma warning restore CA1031, RCS1075
                {
                    // Empty as the generated method's is: it throws its first error, never the drain's.
                }

                reader.Dispose();
                if (raisedLater && transaction.Connection is null)
                {
                    throw new TransactionRolledBackException("NumbersNoPoolAsImmutableArraySync", exception);
                }

                throw;
            }
            catch (Exception exception)
                when (transaction is not null && exception is not (DbException or OperationCanceledException))
            {
                // The generated method's check of the transaction after an exception of its own.
                reader.Dispose();
                if (transaction.Connection is null)
                {
                    throw new TransactionRolledBackException("NumbersNoPoolAsImmutableArraySync", exception);
                }

                throw;
            }
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
        NumbersCount parameters,
        DbTransaction? transaction = null,
        int? commandTimeout = null
    )
    {
        // The generated method's refusal of a transaction that has completed.
        if (transaction is not null && transaction.Connection is null)
        {
            throw new InvalidOperationException(
                "The transaction given to the query 'NumbersAsArraySync' has completed; it is no longer usable."
            );
        }

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

            // int, as the generated method binds.
            var countParameter = (SqlParameter)command.CreateParameter();
            countParameter.ParameterName = "count";
            countParameter.SqlDbType = SqlDbType.Int;
            countParameter.Value = parameters.Count;
            _ = command.Parameters.Add(countParameter);

            using var reader = command.ExecuteReader(CommandBehavior.Default);
            try
            {
                var rows = new List<CountedNumber>();
                while (reader.Read())
                {
                    rows.Add(new CountedNumber(reader.IsDBNull(0) ? null : reader.GetFieldValue<int>(0)));
                }

                while (reader.NextResult())
                {
                    // The generated method's drain: the results after the rows, whose error SqlClient raises only here.
                }

                // A collection expression would wrap the array in another object.
#pragma warning disable IDE0305
                return rows.ToArray();
#pragma warning restore IDE0305
            }
            catch (DbException exception) when (transaction is not null)
            {
                // The generated method's check after a database error: the rest of the batch is read, to see whether a
                // later error rolled the transaction back.
                var raisedLater = false;
                try
                {
                    while (reader.NextResult())
                    {
                        // The generated method's drain.
                    }
                }
                catch (DbException)
                {
                    raisedLater = true;
                }
#pragma warning disable CA1031, RCS1075
                catch (Exception)
#pragma warning restore CA1031, RCS1075
                {
                    // Empty as the generated method's is: it throws its first error, never the drain's.
                }

                reader.Dispose();
                if (raisedLater && transaction.Connection is null)
                {
                    throw new TransactionRolledBackException("NumbersAsArraySync", exception);
                }

                throw;
            }
            catch (Exception exception)
                when (transaction is not null && exception is not (DbException or OperationCanceledException))
            {
                // The generated method's check of the transaction after an exception of its own.
                reader.Dispose();
                if (transaction.Connection is null)
                {
                    throw new TransactionRolledBackException("NumbersAsArraySync", exception);
                }

                throw;
            }
        }
        finally
        {
            if (wasClosed)
            {
                connection.Close();
            }
        }
    }

    // InsertNodaValuesSync of NodaTime.sql, by hand: the arguments of the generated method, and its calls, each value
    // converted by the NodaTime members that the generated conversions call, and boxed as the generated method boxes
    // it.
    private static int InsertNodaValuesByHand(
        DbConnection connection,
        InsertNodaValuesArgs parameters,
        DbTransaction? transaction = null,
        int? commandTimeout = null
    )
    {
        // The generated method's refusal of a transaction that has completed.
        if (transaction is not null && transaction.Connection is null)
        {
            throw new InvalidOperationException(
                "The transaction given to the query 'InsertNodaValuesSync' has completed; it is no longer usable."
            );
        }

        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = InsertNodaValuesSql;
            command.Transaction = transaction;
            if (commandTimeout.HasValue)
            {
                command.CommandTimeout = commandTimeout.Value;
            }

            var id = (SqlParameter)command.CreateParameter();
            id.ParameterName = "id";
            id.SqlDbType = SqlDbType.Int;
            id.Value = parameters.Id;
            _ = command.Parameters.Add(id);

            var date = (SqlParameter)command.CreateParameter();
            date.ParameterName = "dateValue";
            date.SqlDbType = SqlDbType.Date;
            date.Value = parameters.DateValue is { } dateValue ? dateValue.ToDateTimeUnspecified() : DBNull.Value;
            _ = command.Parameters.Add(date);

            var time = (SqlParameter)command.CreateParameter();
            time.ParameterName = "timeValue";
            time.SqlDbType = SqlDbType.Time;
            time.Scale = 7;
            time.Value = parameters.TimeValue is { } timeValue ? new TimeSpan(timeValue.TickOfDay) : DBNull.Value;
            _ = command.Parameters.Add(time);

            var datetime2 = (SqlParameter)command.CreateParameter();
            datetime2.ParameterName = "datetime2Value";
            datetime2.SqlDbType = SqlDbType.DateTime2;
            datetime2.Scale = 7;
            datetime2.Value = parameters.Datetime2Value is { } datetime2Value
                ? datetime2Value.ToDateTimeUnspecified()
                : DBNull.Value;
            _ = command.Parameters.Add(datetime2);

            var datetime = (SqlParameter)command.CreateParameter();
            datetime.ParameterName = "datetimeValue";
            datetime.SqlDbType = SqlDbType.DateTime;
            datetime.Value = parameters.DatetimeValue is { } datetimeValue
                ? datetimeValue.ToDateTimeUnspecified()
                : DBNull.Value;
            _ = command.Parameters.Add(datetime);

            var smalldatetime = (SqlParameter)command.CreateParameter();
            smalldatetime.ParameterName = "smalldatetimeValue";
            smalldatetime.SqlDbType = SqlDbType.SmallDateTime;
            smalldatetime.Value = parameters.SmalldatetimeValue is { } smalldatetimeValue
                ? smalldatetimeValue.ToDateTimeUnspecified()
                : DBNull.Value;
            _ = command.Parameters.Add(smalldatetime);

            var datetimeoffset = (SqlParameter)command.CreateParameter();
            datetimeoffset.ParameterName = "datetimeoffsetValue";
            datetimeoffset.SqlDbType = SqlDbType.DateTimeOffset;
            datetimeoffset.Scale = 7;
            datetimeoffset.Value = parameters.DatetimeoffsetValue is { } datetimeoffsetValue
                ? datetimeoffsetValue.ToDateTimeOffset()
                : DBNull.Value;
            _ = command.Parameters.Add(datetimeoffset);

            return command.ExecuteNonQuery();
        }
        finally
        {
            if (wasClosed)
            {
                connection.Close();
            }
        }
    }

    // GetNodaValuesSync of NodaTime.sql, by hand: the arguments of the generated method, and its calls, each column
    // read as SqlClient's type and converted by the NodaTime members that the generated conversions call.
    private static GetNodaValuesDto GetNodaValuesByHand(
        DbConnection connection,
        GetNodaValuesArgs parameters,
        DbTransaction? transaction = null,
        int? commandTimeout = null
    )
    {
        // The generated method's refusal of a transaction that has completed.
        if (transaction is not null && transaction.Connection is null)
        {
            throw new InvalidOperationException(
                "The transaction given to the query 'GetNodaValuesSync' has completed; it is no longer usable."
            );
        }

        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = GetNodaValuesSql;
            command.Transaction = transaction;
            if (commandTimeout.HasValue)
            {
                command.CommandTimeout = commandTimeout.Value;
            }

            var id = (SqlParameter)command.CreateParameter();
            id.ParameterName = "id";
            id.SqlDbType = SqlDbType.Int;
            id.Value = parameters.Id;
            _ = command.Parameters.Add(id);

            using var reader = command.ExecuteReader(CommandBehavior.Default);
            try
            {
                if (!reader.Read())
                {
                    throw new UnexpectedRowCountException("GetNodaValuesSync", false);
                }

                var row = new GetNodaValuesDto(
                    reader.GetFieldValue<int>(0),
                    reader.IsDBNull(1)
                        ? default(LocalDate?)
                        : LocalDate.FromDateTime(reader.GetFieldValue<DateTime>(1)),
                    reader.IsDBNull(2)
                        ? default(LocalTime?)
                        : LocalTime.FromTicksSinceMidnight(reader.GetFieldValue<TimeSpan>(2).Ticks),
                    reader.IsDBNull(3)
                        ? default(LocalDateTime?)
                        : LocalDateTime.FromDateTime(reader.GetFieldValue<DateTime>(3)),
                    reader.IsDBNull(4)
                        ? default(LocalDateTime?)
                        : LocalDateTime.FromDateTime(reader.GetFieldValue<DateTime>(4)),
                    reader.IsDBNull(5)
                        ? default(LocalDateTime?)
                        : LocalDateTime.FromDateTime(reader.GetFieldValue<DateTime>(5)),
                    reader.IsDBNull(6)
                        ? default(OffsetDateTime?)
                        : OffsetDateTime.FromDateTimeOffset(reader.GetFieldValue<DateTimeOffset>(6))
                );
                if (reader.Read())
                {
                    throw new UnexpectedRowCountException("GetNodaValuesSync", true);
                }

                while (reader.NextResult())
                {
                    // The generated method's drain: the results after the row, whose error SqlClient raises only here.
                }

                return row;
            }
            catch (DbException exception) when (transaction is not null)
            {
                // The generated method's check after a database error: the rest of the batch is read, to see whether a
                // later error rolled the transaction back.
                var raisedLater = false;
                try
                {
                    while (reader.NextResult())
                    {
                        // The generated method's drain.
                    }
                }
                catch (DbException)
                {
                    raisedLater = true;
                }
#pragma warning disable CA1031, RCS1075
                catch (Exception)
#pragma warning restore CA1031, RCS1075
                {
                    // Empty as the generated method's is: it throws its first error, never the drain's.
                }

                reader.Dispose();
                if (raisedLater && transaction.Connection is null)
                {
                    throw new TransactionRolledBackException("GetNodaValuesSync", exception);
                }

                throw;
            }
            catch (Exception exception)
                when (transaction is not null && exception is not (DbException or OperationCanceledException))
            {
                // The generated method's check of the transaction after an exception of its own.
                reader.Dispose();
                if (transaction.Connection is null)
                {
                    throw new TransactionRolledBackException("GetNodaValuesSync", exception);
                }

                throw;
            }
        }
        finally
        {
            if (wasClosed)
            {
                connection.Close();
            }
        }
    }

    // DeleteNodaValuesAsync for a synchronous test.
    private void DeleteNodaValues()
    {
        using var connection = new SqlConnection(sqlServer.ConnectionString);
        connection.Open();
        using var delete = new SqlCommand("DELETE FROM dbo.noda_values", connection);
        _ = delete.ExecuteNonQuery();
    }
}
