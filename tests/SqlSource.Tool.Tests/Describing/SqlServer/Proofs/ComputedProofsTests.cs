using System.Collections.Generic;
using Shouldly;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Describing.SqlServer.Proofs;
using Xunit;
using static SqlSource.Tool.Tests.SqlServerScript;

namespace SqlSource.Tool.Tests.Describing.SqlServer.Proofs;

// The proof of a computed column's definition, as sys.computed_columns writes it, over the rows of "table" for its
// table: each operand that is one name is answered by the table's column of that name.
public sealed class ComputedProofsTests
{
    // dbo.products of the realistic schema: list_price NOT NULL, discount_pct NULL, and sale_price computed from them.
    private static readonly IReadOnlyList<SqlServerColumnRow> Products =
    [
        Row(1, "id", nullable: false),
        Row(2, "list_price", nullable: false),
        Row(3, "discount_pct", nullable: true),
        Row(4, "sale_price", nullable: true) with
        {
            IsComputedColumn = true,
            ComputedDefinition = SalePrice,
        },
        Row(5, "shape", nullable: true) with
        {
            SystemTypeName = "geometry",
            AssemblyQualifiedTypeName = "Microsoft.SqlServer.Types.SqlGeometry, Microsoft.SqlServer.Types",
        },
        Row(6, "solid", nullable: false) with
        {
            SystemTypeName = "geometry",
            AssemblyQualifiedTypeName = "Microsoft.SqlServer.Types.SqlGeometry, Microsoft.SqlServer.Types",
        },
        Row(7, "name_len", nullable: false) with
        {
            IsComputedColumn = true,
        },
        Rowstat with
        {
            ColumnOrdinal = 8,
        },
    ];

    // sale_price AS (list_price * (1 - ISNULL(discount_pct, 0) / 100)), as the server writes it.
    private const string SalePrice = "([list_price]*((1)-isnull([discount_pct],(0))/(100)))";

    [Fact]
    public void Proves_TheRealisticSalePrice_Holds() => ComputedProofs.Proves(SalePrice, Products).ShouldBeTrue();

    // The definition alone, arithmetic of literals, a form over a nullable column with a literal, and a column of the
    // table that is not null.
    [Theory]
    [InlineData("([list_price])")]
    [InlineData("((1)+(2))")]
    [InlineData("(isnull([discount_pct],(0)))")]
    [InlineData("(coalesce([discount_pct],[list_price]))")]
    public void Proves_DefinitionOfColumnsThatAreNotNull_Holds(string definition) =>
        ComputedProofs.Proves(definition, Products).ShouldBeTrue();

    [Theory]
    // A nullable column, or one the server answers not null that is itself computed, which judges another expression.
    [InlineData("([discount_pct]*(2))")]
    [InlineData("([name_len]+(1))")]
    // A user's function, a function that is no form, and what is never asked.
    [InlineData("([dbo].[f]([list_price]))")]
    [InlineData("(abs([list_price]))")]
    [InlineData("(getdate())")]
    // A name no row of the table has, or has in another case, and a hidden row's.
    [InlineData("([price]*(2))")]
    [InlineData("([List_Price]*(2))")]
    [InlineData("([ROWSTAT]+(1))")]
    // A CLR column that ISNULL converts, or whose method is called.
    [InlineData("(isnull([shape],[solid]))")]
    [InlineData("([solid].[STArea]())")]
    // A target in brackets, as the server writes one, is no target the proofs take.
    [InlineData("(CONVERT([int],[id]))")]
    // A text that is not one expression.
    [InlineData("")]
    [InlineData("([list_price]),([id])")]
    [InlineData("*")]
    [InlineData("([list_price]) FROM dbo.products")]
    [InlineData("([list_price]) WHERE 1 = 0")]
    [InlineData("([list_price]")]
    [InlineData("TOP (1) [list_price]")]
    public void Proves_DefinitionThatCanBeNullOrIsNotRead_DoesNotHold(string definition) =>
        ComputedProofs.Proves(definition, Products).ShouldBeFalse();

    // A column of the table as browse mode 2 describes it: a stored int column of dbo.products.
    private static SqlServerColumnRow Row(int ordinal, string name, bool nullable) =>
        IdColumn with
        {
            ColumnOrdinal = ordinal,
            Name = name,
            IsNullable = nullable,
            SourceTable = "products",
            SourceColumn = name,
            IsIdentityColumn = false,
        };
}
