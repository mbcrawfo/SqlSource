using System;
using System.Data;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The synchronous methods of the copies of Queries/Repository.sql under method-type, run through the repositories of
// RepositoryQueries.cs as RepositoryQueryTests runs their asynchronous methods: each source reads GetNoteSync, a
// synchronous method opening its connection by the source's own synchronous call, ListNotesSync reads every note into
// a list, a stream having no synchronous method, and TouchNoteSync counts its row through the generated interface.
// FactoryRepository marks a factory of each kind, and GetNoteBoth shows that its synchronous method calls the one that
// returns no task, with the default token, and GetNoteBothAsync the other, each once and never the other's, and that
// the connection the synchronous one gave is disposed once, by its Disposed event, after a return and after an
// exception.  A borrowed connection is closed again after a return and after an UnexpectedRowCountException, an open
// one is left open, and one with a pending transaction is refused by SqlClient as the asynchronous method's is; a
// null source throws the InvalidOperationException that names it before anything is opened.  Every test is a plain
// method, as the analyzers refuse a synchronous call in an asynchronous one that has an asynchronous twin.
[Collection(UsesSqlServer.Name)]
public sealed class SynchronousRepositoryTests(SqlServerDatabases sqlServer)
{
    // The seed's three notes, the first alone with a remind_at.
    private static readonly RepositoryNote[] Notes =
    [
        new(1, 1, "Asked for a larger plan.", new TimeSpan(9, 30, 0)),
        new(2, 1, "Sent the quote.", null),
        new(3, 2, "Closed the account on site.", null),
    ];

    public static TheoryData<string> Sources => ["ConnectionString", "Factory", "Borrowed"];

    [Theory]
    [MemberData(nameof(Sources))]
    public void GetNoteSync_EachSource_ReadsTheRow(string source)
    {
        var connectionString = EndToEndSources.AppConnectionString(sqlServer);
        var parameters = new GetNoteSyncArgs(1);

        var note = source switch
        {
            "ConnectionString" => new ConnectionStringRepository(connectionString).GetNoteSync(parameters),
            "Factory" => new FactoryRepository(connectionString).GetNoteSync(parameters),
            "Borrowed" => GetNoteSyncBorrowed(sqlServer, parameters),
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "No repository has this source."),
        };

