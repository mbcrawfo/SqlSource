using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using NpgsqlTypes;
using Shouldly;
using SqlSource.Testing.EndToEnd;
using SqlSource.Tests.Postgres.EndToEnd.Types;
using Xunit;

namespace SqlSource.Tests.Postgres.EndToEnd;

// What the Dapper recipe of docs/manual/recipes.md (#models-through-dapper) says beyond the generated queries, on the
// data source that the recipe sets up: a parameter that Dapper cannot send goes as a TypedParameter, the recipe's
// sample copied in Types/, or as a string cast in the SQL; a C# enum parameter is sent as its number; a DateTimeOffset
// parameter takes offset 0 alone; and which records Dapper fills.  A test that writes rolls back.
[Collection(UsesPostgres.Name)]
public sealed class DapperRecipeTests(PostgresContainer postgres)
{
    private const int WrittenId = 3;

    // Each value that the recipe sends as a TypedParameter, by a name: the type it names and the value.  The server
    // gives the parameter that type, and the value is read back as it was sent.
    private static readonly Dictionary<string, (string DataTypeName, object? Value)> Typed = new(StringComparer.Ordinal)
    {
        ["oid"] = ("oid", 42u),
        ["inet"] = ("inet", IPAddress.Parse("10.0.0.1")),
        ["int4range"] = ("int4range", new NpgsqlRange<int>(1, true, 6, false)),
        ["timetz"] = ("timetz", new DateTimeOffset(1, 1, 2, 10, 30, 0, TimeSpan.FromHours(2))),
        ["point"] = ("point", new NpgsqlPoint(1.5, -2)),
        ["note_kind"] = ("note_kind", NoteKind.Email),
        ["null"] = ("inet", null),
    };

    // Each cast of the recipe: the SQL, which casts a string parameter to a type that Dapper's text is not, and the
    // parameters, with the value read back as text.
    private static readonly Dictionary<string, (string Sql, object Parameters, string Text)> Casts = new(
        StringComparer.Ordinal
    )
    {
        ["jsonb"] = (
            "SELECT (@payload::jsonb)::text",
            new
            {
                payload = /*lang=json,strict*/
                """{"a": "b"}""",
            }, /*lang=json,strict*/
            """{"a": "b"}"""
        ),
        ["account_status"] = ("SELECT (@status::account_status)::text", new { status = "closed" }, "closed"),
        ["account_status[]"] = (
            "SELECT (@statuses::account_status[])::text",
            new { statuses = new[] { "active", "closed" } },
            "{active,closed}"
        ),
    };

    public static TheoryData<string> TypedCases => [.. Typed.Keys];

    public static TheoryData<string> CastCases => [.. Casts.Keys];

    public static TheoryData<string> LeftOutColumns => AllTypesQueryTests.LeftOutColumns;

    [Theory]
    [MemberData(nameof(TypedCases))]
    public async Task ExecuteScalarAsync_TypedParameter_IsSentAsItsType(string name)
    {
        var (dataTypeName, value) = Typed[name];
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(TestContext.Current.CancellationToken);

        var type = await connection.ExecuteScalarAsync<string>(
            "SELECT format_type(pg_typeof(@value), NULL)",
            new { value = new TypedParameter(dataTypeName, value) }
        );
        var read = await connection.ExecuteScalarAsync<object>(
            "SELECT @value",
            new { value = new TypedParameter(dataTypeName, value) }
        );

        type.ShouldBe(dataTypeName == "timetz" ? "time with time zone" : dataTypeName);
        read.ShouldBe(value);
    }

    [Theory]
    [MemberData(nameof(CastCases))]
    public async Task ExecuteScalarAsync_StringCastInTheSql_IsReadAsTheType(string name)
    {
        var (sql, parameters, text) = Casts[name];
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(TestContext.Current.CancellationToken);

        var read = await connection.ExecuteScalarAsync<string>(sql, parameters);

        read.ShouldBe(text);
    }

