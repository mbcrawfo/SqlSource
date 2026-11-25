using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer;
using Xunit;
using static SqlSource.Tool.Testing.DescribeRequests;
using static SqlSource.Tool.Tests.SqlServerScript;
using static SqlSource.Tool.Tests.SqlServerSessionCases;
using Calls = SqlSource.Tool.Describing.SqlServer.SqlServerCallNames;

namespace SqlSource.Tool.Tests.Describing.SqlServer;

// The session over a capture the test writes: the proofs that correct the server's nullability, the call "operands"
// that asks about their operands, after "columns" and before "table", and a describe that goes on whatever "operands"
// answers.  No driver runs.
public sealed class SqlServerSessionProofTests
{
    private const string Shown = "SELECT id, COALESCE(nickname, name) AS shown FROM dbo.users";
    private const string ShownOperands = "SELECT nickname\nAS [1],\nname\nAS [2]\nFROM dbo.users";

    private static readonly SqlServerColumnRow NicknameColumn = NameColumn with
    {
        Name = "nickname",
        IsNullable = true,
        SystemTypeName = "nvarchar(50)",
        MaxLength = 100,
        SourceColumn = "nickname",
    };

    // The first operand of COALESCE(nickname, name), which is read twice, as a stored column of the table.
    private static readonly SqlServerColumnRow StoredNickname = OperandRow(1, true) with
    {
        SourceType = "U",
        SourceColumn = "nickname",
        IsComputedColumn = false,
    };

