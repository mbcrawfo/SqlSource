using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Shouldly;
using SqlSource.Tests.Postgres.EndToEnd.Types;
using Xunit;

namespace SqlSource.Tests.Postgres.EndToEnd;

// The queries of Queries/Notes.sql, run on the seed of "app" into their output models: the enum note_kind is read as
// the C# enum NoteKind, which the data source maps, and the domain email as the record struct Email, by its handler.
[Collection(UsesPostgres.Name)]
public sealed class NoteQueryTests(PostgresContainer postgres)
{
    [Fact]
    public async Task QueryAsync_ListAccountNotes_AccountWithNoNote_HasNulls()
    {
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(TestContext.Current.CancellationToken);

        var notes = await connection.QueryAsync<ListAccountNotesDto>(NoteQueries.ListAccountNotes);

        notes.ShouldBe([
            new ListAccountNotesDto(1, "alice", 1, NoteKind.Call, "Asked for a larger plan."),
            new ListAccountNotesDto(1, "alice", 2, NoteKind.Email, "Sent the quote."),
            new ListAccountNotesDto(2, "bob", 3, NoteKind.Visit, "Closed the account on site."),
            new ListAccountNotesDto(3, "carol", null, null, null),
        ]);
    }

    [Fact]
    public async Task QueryAsync_CountAccountNotes_ReadsTheCounts()
    {
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(TestContext.Current.CancellationToken);

        var counts = await connection.QueryAsync<CountAccountNotesDto>(NoteQueries.CountAccountNotes);

        counts.ShouldBe([
            new CountAccountNotesDto(1, 2),
            new CountAccountNotesDto(2, 1),
            new CountAccountNotesDto(3, 0),
        ]);
    }

    [Fact]
    public async Task QueryAsync_ListNoteKinds_ReadsTheEnumAndTheDomain()
    {
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(TestContext.Current.CancellationToken);

        var kinds = await connection.QueryAsync<ListNoteKindsDto>(NoteQueries.ListNoteKinds);

        kinds.ShouldBe([
            new ListNoteKindsDto(NoteKind.Call, new Email("alice@example.com")),
            new ListNoteKindsDto(NoteKind.Email, new Email("alice@example.com")),
            new ListNoteKindsDto(NoteKind.Visit, new Email("bob@example.com")),
        ]);
    }

    // A mark stays in the column's name, which Dapper matches to no property: the marks are for a reader that reads by
    // ordinal, as this one does.
    [Fact]
    public async Task ExecuteReaderAsync_ListMarkedNames_ReadsByOrdinalIntoTheRecord()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = NoteQueries.ListMarkedNames;

        var names = new List<ListMarkedNamesDto>();
        await using (var reader = await command.ExecuteReaderAsync(token))
        {
            reader.GetName(0).ShouldBe("id?");
            reader.GetName(1).ShouldBe("initcap_name!");
            while (await reader.ReadAsync(token))
            {
                names.Add(
                    new ListMarkedNamesDto(
                        await reader.GetFieldValueAsync<int?>(0, token),
                        await reader.GetFieldValueAsync<string>(1, token)
                    )
                );
            }
        }

        names.ShouldBe([
            new ListMarkedNamesDto(1, "Alice"),
            new ListMarkedNamesDto(2, "Bob"),
            new ListMarkedNamesDto(3, "Carol"),
        ]);
    }
}
