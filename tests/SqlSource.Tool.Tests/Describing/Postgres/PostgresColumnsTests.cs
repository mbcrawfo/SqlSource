using System;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing.Snapshot;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// One column of a sidecar from one row of "columns".
public class PostgresColumnsTests
{
    [Fact]
    public void Build_Column_HasItsOrdinalItsNameAndItsTypeWithFacets() =>
        PostgresColumns
            .Build(
                PostgresTables.ExpressionOf(2, "balance", PostgresRows.Numeric.Oid, null, 18, 2),
                null,
                PostgresRows.Of(PostgresRows.Numeric),
                TypeSpellings.None,
                false
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
            PostgresTables.ExpressionOf(0, "names", PostgresRows.VarcharArray.Oid, 10),
            null,
            PostgresRows.Of(PostgresRows.VarcharArray, PostgresRows.Varchar),
            TypeSpellings.None,
            false
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
                    PostgresTables.ExpressionOf(0, "c", PostgresRows.Int4.Oid),
                    null,
                    PostgresRows.Of(),
                    TypeSpellings.None,
                    false
                )
            )
            .Message.ShouldContain("23");

    [Fact]
    public void Build_ColumnWithAnOrigin_HasItAndSaysWhetherItIsIdentityOrComputed()
    {
        var users = PostgresTables.Users;
        var rows = PostgresRows.Of(PostgresRows.Int4, PostgresRows.Text);

        var id = Build(PostgresTables.ColumnOf(0, "user_id", PostgresRows.Int4.Oid, users, 1), users, rows);
        var upper = Build(PostgresTables.ColumnOf(1, "u", PostgresRows.Text.Oid, users, 9), users, rows);

        id.Origin.ShouldBe(new SidecarOrigin("public", "users", "id"));
        (id.Identity, id.Computed).ShouldBe((true, false));
        upper.Origin.ShouldBe(new SidecarOrigin("public", "users", "name_upper"));
        (upper.Identity, upper.Computed).ShouldBe((false, true));
    }

    [Fact]
    public void Build_NotNullColumnOfATable_IsNotNullableUnlessTheQueryIsGated()
    {
        var users = PostgresTables.Users;
        var rows = PostgresRows.Of(PostgresRows.Int4);
        var column = PostgresTables.UsersId;

        var plain = Build(column, users, rows);
        var gated = Build(column, users, rows, gated: true);

        (plain.Nullable, plain.NullableSource).ShouldBe((false, SidecarValues.NullableSource.Catalog));
        (gated.Nullable, gated.NullableSource).ShouldBe((true, SidecarValues.NullableSource.Heuristic));
    }

    [Fact]
    public void Build_ColumnWithoutAnOrigin_SaysNothingOfIdentityOrComputed()
    {
        var column = PostgresColumns.Build(
            PostgresTables.ExpressionOf(0, "n", PostgresRows.Int4.Oid),
            null,
            PostgresRows.Of(PostgresRows.Int4),
            TypeSpellings.None,
            false
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
            PostgresTables.ColumnOf(0, "email", PostgresRows.Varchar.Oid, users, 3, length: 254),
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

    // A column whose capture holds the row description's modifier: the server spells the type with it, and the
    // facets are decoded from it, the element's of an array included.  Npgsql's numbers are not read.
    [Fact]
    public void Build_ColumnWithATypeModifier_IsTheServersSpellingWithTheDecodedFacets()
    {
        var column = PostgresColumns.Build(
            PostgresTables.ExpressionOf(0, "names", PostgresRows.VarcharArray.Oid, 99, null, null, Typmods.Varchar10),
            null,
            PostgresRows.Of(PostgresRows.VarcharArray, PostgresRows.Varchar),
            TypeSpellings.From([new PostgresSpellingRow(1043, Typmods.Varchar10, "character varying(10)")]),
            false
        );

        column.Type.ShouldBe(
            TestSidecars.Postgres(
                "character varying(10)[]",
                SidecarValues.Kind.Array,
                internalName: "_varchar",
                element: TestSidecars.Postgres("character varying(10)", internalName: "varchar", length: 10)
            )
        );
    }

    // The row description gives a column of a domain its base and the domain's modifier.
    [Fact]
    public void Build_ColumnOfADomainWithATypeModifier_IsTheDomainWithItsBaseSpelled()
    {
        var users = PostgresTables.Users;

        var column = Build(
            PostgresTables.ColumnOf(0, "email", PostgresRows.Varchar.Oid, users, 3) with
            {
                TypeModifier = Typmods.Varchar254,
            },
            users,
            PostgresRows.Of(PostgresRows.EmailT, PostgresRows.Varchar),
            TypeSpellings.From([new PostgresSpellingRow(1043, Typmods.Varchar254, "character varying(254)")])
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
        System.Collections.Generic.IReadOnlyDictionary<long, PostgresTypeRow> rows,
        TypeSpellings? spellings = null,
        bool gated = false
    ) =>
        PostgresColumns.Build(
            column,
            ColumnOrigin.Find(column, PostgresTables.Of(table)),
            rows,
            spellings ?? TypeSpellings.None,
            gated
        );
}
