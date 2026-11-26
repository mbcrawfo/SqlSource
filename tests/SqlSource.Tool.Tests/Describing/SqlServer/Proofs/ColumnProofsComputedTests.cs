using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Describing.SqlServer.Proofs;
using Xunit;
using static SqlSource.Tool.Tests.ColumnProofsCases;
using static SqlSource.Tool.Tests.SqlServerScript;

namespace SqlSource.Tool.Tests.Describing.SqlServer.Proofs;

// The proofs of a computed column read from a table that no join of the query can make NULL: by its definition over
// the rows of "table" for its table, or, PERSISTED NOT NULL, by the catalog.
public sealed class ColumnProofsComputedTests
{
    // sale_price AS (list_price * (1 - ISNULL(discount_pct, 0) / 100)), as the server writes it.
    private const string SalePrice = "([list_price]*((1)-isnull([discount_pct],(0))/(100)))";

    private static readonly (string Schema, string Table) ProductsTable = ("dbo", "products");

    // dbo.products.id, a stored column the server answers not null.
    private static readonly SqlServerColumnRow ProductId = IdColumn with { SourceTable = "products" };

    // dbo.products.sale_price, which the server answers nullable by its expression, in the place given.
    private static SqlServerColumnRow SalePriceColumn(int ordinal) =>
        ProductId with
        {
            ColumnOrdinal = ordinal,
            Name = "sale_price",
            IsNullable = true,
            SystemTypeName = "decimal(31,9)",
            MaxLength = 17,
            Precision = 31,
            Scale = 9,
            SourceColumn = "sale_price",
            IsIdentityColumn = false,
            IsComputedColumn = true,
            ComputedDefinition = SalePrice,
        };

    // A stored column of dbo.products, in the place given.
    private static SqlServerColumnRow ProductColumn(int ordinal, string name, bool nullable) =>
        ProductId with
        {
            ColumnOrdinal = ordinal,
            Name = name,
            IsNullable = nullable,
            SourceColumn = name,
            IsIdentityColumn = false,
        };

    // The rows of "table" for dbo.products: list_price NOT NULL and discount_pct NULL, as the realistic schema has
    // them, or list_price NULL too.
    private static IReadOnlyList<SqlServerColumnRow> ProductRows(bool listPriceNullable = false) =>
        [
            ProductId,
            ProductColumn(2, "list_price", listPriceNullable),
            ProductColumn(3, "discount_pct", nullable: true),
            SalePriceColumn(4),
            Rowstat with
            {
                ColumnOrdinal = 5,
            },
        ];

    private static Dictionary<(string Schema, string Table), IReadOnlyList<SqlServerColumnRow>> Tables(
        bool listPriceNullable = false
    ) => new() { [ProductsTable] = ProductRows(listPriceNullable) };

    // The column read from the preserved side of a RIGHT JOIN, its one table, an inner or a LEFT JOIN's first source,
    // or a source of one part that no CTE is named: every row of the result is a row of dbo.products.
    [Theory]
    [InlineData(
        "SELECT p.id, p.sale_price FROM dbo.categories AS c RIGHT JOIN dbo.products AS p ON p.category_id = c.id "
            + "WHERE p.discontinued_at IS NULL ORDER BY c.name"
    )]
    [InlineData("SELECT id, sale_price FROM dbo.products")]
    [InlineData("SELECT p.id, p.sale_price FROM dbo.products AS p JOIN dbo.categories AS c ON c.id = p.category_id")]
    [InlineData(
        "SELECT p.id, p.sale_price FROM dbo.products AS p LEFT JOIN dbo.categories AS c ON c.id = p.category_id"
    )]
    [InlineData("SELECT p.id, p.sale_price FROM dbo.products AS p CROSS JOIN dbo.categories AS c")]
    [InlineData("SELECT p.id, p.sale_price FROM dbo.products AS p GROUP BY p.id, p.sale_price")]
    [InlineData("WITH q AS (SELECT 1 AS one) SELECT p.id, p.sale_price FROM products AS p")]
    public void Apply_ComputedColumnOfATableNoJoinMakesNull_Expression(string sql)
    {
        var proofs = Read(sql, ProductId, SalePriceColumn(2));

        proofs.OperandQueries.ShouldBeEmpty();
        proofs.ComputedTables.ShouldBe([ProductsTable]);
        Nullability(proofs.Apply(null, null, Tables()))
            .ShouldBe([(false, SidecarValues.NullableSource.Server), (false, SidecarValues.NullableSource.Expression)]);
        Nullability(proofs.Apply(null, null, Tables(listPriceNullable: true)))
            .ShouldBe([(false, SidecarValues.NullableSource.Server), (true, SidecarValues.NullableSource.Server)]);
        Nullability(proofs.Apply(null, null, new Dictionary<(string, string), IReadOnlyList<SqlServerColumnRow>>()))
            .ShouldBe([(false, SidecarValues.NullableSource.Server), (true, SidecarValues.NullableSource.Server)]);
        Nullability(proofs.Apply(null))
            .ShouldBe([(false, SidecarValues.NullableSource.Server), (true, SidecarValues.NullableSource.Server)]);
    }

