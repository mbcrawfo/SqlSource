using System;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Columns;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres.Columns;

// The protocol reports a domain column as its base type, so only the catalog knows that the column is a domain.
public sealed class ColumnTypesTests
{
    private static readonly PostgresTableRow Table = PostgresTables.TableOf(
        7,
        "t",
        PostgresTables.Attribute(1, "plain", PostgresRows.Int4.Oid),
        PostgresTables.Attribute(2, "mail", PostgresRows.EmailT.Oid),
        PostgresTables.Attribute(3, "mail2", PostgresRows.D2.Oid),
        PostgresTables.Attribute(4, "tags", PostgresRows.TextArray.Oid)
    );

    private static long OidOf(long reported, int attnum, params PostgresTypeRow[] rows) =>
        ColumnTypes.OidOf(
            PostgresTables.ColumnOf(0, "c", reported, Table, attnum),
            ColumnOrigin.Find(PostgresTables.ColumnOf(0, "c", reported, Table, attnum), PostgresTables.Of(Table)),
            PostgresRows.Of(rows)
        );

    [Fact]
    public void OidOf_ColumnWhoseOriginIsADomain_IsTheDomain() =>
        OidOf(PostgresRows.Varchar.Oid, 2, PostgresRows.EmailT, PostgresRows.Varchar).ShouldBe(PostgresRows.EmailT.Oid);

    // The protocol reports the base at the end of the chain, and the column is the outermost domain.
    [Fact]
    public void OidOf_ColumnWhoseOriginIsADomainOverADomain_IsTheOutermost() =>
        OidOf(PostgresRows.Varchar.Oid, 3, PostgresRows.D2, PostgresRows.EmailT, PostgresRows.Varchar)
            .ShouldBe(PostgresRows.D2.Oid);

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void OidOf_ColumnWhoseOriginIsNoDomain_IsTheReportedType(int attnum)
    {
        var reported = Table.Columns[attnum == 1 ? 0 : 3].TypeOid;

        OidOf(reported, attnum, PostgresRows.Int4, PostgresRows.TextArray, PostgresRows.Text).ShouldBe(reported);
    }

    // The two answers disagree: the column keeps the type the protocol reported, which is what a reader gets.
    [Fact]
    public void OidOf_DomainWhoseBaseDoesNotReachTheReportedType_IsTheReportedType() =>
        OidOf(PostgresRows.Text.Oid, 2, PostgresRows.EmailT, PostgresRows.Varchar, PostgresRows.Text)
            .ShouldBe(PostgresRows.Text.Oid);

    [Fact]
    public void OidOf_ColumnWithoutAnOrigin_IsTheReportedType() =>
        ColumnTypes
            .OidOf(PostgresTables.ExpressionOf(0, "c", PostgresRows.Varchar.Oid, 254), null, PostgresRows.Of())
            .ShouldBe(1043);

    [Fact]
    public void OidOf_OriginTypeTheRowsLack_ThrowsNamingIt() =>
        Should
            .Throw<InvalidOperationException>(() => OidOf(PostgresRows.Varchar.Oid, 2, PostgresRows.Varchar))
            .Message.ShouldContain(PostgresRows.EmailT.Oid.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
