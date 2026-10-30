using System.Linq;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing.Snapshot;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Describing.SqlServer.Columns;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.SqlServer.Columns;

// The columns of a query from the rows of sys.dm_exec_describe_first_result_set in browse mode 2, as the test writes
// them: the server's answer as given, with the hidden rows dropped and an origin only in the session's database.
public sealed class SqlServerColumnsTests
{
    private const string Database = "app";

    // dbo.users.id as browse mode 2 describes it in the database app.
    private static readonly SqlServerColumnRow Id = new(
        IsHidden: false,
        ColumnOrdinal: 1,
        Name: "id",
        IsNullable: false,
        SystemTypeName: "int",
        MaxLength: 4,
        Precision: 10,
        Scale: 0,
        UserTypeSchema: null,
        UserTypeName: null,
        AssemblyQualifiedTypeName: null,
        SourceServer: null,
        SourceDatabase: Database,
        SourceSchema: "dbo",
        SourceTable: "users",
        SourceColumn: "id",
        IsIdentityColumn: true,
        IsComputedColumn: false,
        SourceType: "U"
    );

    private static readonly SqlServerColumnRow Email = Id with
    {
        ColumnOrdinal = 2,
        Name = "email",
        IsNullable = true,
        SystemTypeName = "nvarchar(320)",
        MaxLength = 640,
        Precision = 0,
        SourceColumn = "email",
        IsIdentityColumn = false,
    };

    // The one hidden row that browse mode 2 adds.
    private static readonly SqlServerColumnRow Rowstat = Id with
    {
        IsHidden = true,
        ColumnOrdinal = 3,
        Name = "ROWSTAT",
        SourceColumn = null,
        SourceType = null,
        SourceTable = null,
        SourceSchema = null,
        SourceDatabase = null,
        IsIdentityColumn = false,
    };

    // A column of an expression: the server names no source.
    private static readonly SqlServerColumnRow One = Id with
    {
        Name = null,
        SourceDatabase = null,
        SourceSchema = null,
        SourceTable = null,
        SourceColumn = null,
        SourceType = null,
        IsIdentityColumn = false,
    };

    private static SidecarColumn[] Built(params SqlServerColumnRow[] rows)
    {
        var built = SqlServerColumns.Build(rows, Database);
        built.Succeeded.ShouldBeTrue();
        return [.. built.Value.Select(static column => column.Column)];
    }

    [Fact]
    public void Build_HiddenRow_IsDropped() =>
        Built(Id, Email, Rowstat).Select(static column => column.Name).ShouldBe(["id", "email"]);

    // The answer of a statement with no result set; the session reads it as no rows.
    [Fact]
    public void Build_NoVisibleRow_IsEmpty()
    {
        Built().ShouldBeEmpty();
        Built(Rowstat).ShouldBeEmpty();
    }

    [Fact]
    public void Build_Row_RecordsEachField()
    {
        var built = SqlServerColumns.Build([Id, Email with { Name = "email!" }], Database);

        built.Succeeded.ShouldBeTrue();
        built.Value.ShouldBe([
            new BuiltColumn(
                new SidecarColumn(
                    0,
                    "id",
                    TestSidecars.SqlServer(),
                    false,
                    SidecarValues.NullableSource.Server,
                    new SidecarOrigin("dbo", "users", "id"),
                    true,
                    false
                ),
                null
            ),
            new BuiltColumn(
                new SidecarColumn(
                    1,
                    "email!",
                    TestSidecars.SqlServer("nvarchar(320)", 640, 0, 0),
                    true,
                    SidecarValues.NullableSource.Server,
                    new SidecarOrigin("dbo", "users", "email"),
                    false,
                    false
                ),
                null
            ),
        ]);
    }

    // The type is SqlServerTypes', with the user type the row names.
    [Fact]
    public void Build_RowOfAnAliasType_HasItsUserType() =>
        Built(Email with { UserTypeSchema = "dbo", UserTypeName = "Email" })
            .Single()
            .Type.ShouldBe(
                TestSidecars.SqlServer("nvarchar(320)", 640, 0, 0, new SqlServerUserType("dbo", "Email", null))
            );

    // The function gives no is_nullable only where it cannot say, and a column it cannot say of may be null.
    [Fact]
    public void Build_NullIsNullable_IsNullable() =>
        Built(Id with { IsNullable = null }).Single().Nullable.ShouldBe(true);

    [Fact]
    public void Build_NullFlags_AreNull()
    {
        var column = Built(One with { Name = "n", IsIdentityColumn = null, IsComputedColumn = null }).Single();

        column.Identity.ShouldBeNull();
        column.Computed.ShouldBeNull();
    }

    // SELECT 1, id: the first column has no name, and the failure is its place as the server counts it.
    [Fact]
    public void Build_NullName_FailsWithItsOrdinal()
    {
        var built = SqlServerColumns.Build([One, Id with { ColumnOrdinal = 2 }], Database);

        built.Succeeded.ShouldBeFalse();
        built.Failure.ShouldBe(1);
    }

