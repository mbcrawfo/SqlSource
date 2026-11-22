using System;
using System.Collections.Frozen;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk.Expressions;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk.Expressions;

// Texts one token away from a form, read where the walk would prove all that a form asks of it: every reference of
// "a", SubPlan 1, the groups' rows and the names only pg_catalog holds.  A reader that takes one for the form answers a
// silent wrong not null, so each must stay unknown.  No server need print them: the reader is what keeps them out.
public sealed class PlanExpressionNearFormTests
{
    // A node prints a value of a child in one pair of parentheses, and an operator or a test in a pair of its own.
    [Theory]
    [InlineData("sum(x)", true)]
    [InlineData("sum(x) OVER w1", true)]
    [InlineData("(sum(x) + 1)", true)]
    [InlineData("(x IS NULL)", true)]
    [InlineData("(a AND b)", true)]
    [InlineData("(a OR b)", true)]
    [InlineData("(NOT a)", true)]
    [InlineData("'x'::text", true)]
    [InlineData("(sum(x))", false)]
    [InlineData("(sum(x) OVER w1)", false)]
    [InlineData("((sum(x) + 1))", false)]
    [InlineData("('x'::text)", false)]
    [InlineData("($0)", false)]
    [InlineData("((InitPlan 1).col1)", false)]
    [InlineData("(sum(x", false)]
    public void IsOwnValue_Entry_IsWhetherTheNodeComputesIt(string entry, bool expected) =>
        PlanExpression.IsOwnValue(entry).ShouldBe(expected);

    private static readonly CatalogNames AllNames = new(
        CatalogOverloads.FunctionNames.ToFrozenSet(StringComparer.Ordinal),
        CatalogOverloads.OperatorNames.ToFrozenSet(StringComparer.Ordinal)
    );

    private static ExpressionContext ProvingAll(Func<string, bool>? groupPreserved = null) =>
        new(
            CatalogFunctions: true,
            ProvedTypeOf: static reference => reference.Alias == "a" ? BuiltInTypes.Int4 : null,
            GroupPreserved: groupPreserved,
            GroupsHaveRows: true,
            ScalarSubPlanTypeOf: static name => name == "SubPlan 1" ? BuiltInTypes.Int4 : null,
            SubPlanColumnTypeOf: static (name, _) => name == "SubPlan 1" ? BuiltInTypes.Int4 : null,
            CatalogAlone: AllNames
        );

    [Theory]
    // A subplan's name and its column.
    [InlineData("(foo 1)")]
    [InlineData("(SubPlan 1 foo)")]
    [InlineData("(foo 1 bar)")]
    [InlineData("(ANY (a.id = (SubPlan 1).col0))")]
    // A cast with no type, no ::, or a type that is no type.
    [InlineData("(1)::")]
    [InlineData("(1) foo integer")]
    [InlineData("(1)::integer(a)")]
    // A call of the wrong shape.
    [InlineData("jsonb_build_object('k'::text, VARIADIC $1)")]
    [InlineData("sum(1 ORDER 2)")]
    [InlineData("array_agg(1, 2)")]
    [InlineData("array_agg()")]
    [InlineData("first_value() OVER w1")]
    [InlineData("count")]
    [InlineData("rank")]
    [InlineData("COALESCE")]
    // A CASE that does not start, has no THEN, or does not end where its last END is.
    [InlineData("foo WHEN true THEN 1 ELSE 2 END")]
    [InlineData("CASE ELSE 1 END")]
    [InlineData("CASE WHEN true THEN 1 ELSE NULL::integer CASE END")]
    [InlineData("CASE WHEN true THEN 1 ELSE 2 END NULL::integer END")]
    // A test that is missing a word, or whose operand is no operand.
    [InlineData("(a.x = NULL)")]
    [InlineData("(a.x = b NULL)")]
    [InlineData("(a.x = NOT NULL)")]
    [InlineData("(= IS NULL)")]
    // A literal whose cast names no type.
    [InlineData("'x' foo bar")]
    [InlineData("'x'::Text")]
    [InlineData("'x'::foo(bar)")]
    [InlineData("'x'::numeric(1,)")]
    [InlineData("'x'::text()")]
    public void NullableOf_TextOneTokenFromAForm_IsUnknown(string entry) =>
        PlanExpression.NullableOf(entry, ProvingAll()).ShouldBeNull();

    // IS [NOT] DISTINCT FROM calls the = the parser chose, which may answer NULL, so neither it nor a form over it is
    // proved, even over operands that are.
    [Theory]
    [InlineData("(a.x IS DISTINCT FROM a.y)")]
    [InlineData("(NOT (a.x IS DISTINCT FROM a.y))")]
    [InlineData("CASE WHEN (a.x > 0) THEN (a.x IS DISTINCT FROM a.y) ELSE false END")]
    [InlineData("COALESCE((a.x IS DISTINCT FROM a.y), NULL::boolean)")]
    [InlineData("((a.x IS DISTINCT FROM a.y) = true)")]
    public void NullableOfAndProvedTypeOf_DistinctTest_AreUnknown(string entry)
    {
        PlanExpression.NullableOf(entry, ProvingAll()).ShouldBeNull();
        PlanExpression.ProvedTypeOf(entry, ProvingAll()).ShouldBeNull();
    }

    // Such a test is a boolean all the same, which only the type of an entry says.
    [Theory]
    [InlineData("(a.x IS DISTINCT FROM a.y)")]
    [InlineData("(NOT (a.x IS DISTINCT FROM a.y))")]
    public void TypeOf_DistinctTest_IsBoolean(string entry) =>
        PlanExpression.TypeOf(entry, catalogFunctions: true).ShouldBe(BuiltInTypes.Bool);

    [Theory]
    [InlineData("(foo (a IS DISTINCT FROM b))")]
    [InlineData("(a b c FROM d)")]
    [InlineData("(a IS b FROM c)")]
    [InlineData("(a + b IS DISTINCT FROM c)")]
    public void TypeOf_TextOneTokenFromADistinctTest_IsNull(string entry) =>
        PlanExpression.TypeOf(entry, catalogFunctions: true).ShouldBeNull();

    // A word alone that starts a form is no call of it, however the whole entry is asked.
    [Theory]
    [InlineData("EXISTS")]
    [InlineData("MERGE_ACTION")]
    public void ProvedTypeOf_NameOfAFormAlone_IsNull(string entry) =>
        PlanExpression.ProvedTypeOf(entry, ProvingAll()).ShouldBeNull();

    // A group the walk vetoes proves nothing, in a cast and in an ANY; the same text is proved when the walk allows it.
    [Theory]
    [InlineData("(1)::integer", "1")]
    [InlineData("(ANY (a.id = (SubPlan 1).col1))", "a.id = (SubPlan 1).col1")]
    public void NullableOf_GroupTheWalkVetoes_IsUnknownAndProvedWithoutTheVeto(string entry, string vetoed)
    {
        PlanExpression.NullableOf(entry, ProvingAll(group => group != vetoed)).ShouldBeNull();
        PlanExpression.NullableOf(entry, ProvingAll()).ShouldBe(false);
    }

    // The shortest operand of each kind, beside the near misses above.
    [Theory]
    [InlineData("(a IS NULL)")]
    [InlineData("(a IS NOT NULL)")]
    [InlineData("(1 IS NULL)")]
    [InlineData("('a'::text IS NULL)")]
    [InlineData("(f(x) IS NULL)")]
    public void NullableOf_TestOfTheShortestOperand_IsNotNull(string entry) =>
        PlanExpression.NullableOf(entry, ProvingAll()).ShouldBe(false);
}
