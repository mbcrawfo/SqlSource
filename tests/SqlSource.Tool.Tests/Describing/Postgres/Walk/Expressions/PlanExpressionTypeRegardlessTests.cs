using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk.Expressions;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk.Expressions;

// The type of an entry whether or not it is NULL, which rules an entry of a scanned plan out of a column of another
// family: a wrong type here is a false not null, so what the text does not fix has none.
public sealed class PlanExpressionTypeRegardlessTests
{
    // Every reference is a date, as the walk gives a reference's declared type whether or not it is proved.
    private static readonly ExpressionContext Context = new(true, static _ => BuiltInTypes.Date, GroupsHaveRows: true);

    [Theory]
    [InlineData("NULL::date", BuiltInTypes.Date)]
    [InlineData("NULL::bigint", BuiltInTypes.Int8)]
    [InlineData("(NULL::text)", BuiltInTypes.Text)]
    [InlineData("(o.day)::text", BuiltInTypes.Text)]
    [InlineData("((o.day)::text)", BuiltInTypes.Text)]
    [InlineData("(o.day)::numeric(10,2)", BuiltInTypes.Numeric)]
    [InlineData("max(o.day)", BuiltInTypes.Date)]
    [InlineData("(max(o.day))", BuiltInTypes.Date)]
    [InlineData("o.day", BuiltInTypes.Date)]
    [InlineData("(o.day)", BuiltInTypes.Date)]
    [InlineData("'x'::text", BuiltInTypes.Text)]
    [InlineData("count(*)", BuiltInTypes.Int8)]
    public void TypeRegardlessOfNull_FormWithAType_IsThatType(string entry, long type) =>
        PlanExpression.TypeRegardlessOfNull(entry, Context).ShouldBe(type);

    [Theory]
    [InlineData("NULL::account_status")]
    [InlineData("(o.day)::account_status")]
    [InlineData("(o.day)::shadow.text")]
    [InlineData("custom(o.day)")]
    [InlineData("max(o.day) FILTER (WHERE true)")]
    [InlineData("(o.day + o.day")]
    public void TypeRegardlessOfNull_FormWithNoKnownType_IsNull(string entry) =>
        PlanExpression.TypeRegardlessOfNull(entry, Context).ShouldBeNull();

    // A type's name is the catalog's only when the session finds pg_catalog first.
    [Theory]
    [InlineData("NULL::date")]
    [InlineData("(o.day)::text")]
    [InlineData("max(o.day)")]
    public void TypeRegardlessOfNull_CatalogNotFirst_IsNull(string entry) =>
        PlanExpression.TypeRegardlessOfNull(entry, Context with { CatalogFunctions = false }).ShouldBeNull();
}
