using System;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk;

// An entry of a plan's Output read as a whole: the forms that can never be NULL, the NULL constant, and the texts that
// start or end like a form and are none.  The texts are as PostgreSQL 14 and 18 print them, which
// tests/SqlSource.Tests.Postgres/Describer/PlanOutputTests.cs pins against both servers.
public sealed class PlanExpressionTests
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

    // A span in parentheses the walk says may not be read is read as unknown, whatever its form; the entry as a
    // whole is never asked.
    [Theory]
    [InlineData("COALESCE(('x'::text), 'y'::text)", false)]
    [InlineData("COALESCE(('x'::text))", null)]
    [InlineData("CASE WHEN (a > 1) THEN ('x'::text) ELSE 'y'::text END", null)]
    [InlineData("('x'::text)", false)]
    public void NullableOf_GroupTheWalkVetoes_IsReadAsUnknown(string entry, bool? expected) =>
        PlanExpression
            .NullableOf(entry, catalogFunctions: true, static _ => false, static group => group != "'x'::text")
            .ShouldBe(expected);

    // The walk's typed proof of the references of the tests below: each column's declared type, and none for another.
    private static long? Typed(OutputReference reference) =>
        (reference.Alias is null ? reference.Column : reference.Alias + "." + reference.Column) switch
        {
            "u.balance" or "l.unit_price" => BuiltInTypes.Numeric,
            "l.quantity" or "u.id" or "id" => BuiltInTypes.Int4,
            "u.name" or "name" => BuiltInTypes.Varchar,
            "created_at" => BuiltInTypes.Timestamptz,
            "products.xmax" => BuiltInTypes.Xid,
            "status" => UserStatus,
            _ => null,
        };

    // An enum of the user's.
    private const long UserStatus = 16_390;

    private static ExpressionContext Context(bool catalogFunctions) => new(catalogFunctions, Typed);

    [Theory]
    [InlineData("((l.quantity)::numeric * l.unit_price)")]
    [InlineData("((id)::bigint * 2)")]
    [InlineData("((id + 1) * 3)")]
    [InlineData("(lower((name)::text) || '!'::text)")]
    [InlineData("length((name)::text)")]
    [InlineData("date_trunc('day'::text, created_at)")]
    [InlineData("(created_at)::date")]
    [InlineData("((name)::text = 'x'::text)")]
    [InlineData("(products.xmax = 0)")]
    [InlineData("(((l.quantity)::numeric * l.unit_price))")]
    public void NullableOf_TotalOperatorOverProvedValues_IsNotNull(string entry) =>
        PlanExpression.NullableOf(entry, Context(catalogFunctions: true)).ShouldBe(false);

    [Theory]
    [InlineData("(l.quantity * l.unit_price)")]
    [InlineData("(u.note * 2)")]
    [InlineData("upper(r.during)")]
    [InlineData("(r.during * tstzrange($1, $2))")]
    [InlineData("(l.quantity OPERATOR(public.*) l.unit_price)")]
    [InlineData("public.lower((name)::text)")]
    [InlineData("(- id)")]
    [InlineData("((id)::bigint * 2) * NULL::bigint")]
    [InlineData("(id)::bigint * 2")]
    [InlineData("((id)::bigint * NULL::bigint)")]
    [InlineData("((u.id) * 2)")]
    [InlineData("(u.id)")]
    [InlineData("((u.id))::bigint")]
    [InlineData("(id * 2 + 1)")]
    [InlineData("lower((name)::text, 'x'::text)")]
    [InlineData("lower()")]
    [InlineData("Lower((name)::text)")]
    [InlineData("\"lower\"((name)::text)")]
    [InlineData("((name)::text = 'x'::text COLLATE \"C\")")]
    [InlineData("max((name)::text)")]
    [InlineData("(count(id) + 1)")]
    public void NullableOf_OperatorOrFunctionNotInTheTable_IsUnknown(string entry) =>
        PlanExpression.NullableOf(entry, Context(catalogFunctions: true)).ShouldBeNull();

    [Theory]
    [InlineData("((l.quantity)::numeric * l.unit_price)")]
    [InlineData("((id)::bigint * 2)")]
    [InlineData("((id + 1) * 3)")]
    [InlineData("(lower((name)::text) || '!'::text)")]
    [InlineData("length((name)::text)")]
    [InlineData("date_trunc('day'::text, created_at)")]
    [InlineData("(created_at)::date")]
    [InlineData("((name)::text = 'x'::text)")]
    [InlineData("(products.xmax = 0)")]
    [InlineData("(((l.quantity)::numeric * l.unit_price))")]
    public void NullableOf_TotalFormWhenTheCatalogIsNotFirst_IsUnknown(string entry) =>
        PlanExpression.NullableOf(entry, Context(catalogFunctions: false)).ShouldBeNull();

    [Theory]
    [InlineData("(status)::text")]
    [InlineData("(id)::text")]
    [InlineData("(created_at)::text")]
    [InlineData("(name)::date")]
    [InlineData("(id)::public.bigint")]
    public void NullableOf_CastOutsideAFamily_IsUnknown(string entry) =>
        PlanExpression.NullableOf(entry, Context(catalogFunctions: true)).ShouldBeNull();

    // A form of the table takes the new forms as its arguments, and COALESCE and CASE take them too.
    [Theory]
    [InlineData("COALESCE(u.note, ((id)::bigint * 2))")]
    [InlineData("CASE WHEN (id > 1) THEN (id * 2) ELSE length((name)::text) END")]
    [InlineData("upper(lower((name)::text))")]
    [InlineData("((count(*) * 2) > 1)")]
    [InlineData("((u.id > 1) = true)")]
    public void NullableOf_TotalFormAsAnArgument_IsNotNull(string entry) =>
        PlanExpression.NullableOf(entry, Context(catalogFunctions: true)).ShouldBe(false);

    // A group the walk says may not be read is unknown in an operator as anywhere else.
    [Theory]
    [InlineData("((id * 2) + 1)")]
    [InlineData("(((id * 2))::bigint + 1)")]
    public void NullableOf_OperatorOverAGroupTheWalkVetoes_IsUnknown(string entry) =>
        PlanExpression
            .NullableOf(
                entry,
                Context(catalogFunctions: true) with
                {
                    GroupPreserved = static group => group != "id * 2",
                }
            )
            .ShouldBeNull();

    [Theory]
    [InlineData("((id)::bigint * 2)", BuiltInTypes.Int8)]
    [InlineData("((name)::text = 'x'::text)", BuiltInTypes.Bool)]
    [InlineData("(created_at)::date", BuiltInTypes.Date)]
    [InlineData("length((name)::text)", BuiltInTypes.Int4)]
    [InlineData("(id)::numeric(10,2)", BuiltInTypes.Numeric)]
    [InlineData("(name)::character varying(3)", BuiltInTypes.Varchar)]
    [InlineData("'x'::text", BuiltInTypes.Text)]
    [InlineData("count(*)", BuiltInTypes.Int8)]
    public void ProvedTypeOf_TotalOperator_IsItsResult(string entry, long expected) =>
        PlanExpression.ProvedTypeOf(entry, Context(catalogFunctions: true)).ShouldBe(expected);

    [Theory]
    [InlineData("COALESCE(id, 0)")]
    [InlineData("id")]
    [InlineData("count(id)")]
    [InlineData("(l.quantity * l.unit_price)")]
    public void ProvedTypeOf_FormOfNoKnownType_IsNull(string entry) =>
        PlanExpression.ProvedTypeOf(entry, Context(catalogFunctions: true)).ShouldBeNull();

    // A reference that the old overload's walk proves is of no known type, and feeds no operator, function or cast.
    [Theory]
    [InlineData("(u.id * 2)")]
    [InlineData("(u.id)::bigint")]
    [InlineData("lower(u.name)")]
    public void NullableOf_ReferenceProvedWithoutAType_FeedsNoOperator(string entry) =>
        PlanExpression.NullableOf(entry, catalogFunctions: true, AllProved).ShouldBeNull();

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
    [InlineData("'x'::text", BuiltInTypes.Text)]
    [InlineData("('x'::text)", BuiltInTypes.Text)]
    [InlineData("'x'::character varying", BuiltInTypes.Varchar)]
    [InlineData("'x'::character varying(254)", BuiltInTypes.Varchar)]
    [InlineData("'x'::bpchar", BuiltInTypes.Bpchar)]
    [InlineData("'x'::character(3)", BuiltInTypes.Bpchar)]
    [InlineData("'1'::smallint", BuiltInTypes.Int2)]
    [InlineData("'-1'::integer", BuiltInTypes.Int4)]
    [InlineData("0", BuiltInTypes.Int4)]
    [InlineData("12", BuiltInTypes.Int4)]
    [InlineData("1.5", BuiltInTypes.Numeric)]
    [InlineData("'5'::bigint", BuiltInTypes.Int8)]
    [InlineData("'1.5'::numeric(10,2)", BuiltInTypes.Numeric)]
    [InlineData("'1.5'::real", BuiltInTypes.Float4)]
    [InlineData("'1.5'::double precision", BuiltInTypes.Float8)]
    [InlineData("'2024-01-01'::date", BuiltInTypes.Date)]
    [InlineData("'2024-01-01'::timestamp without time zone", BuiltInTypes.Timestamp)]
    [InlineData("'2024-01-01'::timestamp(3) without time zone", BuiltInTypes.Timestamp)]
    [InlineData("'2024-01-01'::timestamp with time zone", BuiltInTypes.Timestamptz)]
    [InlineData("true", BuiltInTypes.Bool)]
    [InlineData("false", BuiltInTypes.Bool)]
    [InlineData("(u.deleted_at IS NULL)", BuiltInTypes.Bool)]
    [InlineData("((u.deleted_at IS NOT NULL))", BuiltInTypes.Bool)]
    [InlineData("(NOT ((u.email)::text IS DISTINCT FROM (u.name)::text))", BuiltInTypes.Bool)]
    [InlineData("count(*)", BuiltInTypes.Int8)]
    [InlineData("(count(*) FILTER (WHERE (status = 'a'::user_status)))", BuiltInTypes.Int8)]
    [InlineData("count(o.id)", BuiltInTypes.Int8)]
    [InlineData("count(*) OVER (?)", BuiltInTypes.Int8)]
    [InlineData("row_number() OVER w1", BuiltInTypes.Int8)]
    [InlineData("(rank() OVER w2)", BuiltInTypes.Int8)]
    [InlineData("dense_rank() OVER w2", BuiltInTypes.Int8)]
    [InlineData("percent_rank() OVER (?)", BuiltInTypes.Float8)]
    [InlineData("cume_dist() OVER w2", BuiltInTypes.Float8)]
    public void TypeOf_ConstantOrKnownForm_IsItsTypeOid(string entry, long expected) =>
        PlanExpression.TypeOf(entry, catalogFunctions: true).ShouldBe(expected);

    [Theory]
    [InlineData("count(*)")]
    [InlineData("count(o.id)")]
    [InlineData("row_number() OVER w1")]
    [InlineData("percent_rank() OVER (?)")]
    public void TypeOf_FunctionOfAnySchema_IsNull(string entry) =>
        PlanExpression.TypeOf(entry, catalogFunctions: false).ShouldBeNull();

    // The plan prints a type's name through a visibility check, so a type of that name earlier on the path than
    // pg_catalog would print as the built-in one.
    [Theory]
    [InlineData("'x'::text")]
    [InlineData("'2024-01-01'::date")]
    [InlineData("'5'::bigint")]
    [InlineData("('x'::character varying)")]
    public void TypeOf_LiteralWhenTheCatalogIsNotFirst_IsNull(string entry) =>
        PlanExpression.TypeOf(entry, catalogFunctions: false).ShouldBeNull();

    [Theory]
    [InlineData("1", BuiltInTypes.Int4)]
    [InlineData("true", BuiltInTypes.Bool)]
    [InlineData("(u.deleted_at IS NULL)", BuiltInTypes.Bool)]
    public void TypeOf_FormWithoutATypeName_IsItsTypeWhateverThePath(string entry, long expected) =>
        PlanExpression.TypeOf(entry, catalogFunctions: false).ShouldBe(expected);

    [Theory]
    [InlineData("(\"*SELECT* 1\".id)::bigint", "\"*SELECT* 1\".id")]
    [InlineData("(u.id)::numeric(10,2)", "u.id")]
    [InlineData("((u.id + 1))::double precision", "(u.id + 1)")]
    public void CastOperand_CastOfAGroup_IsTheTextInside(string entry, string expected) =>
        PlanExpression.CastOperand(entry).ShouldBe(expected);

    [Theory]
    [InlineData("u.id")]
    [InlineData("(u.id)")]
    [InlineData("u.id::bigint")]
    [InlineData("(u.id)::bigint + 1")]
    [InlineData("(u.id)::bigint COLLATE \"C\"")]
    [InlineData("(u.id)::")]
    [InlineData("(u.id")]
    public void CastOperand_AnythingElse_IsNull(string entry) => PlanExpression.CastOperand(entry).ShouldBeNull();

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
    [InlineData("'x'::")]
    [InlineData("('x'::)")]
    [InlineData("")]
    public void TypeOf_AnythingElse_IsNull(string entry) =>
        PlanExpression.TypeOf(entry, catalogFunctions: true).ShouldBeNull();
}
