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
// of each is written to Commands, as are "BEGIN" and "ROLLBACK" for a transaction, and to Executions with the
// connection and the transaction it ran on.  Its data source is DbDataSource's
// own, whose commands refuse a connection, as Npgsql's do.  It overrides none of the asynchronous members, which call
// the synchronous ones, so that it declares no pair that MethodPairs.Find would find.
internal sealed class FakeDatabase : IEngine
{
    public List<string> Commands { get; } = [];

    public List<Execution> Executions { get; } = [];

    // What Render gives, which renders nothing itself unless a test sets it.
    public Func<object, string?> Renderer { get; set; } = static _ => null;

    private int _copies;

    public DbDataSource SourceOf(string source, string? database) => new Source(this, database ?? "fake");

    // A name of its own for each copy, as an engine's.
    public Task<string> FreshDatabaseAsync(string source) =>
        Task.FromResult(string.Create(CultureInfo.InvariantCulture, $"copy_{++_copies}"));

    public Task DropDatabaseAsync(string database) => Task.CompletedTask;

    public string? CodeOf(DbException exception) => null;

    public Task<string> TransactionStateAsync(DbConnection connection, DbTransaction transaction) =>
        Task.FromResult("active");

    public string? Render(object value) => Renderer(value);

    // A command's text, with the connection and the transaction it ran on.
    public sealed record Execution(string Text, DbConnection Connection, DbTransaction? Transaction);

    private sealed class Source(FakeDatabase database, string name) : DbDataSource
    {
        public override string ConnectionString => "fake";

        protected override DbConnection CreateDbConnection() => new Connection(database, name);
    }

    private sealed class Connection(FakeDatabase database, string name) : DbConnection
    {
        private ConnectionState _state = ConnectionState.Closed;

        [AllowNull]
        public override string ConnectionString { get; set; } = "fake";

        public override string Database => name;

        public override string DataSource => "fake";

        public override string ServerVersion => "1";

        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

        public override void Close() => _state = ConnectionState.Closed;

        public override void Open() => _state = ConnectionState.Open;

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            database.Commands.Add("BEGIN");
            return new Transaction(database, this, isolationLevel);
        }

        protected override DbCommand CreateDbCommand() => new Command(database, this);
    }

    private sealed class Transaction(FakeDatabase database, DbConnection connection, IsolationLevel isolationLevel)
        : DbTransaction
    {
        private bool _completed;

        public override IsolationLevel IsolationLevel => isolationLevel;

        // Null once the transaction has completed, as a driver's is.
        protected override DbConnection? DbConnection => _completed ? null : connection;

        public override void Commit() => Complete("COMMIT");

        public override void Rollback() => Complete("ROLLBACK");

        private void Complete(string command)
        {
            if (_completed)
            {
                throw new InvalidOperationException("The transaction has completed.");
            }

            _completed = true;
            database.Commands.Add(command);
        }
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
            Record();
            return 0;
        }

        public override object? ExecuteScalar() => throw new NotSupportedException();

        public override void Prepare()
        {
            // Nothing is sent, so nothing is prepared.
        }

        protected override DbParameter CreateDbParameter() => throw new NotSupportedException();

        private void Record()
        {
            database.Commands.Add(CommandText);
            database.Executions.Add(new Execution(CommandText, DbConnection!, DbTransaction));
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            Record();
            using var table = new DataTable { Locale = CultureInfo.InvariantCulture };
            _ = table.Columns.Add("n", typeof(int));
            _ = table.Rows.Add(1);
            return table.CreateDataReader();
        }
    }
}
