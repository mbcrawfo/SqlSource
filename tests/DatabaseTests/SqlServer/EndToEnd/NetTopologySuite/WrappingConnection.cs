using System;
using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd.NetTopologySuite;

// A connection that wraps another, as a profiler or a resilience library wraps SqlClient's, so that a generated method
// is given a connection, a command, a transaction and a reader that are not SqlClient's.  The connection, the command
// and the transaction forward every member to the one they wrap, an asynchronous disposal through Dispose.  The reader
// overrides only what DbDataReader requires, and its disposal, so that every other read, GetStream among them, takes
// the base class's route.
internal sealed class WrappingConnection(DbConnection inner) : DbConnection
{
    public DbConnection Inner { get; } = inner;

    [AllowNull]
    public override string ConnectionString
    {
        get => Inner.ConnectionString;
        set => Inner.ConnectionString = value;
    }

    public override int ConnectionTimeout => Inner.ConnectionTimeout;

    public override string Database => Inner.Database;

    public override string DataSource => Inner.DataSource;

    public override string ServerVersion => Inner.ServerVersion;

    public override ConnectionState State => Inner.State;

    public override void ChangeDatabase(string databaseName) => Inner.ChangeDatabase(databaseName);

    public override void Close() => Inner.Close();

    public override Task CloseAsync() => Inner.CloseAsync();

    public override void Open() => Inner.Open();

    public override Task OpenAsync(CancellationToken cancellationToken) => Inner.OpenAsync(cancellationToken);

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
        new WrappingTransaction(this, Inner.BeginTransaction(isolationLevel));

    protected override async ValueTask<DbTransaction> BeginDbTransactionAsync(
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken
    ) => new WrappingTransaction(this, await Inner.BeginTransactionAsync(isolationLevel, cancellationToken));

    protected override DbCommand CreateDbCommand() => new WrappingCommand(this, Inner.CreateCommand());

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Inner.Dispose();
        }

        base.Dispose(disposing);
    }
}

// The transaction of a WrappingConnection, whose connection is the wrapper while the wrapped transaction has one.
internal sealed class WrappingTransaction(WrappingConnection connection, DbTransaction inner) : DbTransaction
{
    public DbTransaction Inner { get; } = inner;

    public override IsolationLevel IsolationLevel => Inner.IsolationLevel;

    protected override DbConnection? DbConnection => Inner.Connection is null ? null : connection;

    public override void Commit() => Inner.Commit();

    public override Task CommitAsync(CancellationToken cancellationToken = default) =>
        Inner.CommitAsync(cancellationToken);

    public override void Rollback() => Inner.Rollback();

    public override Task RollbackAsync(CancellationToken cancellationToken = default) =>
        Inner.RollbackAsync(cancellationToken);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Inner.Dispose();
        }

        base.Dispose(disposing);
    }
}

// The command of a WrappingConnection, which gives the wrapped command the wrapped connection and transaction and
// wraps the reader it executes.
internal sealed class WrappingCommand(WrappingConnection connection, DbCommand inner) : DbCommand
{
    [SuppressMessage(
        "Usage",
        "CA2213:Disposable fields should be disposed",
        Justification = "The command does not own its connection, as SqlClient's does not."
    )]
    private WrappingConnection? _connection = connection;
    private WrappingTransaction? _transaction;

    [AllowNull]
    public override string CommandText
    {
        get => inner.CommandText;
        [SuppressMessage(
            "Security",
            "CA2100:Review SQL queries for security vulnerabilities",
            Justification = "It forwards the text that the generated method sets to the wrapped command."
        )]
        set => inner.CommandText = value;
    }

    public override int CommandTimeout
    {
        get => inner.CommandTimeout;
        set => inner.CommandTimeout = value;
    }

    public override CommandType CommandType
    {
        get => inner.CommandType;
        set => inner.CommandType = value;
    }

    public override bool DesignTimeVisible
    {
        get => inner.DesignTimeVisible;
        set => inner.DesignTimeVisible = value;
    }