    // A * of the one table, and a q.* of a table no join makes NULL, are each column of it.
    [Theory]
    [InlineData("SELECT * FROM dbo.products")]
    [InlineData("SELECT p.* FROM dbo.products AS p JOIN dbo.categories AS c ON c.id = p.category_id")]
    [InlineData("SELECT p.* FROM dbo.categories AS c RIGHT OUTER JOIN dbo.products AS p ON p.category_id = c.id")]
    public void Apply_ComputedColumnByStar_Expression(string sql)
    {
        var proofs = Read(sql, ProductId, SalePriceColumn(2));

        proofs.ComputedTables.ShouldBe([ProductsTable]);
        Nullability(proofs.Apply(null, null, Tables()))
            .ShouldBe([(false, SidecarValues.NullableSource.Server), (false, SidecarValues.NullableSource.Expression)]);
    }

    // The optional side of an outer join, which the server still names as the source, a derived table or a CTE that
    // passes the column on, a one-part name that is a CTE's in another case, a bare name of two sources, a grouping
    // set's total, a set operator, parentheses, an expression, a * of several sources and a q.* of an optional one:
    // a row of the result may be no row of the table, and no table is asked.
    [Theory]
    [InlineData(
        "SELECT p.id, p.sale_price FROM dbo.categories AS c LEFT JOIN dbo.products AS p ON p.category_id = c.id"
    )]
    [InlineData(
        "SELECT p.id, p.sale_price FROM dbo.products AS p RIGHT JOIN dbo.categories AS c ON c.id = p.category_id"
    )]
    [InlineData(
        "SELECT p.id, p.sale_price FROM dbo.products AS p FULL JOIN dbo.categories AS c ON c.id = p.category_id"
    )]
    [InlineData(
        "SELECT p.id, p.sale_price FROM dbo.categories AS c FULL JOIN dbo.products AS p ON p.category_id = c.id"
    )]
    [InlineData(
        "SELECT p.id, p.sale_price FROM dbo.categories AS c LEFT JOIN dbo.products AS p ON p.category_id = c.id "
            + "WHERE p.list_price > 0"
    )]
    [InlineData("SELECT p.id, p.sale_price FROM (SELECT * FROM dbo.products) AS p")]
    [InlineData(
        "SELECT q.id, q.sale_price FROM dbo.categories AS c "
            + "RIGHT JOIN (SELECT p.id, p.sale_price, p.category_id FROM dbo.categories AS k "
            + "LEFT JOIN dbo.products AS p ON p.category_id = k.id) AS q ON q.category_id = c.id"
    )]
    [InlineData("WITH p AS (SELECT * FROM dbo.products) SELECT p.id, p.sale_price FROM p")]
    [InlineData("WITH P AS (SELECT * FROM dbo.products) SELECT p.id, p.sale_price FROM p AS p")]
    [InlineData("WITH Products AS (SELECT * FROM dbo.products) SELECT id, sale_price FROM products")]
    [InlineData("SELECT id, sale_price FROM dbo.products AS p JOIN dbo.categories AS c ON c.id = p.category_id")]
    [InlineData("SELECT p.id, p.sale_price FROM dbo.products AS p GROUP BY ROLLUP (p.id, p.sale_price)")]
    [InlineData("SELECT p.id, p.sale_price FROM dbo.products AS p GROUP BY ALL p.id, p.sale_price")]
    [InlineData("SELECT * FROM dbo.products GROUP BY ROLLUP (id, sale_price)")]
    [InlineData("SELECT * FROM dbo.products GROUP BY GROUPING SETS ((id, sale_price), ())")]
    [InlineData("SELECT * FROM dbo.products GROUP BY ALL id, sale_price")]
    [InlineData("SELECT id, sale_price FROM dbo.products UNION ALL SELECT id, sale_price FROM dbo.products")]
    [InlineData("SELECT id, (sale_price) AS sale_price FROM dbo.products")]
    [InlineData("SELECT id, sale_price + 0 AS sale_price FROM dbo.products")]
    [InlineData("SELECT * FROM dbo.products AS p JOIN dbo.categories AS c ON c.id = p.category_id")]
    [InlineData("SELECT p.* FROM dbo.categories AS c LEFT JOIN dbo.products AS p ON p.category_id = c.id")]
    [InlineData("SELECT p.*, p.* FROM dbo.products AS p")]
    [InlineData("SELECT dbo.p.* FROM dbo.products AS p JOIN dbo.categories AS c ON c.id = p.category_id")]
    public void Apply_ComputedColumnThatMayBeNoRowOfItsTable_StaysNullable(string sql)
    {
        var proofs = Read(sql, ProductId, SalePriceColumn(2));

        proofs.ComputedTables.ShouldBeEmpty();
        Nullability(proofs.Apply(null, null, Tables()))[^1].ShouldBe((true, SidecarValues.NullableSource.Server));
    }

