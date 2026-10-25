using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Shouldly;
using SqlSource.Tests.SqlServer.EndToEnd.Types;
using Xunit;

namespace SqlSource.Tests.SqlServer.EndToEnd;

// The queries of Queries/Notes.sql, run on the seed of "app" through the methods that NoteQueries gets: the alias type
// dbo.Email is read and sent as the record struct Email, and the ids of the accounts and a count, by the file's column
// types and the queries' markers, as the record structs AccountId and NoteCount, through their converters, as columns
// and as parameters, and time is read as a TimeOnly, by the file's type override, or as a TimeSpan, by a query's.  Half
// the reads give the method an open connection and half a closed one, which the method opens.  A write runs in a
// transaction that is rolled back.
[Collection(UsesSqlServer.Name)]
public sealed class NoteQueryTests(SqlServerContainer sqlServer)
{
    // The length of dbo.Email, nvarchar(320), in characters.
    private const int EmailLength = 320;

    [Fact]
    public async Task ListAccountNotes_AnAccountWithNoNote_HasNulls()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);

        var notes = await connection.ListAccountNotes(cancellationToken: token);

        notes.ShouldBe([
            new ListAccountNotesDto(
                new AccountId(1),
                "alice",
                1,
                new AccountId(1),
                "call",
                "Asked for a larger plan.",
                new TimeOnly(9, 30)
            ),
            new ListAccountNotesDto(new AccountId(1), "alice", 2, new AccountId(1), "email", "Sent the quote.", null),
            new ListAccountNotesDto(
                new AccountId(2),
                "bob",
                3,
                new AccountId(2),
                "visit",
                "Closed the account on site.",
                null
            ),
            new ListAccountNotesDto(new AccountId(3), "carol", null, null, null, null, null),
        ]);
    }

    // dbo.accounts.id is an AccountId by the file's column types in the project, and dbo.notes.account_id one by the
    // header's -- column: marker, nullable on the optional side of the join: each is read from the int that SqlClient
    // gives, through FromDatabase, and carol's NULL is read as null, never given to it.
    [Fact]
    public async Task ListAccountNotes_EveryRow_ReadsTheIdsAsAccountIds()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var notes = await connection.ListAccountNotes(cancellationToken: TestContext.Current.CancellationToken);

        notes
            .Select(static note => (note.Id, note.AccountId))
            .ShouldBe([
                (new AccountId(1), new AccountId(1)),
                (new AccountId(1), new AccountId(1)),
                (new AccountId(2), new AccountId(2)),
                (new AccountId(3), null),
            ]);
    }

    // The count, an int on SQL Server, is read from the int that SqlClient gives, through NoteCount's FromDatabase, by
    // the query's -- column: marker.
    [Fact]
    public async Task CountAccountNotes_EveryAccount_ReadsTheCountsAsNoteCounts()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var counts = await connection.CountAccountNotes(cancellationToken: TestContext.Current.CancellationToken);

        counts.ShouldBe([
            new CountAccountNotesDto(new AccountId(1), new NoteCount(2)),
            new CountAccountNotesDto(new AccountId(2), new NoteCount(1)),
            new CountAccountNotesDto(new AccountId(3), new NoteCount(0)),
        ]);
    }

    // The parameter, which the -- param: marker gives the type AccountId and SQL Server describes as an int, is sent as
    // the int that ToDatabase returns.
    [Fact]
    public async Task GetAccountNoteCount_AnAccountId_ReadsItsNoteCount()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);

        var count = await connection.GetAccountNoteCount(
            new GetAccountNoteCountArgs(new AccountId(1)),
            cancellationToken: token
        );

        count.ShouldBe(new GetAccountNoteCountDto(new AccountId(1), new NoteCount(2)));
    }

    [Fact]
    public async Task ListAccountMonths_EveryAccount_ReadsTheMarkedColumn()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);

        var months = await connection.ListAccountMonths(cancellationToken: token);

        months.ShouldBe([
            new ListAccountMonthsDto(new AccountId(1), new DateOnly(2024, 1, 31)),
            new ListAccountMonthsDto(new AccountId(2), new DateOnly(2024, 2, 29)),
            new ListAccountMonthsDto(new AccountId(3), new DateOnly(2024, 3, 31)),
        ]);
    }

    // The method reads by ordinal, so the marks, which stay in the columns' names, set the properties' nullability and
    // nothing else.
    [Fact]
    public async Task ListMarkedNames_EveryAccount_ReadsTheNames()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var names = await connection.ListMarkedNames(cancellationToken: TestContext.Current.CancellationToken);

        names.ShouldBe([
            new ListMarkedNamesDto(new AccountId(1), "ALICE"),
            new ListMarkedNamesDto(new AccountId(2), "BOB"),
            new ListMarkedNamesDto(new AccountId(3), "CAROL"),
        ]);
    }

    // The column is read from the string that SqlClient gives, through FromDatabase.
    [Fact]
    public async Task ListNoteKinds_EveryNote_ReadsTheAliasTypeThroughItsConverter()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);

        var kinds = await connection.ListNoteKinds(cancellationToken: token);

        kinds.ShouldBe([
            new ListNoteKindsDto("call", new Email("alice@example.com")),
            new ListNoteKindsDto("email", new Email("alice@example.com")),
            new ListNoteKindsDto("visit", new Email("bob@example.com")),
        ]);
    }

    [Fact]
    public async Task ListReminders_TheQueryOverride_ReadsTimeAsATimeSpan()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var reminders = await connection.ListReminders(cancellationToken: TestContext.Current.CancellationToken);

        reminders.ShouldBe([new ListRemindersDto(1, new TimeSpan(9, 30, 0))]);
    }

    // The parameter is sent as the string that ToDatabase returns, and the column read through FromDatabase.
    [Fact]
    public async Task FindAccountByEmail_Carol_ReadsHerAccount()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);

        var account = await connection.FindAccountByEmail(
            new FindAccountByEmailArgs(new Email("carol@example.com")),
            cancellationToken: token
        );

        account.ShouldBe(new FindAccountByEmailDto(new AccountId(3), "carol", new Email("carol@example.com")));
    }

    [Fact]
    public async Task FindAccountByEmail_NoAccount_ReturnsNull()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var account = await connection.FindAccountByEmail(
            new FindAccountByEmailArgs(new Email("nobody@example.com")),
            cancellationToken: TestContext.Current.CancellationToken
        );

        account.ShouldBeNull();
    }

    // An email of the alias type's whole length is written through ToDatabase and read back through FromDatabase.
    [Fact]
    public async Task AddAccount_EmailOfTheTypesLength_WritesItWhole()
    {
        var token = TestContext.Current.CancellationToken;
        var email = new Email(EmailOfLength(EmailLength));
        await using var connection = EndToEndSources.App(sqlServer);

        await EndToEndSources.RolledBackAsync(
            connection,
            async transaction =>
            {
                var added = await connection.AddAccount(
                    new AddAccountArgs("dave", email, new DateOnly(2024, 4, 1)),
                    transaction: transaction,
                    cancellationToken: token
                );

                added.Email.ShouldBe(email);
                var found = await connection.FindAccountByEmail(
                    new FindAccountByEmailArgs(email),
                    transaction: transaction,
                    cancellationToken: token
                );
                found.ShouldBe(new FindAccountByEmailDto(added.Id, "dave", email));
            },
            token
        );
    }

    // The parameter is described as nvarchar(320), and a converted value longer than that is sent whole, so the server
    // refuses it rather than storing it cut: 2628, which names the column, from SQL Server 2019 in a database of
    // compatibility level 150 or later, as the server's own default makes this one, and 8152 on 2017.
    [Fact]
    public async Task AddAccount_EmailLongerThanTheType_IsRefusedByTheServer()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var thrown = await Should.ThrowAsync<SqlException>(() =>
            EndToEndSources.RolledBackAsync(
                connection,
                transaction =>
                    connection.AddAccount(
                        new AddAccountArgs("dave", new Email(EmailOfLength(EmailLength + 1)), new DateOnly(2024, 4, 1)),
                        transaction: transaction,
                        cancellationToken: token
                    ),
                token
            )
        );

        thrown.Number.ShouldBe(sqlServer.Major >= 15 ? 2628 : 8152);
    }

    // An email of the given length, which ends in @example.com.
    private static string EmailOfLength(int length)
    {
        const string Domain = "@example.com";
        return new string('a', length - Domain.Length) + Domain;
    }
}
