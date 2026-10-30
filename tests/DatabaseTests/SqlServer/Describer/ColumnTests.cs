using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Describing.SqlServer.Columns;
using Xunit;
using static SqlSource.Tool.Testing.DescribeRequests;

namespace SqlSource.DatabaseTests.SqlServer.Describer;

// The columns of a query's first result set as the server answers them to SqlClient under browse mode 2, held to the
// spike of phase 6, docs/superpowers/specs/2026-10-20-query-generation-phase-6-sql-server-spike.md, each described in
// a session of its own.  A column is written "name nullable origin": nullable 1 or 0, the origin schema.table.column or
// "-" for none, and a name the server does not give "null".
[Collection(UsesSqlServer.Name)]
public sealed class ColumnTests(SqlServerDatabases sqlServer)
{
    // Every row of the spike's table of columns, and of its table of statements with OUTPUT, that answers columns, but
    // the view, the system tables, the rows with no result set and the table with a trigger, which have tests of their
    // own.  Each expression is aliased, but in "SELECT 1, id", which has a column with no name, "SELECT u.id, o.id",
    // which has two named id, and "FOR JSON PATH", whose column the server names.  Two rows of MERGE are not the
    // spike's, one that updates and inserts and one that updates alone: with the spike's two, they show that the server
    // calls inserted and deleted nullable only where an action of the MERGE leaves them empty.
    public static TheoryData<string, string[]> SpikeRows =>
        new()
        {
            {
                "SELECT u.id AS user_id, o.id AS order_id, o.total "
                    + "FROM dbo.users AS u LEFT JOIN dbo.orders AS o ON o.user_id = u.id",
                ["user_id 0 dbo.users.id", "order_id 1 dbo.orders.id", "total 1 dbo.orders.total"]
            },
            {
                "SELECT u.id AS user_id, o.id AS order_id "
                    + "FROM dbo.users AS u RIGHT JOIN dbo.orders AS o ON o.user_id = u.id",
                ["user_id 1 dbo.users.id", "order_id 0 dbo.orders.id"]
            },
            {
                "SELECT u.id AS user_id, o.id AS order_id, ISNULL(u.id, o.user_id) AS either_id "
                    + "FROM dbo.users AS u FULL JOIN dbo.orders AS o ON o.user_id = u.id",
                ["user_id 1 dbo.users.id", "order_id 1 dbo.orders.id", "either_id 1 -"]
            },
            {
                "SELECT u.id AS user_id, o.id AS order_id "
                    + "FROM dbo.users AS u LEFT JOIN dbo.orders AS o ON o.user_id = u.id WHERE o.total > 0",
                ["user_id 0 dbo.users.id", "order_id 1 dbo.orders.id"]
            },
            {
                "SELECT u.id AS user_id, l.id AS order_id, l.total FROM dbo.users AS u OUTER APPLY "
                    + "(SELECT TOP 1 o.id, o.total FROM dbo.orders AS o WHERE o.user_id = u.id ORDER BY o.id) AS l",
                ["user_id 0 dbo.users.id", "order_id 1 dbo.orders.id", "total 1 dbo.orders.total"]
            },
            {
                "SELECT u.id AS user_id, l.id AS order_id, l.total FROM dbo.users AS u CROSS APPLY "
                    + "(SELECT TOP 1 o.id, o.total FROM dbo.orders AS o WHERE o.user_id = u.id ORDER BY o.id) AS l",
                ["user_id 0 dbo.users.id", "order_id 0 dbo.orders.id", "total 0 dbo.orders.total"]
            },
            {
                "SELECT MAX(total) AS max_total, SUM(total) AS sum_total, COUNT(*) AS n, COUNT_BIG(*) AS n_big "
                    + "FROM dbo.orders",
                ["max_total 1 -", "sum_total 1 -", "n 1 -", "n_big 1 -"]
            },
            {
                "SELECT user_id, MAX(total) AS max_total, SUM(total) AS sum_total, COUNT(*) AS n, "
                    + "COUNT_BIG(*) AS n_big FROM dbo.orders GROUP BY user_id",
                ["user_id 0 dbo.orders.user_id", "max_total 1 -", "sum_total 1 -", "n 1 -", "n_big 1 -"]
            },
            {
                "SELECT (SELECT MAX(total) FROM dbo.orders) AS max_total, (SELECT COUNT(*) FROM dbo.orders) AS n",
                ["max_total 1 -", "n 1 -"]
            },
            {
                "SELECT CASE WHEN id > 1 THEN name END AS a, CASE WHEN id > 1 THEN name ELSE N'x' END AS b, "
                    + "IIF(id > 1, name, N'y') AS c FROM dbo.users",
                ["a 1 -", "b 0 -", "c 0 -"]
            },
            {
                "SELECT ISNULL(nickname, N'-') AS a, COALESCE(nickname, name) AS b, "
                    + "COALESCE(nickname, deleted_at) AS c, NULLIF(name, N'') AS d, CONCAT(nickname, N'x') AS e "
                    + "FROM dbo.users",
                ["a 0 -", "b 1 -", "c 1 -", "d 1 -", "e 0 -"]
            },
            {
                "SELECT CAST(id AS varchar(10)) AS a, TRY_CAST(name AS int) AS b, "
                    + "CONVERT(nvarchar(20), created_at) AS c, id + 1 AS d, id / 0 AS e FROM dbo.users",
                ["a 1 -", "b 1 -", "c 1 -", "d 1 -", "e 1 -"]
            },
            {
                "SELECT ROW_NUMBER() OVER (ORDER BY id) AS rn, LAG(id) OVER (ORDER BY id) AS previous_id, "
                    + "SUM(id) OVER () AS sum_id FROM dbo.users",
                ["rn 1 -", "previous_id 1 -", "sum_id 1 -"]
            },
            { "SELECT id, name FROM dbo.users UNION SELECT id, note FROM dbo.orders", ["id 0 -", "name 1 -"] },
            { "SELECT id FROM dbo.users UNION ALL SELECT id FROM dbo.orders", ["id 0 -"] },
            { "SELECT user_id FROM dbo.orders GROUP BY ROLLUP(user_id)", ["user_id 1 dbo.orders.user_id"] },
            {
                "SELECT user_id, id FROM dbo.orders GROUP BY GROUPING SETS ((user_id), (id))",
                ["user_id 1 dbo.orders.user_id", "id 1 dbo.orders.id"]
            },
            { "SELECT user_id FROM dbo.orders GROUP BY CUBE(user_id)", ["user_id 1 dbo.orders.user_id"] },
            {
                "WITH j AS (SELECT u.id AS user_id, o.id AS order_id, o.total "
                    + "FROM dbo.users AS u LEFT JOIN dbo.orders AS o ON o.user_id = u.id) "
                    + "SELECT user_id, order_id, total FROM j",
                ["user_id 0 dbo.users.id", "order_id 1 dbo.orders.id", "total 1 dbo.orders.total"]
            },
            {
                "SELECT j.user_id, j.order_id, j.total FROM (SELECT u.id AS user_id, o.id AS order_id, o.total "
                    + "FROM dbo.users AS u LEFT JOIN dbo.orders AS o ON o.user_id = u.id) AS j",
                ["user_id 0 dbo.users.id", "order_id 1 dbo.orders.id", "total 1 dbo.orders.total"]
            },
            {
                "SELECT id, name_len, name_len_p, rv, email FROM dbo.users",
                [
                    "id 0 dbo.users.id",
                    "name_len 1 dbo.users.name_len",
                    "name_len_p 1 dbo.users.name_len_p",
                    "rv 0 dbo.users.rv",
                    "email 0 dbo.users.email",
                ]
            },
            {
                "SELECT id, amount, valid_from FROM dbo.prices FOR SYSTEM_TIME ALL",
                ["id 0 -", "amount 0 -", "valid_from 0 -"]
            },
            { "SELECT 1, id FROM dbo.users", ["null 0 -", "id 0 dbo.users.id"] },
            {
                "SELECT u.id, o.id FROM dbo.users AS u JOIN dbo.orders AS o ON o.user_id = u.id",
                ["id 0 dbo.users.id", "id 0 dbo.orders.id"]
            },
            {
                "SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.orders AS o WHERE o.user_id = u.id) THEN 1 ELSE 0 END "
                    + "AS has_orders FROM dbo.users AS u",
                ["has_orders 0 -"]
            },
            { "SELECT id, name FROM dbo.users FOR JSON PATH", ["JSON_F52E2B61-18A1-11d1-B105-00805F49916B 1 -"] },
            {
                "SELECT a, b FROM OPENJSON(N'[{\"a\": 1, \"b\": \"x\"}]') WITH (a int, b nvarchar(10))",
                ["a 1 -", "b 1 -"]
            },
            { "SELECT value FROM STRING_SPLIT(N'a,b', N',')", ["value 0 -"] },
            {
                "SELECT p.[1] AS one, p.[2] AS two FROM (SELECT user_id, total FROM dbo.orders) AS s "
                    + "PIVOT (SUM(total) FOR user_id IN ([1], [2])) AS p",
                ["one 1 -", "two 1 -"]
            },
            {
                "SELECT N'x' AS a, 1 AS b, NULL AS c, CAST(NULL AS int) AS d, 1.5 AS e, SYSUTCDATETIME() AS f, "
                    + "NEWID() AS g",
                ["a 0 -", "b 0 -", "c 1 -", "d 1 -", "e 0 -", "f 0 -", "g 1 -"]
            },
            { "SELECT JSON_VALUE(N'{\"a\": 1}', '$.a') AS a, ISJSON(N'{}') AS b", ["a 1 -", "b 1 -"] },
            {
                "INSERT INTO dbo.users (name) "
                    + "OUTPUT inserted.id, inserted.name, inserted.nickname, inserted.created_at VALUES (N'a')",
                [
                    "id 0 dbo.users.id",
                    "name 0 dbo.users.name",
                    "nickname 1 dbo.users.nickname",
                    "created_at 0 dbo.users.created_at",
                ]
            },
            {
                "UPDATE dbo.users SET nickname = N'b' "
                    + "OUTPUT deleted.nickname AS old_nickname, inserted.nickname AS new_nickname, inserted.id "
                    + "WHERE id = 1",
                ["old_nickname 1 dbo.users.nickname", "new_nickname 1 dbo.users.nickname", "id 0 dbo.users.id"]
            },
            {
                "DELETE FROM dbo.orders OUTPUT deleted.id, deleted.total WHERE id = 1",
                ["id 0 dbo.orders.id", "total 0 dbo.orders.total"]
            },
            {
                "MERGE dbo.orders AS t USING dbo.users AS s ON t.user_id = s.id "
                    + "WHEN MATCHED THEN UPDATE SET total = 1 "
                    + "WHEN NOT MATCHED THEN INSERT (user_id, total) VALUES (s.id, 0) "
                    + "WHEN NOT MATCHED BY SOURCE THEN DELETE "
                    + "OUTPUT $action AS action, inserted.id AS inserted_id, inserted.total AS inserted_total, "
                    + "deleted.id AS deleted_id, deleted.total AS deleted_total, s.id AS source_id;",
                [
                    "action 0 -",
                    "inserted_id 1 dbo.orders.id",
                    "inserted_total 1 dbo.orders.total",
                    "deleted_id 1 dbo.orders.id",
                    "deleted_total 1 dbo.orders.total",
                    "source_id 1 dbo.users.id",
                ]
            },
            {
                "MERGE dbo.orders AS t USING dbo.users AS s ON t.user_id = s.id "
                    + "WHEN NOT MATCHED THEN INSERT (user_id, total) VALUES (s.id, 0) "
                    + "OUTPUT $action AS action, inserted.id, inserted.total;",
                ["action 0 -", "id 0 dbo.orders.id", "total 0 dbo.orders.total"]
            },
            {
                "MERGE dbo.orders AS t USING dbo.users AS s ON t.user_id = s.id "
                    + "WHEN MATCHED THEN UPDATE SET total = 1 "
                    + "WHEN NOT MATCHED THEN INSERT (user_id, total) VALUES (s.id, 0) "
                    + "OUTPUT $action AS action, inserted.id AS inserted_id, inserted.total AS inserted_total, "
                    + "deleted.id AS deleted_id, deleted.total AS deleted_total;",
                [
                    "action 0 -",
                    "inserted_id 0 dbo.orders.id",
                    "inserted_total 0 dbo.orders.total",
                    "deleted_id 1 dbo.orders.id",
                    "deleted_total 1 dbo.orders.total",
                ]
            },
            {
                "MERGE dbo.orders AS t USING dbo.users AS s ON t.user_id = s.id "
                    + "WHEN MATCHED THEN UPDATE SET total = 1 "
                    + "OUTPUT $action AS action, inserted.id AS inserted_id, inserted.total AS inserted_total, "
                    + "deleted.id AS deleted_id, deleted.total AS deleted_total;",
                [
                    "action 0 -",
                    "inserted_id 0 dbo.orders.id",
                    "inserted_total 0 dbo.orders.total",
                    "deleted_id 0 dbo.orders.id",
                    "deleted_total 0 dbo.orders.total",
                ]
            },
            {
                "UPDATE o SET total = 0 OUTPUT inserted.id, u.name "
                    + "FROM dbo.orders AS o LEFT JOIN dbo.users AS u ON u.id = o.user_id",
                ["id 0 dbo.orders.id", "name 1 dbo.users.name"]
            },
            {
                "DELETE o OUTPUT deleted.id, u.name "
                    + "FROM dbo.orders AS o LEFT JOIN dbo.users AS u ON u.id = o.user_id",
                ["id 0 dbo.orders.id", "name 1 dbo.users.name"]
            },
            {
                "WITH s AS (SELECT id FROM dbo.users) "
                    + "INSERT INTO dbo.orders (user_id, total) OUTPUT inserted.id SELECT id, 0 FROM s",
                ["id 0 dbo.orders.id"]
            },
        };

