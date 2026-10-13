using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// A query's columns against a real server: what only a server can show, the row description of a statement and
// what Npgsql makes of it.
[Collection(UsesPostgres.Name)]
public sealed class ColumnTests(PostgresContainer postgres)
{
    private async Task<QueryDescription> ShouldDescribeAsync(string sql, IDescribeExchange? exchange = null)
    {
        var described = await Sessions.DescribeAsync(postgres.ConnectionString, Requests.Of(sql), exchange);

        described.Failure.ShouldBeNull(described.Failure?.Descriptor.Id);
        return described.Value.ShouldNotBeNull();
    }

    private async Task<SidecarColumn[]> ColumnsOfAsync(string sql) =>
        [.. (await ShouldDescribeAsync(sql)).Columns.ShouldNotBeNull()];

    private static string[] TypeNamesOf(IEnumerable<SidecarColumn> columns) =>
        [.. columns.Select(static column => column.Type.Name)];

    // One value from the server, on a connection of the test's own.
    private async Task<object?> ScalarAsync(string sql)
    {
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var command = source.CreateCommand(sql);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }

    private async Task<int> ServerMajorAsync() =>
        int.Parse((string)(await ScalarAsync("SHOW server_version_num"))!, CultureInfo.InvariantCulture) / 10000;

    [Fact]
    public async Task Describe_Select_IsRowsWithItsColumnsInOrder()
    {
        var description = await ShouldDescribeAsync("SELECT code, at_local, span, label FROM samples WHERE id = @id");

        description.ResultKind.ShouldBe(SidecarResultKind.Rows);
        var columns = description.Columns.ShouldNotBeNull();
        columns.Select(column => column.Ordinal).ShouldBe([0, 1, 2, 3]);
        columns.Select(column => column.Name).ShouldBe(["code", "at_local", "span", "label"]);
        TypeNamesOf(columns).ShouldBe(["character(2)", "timestamp(3) without time zone", "interval", "text"]);
        description.Parameters.ShouldHaveSingleItem().Name.ShouldBe("id");
    }

    // A cast in a select list keeps its modifier in the row description.  Each name is what format_type gives the
    // type, which Describe_EveryColumnOfATable_IsSpelledAsFormatTypeSpellsIt holds against the server for a column.
    [Theory]
    [InlineData("numeric(18,2)", "numeric(18,2)")]
    [InlineData("numeric(18,0)", "numeric(18,0)")]
    [InlineData("numeric(5)", "numeric(5,0)")]
    [InlineData("numeric", "numeric")]
    [InlineData("varchar(100)", "character varying(100)")]
    [InlineData("varchar", "character varying")]
    [InlineData("char(2)", "character(2)")]
    [InlineData("char", "character(1)")]
    [InlineData("bpchar", "bpchar")]
    [InlineData("bit(3)", "bit(3)")]
    [InlineData("bit", "bit(1)")]
    [InlineData("varbit(5)", "bit varying(5)")]
    [InlineData("varbit", "bit varying")]
    [InlineData("time(4)", "time(4) without time zone")]
    [InlineData("timetz(2)", "time(2) with time zone")]
    [InlineData("timestamp(3)", "timestamp(3) without time zone")]
    [InlineData("timestamptz(0)", "timestamp(0) with time zone")]
    [InlineData("timestamptz", "timestamp with time zone")]
    [InlineData("interval(3)", "interval(3)")]
    [InlineData("interval", "interval")]
    public async Task Describe_CastWithAModifier_IsSpelledAsFormatTypeSpellsIt(string type, string expected) =>
        (await ColumnsOfAsync($"SELECT NULL::{type} AS c")).ShouldHaveSingleItem().Type.Name.ShouldBe(expected);

    // What the describer does not keep, and docs/tech-debt records: the fields of an interval, and the modifier of
    // an array's element.
    [Theory]
    [InlineData("interval year to month", "interval")]
    [InlineData("interval day to second(3)", "interval(3)")]
    [InlineData("varchar(10)[]", "character varying[]")]
    [InlineData("numeric(6,2)[]", "numeric[]")]
    public async Task Describe_CastWhoseModifierIsNotKept_IsSpelledWithoutIt(string type, string expected) =>
        (await ColumnsOfAsync($"SELECT NULL::{type} AS c")).ShouldHaveSingleItem().Type.Name.ShouldBe(expected);

    [Fact]
    public async Task Describe_NumericWithANegativeScale_HasIt()
    {
        Assert.SkipWhen(await ServerMajorAsync() < 15, "PostgreSQL 14 has no negative scale.");

        var column = (await ColumnsOfAsync("SELECT NULL::numeric(2,-3) AS c")).ShouldHaveSingleItem();

        var type = column.Type.ShouldBeOfType<PostgresType>();
        type.Name.ShouldBe("numeric(2,-3)");
        type.Precision.ShouldBe(2);
        type.Scale.ShouldBe(-3);
    }

    [Fact]
    public async Task Describe_Literals_AreTextAndNamedByTheServer()
    {
        var columns = await ColumnsOfAsync("SELECT 'x', NULL, 1");

        columns.Select(column => column.Name).ShouldBe(["?column?", "?column?", "?column?"]);
        TypeNamesOf(columns).ShouldBe(["text", "text", "integer"]);
    }

    [Fact]
    public async Task Describe_TwoColumnsWithOneName_KeepsBoth()
    {
        var columns = await ColumnsOfAsync("SELECT 1 AS id, 'a'::text AS id");

        columns.Select(column => (column.Ordinal, column.Name)).ShouldBe([(0, "id"), (1, "id")]);
        TypeNamesOf(columns).ShouldBe(["integer", "text"]);
    }

    // A suffix is the generator's to read: the describer records the name the server returned.
    [Fact]
    public async Task Describe_SuffixNames_AreRecordedAsReturned() =>
        (await ColumnsOfAsync("SELECT deleted_at AS \"deleted_at!\", name AS \"name?\" FROM users"))
            .Select(column => column.Name)
            .ShouldBe(["deleted_at!", "name?"]);

    // The statement is described and never run.
    [Fact]
    public async Task Describe_InsertReturning_IsRowsAndInsertsNothing()
    {
        var description = await ShouldDescribeAsync(
            "INSERT INTO orders (user_id, total) VALUES (@user_id, @total) RETURNING id, placed_at"
        );

        description.ResultKind.ShouldBe(SidecarResultKind.Rows);
        description.Columns.ShouldNotBeNull().Select(column => column.Name).ShouldBe(["id", "placed_at"]);
        (await ScalarAsync("SELECT count(*) FROM orders")).ShouldBe(0L);
    }

    [Fact]
    public async Task Describe_UpdateWithoutReturning_IsNone()
    {
        var description = await ShouldDescribeAsync("UPDATE users SET name = @name WHERE id = @id");

        description.ResultKind.ShouldBe(SidecarResultKind.None);
        description.Columns.ShouldBeNull();
        description.Plan.ShouldBeNull();
        description.TableMatch.ShouldBeNull();
        description.Parameters.Select(parameter => parameter.Name).ShouldBe(["name", "id"]);
    }

    [Fact]
    public async Task Describe_QueryWithoutATable_AsksColumnsAfterPrepareAndTypesOnce()
    {
        var capture = new DescribeCapture("postgres");

        _ = await ShouldDescribeAsync(
            "SELECT @p::int AS a, 'x' AS b",
            new CapturingExchange(LiveExchange.Instance, capture)
        );

        capture.Calls.Select(call => call.Method).ShouldBe(["open", "prepare", "columns", "types", "deallocate"]);
    }
}
