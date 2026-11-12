using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading.Tasks;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;

namespace SqlSource.Tests.TestHarness.SyncAsync;

// A database without a server, for the tests of PairRunner: every command returns one row of one column, and the text
// of each is written to Commands.  Its data source is DbDataSource's own, whose commands refuse a connection, as
// Npgsql's do.  It overrides none of the asynchronous members, which call the synchronous ones, so that it declares no
// pair that MethodPairs.Find would find.
internal sealed class FakeDatabase : IEngine
{
    public List<string> Commands { get; } = [];

    public DbDataSource SourceOf(string source, string? database) => new Source(this);

    public Task<string> FreshDatabaseAsync(string source) => Task.FromResult("copy");

    public Task DropDatabaseAsync(string database) => Task.CompletedTask;

    public string? CodeOf(DbException exception) => null;

    public Task<string> TransactionStateAsync(DbConnection connection, DbTransaction transaction) =>
        Task.FromResult("active");

    private sealed class Source(FakeDatabase database) : DbDataSource
    {
        public override string ConnectionString => "fake";

        protected override DbConnection CreateDbConnection() => new Connection(database);
    }

    private sealed class Connection(FakeDatabase database) : DbConnection
    {
        private ConnectionState _state = ConnectionState.Closed;

        [AllowNull]
        public override string ConnectionString { get; set; } = "fake";

        public override string Database => "fake";

        public override string DataSource => "fake";

        public override string ServerVersion => "1";

        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

        public override void Close() => _state = ConnectionState.Closed;

        public override void Open() => _state = ConnectionState.Open;

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            throw new NotSupportedException();

        protected override DbCommand CreateDbCommand() => new Command(database, this);
    }

    private sealed class Command(FakeDatabase database, DbConnection connection) : DbCommand
    {
        [AllowNull]
        public override string CommandText { get; set; } = "";

        public override int CommandTimeout { get; set; }

        public override CommandType CommandType { get; set; }

        public override UpdateRowSource UpdatedRowSource { get; set; }

        public override bool DesignTimeVisible { get; set; }

        protected override DbConnection? DbConnection { get; set; } = connection;

        protected override DbParameterCollection DbParameterCollection => throw new NotSupportedException();

        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel()
        {
            // Nothing runs, so nothing is cancelled.
        }

        public override int ExecuteNonQuery()
        {
            database.Commands.Add(CommandText);
            return 0;
        }

        public override object? ExecuteScalar() => throw new NotSupportedException();

        public override void Prepare()
        {
            // Nothing is sent, so nothing is prepared.
        }

        protected override DbParameter CreateDbParameter() => throw new NotSupportedException();

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            database.Commands.Add(CommandText);
            using var table = new DataTable { Locale = CultureInfo.InvariantCulture };
            _ = table.Columns.Add("n", typeof(int));
            _ = table.Rows.Add(1);
            return table.CreateDataReader();
        }
    }
}
