using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;
using static SqlSource.Tool.Testing.DescribeRequests;

namespace SqlSource.DatabaseTests.SqlServer.Describer;

// A CLR type of a user's own assembly, dbo.Grade of the database "user_clr", to which the server gives no system type
// name on any image: a column, a parameter, a set operator, a view and an expression of it are described, named by
// the user type, and its nullability follows the rule of every CLR type, since its Parse answers its Null for 'NULL'.
[Collection(UsesSqlServer.Name)]
public sealed class UserClrTypeTests(SqlServerDatabases sqlServer) : IDisposable
{
    // The server names the version 0.0.0.0, whatever the build's is.
    private const string AssemblyQualifiedName =
        "SqlSource.DatabaseTests.SqlServer.ClrType.Grade, SqlSource.DatabaseTests.SqlServer.ClrType, "
        + "Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";

    // dbo.Grade: Format.Native over a bool and an int, so five bytes.
    private static readonly SqlServerType Grade = new(
        "dbo.Grade",
        5,
        0,
        0,
        new SqlServerUserType("dbo", "Grade", AssemblyQualifiedName)
    );

    private readonly FixtureProjects _fixtures = new();

    public void Dispose() => _fixtures.Dispose();

    // A column of the type that the server names as a stored column holds that column's values, and so does a set
    // operator of two of them and a view's column that passes one on.
    public static TheoryData<string> Unconverted =>
        [
            "SELECT grade AS c FROM dbo.grades",
            "SELECT grade AS c FROM dbo.grades UNION ALL SELECT grade FROM dbo.grades",
            "SELECT grade AS c FROM dbo.grade_view",
        ];

    // The type's Parse answers its Null for 'NULL', and the server answers these not null: each is nullable with the
    // nullableSource heuristic, and the first gives a NULL row.  The rule cannot clear an expression, which holds none.
    public static TheoryData<string, bool> NotCleared =>
        new()
        {
            { "SELECT grade AS c FROM dbo.grades UNION ALL SELECT N'NULL'", true },
            { "SELECT ISNULL(maybe, grade) AS c FROM dbo.grades", false },
        };

    // What the server answers nullable stays so.
    public static TheoryData<string> ServerNullable =>
        [
            "SELECT maybe AS c FROM dbo.grades",
            "SELECT maybe AS c FROM dbo.grade_view",
            "SELECT CAST(N'7' AS dbo.Grade) AS c",
            "SELECT ISNULL(maybe, N'NULL') AS c FROM dbo.grades",
            "SELECT grade AS c FROM dbo.grades UNION ALL SELECT maybe FROM dbo.grades",
        ];

    [Fact]
    public async Task DescribeAsync_ColumnsOfTheType_AreNamedByTheUserType()
    {
        var description = await ShouldDescribeAsync(
            Of(SqlDialectRules.SqlServer, "SELECT grade, maybe FROM dbo.grades")
        );

        description
            .Columns.ShouldNotBeNull()
            .Select(static column => (column.Name, column.Type, column.Nullable))
            .ShouldBe([("grade", Grade, false), ("maybe", Grade, true)]);
    }

    [Theory]
    [MemberData(nameof(Unconverted))]
    public async Task DescribeAsync_ColumnUnconverted_NotNullAndNoNullRow(string sql)
    {
        await ShouldDescribeColumnAsync(sql, false, SidecarValues.NullableSource.Server);
        (await NullRowsAsync(sql)).ShouldBe(0, sql);
    }

    [Theory]
    [MemberData(nameof(NotCleared))]
    public async Task DescribeAsync_NotCleared_NullableWithTheHeuristic(string sql, bool hasANullRow)
    {
        await ShouldDescribeColumnAsync(sql, true, SidecarValues.NullableSource.Heuristic);
        (await NullRowsAsync(sql) > 0).ShouldBe(hasANullRow, sql);
    }

