using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Fixtures;
using static SqlSource.Tool.Testing.DescribeRequests;
using static SqlSource.Tool.Tests.SqlServerScript;

namespace SqlSource.Tool.Tests;

// What the classes of the SQL Server session's tests share: the rows the server answers for an int and an nvarchar(100)
// parameter, the answer of a statement with no rows, the columns of dbo.users, a call as the capture keys it, the
// open of a session, and the failure of a describe whose "columns" or "table" answers as the test gives.
internal static class SqlServerSessionCases
{
    public static readonly SqlServerParameterRow IntRow = new("@x", "int", 4, 10, 0, null, null, null);

    public static readonly SqlServerParameterRow Name100Row = new("@x", "nvarchar(100)", 200, 0, 0, null, null, null);

    public static readonly SqlServerType Name100 = new("nvarchar(100)", 200, 0, 0, null);

    // The answer of "columns" for a statement with no rows, for a test of what is asked before it.
    public static readonly SqlServerColumnsAnswer NoRows = new([], null, null);

    public static readonly SqlServerColumnRow NameColumn = IdColumn with
    {
        ColumnOrdinal = 2,
        Name = "name",
        SystemTypeName = "nvarchar(100)",
        MaxLength = 200,
        Precision = 0,
        SourceColumn = "name",
        IsIdentityColumn = false,
    };

    public static readonly SqlServerColumnRow NicknameColumn = NameColumn with
    {
        ColumnOrdinal = 3,
        Name = "nickname",
        IsNullable = true,
        SystemTypeName = "nvarchar(50)",
        MaxLength = 100,
        SourceColumn = "nickname",
    };

    // The failure of a describe of the SQL, which has no parameter, when "columns" answers as given.
    public static async Task<DescribeFailure> ColumnsAnsweringAsync(string sql, SqlServerColumnsAnswer answer)
    {
        var script = new SqlServerScript()
            .Add("parameters", new SqlServerParametersRequest(sql, null), new SqlServerParametersAnswer([], null))
            .Add("columns", new SqlServerColumnsRequest(sql, null), answer);

        var (described, _) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, sql));

        return described.Failure.ShouldNotBeNull();
    }

    // The failure of a describe of the SQL, whose one column is dbo.users.id, when "table" answers as given.
    public static async Task<DescribeFailure> TableAnsweringAsync(string sql, SqlServerColumnsAnswer answer)
    {
        var script = new SqlServerScript()
            .Add("parameters", new SqlServerParametersRequest(sql, null), new SqlServerParametersAnswer([], null))
            .Add(
                "columns",
                new SqlServerColumnsRequest(sql, null),
                new SqlServerColumnsAnswer([IdColumn, Rowstat], null, null)
            )
            .Add("table", new SqlServerTableRequest("dbo", "users"), answer);

        var (described, _) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, sql));

        return described.Failure.ShouldNotBeNull();
    }

    public static (string Method, string Request) Call<TRequest>(string method, TRequest request) =>
        (method, CaptureJson.KeyOf(CaptureJson.ToElement(request)));

    public static Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(IDescribeExchange exchange) =>
        new SqlServerDescriber().OpenAsync(Database, FixtureReplay.Connection, exchange);
}
