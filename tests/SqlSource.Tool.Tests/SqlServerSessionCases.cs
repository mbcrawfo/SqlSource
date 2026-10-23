using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.SqlServer;
using static SqlSource.Tool.Tests.SqlServerScript;

namespace SqlSource.Tool.Tests;

// What the classes of the SQL Server session's tests share: the rows the server answers for an int and an nvarchar(100)
// parameter, the answer of a statement with no rows, the name column of dbo.users, and a call as the capture keys it.
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

    public static (string Method, string Request) Call<TRequest>(string method, TRequest request) =>
        (method, CaptureJson.KeyOf(CaptureJson.ToElement(request)));
}