    [Theory]
    [MemberData(nameof(SpikeRows))]
    public async Task ColumnsAsync_RowOfTheSpike_IsAsTheSpikeFound(string sql, string[] expected)
    {
        var rows = await ColumnsAsync(sql);

        rows.Where(row => !row.IsHidden).Select(Written).ShouldBe(expected);
        var built = SqlServerColumns.Build(rows, SqlServerDatabases.Database);
        if (rows.FirstOrDefault(row => row is { IsHidden: false, Name: null }) is { } unnamed)
        {
            built.Failure.ShouldBe(unnamed.ColumnOrdinal);
        }
        else
        {
            built.Value.ShouldNotBeNull().Select(column => Written(column.Column)).ShouldBe(expected);
        }
    }

    // Mode 1 adds the key and rowversion columns of every table read; mode 2 adds ROWSTAT alone, whose name the server
    // ends with a NUL.
    [Fact]
    public async Task ColumnsAsync_HiddenRow_IsRowstatAlone()
    {
        var rows = await ColumnsAsync(
            "SELECT u.name, o.total FROM dbo.users AS u JOIN dbo.orders AS o ON o.user_id = u.id"
        );

        var hidden = rows.Where(row => row.IsHidden).ShouldHaveSingleItem();
        hidden.Name.ShouldBe("ROWSTAT\0");
        hidden.SystemTypeName.ShouldBe("int");
        rows.Count(row => !row.IsHidden).ShouldBe(2);
    }