    public override UpdateRowSource UpdatedRowSource
    {
        get => inner.UpdatedRowSource;
        set => inner.UpdatedRowSource = value;
    }

    protected override DbConnection? DbConnection
    {
        get => _connection;
        set
        {
            _connection = (WrappingConnection?)value;
            inner.Connection = _connection?.Inner;
        }
    }

    protected override DbParameterCollection DbParameterCollection => inner.Parameters;

    protected override DbTransaction? DbTransaction
    {
        get => _transaction;
        set
        {
            _transaction = (WrappingTransaction?)value;
            inner.Transaction = _transaction?.Inner;
        }
    }

    public override void Cancel() => inner.Cancel();

    public override int ExecuteNonQuery() => inner.ExecuteNonQuery();

    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) =>
        inner.ExecuteNonQueryAsync(cancellationToken);

    public override object? ExecuteScalar() => inner.ExecuteScalar();

    public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken) =>
        inner.ExecuteScalarAsync(cancellationToken);

    public override void Prepare() => inner.Prepare();

    public override Task PrepareAsync(CancellationToken cancellationToken = default) =>
        inner.PrepareAsync(cancellationToken);

    protected override DbParameter CreateDbParameter() => inner.CreateParameter();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
        new WrappingReader(inner.ExecuteReader(behavior));

    protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(
        CommandBehavior behavior,
        CancellationToken cancellationToken
    ) => new WrappingReader(await inner.ExecuteReaderAsync(behavior, cancellationToken));

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }
}

// The reader of a WrappingCommand, which overrides only the members that DbDataReader requires, forwarding each, and
// its disposal, which closes the wrapped reader.  GetStream, GetFieldValue, ReadAsync and the other virtual members
// take the base class's route: GetStream copies the column through GetBytes.
internal sealed class WrappingReader(DbDataReader inner) : DbDataReader
{
    public override int Depth => inner.Depth;

    public override int FieldCount => inner.FieldCount;

    public override bool HasRows => inner.HasRows;

    public override bool IsClosed => inner.IsClosed;

    public override int RecordsAffected => inner.RecordsAffected;

    public override object this[int ordinal] => inner[ordinal];

    public override object this[string name] => inner[name];

    public override bool GetBoolean(int ordinal) => inner.GetBoolean(ordinal);

    public override byte GetByte(int ordinal) => inner.GetByte(ordinal);

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) =>
        inner.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);

    public override char GetChar(int ordinal) => inner.GetChar(ordinal);

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) =>
        inner.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);

    public override string GetDataTypeName(int ordinal) => inner.GetDataTypeName(ordinal);

    public override DateTime GetDateTime(int ordinal) => inner.GetDateTime(ordinal);

    public override decimal GetDecimal(int ordinal) => inner.GetDecimal(ordinal);

    public override double GetDouble(int ordinal) => inner.GetDouble(ordinal);

    public override IEnumerator GetEnumerator() => inner.GetEnumerator();

    public override Type GetFieldType(int ordinal) => inner.GetFieldType(ordinal);

    public override float GetFloat(int ordinal) => inner.GetFloat(ordinal);

    public override Guid GetGuid(int ordinal) => inner.GetGuid(ordinal);

    public override short GetInt16(int ordinal) => inner.GetInt16(ordinal);

    public override int GetInt32(int ordinal) => inner.GetInt32(ordinal);

    public override long GetInt64(int ordinal) => inner.GetInt64(ordinal);

    public override string GetName(int ordinal) => inner.GetName(ordinal);

    public override int GetOrdinal(string name) => inner.GetOrdinal(name);

    public override string GetString(int ordinal) => inner.GetString(ordinal);

    public override object GetValue(int ordinal) => inner.GetValue(ordinal);

    public override int GetValues(object[] values) => inner.GetValues(values);

    public override bool IsDBNull(int ordinal) => inner.IsDBNull(ordinal);

    public override bool NextResult() => inner.NextResult();

    public override bool Read() => inner.Read();

    public override void Close() => inner.Close();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
