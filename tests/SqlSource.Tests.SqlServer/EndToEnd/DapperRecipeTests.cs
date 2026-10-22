using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlTypes;
using Shouldly;
using SqlSource.Testing.EndToEnd;
using SqlSource.Tests.SqlServer.EndToEnd.Types;
using Xunit;

namespace SqlSource.Tests.SqlServer.EndToEnd;

// What the Dapper recipe of docs/manual/recipes.md (#models-through-dapper) says for SQL Server beyond the generated
// queries, on the connection that the recipe sets up: the type each C# parameter is sent as; a DateTime for a
// datetime2 goes as a TypedParameter, the recipe's sample copied in Types/, or with its DbType in DynamicParameters; a
// SqlVector<float>, which Dapper refuses, goes as a TypedParameter; a string is written to json as it is; DateOnly,
// TimeOnly and an alias type mapped to a struct are read through their handlers; a mark in an alias is not matched; and
// a command on a connection in a transaction is given the transaction.  A test that writes rolls back.
[Collection(UsesSqlServer.Name)]
public sealed class DapperRecipeTests(SqlServerContainer sqlServer)
{
    private const int WrittenId = 3;

    // Each C# type of a parameter, by its name: the parameters, whose property "value" has that type, and the base type
    // that the server says Dapper sent it as.
    private static readonly Dictionary<string, (object Parameters, string BaseType)> Sent = new(StringComparer.Ordinal)
    {
        ["DateTime"] = (new { value = new DateTime(2024, 3, 15, 13, 45, 30, DateTimeKind.Unspecified) }, "datetime"),
        ["DateOnly"] = (new { value = new DateOnly(2024, 3, 15) }, "date"),
        ["TimeOnly"] = (new { value = new TimeOnly(13, 45, 30) }, "time"),
        ["TimeSpan"] = (new { value = new TimeSpan(13, 45, 30) }, "time"),
        ["DateTimeOffset"] = (
            new { value = new DateTimeOffset(2024, 3, 15, 13, 45, 30, TimeSpan.FromHours(2)) },
            "datetimeoffset"
        ),
        ["string"] = (new { value = "Grüße" }, "nvarchar"),
    };

    public static TheoryData<string> SentCases => [.. Sent.Keys];

    [Theory]
    [MemberData(nameof(SentCases))]
    public async Task ExecuteScalarAsync_Parameter_IsSentAsItsType(string name)
    {
        var (parameters, baseType) = Sent[name];
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var sent = await connection.ExecuteScalarAsync<string>(
            "SELECT CAST(SQL_VARIANT_PROPERTY(@value, 'BaseType') AS nvarchar(128))",
            parameters
        );

        sent.ShouldBe(baseType);
    }

    // A DateTime sent as datetime keeps 1/300 of a second: the column of datetime2 reads back another value.
    [Fact]
    public async Task ExecuteScalarAsync_DateTimeIntoDatetime2_LosesItsDigits()
    {
        var full = await FullRowAsync();

        var written = await WriteAsync(
            "all_types",
            "datetime2_value",
            new { id = WrittenId, value = full.Datetime2Value }
        );

        written.ShouldBe(new DateTime(2024, 3, 15, 13, 45, 30, DateTimeKind.Unspecified).AddTicks(1233333));
        written.ShouldNotBe(full.Datetime2Value);
    }

    [Fact]
    public async Task ExecuteScalarAsync_Datetime2ByTypedParameter_IsWrittenExactly()
    {
        var full = await FullRowAsync();

        var written = await WriteAsync(
            "all_types",
            "datetime2_value",
            new { id = WrittenId, value = new TypedParameter(SqlDbType.DateTime2, full.Datetime2Value) }
        );

        written.ShouldBe(full.Datetime2Value);
    }

    // The input model is the template of the parameters, and the one added with its DbType takes its place.
    [Fact]
    public async Task ExecuteScalarAsync_Datetime2ByDbTypeOverTheModel_IsWrittenExactly()
    {
        var full = await FullRowAsync();
        var parameters = new DynamicParameters(new { id = WrittenId, value = full.Datetime2Value });
        parameters.Add("value", full.Datetime2Value, DbType.DateTime2);

        var written = await WriteAsync("all_types", "datetime2_value", parameters);

        written.ShouldBe(full.Datetime2Value);
    }