    // Mode 1 names the view's base tables, and mode 2 the view.
    [Fact]
    public async Task ColumnsAsync_ViewStar_OriginIsTheView()
    {
        var columns = await BuiltAsync("SELECT * FROM dbo.user_orders");

        columns
            .Select(column => Written(column.Column))
            .ShouldBe([
                "user_id 0 dbo.user_orders.user_id",
                "name 0 dbo.user_orders.name",
                "order_id 1 dbo.user_orders.order_id",
                "total 1 dbo.user_orders.total",
            ]);
    }

    // The source of a column of msdb is msdb, and of master.sys.databases the system resource: neither is the session's
    // database, so neither column has an origin.
    [Theory]
    [InlineData("SELECT job_id, name FROM msdb.dbo.sysjobs", "msdb")]
    [InlineData("SELECT database_id, name FROM master.sys.databases", "mssqlsystemresource")]
    public async Task ColumnsAsync_OtherDatabase_HasNoOrigin(string sql, string database)
    {
        var rows = await ColumnsAsync(sql);

        var visible = rows.Where(row => !row.IsHidden).ToList();
        visible.Count.ShouldBe(2);
        visible.ShouldAllBe(row => row.SourceDatabase == database && row.SourceTable != null);
        var columns = SqlServerColumns.Build(rows, SqlServerDatabases.Database).Value.ShouldNotBeNull();
        columns.ShouldAllBe(column =>
            column.Column.Origin == null && column.NoOriginReason == SqlServerColumns.AnotherDatabase
        );
    }