    [Theory]
    [MemberData(nameof(ServerNullable))]
    public Task DescribeAsync_ServerNullable_StaysNullable(string sql) =>
        ShouldDescribeColumnAsync(sql, true, SidecarValues.NullableSource.Server);

    // The server infers the type for a comparison with a column of it and for an INSERT into one.
    [Theory]
    [InlineData("SELECT id FROM dbo.grades WHERE grade = @g")]
    [InlineData("INSERT INTO dbo.grades (id, grade) VALUES (3, @g)")]
    public async Task DescribeAsync_InferredParameter_IsTheType(string sql)
    {
        var description = await ShouldDescribeAsync(Of(SqlDialectRules.SqlServer, sql));

        description.Parameters.ShouldBe([new DescribedParameter("g", Grade, SidecarValues.TypeSource.Inferred)]);
    }

    // A -- param: marker declares the type by its schema and name, which columns then declares in brackets.
    [Fact]
    public async Task DescribeAsync_DeclaredParameter_IsTheType()
    {
        var description = await ShouldDescribeAsync(
            Of(SqlDialectRules.SqlServer, "SELECT @g AS given FROM dbo.grades", ("g", "dbo.Grade"))
        );

        description.Parameters.ShouldBe([new DescribedParameter("g", Grade, SidecarValues.TypeSource.Declared)]);
        var column = description.Columns.ShouldNotBeNull().ShouldHaveSingleItem();
        (column.Type, column.Nullable).ShouldBe((Grade, true));
    }

    // The run describes a file with a query of the type and one without, and writes the sidecar of both.
    [Fact]
    public async Task Describe_FileWithAQueryOfTheType_WritesItsSidecar()
    {
        var project = _fixtures.Copy("SqlServer");
        _ = _fixtures.WriteFile(
            "SqlServer/Queries/Orders.sql",
            "-- name: ListGrades\nSELECT id, grade, maybe FROM dbo.grades;\n\n"
                + "-- name: ListIds\nSELECT id FROM dbo.grades;\n"
        );
        using var run = CliRun.Real(Path.GetDirectoryName(project)!);
        run.ReleasedDescribers = true;

        var result = await run.RunAsync(
            "describe",
            project,
            "--connection",
            SqlServerDatabases.Database + "=" + sqlServer.ConnectionStringOf(UserClrDatabase.Name)
        );

        result.ExitCode.ShouldBe(0, result.Error);
        result.Out.ShouldBe("fixtures (mssql): 2 described, 0 skipped, 0 failed\n");
        var entries = SidecarFiles.Read(_fixtures.PathOf("SqlServer/Queries/Orders.sql")).Queries;
        entries.Select(static entry => entry.Name).ShouldBe(["ListGrades", "ListIds"]);
        entries[0].Columns.ShouldNotBeNull()[1].Type.ShouldBe(Grade);
    }

    private Task<QueryDescription> ShouldDescribeAsync(DescribeRequest request) =>
        Sessions.ShouldDescribeAsync(sqlServer.ConnectionStringOf(UserClrDatabase.Name), request);

    private async Task ShouldDescribeColumnAsync(string sql, bool nullable, string source)
    {
        var description = await ShouldDescribeAsync(Of(SqlDialectRules.SqlServer, sql));

        var column = description.Columns.ShouldNotBeNull().ShouldHaveSingleItem();
        (column.Type, column.Nullable, column.NullableSource).ShouldBe((Grade, nullable, source), sql);
    }

    // The rows of the query whose c is NULL, read as the query returns them: a filter around it can let the optimizer
    // fold the expression away.
    private async Task<int> NullRowsAsync(string sql)
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = sqlServer.SourceOf(UserClrDatabase.Name);
        await using var command = source.CreateCommand(sql);
        await using var reader = await command.ExecuteReaderAsync(token);
        var nulls = 0;
        while (await reader.ReadAsync(token))
        {
            nulls += await reader.IsDBNullAsync(0, token) ? 1 : 0;
        }

        return nulls;
    }
}