    [Fact]
    public async Task ExecuteScalarAsync_SqlVectorParameter_IsRefusedByDapper()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var error = await Should.ThrowAsync<NotSupportedException>(() =>
            connection.ExecuteScalarAsync<object>(
                "SELECT @value",
                new { value = new SqlVector<float>(new[] { 1.5f, -2.25f, 0.5f }) }
            )
        );

        error.Message.ShouldContain("SqlVector");
    }

    [Fact]
    public async Task ExecuteScalarAsync_VectorByTypedParameter_IsWritten()
    {
        sqlServer.SkipBelow(17, "SQL Server before 2025 has no vector.");
        float[] elements = [1.5f, -2.25f, 0.5f];

        var written = await WriteAsync(
            "new_types",
            "vector_value",
            new { id = WrittenId, value = new TypedParameter(SqlDbType.Vector, new SqlVector<float>(elements)) }
        );

        written.ShouldBeOfType<SqlVector<float>>().Memory.ToArray().ShouldBe(elements);
    }

    // Dapper sends a string as nvarchar, which the server converts to json: no remedy is needed.
    [Fact]
    public async Task ExecuteScalarAsync_StringIntoJson_IsWritten()
    {
        sqlServer.SkipBelow(17, "SQL Server before 2025 has no json.");
        const string Json = /*lang=json,strict*/
            """{"name":"Zoë","tags":["a","b"]}""";

        var written = await WriteAsync("new_types", "json_value", new { id = WrittenId, value = Json });

        written.ShouldBe(Json);
    }

    [Fact]
    public async Task QuerySingleAsync_TypeOverrideTypes_AreReadThroughTheirHandlers()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var row = await connection.QuerySingleAsync<Overridden>(
            """
            SELECT a.opened_on, n.remind_at, a.email
            FROM dbo.notes AS n
            JOIN dbo.accounts AS a ON a.id = n.account_id
            WHERE n.id = @id
            """,
            new { id = 1 }
        );

        row.ShouldBe(new Overridden(new DateOnly(2024, 1, 1), new TimeOnly(9, 30), new Email("alice@example.com")));
    }

    // A mark stays in the column's name, and a record's constructor matches the columns by name.
    [Fact]
    public async Task QueryAsync_ColumnWithAMark_IsNotMatched()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var error = await Should.ThrowAsync<InvalidOperationException>(() =>
            connection.QueryAsync<ListMarkedNamesDto>(NoteQueries.ListMarkedNames)
        );

        error.Message.ShouldContain("upper_name!");
    }

    [Fact]
    public async Task QueryAsync_WithoutTheTransactionOfTheConnection_IsRefused()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        try
        {
            var error = await Should.ThrowAsync<InvalidOperationException>(() =>
                connection.QueryAsync<int>("SELECT id FROM dbo.accounts")
            );

            error.Message.ShouldContain("transaction");
            (await connection.QueryAsync<int>("SELECT id FROM dbo.accounts", transaction: transaction)).ShouldBe([
                1,
                2,
                3,
            ]);
        }
        finally
        {
            await transaction.RollbackAsync(token);
        }
    }

    // Row 1 of all_types, as the read side gives it.
    private async Task<AllTypesDto> FullRowAsync()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return (await connection.QueryAsync<AllTypesDto>(AllTypesQueries.ListAllTypes)).Single(static row =>
            row.Id == SqlServerAllTypesRows.FullRowId
        );
    }

    // Inserts a row of the table with the parameters @id and @value in the column, in a transaction that is rolled
    // back, and gives the value the server wrote.
    private async Task<object> WriteAsync(string table, string column, object parameters)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        try
        {
            var written = await connection.ExecuteScalarAsync<object>(
                $"INSERT INTO dbo.{table} (id, {column}) OUTPUT INSERTED.{column} VALUES (@id, @value)",
                parameters,
                transaction
            );
            return written.ShouldNotBeNull(column);
        }
        finally
        {
            await transaction.RollbackAsync(token);
        }
    }

    private sealed record Overridden(DateOnly OpenedOn, TimeOnly RemindAt, Email Email);
}
