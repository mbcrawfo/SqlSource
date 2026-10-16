using System;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// An entry of a plan's Output read as a whole: the forms that can never be NULL, the NULL constant, and the texts that
// start or end like a form and are none.  The texts are as PostgreSQL 14 and 18 print them, which
// tests/SqlSource.Tests.Postgres/Describer/PlanOutputTests.cs pins against both servers.
public class PlanExpressionTests
{
    private static readonly Func<OutputReference, bool> NoneProved = static _ => false;

    private static readonly Func<OutputReference, bool> AllProved = static _ => true;

    [Theory]
    [InlineData("count(*)")]
    [InlineData("(count(*))")]
    [InlineData("(count(*) FILTER (WHERE (status = 'active'::user_status)))")]
    [InlineData("count(*) OVER (?)")]
    [InlineData("count(*) OVER w1")]
    [InlineData("(row_number() OVER (?))")]
    [InlineData("row_number() OVER w1")]
    [InlineData("(rank() OVER w2)")]
    [InlineData("(dense_rank() OVER w2)")]
    [InlineData("(percent_rank() OVER w2)")]
    [InlineData("(cume_dist() OVER w2)")]
    [InlineData("'lit'::text")]
    [InlineData("('a'::text)")]
    [InlineData("1")]
    [InlineData("1.5")]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("'-1'::integer")]
    [InlineData("'5000000000'::bigint")]
    [InlineData("'1.5'::double precision")]
    [InlineData("'x'::character varying(254)")]
    [InlineData("'{}'::text[]")]
    [InlineData("'it''s'::text")]
    public void NullableOf_FormOfAFunctionOrAConstant_IsNotNull(string entry) =>
        PlanExpression.NullableOf(entry, catalogFunctions: true, NoneProved).ShouldBe(false);

    [Theory]
    [InlineData("(u.deleted_at IS NULL)")]
    [InlineData("(u.deleted_at IS NOT NULL)")]
    [InlineData("((deleted_at IS NULL))")]
    [InlineData("((u.email)::text IS DISTINCT FROM (u.name)::text)")]
    [InlineData("(NOT ((u.email)::text IS DISTINCT FROM (u.name)::text))")]
    [InlineData("COALESCE(max((email)::text), 'x'::text)")]
    [InlineData("(COALESCE((email)::character varying, 'x'::character varying))")]
    [InlineData("LEAST(1, u.id)")]
    [InlineData("CASE WHEN (u.deleted_at IS NULL) THEN 'open'::text ELSE 'closed'::text END")]
    [InlineData("CASE WHEN CASE WHEN (id > 1) THEN true ELSE false END THEN 'then'::text ELSE 'else END'::text END")]
    [InlineData("CASE WHEN (name = ' END '::text) THEN 'a'::text ELSE 'b'::text END")]
    public void NullableOf_FormThatCallsNoFunction_IsNotNull(string entry) =>
        PlanExpression.NullableOf(entry, catalogFunctions: false, NoneProved).ShouldBe(false);

    [Theory]
    [InlineData("NULL::text")]
    [InlineData("NULL::character varying(254)")]
    [InlineData("(NULL::integer)")]
    public void NullableOf_NullConstant_IsNullable(string entry) =>
        PlanExpression.NullableOf(entry, catalogFunctions: true, NoneProved).ShouldBe(true);

    [Theory]
    [InlineData("(count(DISTINCT name))")]
    [InlineData("count(id)")]
    [InlineData("count(u.id) OVER w1")]
    [InlineData("(count(email) FILTER (WHERE (status = 'active'::user_status)))")]
    [InlineData("rank(id) OVER w1")]
    [InlineData("row_number(x) OVER (?)")]
    [InlineData("(count(*) + NULL::integer)")]
    [InlineData("(COALESCE(o.note, 'x'::text) || NULL::text)")]
    [InlineData("((a IS NULL) AND b)")]
    [InlineData("(name = ') IS NULL'::text)")]
    [InlineData("\"count\"(*)")]
    [InlineData("shadow.count(*)")]
    [InlineData("pg_catalog.count(*)")]
    [InlineData("row_number()")]
    [InlineData("max(x)")]
    [InlineData("upper((u.name)::text)")]
    [InlineData("(u.email)::text")]
    [InlineData("('x'::character varying(254))::email_t")]
    [InlineData("CASE u.status WHEN 'active'::user_status THEN 'A'::text ELSE NULL::text END")]
    [InlineData("CASE WHEN x THEN 'a'::text END")]
    [InlineData("COALESCE(NULL::text, NULL::text)")]
    [InlineData("u.id")]
    [InlineData("id")]
    [InlineData("(hashed SubPlan 2)")]
    [InlineData("(ANY (u.id = (hashed SubPlan 2).col1))")]
    [InlineData("(InitPlan 1).col1")]
    [InlineData("E'a\\\\b'::text")]
    [InlineData("(-1)")]
    [InlineData("'open")]
    [InlineData("(count(*)")]
    [InlineData("count(*))")]
    [InlineData("count(*) OVER w1 w2")]
    [InlineData("count(*) FILTER")]
    [InlineData("'a'::text 'b'::text")]
    [InlineData("((a IS NULL) IS NULL) OR b")]
    [InlineData("(a IS NULL IS NULL)")]
    [InlineData("CASE WHEN a THEN 'a'::text ELSE 'b'::text END || 'c'::text")]
    [InlineData("")]
    public void NullableOf_NearMiss_IsUnknown(string entry) =>
        PlanExpression.NullableOf(entry, catalogFunctions: true, AllProved).ShouldBeNull();

    [Theory]
    [InlineData("count(*)")]
    [InlineData("count(*) OVER w1")]
    [InlineData("(row_number() OVER (?))")]
    [InlineData("COALESCE(count(*), NULL::bigint)")]
    public void NullableOf_FunctionWhenTheCatalogIsNotFirst_IsUnknown(string entry) =>
        PlanExpression.NullableOf(entry, catalogFunctions: false, NoneProved).ShouldBeNull();

    [Theory]
    [InlineData("GREATEST(u.created_at, u.deleted_at)")]
    [InlineData("(GREATEST(created_at, deleted_at))")]
    [InlineData("COALESCE(u.deleted_at, u.created_at)")]
    [InlineData("CASE WHEN (x > 1) THEN u.created_at ELSE 'epoch'::timestamp with time zone END")]
    public void NullableOf_ReferenceArgumentTheWalkProves_IsNotNull(string entry) =>
        PlanExpression.NullableOf(entry, catalogFunctions: false, CreatedAt).ShouldBe(false);

    [Theory]
    [InlineData("GREATEST(u.deleted_at, u.closed_at)")]
    [InlineData("CASE WHEN (x > 1) THEN u.created_at ELSE u.deleted_at END")]
    public void NullableOf_ReferenceArgumentTheWalkDoesNotProve_IsUnknown(string entry) =>
        PlanExpression.NullableOf(entry, catalogFunctions: false, CreatedAt).ShouldBeNull();

    [Fact]
    public void NullableOf_WholeEntryIsAReference_IsUnknownAndNotAsked()
    {
        var asked = false;

        var answer = PlanExpression.NullableOf(
            "u.created_at",
            catalogFunctions: true,
            reference =>
            {
                asked = true;
                return true;
            }
        );

        answer.ShouldBeNull();
        asked.ShouldBeFalse();
    }

    private static bool CreatedAt(OutputReference reference) => reference.Column == "created_at";

    // A span in parentheses the walk says may not be read is unknown, whatever its form; the entry as a whole is never
    // asked.
    [Theory]
    [InlineData("COALESCE(('x'::text), 'y'::text)", false)]
    [InlineData("COALESCE(('x'::text))", null)]
    [InlineData("CASE WHEN (a > 1) THEN ('x'::text) ELSE 'y'::text END", null)]
    [InlineData("('x'::text)", false)]
    public void NullableOf_GroupTheWalkVetoes_IsUnknown(string entry, bool? expected) =>
        PlanExpression
            .NullableOf(entry, catalogFunctions: true, static _ => false, static group => group != "'x'::text")
            .ShouldBe(expected);

    [Fact]
    public void Groups_Entry_IsTheTextInsideEachPairOfParenthesesOutermostFirst() =>
        PlanExpression
            .Groups("COALESCE((sum(b.opt)), ')'::text)")
            .ShouldBe(["(sum(b.opt)), ')'::text", "sum(b.opt)", "b.opt"]);

    [Theory]
    [InlineData("('x'::text)", "'x'::text")]
    [InlineData("((a IS NULL))", "(a IS NULL)")]
    [InlineData("(a) + (b)", null)]
    [InlineData("count(*)", null)]
    [InlineData("('x)'::text", null)]
    public void Unwrapped_Entry_IsTheTextInsideTheParenthesesThatWrapItWhole(string entry, string? expected) =>
        PlanExpression.Unwrapped(entry).ShouldBe(expected);

    [Theory]
    [InlineData("(u.id)", "id", true)]
    [InlineData("upper((u.\"Id\")::text)", "Id", true)]
    [InlineData("$1", "id", true)]
    [InlineData("'id'::text", "id", false)]
    [InlineData("row_number() OVER w1", "id", false)]
    [InlineData("(u.name", "id", true)]
    public void MayNameColumn_Entry_IsWhetherItNamesTheColumnOrHoldsAParameter(
        string entry,
        string column,
        bool expected
    ) => PlanExpression.MayNameColumn(entry, column).ShouldBe(expected);

    [Theory]
    [InlineData("'x'::text", 25)]
    [InlineData("('x'::text)", 25)]
    [InlineData("'x'::character varying", 1043)]
    [InlineData("'x'::character varying(254)", 1043)]
    [InlineData("'x'::bpchar", 1042)]
    [InlineData("'x'::character(3)", 1042)]
    [InlineData("'1'::smallint", 21)]
    [InlineData("'-1'::integer", 23)]
    [InlineData("0", 23)]
    [InlineData("12", 23)]
    [InlineData("1.5", 1700)]
    [InlineData("'5'::bigint", 20)]
    [InlineData("'1.5'::numeric(10,2)", 1700)]
    [InlineData("'1.5'::real", 700)]
    [InlineData("'1.5'::double precision", 701)]
    [InlineData("'2024-01-01'::date", 1082)]
    [InlineData("'2024-01-01'::timestamp without time zone", 1114)]
    [InlineData("'2024-01-01'::timestamp(3) without time zone", 1114)]
    [InlineData("'2024-01-01'::timestamp with time zone", 1184)]
    [InlineData("true", 16)]
    [InlineData("false", 16)]
    [InlineData("(u.deleted_at IS NULL)", 16)]
    [InlineData("((u.deleted_at IS NOT NULL))", 16)]
    [InlineData("(NOT ((u.email)::text IS DISTINCT FROM (u.name)::text))", 16)]
    [InlineData("count(*)", 20)]
    [InlineData("(count(*) FILTER (WHERE (status = 'a'::user_status)))", 20)]
    [InlineData("count(o.id)", 20)]
    [InlineData("count(*) OVER (?)", 20)]
    [InlineData("row_number() OVER w1", 20)]
    [InlineData("(rank() OVER w2)", 20)]
    [InlineData("dense_rank() OVER w2", 20)]
    [InlineData("percent_rank() OVER (?)", 701)]
    [InlineData("cume_dist() OVER w2", 701)]
    public void TypeOf_ConstantOrKnownForm_IsItsTypeOid(string entry, long expected) =>
        PlanExpression.TypeOf(entry, catalogFunctions: true).ShouldBe(expected);

    [Theory]
    [InlineData("count(*)")]
    [InlineData("count(o.id)")]
    [InlineData("row_number() OVER w1")]
    [InlineData("percent_rank() OVER (?)")]
    public void TypeOf_FunctionOfAnySchema_IsNull(string entry) =>
        PlanExpression.TypeOf(entry, catalogFunctions: false).ShouldBeNull();

    [Theory]
    [InlineData("NULL::text")]
    [InlineData("COALESCE(max((email)::text), 'x'::text)")]
    [InlineData("CASE WHEN (u.deleted_at IS NULL) THEN 'open'::text ELSE 'closed'::text END")]
    [InlineData("'active'::user_status")]
    [InlineData("'{}'::text[]")]
    [InlineData("'x'::text COLLATE \"C\"")]
    [InlineData("u.id")]
    [InlineData("(u.id)::bigint")]
    [InlineData("(u.id)")]
    [InlineData("count(*) + 1")]
    [InlineData("(count(*) + 1)")]
    [InlineData("count(*) OVER")]
    [InlineData("rank()")]
    [InlineData("rank(1) OVER w1")]
    [InlineData("1 + 1")]
    [InlineData("1.5e3")]
    [InlineData("true AND false")]
    [InlineData("(a IS NULL) AND b")]
    [InlineData("'x'::text || 'y'::text")]
    [InlineData("'x'::text, 'y'::text")]
    [InlineData("('x'::text")]
    [InlineData("")]
    public void TypeOf_AnythingElse_IsNull(string entry) =>
        PlanExpression.TypeOf(entry, catalogFunctions: true).ShouldBeNull();
}
