using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tests.Postgres.EndToEnd.Types;
using Xunit;

namespace SqlSource.Tests.Postgres.EndToEnd;

// The queries of Queries/Notes.sql, run on the seed of "app" through the methods that NoteQueries gets: the enum
// note_kind is read as the C# enum NoteKind, which the data source maps, the domains email and note_ref as the record
// structs Email and NoteRef, and the ids of the accounts and a count, by the file's column types and the queries'
// markers, as the record structs AccountId and NoteCount, through their converters, as columns and as parameters.  Half
// the tests give the method an open connection and half a closed one, which the method opens.
[Collection(UsesPostgres.Name)]
public sealed class NoteQueryTests(PostgresContainer postgres)
{
    [Fact]
    public async Task ListAccountNotes_AnAccountWithNoNote_HasNulls()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var notes = await connection.ListAccountNotes(cancellationToken: token);

        notes.ShouldBe([
            new ListAccountNotesDto(
                new AccountId(1),
                "alice",
                1,
                new AccountId(1),
                NoteKind.Call,
                "Asked for a larger plan."
            ),
            new ListAccountNotesDto(new AccountId(1), "alice", 2, new AccountId(1), NoteKind.Email, "Sent the quote."),
            new ListAccountNotesDto(
                new AccountId(2),
                "bob",
                3,
                new AccountId(2),
                NoteKind.Visit,
                "Closed the account on site."
            ),
            new ListAccountNotesDto(new AccountId(3), "carol", null, null, null, null),
        ]);
    }

    // accounts.id is an AccountId by the file's column types in the project, and notes.account_id one by the header's
    // -- column: marker, nullable on the optional side of the join: each is read from the int that Npgsql gives,
    // through FromDatabase, and carol's NULL is read as null, never given to it.
    [Fact]
    public async Task ListAccountNotes_EveryRow_ReadsTheIdsAsAccountIds()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var notes = await connection.ListAccountNotes(cancellationToken: token);

        notes
            .Select(static note => (note.Id, note.AccountId))
            .ShouldBe([
                (new AccountId(1), new AccountId(1)),
                (new AccountId(1), new AccountId(1)),
                (new AccountId(2), new AccountId(2)),
                (new AccountId(3), null),
            ]);
    }

    // The count, a bigint, is read from the long that Npgsql gives, through NoteCount's FromDatabase, by the query's
    // -- column: marker.
    [Fact]
    public async Task CountAccountNotes_EveryAccount_ReadsTheCountsAsNoteCounts()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var counts = await connection.CountAccountNotes(cancellationToken: token);

        counts.ShouldBe([
            new CountAccountNotesDto(new AccountId(1), new NoteCount(2)),
            new CountAccountNotesDto(new AccountId(2), new NoteCount(1)),
            new CountAccountNotesDto(new AccountId(3), new NoteCount(0)),
        ]);
    }

    // The parameter, which the -- param: marker gives the type AccountId and the database infers as an integer, is sent
    // as the int that ToDatabase returns.
    [Fact]
    public async Task GetAccountNoteCount_AnAccountId_ReadsItsNoteCount()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var count = await connection.GetAccountNoteCount(
            new GetAccountNoteCountArgs(new AccountId(1)),
            cancellationToken: token
        );

        count.ShouldBe(new GetAccountNoteCountDto(new AccountId(1), new NoteCount(2)));
    }

    [Fact]
    public async Task ListDisplayNames_EveryAccount_ReadsTheNames()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var names = await connection.ListDisplayNames(cancellationToken: token);

        names.ShouldBe([
            new ListDisplayNamesDto(new AccountId(1), "Alice"),
            new ListDisplayNamesDto(new AccountId(2), "Bob"),
            new ListDisplayNamesDto(new AccountId(3), "Carol"),
        ]);
    }

    // The method reads by ordinal, so the marks, which stay in the columns' names, set the properties' nullability and
    // nothing else.
    [Fact]
    public async Task ListMarkedNames_EveryAccount_ReadsTheNames()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var names = await connection.ListMarkedNames(cancellationToken: token);

        names.ShouldBe([
            new ListMarkedNamesDto(new AccountId(1), "Alice"),
            new ListMarkedNamesDto(new AccountId(2), "Bob"),
            new ListMarkedNamesDto(new AccountId(3), "Carol"),
        ]);
    }

    [Fact]
    public async Task ListNoteKinds_EveryNote_ReadsTheEnumAndTheDomain()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var kinds = await connection.ListNoteKinds(cancellationToken: token);

        kinds.ShouldBe([
            new ListNoteKindsDto(NoteKind.Call, new Email("alice@example.com")),
            new ListNoteKindsDto(NoteKind.Email, new Email("alice@example.com")),
            new ListNoteKindsDto(NoteKind.Visit, new Email("bob@example.com")),
        ]);
    }

    // The parameter is sent as the long that ToDatabase returns, and the column read from the long that Npgsql gives,
    // through FromDatabase.
    [Fact]
    public async Task GetNoteByRef_ARef_ReadsTheNote()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var note = await connection.GetNoteByRef(
            new GetNoteByRefArgs(new NoteRef(5_000_000_003)),
            cancellationToken: token
        );

        note.ShouldBe(new GetNoteByRefDto(3, new NoteRef(5_000_000_003), "Closed the account on site."));
    }

    // The shape is one, so a ref that no note has throws, and the connection the method opened is closed again.
    [Fact]
    public async Task GetNoteByRef_NoMatchingRef_ThrowsUnexpectedRowCount()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var exception = await Should.ThrowAsync<UnexpectedRowCountException>(() =>
            connection.GetNoteByRef(new GetNoteByRefArgs(new NoteRef(1)), cancellationToken: token)
        );

        exception.QueryName.ShouldBe("GetNoteByRef");
        exception.Message.ShouldBe("The query 'GetNoteByRef' returned no rows.");
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // The parameter is sent as the string that ToDatabase returns, and note 2's NULL ref is read as null, never given
    // to FromDatabase.
    [Fact]
    public async Task FindNotesByEmail_Alice_ReadsHerNotes()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var notes = await connection.FindNotesByEmail(
            new FindNotesByEmailArgs(new Email("alice@example.com")),
            cancellationToken: token
        );

        notes.ShouldBe([
            new FindNotesByEmailDto(1, new NoteRef(5_000_000_001), new Email("alice@example.com")),
            new FindNotesByEmailDto(2, null, new Email("alice@example.com")),
        ]);
    }

    // The parameter is bound by the domain's name, and Npgsql sends a domain as its base type, so the domain's CHECK,
    // which "nobody" fails, is never applied to it: the comparison finds no row, and nothing throws.
    [Fact]
    public async Task FindNotesByEmail_ValueTheDomainRefuses_FindsNoRow()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var notes = await connection.FindNotesByEmail(
            new FindNotesByEmailArgs(new Email("nobody")),
            cancellationToken: token
        );

        notes.ShouldBeEmpty();
    }
}
