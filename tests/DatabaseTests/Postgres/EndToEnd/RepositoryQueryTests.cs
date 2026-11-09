using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The queries of Queries/Repository.sql, run on the seed of "app" through the instance methods of the repositories of
// RepositoryQueries.cs, which take no connection: each reads through its own source, a data source, a connection
// string, a factory whose connection the method disposes, even at a break out of a stream, and a borrowed connection,
// which the method leaves as it found it.  A source that is null throws before the method opens anything, and the
// generated interface runs the same methods.  A repository method takes no transaction, so TouchNote writes the body
// it reads and leaves the seed as it was.
[Collection(UsesPostgres.Name)]
public sealed class RepositoryQueryTests(PostgresDatabases postgres)
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
    public async Task GetNote_EachSource_ReadsTheRow(string source)
    {
        var token = TestContext.Current.CancellationToken;
        await using var dataSource = EndToEndSources.App(postgres);
        await using var connection = dataSource.CreateConnection();
        var parameters = new GetNoteArgs(2);

        var note = source switch
        {
            "DataSource" => await new DataSourceRepository(dataSource).GetNote(parameters, token),
            "ConnectionString" => await new ConnectionStringRepository(
                EndToEndSources.AppConnectionString(postgres)
            ).GetNote(parameters, token),
            "Factory" => await new FactoryRepository(dataSource).GetNote(parameters, token),
            "Borrowed" => await new BorrowedRepository(connection).GetNote(parameters, token),
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "No repository has this source."),
        };

        note.ShouldBe(Notes[1]);
    }

    // The factory opens its connection, so the method does not close it, and only its disposal at the break can.
    // Npgsql raises no Disposed event, so the disposal shows as the ObjectDisposedException of an open after it.
    [Fact]
    public async Task ListNotes_BreakAfterFirstRow_DisposesTheFactoryConnection()
    {
        var token = TestContext.Current.CancellationToken;
        await using var dataSource = EndToEndSources.App(postgres);
        var repository = new FactoryRepository(dataSource);

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
        _ = await Should.ThrowAsync<ObjectDisposedException>(() => connection.OpenAsync(token));
    }

    [Fact]
    public async Task ListNotes_BorrowedOpenConnection_IsLeftOpen()
    {
        var token = TestContext.Current.CancellationToken;
        await using var dataSource = EndToEndSources.App(postgres);
        await using var connection = await dataSource.OpenConnectionAsync(token);
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
        await using var dataSource = EndToEndSources.App(postgres);
        await using var connection = dataSource.CreateConnection();
        var repository = new BorrowedRepository(connection);

        var note = await repository.GetNote(new GetNoteArgs(1), token);

        note.ShouldBe(Notes[0]);
        connection.State.ShouldBe(ConnectionState.Closed);
        await connection.OpenAsync(token);
        connection.State.ShouldBe(ConnectionState.Open);
    }

    [Fact]
    public async Task GetNote_NullConnectionString_ThrowsNamingTheMember()
    {
        var repository = new ConnectionStringRepository(null!);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            repository.GetNote(new GetNoteArgs(1), TestContext.Current.CancellationToken)
        );

        exception.Message.ShouldBe(
            "The connection source 'connectionString' of "
                + "'SqlSource.DatabaseTests.Postgres.EndToEnd.ConnectionStringRepository' is null."
        );
    }

    // The update writes the body as it is, so the note reads as the seed has it after the count.
    [Fact]
    public async Task TouchNote_ThroughTheInterface_CountsTheRow()
    {
        var token = TestContext.Current.CancellationToken;
        await using var dataSource = EndToEndSources.App(postgres);
        var repository = new DataSourceRepository(dataSource);

        var count = await TouchNoteAsync(repository, token);

        count.ShouldBe(1);
        (await repository.GetNote(new GetNoteArgs(1), token)).ShouldBe(Notes[0]);
    }

    // Code that knows the repository only by its generated interface, as a caller that may be given a fake does.
    private static Task<int> TouchNoteAsync<TRepository>(TRepository repository, CancellationToken token)
        where TRepository : IDataSourceRepository => repository.TouchNote(new TouchNoteArgs(1), token);
}
