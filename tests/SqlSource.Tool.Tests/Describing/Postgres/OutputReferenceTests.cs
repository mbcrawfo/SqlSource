using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// An entry of a plan's Output read as a plain reference to a column, as PostgreSQL prints one: a name it need not
// quote is lower-case letters, digits and underscores, and any other is in double quotes with "" for a quote.
public class OutputReferenceTests
{
    [Theory]
    [InlineData("u.id", "u", "id")]
    [InlineData("users.id", "users", "id")]
    [InlineData("u_1.id", "u_1", "id")]
    [InlineData("\"Order\".total", "Order", "total")]
    [InlineData("o.\"Total Due\"", "o", "Total Due")]
    [InlineData("\"a\"\"b\".c", "a\"b", "c")]
    [InlineData("\"a.b\".c", "a.b", "c")]
    [InlineData("\"left\".\"right\"", "left", "right")]
    public void Parse_QualifiedName_IsTheAliasAndTheColumn(string entry, string alias, string column) =>
        OutputReference.Parse(entry).ShouldBe(new OutputReference(alias, column));

    [Theory]
    [InlineData("id", "id")]
    [InlineData("_x1", "_x1")]
    [InlineData("\"Total\"", "Total")]
    public void Parse_BareName_IsTheColumnWithNoAlias(string entry, string column) =>
        OutputReference.Parse(entry).ShouldBe(new OutputReference(null, column));

    [Theory]
    [InlineData("")]
    [InlineData("$1")]
    [InlineData("1")]
    [InlineData("NULL::numeric")]
    [InlineData("(SubPlan 1)")]
    [InlineData("COALESCE(u.id, p.id)")]
    [InlineData("count(*)")]
    [InlineData("count(*) OVER (?)")]
    [InlineData("u.id + 1")]
    [InlineData("(u.id)")]
    [InlineData("u.id::text")]
    [InlineData("u.*")]
    [InlineData("\"left\"((u.name)::text, 1)")]
    [InlineData("a.b.c")]
    [InlineData("u.")]
    [InlineData(".id")]
    [InlineData("u..id")]
    [InlineData("u.1x")]
    [InlineData("U.id")]
    [InlineData("u.Id")]
    [InlineData("\"open")]
    [InlineData("\"\".id")]
    [InlineData("'u.id'")]
    [InlineData("u.id ")]
    [InlineData(" u.id")]
    public void Parse_Expression_IsNoReference(string entry) => OutputReference.Parse(entry).ShouldBeNull();
}
