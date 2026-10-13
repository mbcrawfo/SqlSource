using System;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// Where a column comes from, by the table OID and the attribute number of its row description.
public class ColumnOriginTests
{
    private static readonly PostgresTableRow Users = PostgresTables.Users;

    [Fact]
    public void Find_ColumnOfATable_IsItsTableAndItsAttribute()
    {
        var origin = ColumnOrigin
            .Find(PostgresTables.ColumnOf(0, "mail", 1043, Users, 3), PostgresTables.Of(Users))
            .ShouldNotBeNull();

        origin.Table.ShouldBeSameAs(Users);
        origin.Attribute.Name.ShouldBe("email");
    }

    [Fact]
    public void Find_ColumnOfNoTable_IsNoOrigin() =>
        ColumnOrigin
            .Find(new PostgresColumnRow(0, "n", 20, 0, 0, null, null, null), PostgresTables.Of(Users))
            .ShouldBeNull();

    // ctid and xmin report their table and an attribute number below zero.  The table may have no column at all,
    // and may be missing from an answer the test writes: neither is looked at.
    [Theory]
    [InlineData(-1)]
    [InlineData(-6)]
    [InlineData(0)]
    public void Find_SystemColumn_IsNoOrigin(int attributeNumber)
    {
        var column = new PostgresColumnRow(0, "ctid", 27, Users.Oid, attributeNumber, null, null, null);

        ColumnOrigin.Find(column, PostgresTables.Of(Users)).ShouldBeNull();
        ColumnOrigin.Find(column, PostgresTables.Of()).ShouldBeNull();
        ColumnOrigin.Find(column, PostgresTables.Of(PostgresTables.TableOf(Users.Oid, "users"))).ShouldBeNull();
    }

    [Fact]
    public void Find_TableTheAnswerLacks_ThrowsNamingTheColumn() =>
        Should
            .Throw<InvalidOperationException>(() =>
                ColumnOrigin.Find(PostgresTables.ColumnOf(0, "mail", 1043, Users, 3), PostgresTables.Of())
            )
            .Message.ShouldContain("'mail'");

    [Fact]
    public void Find_AttributeTheAnswerLacks_ThrowsNamingTheTable() =>
        Should
            .Throw<InvalidOperationException>(() =>
                ColumnOrigin.Find(PostgresTables.ColumnOf(0, "x", 23, Users, 42), PostgresTables.Of(Users))
            )
            .Message.ShouldContain("'users'");
}