    // The tool's source_type is the BaseType of the source in the session's database: U for a table, a synonym's
    // table among them, V for a view, IF and TF for the functions, whose columns the server names the function as the
    // source of; and null for a source of another database, which OBJECTPROPERTYEX would read in the session's.
    [Theory]
    [InlineData("SELECT id FROM dbo.users", "U")]
    [InlineData("SELECT user_id FROM dbo.user_orders", "V")]
    [InlineData("SELECT id FROM dbo.orders_of(1)", "IF")]
    [InlineData("SELECT id FROM dbo.orders_table_of(1)", "TF")]
    [InlineData("SELECT job_id FROM msdb.dbo.sysjobs", null)]
    [InlineData("SELECT database_id FROM master.sys.databases", null)]
    [InlineData("SELECT object_id FROM sys.objects", null)]
    [InlineData("SELECT 1 AS one", null)]
    public async Task ColumnsAsync_SourceType_IsTheBaseTypeOfASourceOfTheSessionsDatabase(string sql, string? type) =>
        (await ColumnsAsync(sql)).Single(row => !row.IsHidden).SourceType.ShouldBe(type);

    // The tool's persisted_not_null is the catalog's, of the source column: true for name_len_p, PERSISTED NOT NULL,
    // and false for the computed name_len and the plain id.  It holds through a LEFT JOIN too, where the column is NULL
    // for an order without its user: the flag says what the table holds, and only the proofs read the query.
    [Theory]
    [InlineData("SELECT name_len, name_len_p, id FROM dbo.users", new[] { false, true, false })]
    [InlineData(
        "SELECT o.id, u.name_len_p FROM dbo.orders AS o LEFT JOIN dbo.users AS u ON u.id = o.user_id",
        new[] { false, true }
    )]
    [InlineData("SELECT 1 AS one", new[] { false })]
    public async Task ColumnsAsync_PersistedNotNull_IsRead(string sql, bool[] expected) =>
        (await ColumnsAsync(sql))
            .Where(static row => !row.IsHidden)
            .Select(static row => row.PersistedNotNull)
            .ShouldBe([.. expected.Select(static flag => (bool?)flag)]);