        note.ShouldBe(Notes[0]);
    }

    [Fact]
    public void ListNotesSync_ConnectionString_ReadsEveryNote()
    {
        var repository = new ConnectionStringRepository(EndToEndSources.AppConnectionString(sqlServer));

        var notes = repository.ListNotesSync();

        notes.ShouldBe(Notes);
    }

    // The update writes the body as it is, so the note reads as the seed has it after the count.
    [Fact]
    public void TouchNoteSync_ThroughTheInterface_CountsTheRow()
    {
        var repository = new ConnectionStringRepository(EndToEndSources.AppConnectionString(sqlServer));

        var count = TouchNoteSync(repository);

        count.ShouldBe(1);
        repository.GetNoteSync(new GetNoteSyncArgs(1)).ShouldBe(Notes[0]);
    }

    [Fact]
    public void GetNoteBoth_SynchronousMethod_CallsTheSynchronousFactoryAloneWithTheDefaultToken()
    {
        var repository = new FactoryRepository(EndToEndSources.AppConnectionString(sqlServer));

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
        var repository = new FactoryRepository(EndToEndSources.AppConnectionString(sqlServer));

        var note = await repository.GetNoteBothAsync(new GetNoteBothArgs(3), token);

        note.ShouldBe(Notes[2]);
        _ = repository.Connections.ShouldHaveSingleItem();
        repository.SyncConnections.ShouldBeEmpty();
        repository.SyncTokens.ShouldBeEmpty();
    }

    [Fact]
    public void GetNoteSync_Factory_DisposesTheConnectionOnceAfterAReturn()
    {
        var repository = new FactoryRepository(EndToEndSources.AppConnectionString(sqlServer));

        var note = repository.GetNoteSync(new GetNoteSyncArgs(1));

        note.ShouldBe(Notes[0]);
        var connection = repository.SyncConnections.ShouldHaveSingleItem();
        connection.State.ShouldBe(ConnectionState.Closed);
        repository.SyncDisposed.ShouldBe([connection]);
        repository.Disposed.ShouldBeEmpty();
    }

    [Fact]
    public void FindNoteOfAccountSync_Factory_DisposesTheConnectionOnceAfterAnException()
    {
        var repository = new FactoryRepository(EndToEndSources.AppConnectionString(sqlServer));

        var exception = Should.Throw<UnexpectedRowCountException>(() =>
            repository.FindNoteOfAccountSync(new FindNoteOfAccountSyncArgs(1))
        );

        exception.QueryName.ShouldBe("FindNoteOfAccountSync");
        exception.Message.ShouldBe("The query 'FindNoteOfAccountSync' returned more than one row.");
        var connection = repository.SyncConnections.ShouldHaveSingleItem();
        connection.State.ShouldBe(ConnectionState.Closed);
        repository.SyncDisposed.ShouldBe([connection]);
    }

    [Fact]
    public void FindNoteOfAccountSync_OneOrNoRow_ReadsItOrReturnsNull()
    {
        var repository = new ConnectionStringRepository(EndToEndSources.AppConnectionString(sqlServer));

        repository.FindNoteOfAccountSync(new FindNoteOfAccountSyncArgs(2)).ShouldBe(Notes[2]);
        repository.FindNoteOfAccountSync(new FindNoteOfAccountSyncArgs(3)).ShouldBeNull();
    }

    // Closed again and not disposed: the caller's connection opens once more.
    [Fact]
    public void GetNoteSync_BorrowedClosedConnection_IsClosedAgain()
    {
        using var connection = EndToEndSources.App(sqlServer);
        var repository = new BorrowedRepository(connection);

        var note = repository.GetNoteSync(new GetNoteSyncArgs(2));

        note.ShouldBe(Notes[1]);
        connection.State.ShouldBe(ConnectionState.Closed);
        connection.Open();
        connection.State.ShouldBe(ConnectionState.Open);
    }

    [Fact]
    public void FindNoteOfAccountSync_BorrowedClosedConnection_IsClosedAgainAfterTheException()
    {
        using var connection = EndToEndSources.App(sqlServer);
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
        using var connection = EndToEndSources.App(sqlServer);
        connection.Open();
        var repository = new BorrowedRepository(connection);

        _ = Should.Throw<UnexpectedRowCountException>(() =>
            repository.FindNoteOfAccountSync(new FindNoteOfAccountSyncArgs(1))
        );

        connection.State.ShouldBe(ConnectionState.Open);
    }

    [Fact]
    public void ListNotesSync_BorrowedOpenConnection_IsLeftOpen()
    {
        using var connection = EndToEndSources.App(sqlServer);
        connection.Open();
        var repository = new BorrowedRepository(connection);

        var notes = repository.ListNotesSync();

        notes.ShouldBe(Notes);
        connection.State.ShouldBe(ConnectionState.Open);
    }

    // SqlClient refuses a command on a connection whose transaction it is not given, and a repository method takes no
    // transaction, so it refuses the method of a borrowed connection with one pending, which the caller rolls back.
    // The exception is the asynchronous method's, with the message of the synchronous ExecuteReader in place of its
    // BeginExecuteReader.
    [Fact]
    public void GetNoteSync_BorrowedConnectionWithAPendingTransaction_IsRefused()
    {
        using var connection = EndToEndSources.App(sqlServer);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var repository = new BorrowedRepository(connection);

        var thrown = Should.Throw<InvalidOperationException>(() => repository.GetNoteSync(new GetNoteSyncArgs(1)));

        thrown.Message.ShouldContain("ExecuteReader requires the command to have a transaction");
        connection.State.ShouldBe(ConnectionState.Open);
        transaction.Rollback();
    }

    [Fact]
    public void GetNoteSync_NullConnectionString_ThrowsNamingTheMember()
    {
        var repository = new ConnectionStringRepository(null!);

        ShouldThrowNullSource(
            () => repository.GetNoteSync(new GetNoteSyncArgs(1)),
            "The connection source 'connectionString' of "
                + "'SqlSource.DatabaseTests.SqlServer.EndToEnd.ConnectionStringRepository' is null."
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
                + "'SqlSource.DatabaseTests.SqlServer.EndToEnd.FactoryRepository' is null."
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
                + "'SqlSource.DatabaseTests.SqlServer.EndToEnd.BorrowedRepository' is null."
        );
    }

    // A borrowed connection made for the one row of the theory that needs it, and disposed by its owner, the test.
    private static RepositoryNote? GetNoteSyncBorrowed(SqlServerDatabases sqlServer, GetNoteSyncArgs parameters)
    {
        using var connection = EndToEndSources.App(sqlServer);
        return new BorrowedRepository(connection).GetNoteSync(parameters);
    }

    // Code that knows the repository only by its generated interface, as a caller that may be given a fake does.
    private static int TouchNoteSync<TRepository>(TRepository repository)
        where TRepository : IConnectionStringRepository => repository.TouchNoteSync(new TouchNoteSyncArgs(1));

    // The method throws, before it opens anything, the InvalidOperationException that names the null source.
    private static void ShouldThrowNullSource(Action call, string message)
    {
        var exception = Should.Throw<InvalidOperationException>(call);
        exception.Message.ShouldBe(message);
    }
}