    // Each column whose parameter Dapper cannot send, by the recipe's remedy: a string cast in the SQL to the column's
    // type, and any other value as a TypedParameter that names it.  The value of row 1 is inserted in a transaction
    // that is rolled back, and the column reads back as row 1's, both as text.
    [Theory]
    [MemberData(nameof(LeftOutColumns))]
    public async Task ExecuteScalarAsync_LeftOutColumnByTheRecipe_IsWritten(string property)
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);
        var full = (await connection.QueryAsync<AllTypesDto>(AllTypesQueries.ListAllTypes)).Single(static row =>
            row.Id == AllTypesRows.FullRowId
        );
        var value = typeof(AllTypesDto).GetProperty(property).ShouldNotBeNull(property).GetValue(full);
        var column = AllTypesQueryTests.ColumnOf(property);
        var (typeName, sqlType) = await connection.QuerySingleAsync<(string, string)>(
            """
            SELECT t.typname, format_type(a.atttypid, a.atttypmod)
            FROM pg_attribute AS a
            JOIN pg_type AS t ON t.oid = a.atttypid
            WHERE a.attrelid = 'all_types'::regclass AND a.attname = @column
            """,
            new { column }
        );
        var cast = value is string or string[];
        var sql = cast
            ? $"INSERT INTO all_types (id, {column}) VALUES (@id, @value::{sqlType}) RETURNING {column}::text"
            : $"INSERT INTO all_types (id, {column}) VALUES (@id, @value) RETURNING {column}::text";
        object parameters = cast
            ? new { id = WrittenId, value }
            : new { id = WrittenId, value = new TypedParameter(typeName, value) };

        await using var transaction = await connection.BeginTransactionAsync(token);
        try
        {
            var written = await connection.ExecuteScalarAsync<string>(sql, parameters, transaction);
            var expected = await connection.ExecuteScalarAsync<string>(
                $"SELECT {column}::text FROM all_types WHERE id = @id",
                new { id = AllTypesRows.FullRowId },
                transaction
            );

            _ = written.ShouldNotBeNull(property);
            written.ShouldBe(expected, property);
        }
        finally
        {
            // A parameter that Npgsql fails to write breaks the connection and disposes the transaction, whose
            // rollback would then throw over that error.
            if (connection.State == ConnectionState.Open)
            {
                await transaction.RollbackAsync(token);
            }
        }
    }

    // The server compares the column with an integer, which shows that Dapper sent the enum's number.
    [Fact]
    public async Task QueryAsync_CSharpEnumParameterAgainstTheEnum_IsRefusedAsANumber()
    {
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(TestContext.Current.CancellationToken);

        var error = await Should.ThrowAsync<PostgresException>(() =>
            connection.QueryAsync<int>("SELECT id FROM notes WHERE kind = @kind", new { kind = NoteKind.Email })
        );

        error.SqlState.ShouldBe(PostgresErrorCodes.UndefinedFunction, error.MessageText);
        error.MessageText.ShouldContain("note_kind = integer");
    }

    [Fact]
    public async Task ExecuteScalarAsync_NpgsqlParameterAsAValue_IsRefused()
    {
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(TestContext.Current.CancellationToken);

        var error = await Should.ThrowAsync<NotSupportedException>(() =>
            connection.ExecuteScalarAsync<object>(
                "SELECT @value",
                new { value = new NpgsqlParameter { DataTypeName = "oid", Value = 42u } }
            )
        );

        error.Message.ShouldContain(typeof(NpgsqlParameter).FullName!);
    }

    [Fact]
    public async Task ExecuteScalarAsync_DateTimeOffsetParameter_TakesOffsetZeroAlone()
    {
        var local = new DateTimeOffset(2024, 1, 1, 9, 0, 0, TimeSpan.FromHours(2));
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(TestContext.Current.CancellationToken);

        _ = await Should.ThrowAsync<ArgumentException>(() =>
            connection.ExecuteScalarAsync<string>("SELECT format_type(pg_typeof(@value), NULL)", new { value = local })
        );
        var type = await connection.ExecuteScalarAsync<string>(
            "SELECT format_type(pg_typeof(@value), NULL)",
            new { value = local.ToUniversalTime() }
        );

        type.ShouldBe("timestamp with time zone");
    }

    // Npgsql gives a timestamptz of infinity as DateTime.MaxValue and one of -infinity as DateTime.MinValue, of no
    // kind.  The handler reads both at offset 0, where a local offset would put one of them out of range.
    [Theory]
    [InlineData("infinity")]
    [InlineData("-infinity")]
    public async Task QuerySingleAsync_TimestamptzInfinity_ReadsAtOffsetZero(string value)
    {
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(TestContext.Current.CancellationToken);

        var row = await connection.QuerySingleAsync<Instant>("SELECT @value::timestamptz AS at", new { value });

        row.At.Offset.ShouldBe(TimeSpan.Zero);
        row.At.UtcDateTime.ShouldBe(value == "infinity" ? DateTime.MaxValue : DateTime.MinValue);
    }

    [Fact]
    public async Task QueryAsync_RecordWithRangesAndHstore_IsFilled()
    {
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(TestContext.Current.CancellationToken);

        var rows = (
            await connection.QueryAsync<RangesAndHstore>(
                "SELECT int4range_value, int4multirange_value, hstore_value FROM all_types ORDER BY id"
            )
        ).ToList();

        rows.Count.ShouldBe(2);
        rows[0].Int4rangeValue.ShouldBe(new NpgsqlRange<int>(1, true, 10, false));
        rows[0]
            .Int4multirangeValue.ShouldBe([
                new NpgsqlRange<int>(1, true, 5, false),
                new NpgsqlRange<int>(10, true, 20, false),
            ]);
        rows[0]
            .HstoreValue.ShouldBe(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["k1"] = "v1",
                    ["k2"] = "Zoë",
                    ["k3"] = null,
                },
                ignoreOrder: true
            );
        rows[1].ShouldBe(new RangesAndHstore(null, null, null));
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Dapper makes it.")]
    private sealed record Instant(DateTimeOffset At);

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Dapper makes it.")]
    private sealed record RangesAndHstore(
        NpgsqlRange<int>? Int4rangeValue,
        NpgsqlRange<int>[]? Int4multirangeValue,
        Dictionary<string, string?>? HstoreValue
    );
}