    // A row that is no computed column of a table with its definition, one of a CLR type, and one the server answers
    // not null, asks no table, though its item reads the table alone.
    [Theory]
    [InlineData("not computed")]
    [InlineData("view")]
    [InlineData("no definition")]
    [InlineData("clr")]
    [InlineData("not null")]
    public void Read_RowThatIsNoComputedColumnToProve_AsksNoTable(string how)
    {
        var row = SalePriceColumn(2);
        row = how switch
        {
            "not computed" => row with { IsComputedColumn = false },
            "view" => row with { SourceType = "V" },
            "no definition" => row with { ComputedDefinition = null },
            "clr" => row with
            {
                SystemTypeName = "geometry",
                AssemblyQualifiedTypeName = "Microsoft.SqlServer.Types.SqlGeometry, Microsoft.SqlServer.Types",
            },
            _ => row with { IsNullable = false },
        };

        var proofs = Read("SELECT id, sale_price FROM dbo.products", ProductId, row);

        proofs.ComputedTables.ShouldBeEmpty();
    }

    // Each table once, in the order of the columns.
    [Fact]
    public void ComputedTables_TwoColumnsOfOneTable_AskItOnce()
    {
        var proofs = Read(
            "SELECT p.sale_price, p.sale_price AS again, o.total FROM dbo.products AS p "
                + "JOIN dbo.orders AS o ON o.id = p.id",
            SalePriceColumn(1),
            SalePriceColumn(2) with
            {
                Name = "again",
            },
            SalePriceColumn(3) with
            {
                Name = "total",
                SourceTable = "orders",
                SourceColumn = "total",
            }
        );

        proofs.ComputedTables.ShouldBe([ProductsTable, ("dbo", "orders")]);
    }

    // A PERSISTED NOT NULL column read from a table no join makes NULL is a row of the table, by a column alone or by
    // the one q.* of that table.
    [Theory]
    [InlineData("SELECT o.id, u.name_len_p FROM dbo.orders AS o JOIN dbo.users AS u ON u.id = o.user_id")]
    [InlineData("SELECT u.id, u.name_len_p FROM dbo.users AS u LEFT JOIN dbo.orders AS o ON o.user_id = u.id")]
    [InlineData("SELECT u.id, u.name_len_p FROM dbo.orders AS o RIGHT JOIN dbo.users AS u ON u.id = o.user_id")]
    public void Apply_PersistedNotNullOfATableNoJoinMakesNull_Catalog(string sql)
    {
        var proofs = Read(sql, IdColumn, PersistedColumn(2));

        proofs.ComputedTables.ShouldBeEmpty();
        Nullability(proofs.Apply(null))
            .ShouldBe([(false, SidecarValues.NullableSource.Server), (false, SidecarValues.NullableSource.Catalog)]);
    }

    [Fact]
    public void Apply_PersistedNotNullByTheStarOfATableNoJoinMakesNull_Catalog()
    {
        var proofs = Read(
            "SELECT u.* FROM dbo.orders AS o JOIN dbo.users AS u ON u.id = o.user_id",
            IdColumn,
            PersistedColumn(2)
        );

        Nullability(proofs.Apply(null))
            .ShouldBe([(false, SidecarValues.NullableSource.Server), (false, SidecarValues.NullableSource.Catalog)]);
    }

    // The rows of "table" are SELECT * from the table alone, so a computed row whose definition is proved over them is
    // not nullable there, and a query the proof corrected still matches its table; one whose definition is not proved,
    // or that is not of a table, comes as it came.
    [Fact]
    public void TableRows_ComputedColumnProvedOverTheTable_NotNullable()
    {
        IReadOnlyList<SqlServerColumnRow> rows =
        [
            .. ProductRows().SkipLast(1),
            SalePriceColumn(5) with
            {
                Name = "doubled_discount",
                SourceColumn = "doubled_discount",
                ComputedDefinition = "([discount_pct]*(2))",
            },
            SalePriceColumn(6) with
            {
                Name = "of_view",
                SourceColumn = "of_view",
                SourceType = "V",
            },
            Rowstat with
            {
                ColumnOrdinal = 7,
            },
        ];

        ColumnProofs
            .TableRows(rows, [])
            .Select(static row => row.IsNullable)
            .ShouldBe([false, false, true, false, true, true, false]);
    }
}