    // The generator reports two columns of one name; the describer records both.
    [Fact]
    public void Build_TwoColumnsOfOneName_AreBothRecorded() =>
        Built(Id, Id with { ColumnOrdinal = 2 })
            .Select(static column => (column.Ordinal, column.Name))
            .ShouldBe([(0, "id"), (1, "id")]);

    [Fact]
    public void Build_ColumnWithoutAnOrigin_SaysWhy()
    {
        var built = SqlServerColumns.Build([One with { Name = "n" }], Database);

        built.Succeeded.ShouldBeTrue();
        built.Value.Single().Column.Origin.ShouldBeNull();
        built.Value.Single().NoOriginReason.ShouldBe("expression");
    }

    [Fact]
    public void OriginOf_ColumnOfTheSessionsDatabase_IsItsSchemaTableAndColumn() =>
        SqlServerColumns.OriginOf(Id, Database).ShouldBe((new SidecarOrigin("dbo", "users", "id"), null));

    [Fact]
    public void OriginOf_Expression_IsNone() => SqlServerColumns.OriginOf(One, Database).ShouldBe((null, "expression"));

    // msdb.dbo.sysjobs: a table of that name in the session's database is another table.
    [Fact]
    public void OriginOf_OtherDatabase_IsNone() =>
        SqlServerColumns
            .OriginOf(Id with { SourceDatabase = "msdb", SourceTable = "sysjobs", SourceColumn = "job_id" }, Database)
            .ShouldBe((null, "another database"));

    // master.sys.databases: the server names the system resource as its database.
    [Fact]
    public void OriginOf_SystemResource_IsNone() =>
        SqlServerColumns
            .OriginOf(
                Id with
                {
                    SourceDatabase = "mssqlsystemresource",
                    SourceSchema = "sys",
                    SourceTable = "databases",
                    SourceColumn = "name",
                },
                Database
            )
            .ShouldBe((null, "another database"));

    [Fact]
    public void OriginOf_View_IsItsSchemaViewAndColumn() =>
        SqlServerColumns
            .OriginOf(Id with { SourceTable = "user_orders", SourceColumn = "user_id", SourceType = "V" }, Database)
            .ShouldBe((new SidecarOrigin("dbo", "user_orders", "user_id"), null));

    // The server names an inline or a multi-statement function as its columns' source; a column of a function has no
    // origin, as on PostgreSQL, and a source whose type the tool could not read has none either.
    [Theory]
    [InlineData("IF")]
    [InlineData("TF")]
    [InlineData("FT")]
    [InlineData(null)]
    public void OriginOf_SourceNotATableOrView_IsNone(string? type) =>
        SqlServerColumns
            .OriginOf(Id with { SourceTable = "orders_of", SourceType = type }, Database)
            .ShouldBe((null, "not a table or view"));

    // The type is read for the session's database alone, so a function of another database is of another database.
    [Fact]
    public void OriginOf_FunctionOfAnotherDatabase_IsOfAnotherDatabase() =>
        SqlServerColumns
            .OriginOf(Id with { SourceDatabase = "msdb", SourceType = "IF" }, Database)
            .ShouldBe((null, "another database"));

    [Fact]
    public void OriginOf_ExpressionWithNoType_IsAnExpression() =>
        SqlServerColumns.OriginOf(One with { SourceType = null }, Database).ShouldBe((null, "expression"));

    [Fact]
    public void Build_ColumnOfAFunction_SaysWhy()
    {
        var built = SqlServerColumns.Build([Id with { SourceTable = "orders_of", SourceType = "IF" }], Database);

        built.Succeeded.ShouldBeTrue();
        built.Value.Single().Column.Origin.ShouldBeNull();
        built.Value.Single().NoOriginReason.ShouldBe(SqlServerColumns.NotATableOrView);
    }

    [Fact]
    public void OriginOf_OtherServer_IsNone() =>
        SqlServerColumns.OriginOf(Id with { SourceServer = "linked" }, Database).ShouldBe((null, "another server"));

    // The database's name is compared ordinally, as the server gave both.
    [Fact]
    public void OriginOf_DatabaseNamedInAnotherCase_IsNone() =>
        SqlServerColumns.OriginOf(Id with { SourceDatabase = "APP" }, Database).ShouldBe((null, "another database"));

    // A source named in part is no base column, whether or not the server named its database.
    [Theory]
    [InlineData(Database, null, "users", "id")]
    [InlineData(Database, "dbo", null, "id")]
    [InlineData(Database, "dbo", "users", null)]
    [InlineData(null, "dbo", "users", null)]
    [InlineData(null, null, null, "id")]
    public void OriginOf_PartlyGiven_IsNone(string? database, string? schema, string? table, string? column) =>
        SqlServerColumns
            .OriginOf(
                Id with
                {
                    SourceDatabase = database,
                    SourceSchema = schema,
                    SourceTable = table,
                    SourceColumn = column,
                },
                Database
            )
            .ShouldBe((null, "expression"));

    // A capture recorded before open asked the database: no column is known to be of the session's database.
    [Fact]
    public void OriginOf_NoSessionDatabase_IsNone() =>
        SqlServerColumns.OriginOf(Id, null).ShouldBe((null, "another database"));
}
