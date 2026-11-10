using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The queries of Queries/Repository.sql, run on the seed of "app" through the instance methods of the repositories of
// RepositoryQueries.cs, which take no connection: each reads through its own source, a connection string, a factory
// whose connection the method disposes, even at a break out of a stream, and a borrowed connection, which the method
// leaves as it found it.  A source that is null throws before the method opens anything, and the generated interface
// runs the same methods.  A repository method takes no transaction, so TouchNote writes the body it reads and leaves
// the seed as it was.
[Collection(UsesSqlServer.Name)]
public sealed class RepositoryQueryTests(SqlServerDatabases sqlServer)
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
    public async Task GetNote_EachSource_ReadsTheRow(string source)
    {
        var token = TestContext.Current.CancellationToken;
        var connectionString = EndToEndSources.AppConnectionString(sqlServer);
        var parameters = new GetNoteArgs(1);

        var note = source switch
        {
            "ConnectionString" => await new ConnectionStringRepository(connectionString).GetNote(parameters, token),
            "Factory" => await new FactoryRepository(connectionString).GetNote(parameters, token),
            "Borrowed" => await GetNoteBorrowedAsync(sqlServer, parameters, token),
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "No repository has this source."),
        };

        note.ShouldBe(Notes[0]);
    }

    // The factory opens its connection, so the method does not close it, and only its disposal at the break can; the
    // Disposed event, raised once, proves that the method disposed it exactly once.
    [Fact]
    public async Task ListNotes_BreakAfterFirstRow_DisposesTheFactoryConnection()
    {
        var token = TestContext.Current.CancellationToken;
        var repository = new FactoryRepository(EndToEndSources.AppConnectionString(sqlServer));
        var rows = new List<RepositoryNote>();

        await foreach (var note in repository.ListNotes(token))
        {
            rows.Add(note);
            if (rows.Count == 1)
            {
                break;
            }
        }

        rows.ShouldBe([Notes[0]]);
        var connection = repository.Connections.ShouldHaveSingleItem();
        connection.State.ShouldBe(ConnectionState.Closed);
        repository.Disposed.ShouldBe([connection]);
    }

    [Fact]
    public async Task ListNotes_BorrowedOpenConnection_IsLeftOpen()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);
        var repository = new BorrowedRepository(connection);

        var notes = await repository.ListNotes(token).ToListAsync(token);

        notes.ShouldBe(Notes);
        connection.State.ShouldBe(ConnectionState.Open);
    }

    // Closed again and not disposed: the caller's connection opens once more.
    [Fact]
    public async Task GetNote_BorrowedClosedConnection_IsClosedAgain()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        var repository = new BorrowedRepository(connection);

        var note = await repository.GetNote(new GetNoteArgs(2), token);

        note.ShouldBe(Notes[1]);
        connection.State.ShouldBe(ConnectionState.Closed);
        await connection.OpenAsync(token);
        connection.State.ShouldBe(ConnectionState.Open);
    }

    [Fact]
    public async Task GetNote_NullConnectionString_ThrowsNamingTheMember()
    {
        var repository = new ConnectionStringRepository(null!);

        await ShouldThrowNullSourceAsync(
            () => repository.GetNote(new GetNoteArgs(1), TestContext.Current.CancellationToken),
            "The connection source 'connectionString' of "
                + "'SqlSource.DatabaseTests.SqlServer.EndToEnd.ConnectionStringRepository' is null."
        );
    }

    [Fact]
    public async Task GetNote_FactoryReturnsNull_ThrowsNamingTheMember()
    {
        var repository = new FactoryRepository(null);

        await ShouldThrowNullSourceAsync(
            () => repository.GetNote(new GetNoteArgs(1), TestContext.Current.CancellationToken),
            "The connection source 'OpenAsync' of "
                + "'SqlSource.DatabaseTests.SqlServer.EndToEnd.FactoryRepository' is null."
        );
    }

    [Fact]
    public async Task GetNote_NullBorrowedConnection_ThrowsNamingTheMember()
    {
        var repository = new BorrowedRepository(null!);

        await ShouldThrowNullSourceAsync(
            () => repository.GetNote(new GetNoteArgs(1), TestContext.Current.CancellationToken),
            "The connection source 'Connection' of "
                + "'SqlSource.DatabaseTests.SqlServer.EndToEnd.BorrowedRepository' is null."
        );
    }

    // The update writes the body as it is, so the note reads as the seed has it after the count.
    [Fact]
    public async Task TouchNote_ThroughTheInterface_CountsTheRow()
    {
        var token = TestContext.Current.CancellationToken;
        var repository = new ConnectionStringRepository(EndToEndSources.AppConnectionString(sqlServer));

        var count = await TouchNoteAsync(repository, token);

        count.ShouldBe(1);
        (await repository.GetNote(new GetNoteArgs(1), token)).ShouldBe(Notes[0]);
    }

    // Code that knows the repository only by its generated interface, as a caller that may be given a fake does.
    private static Task<int> TouchNoteAsync<TRepository>(TRepository repository, CancellationToken token)
        where TRepository : IConnectionStringRepository => repository.TouchNote(new TouchNoteArgs(1), token);

    // A borrowed connection made for the one row of the theory that needs it, and disposed by its owner, the test.
    private static async Task<RepositoryNote?> GetNoteBorrowedAsync(
        SqlServerDatabases sqlServer,
        GetNoteArgs parameters,
        CancellationToken token
    )
    {
        await using var connection = EndToEndSources.App(sqlServer);
        return await new BorrowedRepository(connection).GetNote(parameters, token);
    }

    // The method throws, before it opens anything, the InvalidOperationException that names the null source.
    private static async Task ShouldThrowNullSourceAsync(Func<Task> call, string message)
    {
        var exception = await Should.ThrowAsync<InvalidOperationException>(call);
        exception.Message.ShouldBe(message);
    }
}