    [Fact]
    public async Task DescribeAsync_FormNeedsOperands_AsksOperandsAndCorrectsTheColumn()
    {
        var operands = new SqlServerColumnsRequest(ShownOperands, null);
        var script = Script(Shown, [IdColumn, ExpressionRow(2, "shown"), Rowstat with { ColumnOrdinal = 3 }])
            .Operands(operands, new SqlServerColumnsAnswer([StoredNickname, OperandRow(2, false)], null, null));

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Shown));

        asked.ShouldBe([
            Call(Calls.Parameters, new SqlServerParametersRequest(Shown, null, null)),
            Call(Calls.Columns, new SqlServerColumnsRequest(Shown, null)),
            Call(Calls.Operands, operands),
        ]);
        Nullability(described)
            .ShouldBe([(false, SidecarValues.NullableSource.Server), (false, SidecarValues.NullableSource.Expression)]);
    }

    // A column alone is what "columns" answered already, so a describe of nullable columns of dbo.users asks no
    // "operands" and goes on to "table".
    [Fact]
    public async Task DescribeAsync_BareNullableColumn_AsksTableAndNoOperands()
    {
        const string Sql = "SELECT id, nickname FROM dbo.users";
        var table = new SqlServerTableRequest("dbo", "users");
        var script = Script(Sql, [IdColumn, NicknameColumn, Rowstat with { ColumnOrdinal = 3 }])
            .Table(table, new SqlServerColumnsAnswer([IdColumn, NicknameColumn, Rowstat], null, null));

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.Select(static call => call.Method).ShouldBe([Calls.Parameters, Calls.Columns, Calls.Table]);
        asked[2].ShouldBe(Call(Calls.Table, table));
        Nullability(described)
            .ShouldBe([(false, SidecarValues.NullableSource.Server), (true, SidecarValues.NullableSource.Server)]);
        described.Value.ShouldNotBeNull().TableMatch.ShouldBe(SidecarValues.TableMatch.Matched);
    }

    // "operands" whose connection was lost, whose describe answers error rows, or whose rows are not the operands:
    // nothing is reported, the proofs of the text alone hold, and the others keep the server's answer.
    [Theory]
    [InlineData("lost")]
    [InlineData("error rows")]
    [InlineData("short")]
    public async Task DescribeAsync_OperandsFails_KeepsTextProofsAndGoesOn(string how)
    {
        const string Sql = "SELECT COUNT(*) AS n, COALESCE(nickname, name) AS shown FROM dbo.users";
        var answer = how switch
        {
            "lost" => new SqlServerColumnsAnswer(null, null, Lost),
            "error rows" => new SqlServerColumnsAnswer(
                null,
                [new SqlServerDescribeError(207, 2, "Invalid column name 'nickname'.")],
                null
            ),
            _ => new SqlServerColumnsAnswer([OperandRow(1, false)], null, null),
        };
        var script = Script(Sql, [ExpressionRow(1, "n"), ExpressionRow(2, "shown")])
            .Operands(new SqlServerColumnsRequest(ShownOperands, null), answer);

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.Select(static call => call.Method).ShouldBe([Calls.Parameters, Calls.Columns, Calls.Operands]);
        Nullability(described)
            .ShouldBe([(false, SidecarValues.NullableSource.Expression), (true, SidecarValues.NullableSource.Server)]);
        described.Value.ShouldNotBeNull().TableMatch.ShouldBe(SidecarValues.TableMatch.NoOrigin);
    }

    // "operands" is declared as "columns" is: every parameter at its described type.
    [Fact]
    public async Task DescribeAsync_Parameters_OperandsDeclaredAsColumns()
    {
        const string Sql = "SELECT COALESCE(nickname, name) AS shown FROM dbo.users WHERE id = @x";
        var operands = new SqlServerColumnsRequest(
            "SELECT nickname\nAS [1],\nname\nAS [2]\nFROM dbo.users WHERE id = @x",
            "@x int"
        );
        var script = new SqlServerScript()
            .Parameters(new SqlServerParametersRequest(Sql, null, null), new SqlServerParametersAnswer([IntRow], null))
            .Columns(
                new SqlServerColumnsRequest(Sql, "@x int"),
                new SqlServerColumnsAnswer([ExpressionRow(1, "shown")], null, null)
            )
            .Operands(operands, new SqlServerColumnsAnswer([StoredNickname, OperandRow(2, false)], null, null));

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked[^1].ShouldBe(Call(Calls.Operands, operands));
        Nullability(described).ShouldBe([(false, SidecarValues.NullableSource.Expression)]);
    }

    // A set operator asks "operands" once for each branch, and a column is proved when both answers prove it.
    [Fact]
    public async Task DescribeAsync_SetOperator_AsksEachBranchInOrder()
    {
        const string Sql = "SELECT nickname AS shown FROM dbo.users UNION ALL SELECT name FROM dbo.users";
        var first = new SqlServerColumnsRequest("SELECT nickname\nAS [1]\nFROM dbo.users", null);
        var second = new SqlServerColumnsRequest("SELECT name\nAS [1]\nFROM dbo.users", null);
        var script = Script(Sql, [ExpressionRow(1, "shown")])
            .Operands(first, new SqlServerColumnsAnswer([OperandRow(1, false)], null, null))
            .Operands(second, new SqlServerColumnsAnswer([OperandRow(1, false)], null, null));

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.Skip(2).ShouldBe([Call(Calls.Operands, first), Call(Calls.Operands, second)]);
        Nullability(described).ShouldBe([(false, SidecarValues.NullableSource.Expression)]);
    }

    // An answer that proves nothing fails its own branch's operands, and so the column, and the describe goes on.
    [Fact]
    public async Task DescribeAsync_OneBranchsOperandsFail_KeepsTheServersAnswer()
    {
        const string Sql = "SELECT nickname AS shown FROM dbo.users UNION ALL SELECT name FROM dbo.users";
        var script = Script(Sql, [ExpressionRow(1, "shown")])
            .Operands(
                new SqlServerColumnsRequest("SELECT nickname\nAS [1]\nFROM dbo.users", null),
                new SqlServerColumnsAnswer(null, null, Lost)
            )
            .Operands(
                new SqlServerColumnsRequest("SELECT name\nAS [1]\nFROM dbo.users", null),
                new SqlServerColumnsAnswer([OperandRow(1, false)], null, null)
            );

        var (described, _) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        Nullability(described).ShouldBe([(true, SidecarValues.NullableSource.Server)]);
    }

    // A failed branch holds the places of its operands, so the next branch's answers are not read as its own: nickname
    // of the second branch is nullable and name not null, and neither column is proved.
    [Fact]
    public async Task DescribeAsync_FirstBranchsOperandsFail_LaterAnswersKeepTheirPlaces()
    {
        const string Sql = "SELECT 1 AS x, nickname AS y FROM dbo.users UNION ALL SELECT nickname, name FROM dbo.users";
        var script = Script(Sql, [ExpressionRow(1, "x"), ExpressionRow(2, "y")])
            .Operands(
                new SqlServerColumnsRequest("SELECT nickname\nAS [1]\nFROM dbo.users", null),
                new SqlServerColumnsAnswer(null, null, Lost)
            )
            .Operands(
                new SqlServerColumnsRequest("SELECT nickname\nAS [1],\nname\nAS [2]\nFROM dbo.users", null),
                new SqlServerColumnsAnswer([OperandRow(1, true), OperandRow(2, false)], null, null)
            );

        var (described, _) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        Nullability(described)
            .ShouldBe([(true, SidecarValues.NullableSource.Server), (true, SidecarValues.NullableSource.Server)]);
    }

    // A set operator's column of a CLR type answered not null asks each branch's item, and keeps the server's answer
    // when both are stored columns of a CLR type; a branch of a string makes it nullable.
    [Theory]
    [InlineData(true, false, SidecarValues.NullableSource.Server)]
    [InlineData(false, true, SidecarValues.NullableSource.Heuristic)]
    public async Task DescribeAsync_ClrSetOperator_AsksEachBranchsItem(bool secondIsClr, bool nullable, string source)
    {
        const string Sql = "SELECT solid AS g FROM dbo.shapes UNION ALL SELECT other FROM dbo.shapes";
        var first = new SqlServerColumnsRequest("SELECT solid\nAS [1]\nFROM dbo.shapes", null);
        var second = new SqlServerColumnsRequest("SELECT other\nAS [1]\nFROM dbo.shapes", null);
        var script = Script(Sql, [GeometryRow(1, "g")])
            .Operands(first, new SqlServerColumnsAnswer([StoredOperand(GeometryRow(1, "1"))], null, null))
            .Operands(
                second,
                new SqlServerColumnsAnswer(
                    [StoredOperand(secondIsClr ? GeometryRow(1, "1") : OperandRow(1, false))],
                    null,
                    null
                )
            );

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.Skip(2).ShouldBe([Call(Calls.Operands, first), Call(Calls.Operands, second)]);
        Nullability(described).ShouldBe([(nullable, source)]);
    }

    // A view's column of a CLR type asks "origins", the query's own text, after "columns" and before "table": kept not
    // null when the view passes a stored column on, and nullable otherwise, in the query and in the rows of "table"
    // alike, so the view matches either way.
    [Theory]
    [InlineData("U", false, SidecarValues.NullableSource.Server)]
    [InlineData("V", true, SidecarValues.NullableSource.Heuristic)]
    public async Task DescribeAsync_ClrColumnOfAView_AsksOriginsAndMatches(
        string originType,
        bool nullable,
        string source
    )
    {
        const string Sql = "SELECT * FROM dbo.shapes_view";
        var shape = GeometryRow(1, "shape") with
        {
            SourceDatabase = Database,
            SourceSchema = "dbo",
            SourceTable = "shapes_view",
            SourceColumn = "shape",
            SourceType = "V",
            IsComputedColumn = false,
        };
        var origins = new SqlServerColumnsRequest(Sql, null);
        var table = new SqlServerTableRequest("dbo", "shapes_view");
        var script = Script(Sql, [shape, Rowstat])
            .Origins(
                origins,
                new SqlServerColumnsAnswer(
                    [shape with { SourceTable = "shapes", SourceType = originType }, Rowstat],
                    null,
                    null
                )
            )
            .Table(table, new SqlServerColumnsAnswer([shape, Rowstat], null, null));

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked
            .Select(static call => call.Method)
            .ShouldBe([Calls.Parameters, Calls.Columns, Calls.Origins, Calls.Table]);
        asked[2].ShouldBe(Call(Calls.Origins, origins));
        Nullability(described).ShouldBe([(nullable, source)]);
        described.Value.ShouldNotBeNull().TableMatch.ShouldBe(SidecarValues.TableMatch.Matched);
    }

    // "origins" that fails, or whose rows are not the columns, leaves the column nullable, and the describe goes on.
    [Fact]
    public async Task DescribeAsync_OriginsFails_NullableAndGoesOn()
    {
        const string Sql = "SELECT shape FROM dbo.shapes_view";
        var shape = GeometryRow(1, "shape") with
        {
            SourceDatabase = Database,
            SourceSchema = "dbo",
            SourceTable = "shapes_view",
            SourceColumn = "shape",
            SourceType = "V",
            IsComputedColumn = false,
        };
        var script = Script(Sql, [shape, Rowstat])
            .Origins(new SqlServerColumnsRequest(Sql, null), new SqlServerColumnsAnswer(null, null, Lost))
            .Table(
                new SqlServerTableRequest("dbo", "shapes_view"),
                new SqlServerColumnsAnswer([shape, Rowstat], null, null)
            );

        var (described, _) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        Nullability(described).ShouldBe([(true, SidecarValues.NullableSource.Heuristic)]);
    }

    // A geometry row that the server answers not null and names no source for.
    private static SqlServerColumnRow GeometryRow(int ordinal, string name) =>
        ExpressionRow(ordinal, name) with
        {
            IsNullable = false,
            SystemTypeName = "geometry",
            MaxLength = -1,
            Precision = 0,
            UserTypeSchema = "sys",
            UserTypeName = "geometry",
            AssemblyQualifiedTypeName = "Microsoft.SqlServer.Types.SqlGeometry, Microsoft.SqlServer.Types",
        };

    // A computed column read from the preserved side of a RIGHT JOIN: "table" is asked for its table after "operands",
    // and its definition over those rows proves it.  No column list is one table's, so the match asks nothing more.
    [Fact]
    public async Task DescribeAsync_ComputedColumnOfATableNoJoinMakesNull_AsksItsTableAfterOperands()
    {
        const string Sql =
            "SELECT p.sale_price, COALESCE(p.discount_pct, 0) AS discount FROM dbo.categories AS c "
            + "RIGHT JOIN dbo.products AS p ON p.category_id = c.id";
        var operands = new SqlServerColumnsRequest(
            "SELECT p.discount_pct\nAS [1]\n"
                + "FROM dbo.categories AS c RIGHT JOIN dbo.products AS p ON p.category_id = c.id",
            null
        );
        var script = Script(Sql, [SalePriceColumn(1), ExpressionRow(2, "discount")])
            .Operands(operands, new SqlServerColumnsAnswer([StoredOperand(OperandRow(1, true))], null, null))
            .Table(Products, new SqlServerColumnsAnswer(ProductRows, null, null));

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.ShouldBe([
            Call(Calls.Parameters, new SqlServerParametersRequest(Sql, null, null)),
            Call(Calls.Columns, new SqlServerColumnsRequest(Sql, null)),
            Call(Calls.Operands, operands),
            Call(Calls.Table, Products),
        ]);
        Nullability(described)
            .ShouldBe([
                (false, SidecarValues.NullableSource.Expression),
                (false, SidecarValues.NullableSource.Expression),
            ]);
    }

    // The match of a query of the table alone takes the answer the proof was given, and asks "table" no second time:
    // the corrected column matches the corrected table's row.
    [Fact]
    public async Task DescribeAsync_ComputedColumnOfTheMatchedTable_AsksTableOnce()
    {
        const string Sql = "SELECT id, list_price, discount_pct, sale_price FROM dbo.products";
        var script = Script(Sql, ProductRows).Table(Products, new SqlServerColumnsAnswer(ProductRows, null, null));

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.Select(static call => call.Method).ShouldBe([Calls.Parameters, Calls.Columns, Calls.Table]);
        Nullability(described)[3].ShouldBe((false, SidecarValues.NullableSource.Expression));
        described.Value.ShouldNotBeNull().TableMatch.ShouldBe(SidecarValues.TableMatch.Matched);
    }

    // A "table" that fails for the computed column's table proves nothing and fails nothing, unless the match needs
    // that very table, which it would have asked all the same.
    [Theory]
    [InlineData("lost", false)]
    [InlineData("error rows", false)]
    [InlineData("lost", true)]
    public async Task DescribeAsync_TableOfAComputedColumnFails_ProvesNothing(string how, bool matchNeedsIt)
    {
        var sql = matchNeedsIt
            ? "SELECT p.sale_price FROM dbo.products AS p"
            : "SELECT p.sale_price, c.id FROM dbo.categories AS c RIGHT JOIN dbo.products AS p ON p.category_id = c.id";
        var answer =
            how == "lost"
                ? new SqlServerColumnsAnswer(null, null, Lost)
                : new SqlServerColumnsAnswer(null, [new SqlServerDescribeError(208, 7, "Invalid object name.")], null);
        IReadOnlyList<SqlServerColumnRow> rows = matchNeedsIt
            ? [SalePriceColumn(1)]
            : [SalePriceColumn(1), IdColumn with { ColumnOrdinal = 2, SourceTable = "categories" }];
        var script = Script(sql, rows).Table(Products, answer);

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, sql));

        asked.Select(static call => call.Method).ShouldBe([Calls.Parameters, Calls.Columns, Calls.Table]);
        if (matchNeedsIt)
        {
            _ = described.Failure.ShouldNotBeNull();
        }
        else
        {
            Nullability(described)[0].ShouldBe((true, SidecarValues.NullableSource.Server));
        }
    }

    // An UPDATE's OUTPUT of a column its SET writes asks "operands" for the value written over the UPDATE's rows, after
    // "columns" and before the "table" of the match, and no text names inserted.
    [Fact]
    public async Task DescribeAsync_UpdateOutputOfAWrittenColumn_AsksTheValueBeforeTheMatch()
    {
        const string Sql =
            "UPDATE u SET u.nickname = c.name OUTPUT inserted.id, inserted.nickname "
            + "FROM dbo.users AS u JOIN dbo.customers AS c ON c.id = u.id";
        var operands = new SqlServerColumnsRequest(
            "SELECT c.name\nAS [1]\nFROM dbo.users AS u JOIN dbo.customers AS c ON c.id = u.id",
            null
        );
        var table = new SqlServerTableRequest("dbo", "users");
        var script = Script(Sql, [IdColumn, NicknameColumn, Rowstat with { ColumnOrdinal = 3 }])
            .Operands(operands, new SqlServerColumnsAnswer([OperandRow(1, false)], null, null))
            .Table(table, new SqlServerColumnsAnswer([IdColumn, NicknameColumn, Rowstat], null, null));

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.ShouldBe([
            Call(Calls.Parameters, new SqlServerParametersRequest(Sql, null, null)),
            Call(Calls.Columns, new SqlServerColumnsRequest(Sql, null)),
            Call(Calls.Operands, operands),
            Call(Calls.Table, table),
        ]);
        Nullability(described)
            .ShouldBe([(false, SidecarValues.NullableSource.Server), (false, SidecarValues.NullableSource.Expression)]);
    }

    private static readonly SqlServerTableRequest Products = new("dbo", "products");

    // dbo.products of the realistic schema as "table" describes it: list_price NOT NULL, discount_pct NULL and
    // sale_price computed from them, which the server answers nullable.
    private static readonly IReadOnlyList<SqlServerColumnRow> ProductRows =
    [
        IdColumn with
        {
            SourceTable = "products",
        },
        IdColumn with
        {
            ColumnOrdinal = 2,
            Name = "list_price",
            SourceTable = "products",
            SourceColumn = "list_price",
            IsIdentityColumn = false,
        },
        IdColumn with
        {
            ColumnOrdinal = 3,
            Name = "discount_pct",
            IsNullable = true,
            SourceTable = "products",
            SourceColumn = "discount_pct",
            IsIdentityColumn = false,
        },
        SalePriceColumn(4),
        Rowstat with
        {
            ColumnOrdinal = 5,
        },
    ];

    // dbo.products.sale_price, AS (list_price * (1 - ISNULL(discount_pct, 0) / 100)), in the place given.
    private static SqlServerColumnRow SalePriceColumn(int ordinal) =>
        IdColumn with
        {
            ColumnOrdinal = ordinal,
            Name = "sale_price",
            IsNullable = true,
            SourceTable = "products",
            SourceColumn = "sale_price",
            IsIdentityColumn = false,
            IsComputedColumn = true,
            ComputedDefinition = "([list_price]*((1)-isnull([discount_pct],(0))/(100)))",
        };

    // The row, answered as a stored column of a table.
    private static SqlServerColumnRow StoredOperand(SqlServerColumnRow row) =>
        row with
        {
            SourceType = "U",
            SourceColumn = "x",
            IsComputedColumn = false,
        };

    // A script of a query with no parameter whose "columns" answers the rows.
    private static SqlServerScript Script(string sql, IReadOnlyList<SqlServerColumnRow> rows) =>
        new SqlServerScript().NoParametersThenColumns(sql, new SqlServerColumnsAnswer(rows, null, null));

    private static List<(bool? Nullable, string? Source)> Nullability(
        Outcome<QueryDescription, DescribeFailure> described
    ) =>
        [
            .. described
                .Value.ShouldNotBeNull()
                .Columns.ShouldNotBeNull()
                .Select(static column => (column.Nullable, column.NullableSource)),
        ];
}
