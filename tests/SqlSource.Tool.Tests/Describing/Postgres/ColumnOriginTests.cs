using System;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// Where a column comes from, by the table OID and the attribute number of its row description.
public sealed class ColumnOriginTests
{
    private static readonly PostgresTableRow Users = PostgresTables.Users;

    [Fact]
    public void Find_ColumnOfATable_IsItsTableAndItsAttribute()
    {
        var origin = ColumnOrigin
            .Find(PostgresTables.ColumnOf(0, "mail", PostgresRows.Varchar.Oid, Users, 3), PostgresTables.Of(Users))
            .ShouldNotBeNull();

        origin.Table.ShouldBeSameAs(Users);
        origin.Attribute.Name.ShouldBe("email");
    }

    [Fact]
    public void Find_ColumnOfNoTable_IsNoOrigin() =>
        ColumnOrigin
            .Find(PostgresTables.ExpressionOf(0, "n", PostgresRows.Int8.Oid), PostgresTables.Of(Users))
            .ShouldBeNull();

    // ctid and xmin report their table and an attribute number below zero.  Zero is no column either.  The table
    // may have no column at all, and may be missing from an answer the test writes: neither is looked at.
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
                ColumnOrigin.Find(
                    PostgresTables.ColumnOf(0, "mail", PostgresRows.Varchar.Oid, Users, 3),
                    PostgresTables.Of()
                )
            )
            .Message.ShouldContain("'mail'");

    [Fact]
    public void Find_AttributeTheAnswerLacks_ThrowsNamingTheTable() =>
        Should
            .Throw<InvalidOperationException>(() =>
                ColumnOrigin.Find(
                    PostgresTables.ColumnOf(0, "x", PostgresRows.Int4.Oid, Users, 42),
                    PostgresTables.Of(Users)
                )
            )
            .Message.ShouldContain("'users'");

    // What Find would not throw for, nor look up: the answer is whole, or the column names no table.
    [Fact]
    public void MissingFrom_ColumnOfATable_IsNull() =>
        ColumnOrigin
            .MissingFrom(
                PostgresTables.ColumnOf(0, "mail", PostgresRows.Varchar.Oid, Users, 3),
                PostgresTables.Of(Users)
            )
            .ShouldBeNull();

    [Fact]
    public void MissingFrom_ColumnOfNoTable_IsNull() =>
        ColumnOrigin
            .MissingFrom(PostgresTables.ExpressionOf(0, "n", PostgresRows.Int8.Oid), PostgresTables.Of())
            .ShouldBeNull();

    [Theory]
    [InlineData(-1)]
    [InlineData(-6)]
    [InlineData(0)]
    public void MissingFrom_SystemColumn_IsNullWhetherTheAnswerHoldsItsTableOrNot(int attributeNumber)
    {
        var column = new PostgresColumnRow(0, "ctid", 27, Users.Oid, attributeNumber, null, null, null);

        ColumnOrigin.MissingFrom(column, PostgresTables.Of(Users)).ShouldBeNull();
        ColumnOrigin.MissingFrom(column, PostgresTables.Of()).ShouldBeNull();
    }

    [Fact]
    public void MissingFrom_TableTheAnswerLacks_NamesTheColumn() =>
        ColumnOrigin
            .MissingFrom(PostgresTables.ColumnOf(0, "mail", PostgresRows.Varchar.Oid, Users, 3), PostgresTables.Of())
            .ShouldBe("the table of the column 'mail' no longer exists");

    [Fact]
    public void MissingFrom_AttributeTheAnswerLacks_NamesTheColumnAndTheTable() =>
        ColumnOrigin
            .MissingFrom(PostgresTables.ColumnOf(0, "x", PostgresRows.Int4.Oid, Users, 42), PostgresTables.Of(Users))
            .ShouldBe("the column 'x' no longer exists in its table 'public.users'");

    // Every column's origin, in order, where the answer holds each: none for an expression or a system column.
    [Fact]
    public void FindAll_EveryColumnFound_IsEachOriginInOrder()
    {
        var origins = ColumnOrigin
            .FindAll(
                [
                    PostgresTables.ColumnOf(0, "mail", PostgresRows.Varchar.Oid, Users, 3),
                    PostgresTables.ExpressionOf(1, "n", PostgresRows.Int8.Oid),
                    new PostgresColumnRow(2, "ctid", 27, Users.Oid, -1, null, null, null),
                ],
                PostgresTables.Of(Users)
            )
            .Value.ShouldNotBeNull();

        origins.Length.ShouldBe(3);
        origins[0].ShouldNotBeNull().Attribute.Name.ShouldBe("email");
        origins[1].ShouldBeNull();
        origins[2].ShouldBeNull();
    }

    // The first column whose origin the answer lacks, in column order, as MissingFrom says it.
    [Fact]
    public void FindAll_TwoColumnsMissing_IsTheFirstOnesMessage()
    {
        var found = ColumnOrigin.FindAll(
            [
                PostgresTables.ExpressionOf(0, "n", PostgresRows.Int8.Oid),
                PostgresTables.ColumnOf(1, "x", PostgresRows.Int4.Oid, Users, 42),
                PostgresTables.ColumnOf(2, "y", PostgresRows.Int4.Oid, PostgresTables.TableOf(16999, "gone"), 1),
            ],
            PostgresTables.Of(Users)
        );

        found.Succeeded.ShouldBeFalse();
        found.Failure.ShouldBe("the column 'x' no longer exists in its table 'public.users'");
    }
}
