using System;
using System.Data;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The synchronous methods of the copies of Queries/Repository.sql under method-type, run through the repositories of
// RepositoryQueries.cs as RepositoryQueryTests runs their asynchronous methods: each source reads GetNoteSync, a
// synchronous method opening its connection by the source's own synchronous call, ListNotesSync reads every note into
// a list, a stream having no synchronous method, and TouchNoteSync counts its row through the generated interface.
// FactoryRepository marks a factory of each kind, and GetNoteBoth shows that its synchronous method calls the one that
// returns no task, with the default token, and GetNoteBothAsync the other, each once and never the other's, and that
// the connection the synchronous one gave is disposed after a return and after an exception.  A borrowed connection is
// closed again after a return and after an UnexpectedRowCountException, and an open one is left open; a null source
// throws the InvalidOperationException that names it before anything is opened; and a multiplexing data source
// refuses a synchronous command, as it does one of a method on a connection, except in a transaction that the caller
// began on a borrowed connection.  Every test is a plain method, as the analyzers refuse a synchronous call in an
// asynchronous one that has an asynchronous twin.
[Collection(UsesPostgres.Name)]
public sealed class SynchronousRepositoryTests(PostgresDatabases postgres)
{
    // The seed's three notes, the second with no ref.
    private static readonly RepositoryNote[] Notes =
    [
        new(1, 1, "Asked for a larger plan.", 5_000_000_001),
        new(2, 1, "Sent the quote.", null),
        new(3, 2, "Closed the account on site.", 5_000_000_003),
    ];

    public static TheoryData<string> Sources => ["DataSource", "ConnectionString", "Factory", "Borrowed"];

    [Theory]
    [MemberData(nameof(Sources))]
    public void GetNoteSync_EachSource_ReadsTheRow(string source)
    {
        using var dataSource = EndToEndSources.App(postgres);
        var parameters = new GetNoteSyncArgs(2);

        var note = source switch
        {
            "DataSource" => new DataSourceRepository(dataSource).GetNoteSync(parameters),
            "ConnectionString" => new ConnectionStringRepository(
                EndToEndSources.AppConnectionString(postgres)
            ).GetNoteSync(parameters),
            "Factory" => new FactoryRepository(dataSource).GetNoteSync(parameters),
            "Borrowed" => GetNoteSyncBorrowed(dataSource, parameters),
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "No repository has this source."),
        };

