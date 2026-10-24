using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Shouldly;
using SqlSource.Tests.SqlServer.EndToEnd.Types;
using Xunit;

namespace SqlSource.Tests.SqlServer.EndToEnd;

// The queries of Queries/Notes.sql, run on the seed of "app" into their output models: the alias type dbo.Email is
// read as the record struct Email, by its handler, and time as a TimeOnly, by the file's type override, or as a
// TimeSpan, by a query's.
[Collection(UsesSqlServer.Name)]
public sealed class NoteQueryTests(SqlServerContainer sqlServer)
{
    [Fact]
    public async Task QueryAsync_ListAccountNotesOfAnAccountWithNoNote_HasNulls()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var notes = await connection.QueryAsync<ListAccountNotesDto>(NoteQueries.ListAccountNotes);

        notes.ShouldBe([
            new ListAccountNotesDto(1, "alice", 1, "call", "Asked for a larger plan.", new TimeOnly(9, 30)),
            new ListAccountNotesDto(1, "alice", 2, "email", "Sent the quote.", null),
            new ListAccountNotesDto(2, "bob", 3, "visit", "Closed the account on site.", null),
            new ListAccountNotesDto(3, "carol", null, null, null, null),
        ]);
    }

    [Fact]
    public async Task QueryAsync_CountAccountNotes_ReadsTheCounts()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var counts = await connection.QueryAsync<CountAccountNotesDto>(NoteQueries.CountAccountNotes);

        counts.ShouldBe([
            new CountAccountNotesDto(1, 2),
            new CountAccountNotesDto(2, 1),
            new CountAccountNotesDto(3, 0),
        ]);
    }

    [Fact]
    public async Task QueryAsync_ListAccountMonths_ReadsTheMarkedColumn()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var months = await connection.QueryAsync<ListAccountMonthsDto>(NoteQueries.ListAccountMonths);

        months.ShouldBe([
            new ListAccountMonthsDto(1, new DateOnly(2024, 1, 31)),
            new ListAccountMonthsDto(2, new DateOnly(2024, 2, 29)),
            new ListAccountMonthsDto(3, new DateOnly(2024, 3, 31)),
        ]);
    }

    [Fact]
    public async Task QueryAsync_ListNoteKinds_ReadsTheAliasTypeThroughItsHandler()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var kinds = await connection.QueryAsync<ListNoteKindsDto>(NoteQueries.ListNoteKinds);

        kinds.ShouldBe([
            new ListNoteKindsDto("call", new Email("alice@example.com")),
            new ListNoteKindsDto("email", new Email("alice@example.com")),
            new ListNoteKindsDto("visit", new Email("bob@example.com")),
        ]);
    }

    [Fact]
    public async Task QueryAsync_ListReminders_ReadsTimeAsATimeSpan()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var reminders = await connection.QueryAsync<ListRemindersDto>(NoteQueries.ListReminders);

        reminders.ShouldBe([new ListRemindersDto(1, new TimeSpan(9, 30, 0))]);
    }

    // A mark stays in the column's name, which Dapper matches to no property: the marks are for a reader that reads by
    // ordinal, as this one does.
    [Fact]
    public async Task ExecuteReaderAsync_ListMarkedNames_ReadsByOrdinalIntoTheRecord()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = NoteQueries.ListMarkedNames;

        var names = new List<ListMarkedNamesDto>();
        await using (var reader = await command.ExecuteReaderAsync(token))
        {
            reader.GetName(0).ShouldBe("id?");
            reader.GetName(1).ShouldBe("upper_name!");
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
            new ListMarkedNamesDto(1, "ALICE"),
            new ListMarkedNamesDto(2, "BOB"),
            new ListMarkedNamesDto(3, "CAROL"),
        ]);
    }
}