    // A view is its columns' source under mode 2, so its column of name_len_p, read through a LEFT JOIN that makes it
    // NULL, is not the table's: persisted_not_null is false, as sys.tables holds no view.
    [Fact]
    public async Task ColumnsAsync_ViewOfAPersistedNotNullColumn_IsNotPersistedNotNull()
    {
        await sqlServer.ExecuteAsync("DROP VIEW IF EXISTS dbo.order_name_lengths");
        await sqlServer.ExecuteAsync(
            "CREATE VIEW dbo.order_name_lengths AS SELECT o.id, u.name_len_p "
                + "FROM dbo.orders AS o LEFT JOIN dbo.users AS u ON u.id = o.user_id"
        );
        try
        {
            var row = (await ColumnsAsync("SELECT name_len_p FROM dbo.order_name_lengths")).Single(row =>
                !row.IsHidden
            );

            (row.SourceTable, row.SourceType, row.PersistedNotNull).ShouldBe(("order_name_lengths", "V", false));
        }
        finally
        {
            await sqlServer.ExecuteAsync("DROP VIEW dbo.order_name_lengths");
        }
    }

    // The server names the synonym's table as the source, and not the synonym.
    [Fact]
    public async Task ColumnsAsync_Synonym_IsItsTable()
    {
        await sqlServer.ExecuteAsync("DROP SYNONYM IF EXISTS dbo.orders_synonym");
        await sqlServer.ExecuteAsync("CREATE SYNONYM dbo.orders_synonym FOR dbo.orders");
        try
        {
            var row = (await ColumnsAsync("SELECT id FROM dbo.orders_synonym")).Single(row => !row.IsHidden);

            (row.SourceTable, row.SourceType).ShouldBe(("orders", "U"));
        }
        finally
        {
            await sqlServer.ExecuteAsync("DROP SYNONYM dbo.orders_synonym");
        }
    }