        note.ShouldBe(Notes[1]);
    }

    [Fact]
    public void ListNotesSync_DataSource_ReadsEveryNote()
    {
        using var dataSource = EndToEndSources.App(postgres);
        var repository = new DataSourceRepository(dataSource);

        var notes = repository.ListNotesSync();

        notes.ShouldBe(Notes);
    }

    // The update writes the body as it is, so the note reads as the seed has it after the count.
    [Fact]
    public void TouchNoteSync_ThroughTheInterface_CountsTheRow()
    {
        using var dataSource = EndToEndSources.App(postgres);
        var repository = new DataSourceRepository(dataSource);

        var count = TouchNoteSync(repository);

        count.ShouldBe(1);
        repository.GetNoteSync(new GetNoteSyncArgs(1)).ShouldBe(Notes[0]);
    }

    [Fact]
    public void GetNoteBoth_SynchronousMethod_CallsTheSynchronousFactoryAloneWithTheDefaultToken()
    {
        using var dataSource = EndToEndSources.App(postgres);
        var repository = new FactoryRepository(dataSource);

        var note = repository.GetNoteBoth(new GetNoteBothArgs(3));

        note.ShouldBe(Notes[2]);
        repository.SyncTokens.ShouldBe([default]);
        _ = repository.SyncConnections.ShouldHaveSingleItem();
        repository.Connections.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetNoteBothAsync_AsynchronousMethod_CallsTheAsynchronousFactoryAlone()
    {
        var token = TestContext.Current.CancellationToken;
        await using var dataSource = EndToEndSources.App(postgres);
        var repository = new FactoryRepository(dataSource);

        var note = await repository.GetNoteBothAsync(new GetNoteBothArgs(3), token);

        note.ShouldBe(Notes[2]);
        _ = repository.Connections.ShouldHaveSingleItem();
        repository.SyncConnections.ShouldBeEmpty();
        repository.SyncTokens.ShouldBeEmpty();
    }

    // Npgsql raises no Disposed event, so the disposal shows as the ObjectDisposedException of an open after it.
    [Fact]
    public void GetNoteSync_Factory_DisposesTheConnectionAfterAReturn()
    {
        using var dataSource = EndToEndSources.App(postgres);
        var repository = new FactoryRepository(dataSource);

        var note = repository.GetNoteSync(new GetNoteSyncArgs(1));

        note.ShouldBe(Notes[0]);
        var connection = repository.SyncConnections.ShouldHaveSingleItem();
        connection.State.ShouldBe(ConnectionState.Closed);
        _ = Should.Throw<ObjectDisposedException>(connection.Open);
    }

    [Fact]
    public void FindNoteOfAccountSync_Factory_DisposesTheConnectionAfterAnException()
    {
        using var dataSource = EndToEndSources.App(postgres);
        var repository = new FactoryRepository(dataSource);

        var exception = Should.Throw<UnexpectedRowCountException>(() =>
            repository.FindNoteOfAccountSync(new FindNoteOfAccountSyncArgs(1))
        );

        exception.QueryName.ShouldBe("FindNoteOfAccountSync");
        exception.Message.ShouldBe("The query 'FindNoteOfAccountSync' returned more than one row.");
        var connection = repository.SyncConnections.ShouldHaveSingleItem();
        connection.State.ShouldBe(ConnectionState.Closed);
        _ = Should.Throw<ObjectDisposedException>(connection.Open);
    }

    [Fact]
    public void FindNoteOfAccountSync_OneOrNoRow_ReadsItOrReturnsNull()
    {
        using var dataSource = EndToEndSources.App(postgres);
        var repository = new DataSourceRepository(dataSource);

        repository.FindNoteOfAccountSync(new FindNoteOfAccountSyncArgs(2)).ShouldBe(Notes[2]);
        repository.FindNoteOfAccountSync(new FindNoteOfAccountSyncArgs(3)).ShouldBeNull();
    }

    // Closed again and not disposed: the caller's connection opens once more.
    [Fact]
    public void GetNoteSync_BorrowedClosedConnection_IsClosedAgain()
    {
        using var dataSource = EndToEndSources.App(postgres);
        using var connection = dataSource.CreateConnection();
        var repository = new BorrowedRepository(connection);

        var note = repository.GetNoteSync(new GetNoteSyncArgs(1));

        note.ShouldBe(Notes[0]);
        connection.State.ShouldBe(ConnectionState.Closed);
        connection.Open();
        connection.State.ShouldBe(ConnectionState.Open);
    }

    [Fact]
    public void FindNoteOfAccountSync_BorrowedClosedConnection_IsClosedAgainAfterTheException()
    {
        using var dataSource = EndToEndSources.App(postgres);
        using var connection = dataSource.CreateConnection();
        var repository = new BorrowedRepository(connection);

        _ = Should.Throw<UnexpectedRowCountException>(() =>
            repository.FindNoteOfAccountSync(new FindNoteOfAccountSyncArgs(1))
        );

        connection.State.ShouldBe(ConnectionState.Closed);
        connection.Open();
        connection.State.ShouldBe(ConnectionState.Open);
    }

    [Fact]
    public void FindNoteOfAccountSync_BorrowedOpenConnection_IsLeftOpenAfterTheException()
    {
        using var dataSource = EndToEndSources.App(postgres);
        using var connection = dataSource.OpenConnection();
        var repository = new BorrowedRepository(connection);

        _ = Should.Throw<UnexpectedRowCountException>(() =>
            repository.FindNoteOfAccountSync(new FindNoteOfAccountSyncArgs(1))
        );

        connection.State.ShouldBe(ConnectionState.Open);
    }

    [Fact]
    public void ListNotesSync_BorrowedOpenConnection_IsLeftOpen()
    {
        using var dataSource = EndToEndSources.App(postgres);
        using var connection = dataSource.OpenConnection();
        var repository = new BorrowedRepository(connection);

        var notes = repository.ListNotesSync();

        notes.ShouldBe(Notes);
        connection.State.ShouldBe(ConnectionState.Open);
    }

    [Fact]
    public void GetNoteSync_NullConnectionString_ThrowsNamingTheMember()
    {
        var repository = new ConnectionStringRepository(null!);

        ShouldThrowNullSource(
            () => repository.GetNoteSync(new GetNoteSyncArgs(1)),
            "The connection source 'connectionString' of "
                + "'SqlSource.DatabaseTests.Postgres.EndToEnd.ConnectionStringRepository' is null."
        );
    }

    [Fact]
    public void GetNoteSync_NullDataSource_ThrowsNamingTheMember()
    {
        var repository = new DataSourceRepository(null!);

        ShouldThrowNullSource(
            () => repository.GetNoteSync(new GetNoteSyncArgs(1)),
            "The connection source 'dataSource' of "
                + "'SqlSource.DatabaseTests.Postgres.EndToEnd.DataSourceRepository' is null."
        );
    }

    // The synchronous factory is the one that names the member, and it opened nothing.
    [Fact]
    public void GetNoteSync_FactoryReturnsNull_ThrowsNamingTheMember()
    {
        var repository = new FactoryRepository(null);

        ShouldThrowNullSource(
            () => repository.GetNoteSync(new GetNoteSyncArgs(1)),
            "The connection source 'Open' of "
                + "'SqlSource.DatabaseTests.Postgres.EndToEnd.FactoryRepository' is null."
        );
        repository.SyncConnections.ShouldBeEmpty();
    }

    [Fact]
    public void GetNoteSync_NullBorrowedConnection_ThrowsNamingTheMember()
    {
        var repository = new BorrowedRepository(null!);

        ShouldThrowNullSource(
            () => repository.GetNoteSync(new GetNoteSyncArgs(1)),
            "The connection source 'Connection' of "
                + "'SqlSource.DatabaseTests.Postgres.EndToEnd.BorrowedRepository' is null."
        );
    }

    // Npgsql's limit, which the method neither hides nor works around: a multiplexing data source refuses a synchronous
    // command, and the repository has no transaction of its own to run one in.
    [Fact]
    public void GetNoteSync_MultiplexingDataSource_ThrowsNotSupported()
    {
        using var dataSource = EndToEndSources.AppMultiplexing(postgres);
        var repository = new DataSourceRepository(dataSource);

        var exception = Should.Throw<NotSupportedException>(() => repository.GetNoteSync(new GetNoteSyncArgs(1)));

        exception.Message.ShouldBe("Synchronous command execution is not supported when multiplexing is on");
    }

    // The exception to that limit: a borrowed connection on which the caller has begun a transaction runs the method in
    // it, since Npgsql runs a synchronous command inside a transaction on a multiplexing data source.
    [Fact]
    public void GetNoteSync_MultiplexingBorrowedConnectionInATransaction_ReadsTheRow()
    {
        using var dataSource = EndToEndSources.AppMultiplexing(postgres);
        using var connection = dataSource.OpenConnection();
        using var transaction = connection.BeginTransaction();
        var repository = new BorrowedRepository(connection);

        var note = repository.GetNoteSync(new GetNoteSyncArgs(3));

        note.ShouldBe(Notes[2]);
        connection.State.ShouldBe(ConnectionState.Open);
        transaction.Rollback();
    }

    // A borrowed connection made for the one row of the theory that needs it, and disposed by its owner, the test.
    private static RepositoryNote? GetNoteSyncBorrowed(NpgsqlDataSource dataSource, GetNoteSyncArgs parameters)
    {
        using var connection = dataSource.CreateConnection();
        return new BorrowedRepository(connection).GetNoteSync(parameters);
    }

    // Code that knows the repository only by its generated interface, as a caller that may be given a fake does.
    private static int TouchNoteSync<TRepository>(TRepository repository)
        where TRepository : IDataSourceRepository => repository.TouchNoteSync(new TouchNoteSyncArgs(1));

    // The method throws, before it opens anything, the InvalidOperationException that names the null source.
    private static void ShouldThrowNullSource(Action call, string message)
    {
        var exception = Should.Throw<InvalidOperationException>(call);
        exception.Message.ShouldBe(message);
    }
}
