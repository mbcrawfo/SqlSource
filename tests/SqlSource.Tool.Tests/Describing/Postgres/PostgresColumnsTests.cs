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
            .Build(
                new PostgresColumnRow(2, "balance", 1700, 0, 0, null, 18, 2),
                null,
                PostgresRows.Of(PostgresRows.Numeric)
            )
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
            null,
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
                PostgresColumns.Build(
                    new PostgresColumnRow(0, "c", 23, 0, 0, null, null, null),
                    null,
                    PostgresRows.Of()
                )
            )
            .Message.ShouldContain("23");

    [Fact]
    public void Build_ColumnWithAnOrigin_HasItAndSaysWhetherItIsIdentityOrComputed()
    {
        var users = PostgresTables.Users;
        var rows = PostgresRows.Of(PostgresRows.Int4, PostgresRows.Text);

        var id = Build(PostgresTables.ColumnOf(0, "user_id", 23, users, 1), users, rows);
        var upper = Build(PostgresTables.ColumnOf(1, "u", 25, users, 9), users, rows);

        id.Origin.ShouldBe(new SidecarOrigin("public", "users", "id"));
        (id.Identity, id.Computed).ShouldBe((true, false));
        upper.Origin.ShouldBe(new SidecarOrigin("public", "users", "name_upper"));
        (upper.Identity, upper.Computed).ShouldBe((false, true));
    }

    [Fact]
    public void Build_ColumnWithoutAnOrigin_SaysNothingOfIdentityOrComputed()
    {
        var column = PostgresColumns.Build(
            new PostgresColumnRow(0, "n", 23, 0, 0, null, null, null),
            null,
            PostgresRows.Of(PostgresRows.Int4)
        );

        column.Origin.ShouldBeNull();
        column.Identity.ShouldBeNull();
        column.Computed.ShouldBeNull();
    }

    // The domain is the column's type, and the modifier the protocol reported lands on its base.
    [Fact]
    public void Build_ColumnWhoseOriginIsADomain_IsTheDomainWithTheFacetsOnItsBase()
    {
        var users = PostgresTables.Users;

        var column = Build(
            PostgresTables.ColumnOf(0, "email", 1043, users, 3, length: 254),
            users,
            PostgresRows.Of(PostgresRows.EmailT, PostgresRows.Varchar)
        );

        column.Type.ShouldBe(
            TestSidecars.Postgres(
                "public.email_t",
                SidecarValues.Kind.Domain,
                "public",
                "email_t",
                baseType: TestSidecars.Postgres("character varying(254)", internalName: "varchar", length: 254)
            )
        );
    }

    private static SidecarColumn Build(
        PostgresColumnRow column,
        PostgresTableRow table,
        System.Collections.Generic.IReadOnlyDictionary<long, PostgresTypeRow> rows
    ) => PostgresColumns.Build(column, ColumnOrigin.Find(column, PostgresTables.Of(table)), rows);
}
