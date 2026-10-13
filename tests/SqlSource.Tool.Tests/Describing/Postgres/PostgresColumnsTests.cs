using System;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tests.Snapshot;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// One column of a sidecar from one row of "columns".
public class PostgresColumnsTests
{
    [Fact]
    public void Build_Column_HasItsOrdinalItsNameAndItsTypeWithFacets() =>
        PostgresColumns
            .Build(new PostgresColumnRow(2, "balance", 1700, 0, 0, null, 18, 2), PostgresRows.Of(PostgresRows.Numeric))
            .ShouldBe(
                new SidecarColumn(
                    2,
                    "balance",
                    TestSidecars.Postgres("numeric(18,2)", internalName: "numeric", precision: 18, scale: 2),
                    null,
                    SidecarValues.NullableSource.NoOrigin,
                    null,
                    null,
                    null
                )
            );

    // Npgsql gives varchar(10)[] the size 10, and the element is recorded without it.
    [Fact]
    public void Build_ArrayColumn_DropsTheFacetsOfItsElement()
    {
        var column = PostgresColumns.Build(
            new PostgresColumnRow(0, "names", 1015, 0, 0, 10, null, null),
            PostgresRows.Of(PostgresRows.VarcharArray, PostgresRows.Varchar)
        );

        column.Type.ShouldBe(
            TestSidecars.Postgres(
                "character varying[]",
                SidecarValues.Kind.Array,
                internalName: "_varchar",
                element: TestSidecars.Postgres("character varying", internalName: "varchar")
            )
        );
    }

    [Fact]
    public void Build_TypeTheRowsLack_ThrowsNamingIt() =>
        Should
            .Throw<InvalidOperationException>(() =>
                PostgresColumns.Build(new PostgresColumnRow(0, "c", 23, 0, 0, null, null, null), PostgresRows.Of())
            )
            .Message.ShouldContain("23");
}