    // A column of a function has no origin: SELECT * from the function, which the table match would ask, fails
    // without its arguments.
    [Theory]
    [InlineData("SELECT id, total FROM dbo.orders_of(1)")]
    [InlineData("SELECT id, total FROM dbo.orders_table_of(1)")]
    public async Task ColumnsAsync_TableValuedFunction_HasNoOrigin(string sql)
    {
        var rows = await ColumnsAsync(sql);

        rows.Where(row => !row.IsHidden).ShouldAllBe(row => row.SourceTable != null && row.SourceColumn != null);
        SqlServerColumns
            .Build(rows, SqlServerDatabases.Database)
            .Value.ShouldNotBeNull()
            .ShouldAllBe(column =>
                column.Column.Origin == null && column.NoOriginReason == SqlServerColumns.NotATableOrView
            );
    }

    // An inferred parameter of an alias type named by a keyword is declared to "columns" in brackets: the server
    // refuses dbo.User with 156.
    [Fact]
    public async Task DescribeAsync_InferredAliasTypeNamedByAKeyword_IsDeclaredInBrackets()
    {
        var (described, capture) = await Sessions.DescribeCapturedAsync(
            sqlServer.ConnectionString,
            Of(SqlDialectRules.SqlServer, "SELECT id, owner FROM dbo.accounts WHERE owner = @owner")
        );

        var description = described.Value.ShouldNotBeNull(described.Failure?.Descriptor.Id);
        description
            .Parameters.ShouldHaveSingleItem()
            .Type.ShouldBe(new SqlServerType("nvarchar(50)", 100, 0, 0, new SqlServerUserType("dbo", "User", null)));
        description.Columns.ShouldNotBeNull().Select(column => column.Name).ShouldBe(["id", "owner"]);
        capture
            .Calls.Single(call => call.Method == SqlServerCallNames.Columns)
            .Request.Deserialize<SqlServerColumnsRequest>(CaptureJson.Options)
            .ShouldNotBeNull()
            .Params.ShouldBe("@owner [dbo].[User]");
    }

    [Theory]
    [InlineData("DELETE FROM dbo.orders OUTPUT deleted.id INTO dbo.victim WHERE id = 1")]
    [InlineData("UPDATE dbo.users SET nickname = N'x' WHERE id = 1")]
    [InlineData("SELECT id INTO #x FROM dbo.users")]
    public async Task ColumnsAsync_NoResultSet_HasNoRows(string sql)
    {
        var rows = await ColumnsAsync(sql);

        rows.ShouldBeEmpty();
        SqlServerColumns.Build(rows, SqlServerDatabases.Database).Value.ShouldNotBeNull().ShouldBeEmpty();
    }

    // The spike's one row of its table of statements that the server refuses under mode 1 is refused under mode 2.
    [Fact]
    public async Task ColumnsAsync_OutputFromTableWithTrigger_AnswersErrorRows334Then11501()
    {
        var answer = await AnswerAsync("INSERT INTO dbo.trig (id, v) OUTPUT inserted.id VALUES (1, 1)");

        answer.Rows.ShouldBeNull(
            string.Join("; ", answer.Rows?.Select(row => $"{row.ColumnOrdinal} {row.Name ?? "null"}") ?? [])
        );
        answer.Error.ShouldBeNull(
            string.Join("; ", answer.Error?.Errors.Select(error => $"Msg {error.Number}: {error.Message}") ?? [])
        );
        answer.Errors.ShouldNotBeNull().Select(error => error.Number).ShouldBe([334, 11501]);
    }

    [Fact]
    public async Task ColumnsAsync_IdentityAndComputed_AreTheServersFlags()
    {
        var columns = await BuiltAsync("SELECT id, name, name_len, name_len_p FROM dbo.users");

        columns
            .Select(column => (column.Column.Identity, column.Column.Computed))
            .ShouldBe([(true, false), (false, false), (false, true), (false, true)]);
    }

    // SqlClient knows both types, so the server names them, where sqlcmd is told varchar(max).
    [Fact]
    public async Task ColumnsAsync_JsonAndVector_On2025()
    {
        sqlServer.SkipBelow(17, "json and vector are new in SQL Server 2025");
        await sqlServer.ExecuteAsync("DROP TABLE IF EXISTS dbo.json_vector");
        await sqlServer.ExecuteAsync("CREATE TABLE dbo.json_vector (id int PRIMARY KEY, j json, v vector(3))");
        try
        {
            var rows = await ColumnsAsync("SELECT j, v FROM dbo.json_vector");

            rows.Where(row => !row.IsHidden).Select(row => row.SystemTypeName).ShouldBe(["json", "vector(3)"]);
        }
        finally
        {
            await sqlServer.ExecuteAsync("DROP TABLE dbo.json_vector");
        }
    }

    // The answer of "columns" for the SQL, with no declarations, in a session of its own.
    private async Task<SqlServerColumnsAnswer> AnswerAsync(string sql)
    {
        await using var session = await Sessions.OpenSessionAsync(sqlServer.ConnectionString);
        return await new SqlServerLive(session.Connection).ColumnsAsync(
            new SqlServerColumnsRequest(sql, null),
            TestContext.Current.CancellationToken
        );
    }

    // The rows of a describe that must answer them.
    private async Task<IReadOnlyList<SqlServerColumnRow>> ColumnsAsync(string sql)
    {
        var answer = await AnswerAsync(sql);

        answer.Error.ShouldBeNull(
            string.Join("; ", answer.Error?.Errors.Select(error => $"Msg {error.Number}: {error.Message}") ?? [])
        );
        answer.Errors.ShouldBeNull(
            string.Join("; ", answer.Errors?.Select(error => $"Msg {error.Number}: {error.Message}") ?? [])
        );
        return answer.Rows.ShouldNotBeNull();
    }

    private async Task<IReadOnlyList<BuiltColumn>> BuiltAsync(string sql) =>
        SqlServerColumns.Build(await ColumnsAsync(sql), SqlServerDatabases.Database).Value.ShouldNotBeNull();

    private static string Written(SqlServerColumnRow row) =>
        $"{row.Name ?? "null"} {Nullable(row.IsNullable)} "
        + Origin(row.SourceSchema, row.SourceTable, row.SourceColumn, row.SourceDatabase);

    private static string Written(SidecarColumn column) =>
        $"{column.Name} {Nullable(column.Nullable)} "
        + (column.Origin is { } origin ? $"{origin.Schema}.{origin.Table}.{origin.Column}" : "-");

    private static string Nullable(bool? nullable) =>
        nullable switch
        {
            true => "1",
            false => "0",
            null => "?",
        };

    // A source of the session's database, written as Build writes an origin; another database's, in full.
    private static string Origin(string? schema, string? table, string? column, string? database) =>
        (schema, table, column) switch
        {
            (null, null, null) => "-",
            _ when database == SqlServerDatabases.Database => $"{schema}.{table}.{column}",
            _ => $"{database}.{schema}.{table}.{column}",
        };
}
