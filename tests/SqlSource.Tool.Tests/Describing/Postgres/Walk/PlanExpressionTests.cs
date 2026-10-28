using System;
using System.Collections.Frozen;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk;

// An entry of a plan's Output read as a whole: the forms that can never be NULL, the NULL constant, and the texts that
// start or end like a form and are none.  The texts are as PostgreSQL 14 and 18 print them, which
// tests/DatabaseTests/Postgres/Describer/PlanOutputTests.cs pins against both servers.
public sealed class PlanExpressionTests
{
    private static readonly Func<OutputReference, bool> NoneProved = static _ => false;

    private static readonly Func<OutputReference, bool> AllProved = static _ => true;

    // The context of a walk that proves each reference the predicate names, of no known type, and reads every group
    // unless groupPreserved vetoes one.
    private static ExpressionContext Untyped(
        bool catalogFunctions,
        Func<OutputReference, bool> proved,
        Func<string, bool>? groupPreserved = null
    ) => new(catalogFunctions, reference => proved(reference) ? TotalFunctions.UnknownType : null, groupPreserved);

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
        PlanExpression.NullableOf(entry, Untyped(catalogFunctions: true, NoneProved)).ShouldBe(false);

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
        PlanExpression.NullableOf(entry, Untyped(catalogFunctions: false, NoneProved)).ShouldBe(false);

    [Theory]
    [InlineData("NULL::text")]
    [InlineData("NULL::character varying(254)")]
    [InlineData("(NULL::integer)")]
    public void NullableOf_NullConstant_IsNullable(string entry) =>
        PlanExpression.NullableOf(entry, Untyped(catalogFunctions: true, NoneProved)).ShouldBe(true);

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
        PlanExpression.NullableOf(entry, Untyped(catalogFunctions: true, AllProved)).ShouldBeNull();

    [Theory]
    [InlineData("count(*)")]
    [InlineData("count(*) OVER w1")]
    [InlineData("(row_number() OVER (?))")]
    [InlineData("COALESCE(count(*), NULL::bigint)")]
    public void NullableOf_FunctionWhenTheCatalogIsNotFirst_IsUnknown(string entry) =>
        PlanExpression.NullableOf(entry, Untyped(catalogFunctions: false, NoneProved)).ShouldBeNull();

    [Theory]
    [InlineData("GREATEST(u.created_at, u.deleted_at)")]
    [InlineData("(GREATEST(created_at, deleted_at))")]
    [InlineData("COALESCE(u.deleted_at, u.created_at)")]
    [InlineData("CASE WHEN (x > 1) THEN u.created_at ELSE 'epoch'::timestamp with time zone END")]
    public void NullableOf_ReferenceArgumentTheWalkProves_IsNotNull(string entry) =>
        PlanExpression.NullableOf(entry, Untyped(catalogFunctions: false, CreatedAt)).ShouldBe(false);

    [Theory]
    [InlineData("GREATEST(u.deleted_at, u.closed_at)")]
    [InlineData("CASE WHEN (x > 1) THEN u.created_at ELSE u.deleted_at END")]
    public void NullableOf_ReferenceArgumentTheWalkDoesNotProve_IsUnknown(string entry) =>
        PlanExpression.NullableOf(entry, Untyped(catalogFunctions: false, CreatedAt)).ShouldBeNull();

    [Fact]
    public void NullableOf_WholeEntryIsAReference_IsUnknownAndNotAsked()
    {
        var asked = false;

        var answer = PlanExpression.NullableOf(
            "u.created_at",
            Untyped(
                catalogFunctions: true,
                reference =>
                {
                    asked = true;
                    return true;
                }
            )
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
            .NullableOf(entry, Untyped(catalogFunctions: true, NoneProved, static group => group != "'x'::text"))
            .ShouldBe(expected);

    // The walk's typed proof of the references of the tests below: each column's declared type, and none for another.
    private static long? Typed(OutputReference reference) =>
        (reference.Alias is null ? reference.Column : reference.Alias + "." + reference.Column) switch
        {
            "u.balance" or "l.unit_price" => BuiltInTypes.Numeric,
            "l.quantity" or "u.id" or "id" or "p.id" => BuiltInTypes.Int4,
            "u.name" or "name" => BuiltInTypes.Varchar,
            "p.name" => BuiltInTypes.Text,
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

    // A form of the table takes another as its argument, and COALESCE and CASE take them too.
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
    [InlineData("COALESCE(id, u.name)")]
    [InlineData("id")]
    [InlineData("count(id)")]
    [InlineData("(l.quantity * l.unit_price)")]
    public void ProvedTypeOf_FormOfNoKnownType_IsNull(string entry) =>
        PlanExpression.ProvedTypeOf(entry, Context(catalogFunctions: true)).ShouldBeNull();

    // A reference the walk proves of no known type feeds no operator, function or cast.
    [Theory]
    [InlineData("(u.id * 2)")]
    [InlineData("(u.id)::bigint")]
    [InlineData("lower(u.name)")]
    public void NullableOf_ReferenceProvedWithoutAType_FeedsNoOperator(string entry) =>
        PlanExpression.NullableOf(entry, Untyped(catalogFunctions: true, AllProved)).ShouldBeNull();

    // The context of a node whose groups each have a row, or of one whose may have none.
    private static ExpressionContext Grouped(bool groupsHaveRows) =>
        Context(catalogFunctions: true) with
        {
            GroupsHaveRows = groupsHaveRows,
        };

    [Theory]
    [InlineData("sum(((l.quantity)::numeric * l.unit_price))")]
    [InlineData("max(created_at)")]
    [InlineData("string_agg(DISTINCT p.name, ', '::text ORDER BY p.name)")]
    [InlineData("sum(l.quantity)")]
    [InlineData("avg(id)")]
    [InlineData("bool_and((id > 0))")]
    [InlineData("COALESCE(max(created_at), now())")]
    public void NullableOf_AggregateOverAGroupWithARow_IsNotNull(string entry) =>
        PlanExpression.NullableOf(entry, Grouped(groupsHaveRows: true)).ShouldBe(false);

    [Theory]
    [InlineData("sum(((l.quantity)::numeric * l.unit_price))")]
    [InlineData("max(created_at)")]
    [InlineData("string_agg(DISTINCT p.name, ', '::text ORDER BY p.name)")]
    [InlineData("sum(l.quantity)")]
    [InlineData("avg(id)")]
    [InlineData("bool_and((id > 0))")]
    [InlineData("COALESCE(max(created_at), now())")]
    public void NullableOf_AggregateOfANodeWhoseGroupsMayHaveNoRow_IsUnknown(string entry) =>
        PlanExpression.NullableOf(entry, Grouped(groupsHaveRows: false)).ShouldBeNull();

    // A FILTER may leave a group no row, a window's frame may be empty, an argument may be NULL, and array_agg and an
    // ordered-set aggregate are in no row of the table.
    [Theory]
    [InlineData("max(created_at) FILTER (WHERE (id > 1))")]
    [InlineData("sum(id) OVER w1")]
    [InlineData("sum(id) OVER (?)")]
    [InlineData("max(o.note)")]
    [InlineData("array_agg(DISTINCT p.id ORDER BY p.id)")]
    [InlineData("percentile_cont('0.5'::double precision) WITHIN GROUP (ORDER BY id)")]
    [InlineData("max((name)::text) ORDER BY id")]
    [InlineData("max(DISTINCT)")]
    [InlineData("max(ORDER BY id)")]
    [InlineData("max()")]
    [InlineData("max(id, id)")]
    [InlineData("string_agg(p.name, ', '::text, p.name)")]
    [InlineData("string_agg(p.name, ORDER BY p.name)")]
    [InlineData("public.max(id)")]
    [InlineData("\"max\"(id)")]
    [InlineData("MAX(id)")]
    [InlineData("max((u.id))")]
    public void NullableOf_AggregateThatMayBeNull_IsUnknown(string entry) =>
        PlanExpression.NullableOf(entry, Grouped(groupsHaveRows: true)).ShouldBeNull();

    // An aggregate is the catalog's only when the session finds pg_catalog first.
    [Fact]
    public void NullableOf_AggregateWhenTheCatalogIsNotFirst_IsUnknown() =>
        PlanExpression
            .NullableOf("max(created_at)", Context(catalogFunctions: false) with { GroupsHaveRows = true })
            .ShouldBeNull();

    // The context of a node whose groups each have a row, where a child printed sum(id) or no span at all.
    private static ExpressionContext GroupedWithSumFromChild(bool fromChild) =>
        Grouped(groupsHaveRows: true) with
        {
            GroupFromChild = group => fromChild && group == "sum(id)",
        };

    // A value a child printed was computed under the child's grouping, which may be a plain one.
    [Theory]
    [InlineData("((sum(id)) * 2)")]
    [InlineData("((sum(id))::numeric * 2.5)")]
    [InlineData("COALESCE((sum(id)), NULL::bigint)")]
    [InlineData("CASE WHEN (id > 1) THEN (sum(id) * 2) ELSE (sum(id)) END")]
    public void NullableOf_AggregateInASpanAChildPrinted_IsUnknown(string entry) =>
        PlanExpression.NullableOf(entry, GroupedWithSumFromChild(fromChild: true)).ShouldBeNull();

    // The same spans when no child printed them: the node computed the aggregate over its own groups.
    [Theory]
    [InlineData("((sum(id)) * 2)")]
    [InlineData("((sum(id))::numeric * 2.5)")]
    [InlineData("COALESCE((sum(id)), NULL::bigint)")]
    [InlineData("CASE WHEN (id > 1) THEN (sum(id) * 2) ELSE (sum(id)) END")]
    public void NullableOf_AggregateInASpanTheNodeComputes_IsNotNull(string entry) =>
        PlanExpression.NullableOf(entry, GroupedWithSumFromChild(fromChild: false)).ShouldBe(false);

    // Every span inside a group a child printed is read without the node's groups, however deep.
    [Fact]
    public void NullableOf_AggregateDeepInASpanAChildPrinted_IsUnknown() =>
        PlanExpression
            .NullableOf(
                "(((sum(id) + 1)) * 2)",
                Grouped(groupsHaveRows: true) with
                {
                    GroupFromChild = static group => group == "(sum(id) + 1)",
                }
            )
            .ShouldBeNull();

    [Theory]
    [InlineData("sum(l.quantity)", BuiltInTypes.Int8)]
    [InlineData("avg(id)", BuiltInTypes.Numeric)]
    [InlineData("string_agg(DISTINCT p.name, ', '::text ORDER BY p.name)", BuiltInTypes.Text)]
    [InlineData("(sum(l.quantity) * 2)", BuiltInTypes.Int8)]
    public void ProvedTypeOf_AggregateOverAGroupWithARow_IsItsResult(string entry, long expected) =>
        PlanExpression.ProvedTypeOf(entry, Grouped(groupsHaveRows: true)).ShouldBe(expected);

    // The walk's answers for the subplans of the tests below: the value of SubPlan 3 as a scalar, and the first column
    // of SubPlan 2, with "a.id" and "a.x" proved integers.
    private static ExpressionContext SubPlans(long? scalar, long? column, bool catalogFunctions = true) =>
        new(
            catalogFunctions,
            static reference => reference.Alias == "a" && reference.Column is "id" or "x" ? BuiltInTypes.Int4 : null,
            ScalarSubPlanTypeOf: name => name == "SubPlan 3" ? scalar : null,
            SubPlanColumnTypeOf: (name, number) => name == "SubPlan 2" && number == 1 ? column : null
        );

    [Theory]
    [InlineData("(SubPlan 3)", BuiltInTypes.Int8, null, false)]
    [InlineData("((SubPlan 3))", BuiltInTypes.Int8, null, false)]
    [InlineData("(SubPlan 3)", null, null, null)]
    [InlineData("(hashed SubPlan 3)", BuiltInTypes.Int8, null, null)]
    [InlineData("(SubPlan 4)", BuiltInTypes.Int8, null, null)]
    [InlineData("EXISTS(SubPlan 1)", null, null, false)]
    [InlineData("(EXISTS(SubPlan 1))", null, null, false)]
    [InlineData("(ANY (a.id = (hashed SubPlan 2).col1))", null, BuiltInTypes.Int4, false)]
    [InlineData("(ANY (a.id = (SubPlan 2).col1))", null, BuiltInTypes.Int4, false)]
    [InlineData("(ANY (a.id < (SubPlan 2).col1))", null, BuiltInTypes.Int4, false)]
    [InlineData("(ANY (a.id = (hashed SubPlan 2).col1))", null, null, null)]
    [InlineData("(ANY (a.id = (SubPlan 2).col1))", null, null, null)]
    [InlineData("(ANY (a.id = (hashed SubPlan 2).col1))", null, BuiltInTypes.Text, null)]
    [InlineData("(ANY (a.id = (SubPlan 2).col1))", null, BuiltInTypes.Text, null)]
    [InlineData("(ANY (a.email = (hashed SubPlan 2).col1))", null, BuiltInTypes.Int4, null)]
    [InlineData("(ANY (a.email = (SubPlan 2).col1))", null, BuiltInTypes.Int4, null)]
    [InlineData("(ANY (a.id + (SubPlan 2).col1))", null, BuiltInTypes.Int4, null)]
    [InlineData("(ANY (a.id = ((SubPlan 2).col1)::integer))", null, BuiltInTypes.Int4, null)]
    [InlineData("(ANY (a.id = (SubPlan 2).col01))", null, BuiltInTypes.Int4, null)]
    [InlineData("(ANY (a.id = (SubPlan 2).column1))", null, BuiltInTypes.Int4, null)]
    [InlineData("(ANY (a.id = (rescan SubPlan 2).col1))", null, BuiltInTypes.Int4, null)]
    [InlineData("(ANY ((a.id = (SubPlan 2).col1) AND (a.x = (SubPlan 2).col2)))", null, BuiltInTypes.Int4, null)]
    [InlineData("(ALL (a.id = (SubPlan 2).col1))", null, BuiltInTypes.Int4, null)]
    [InlineData("((a.id = (SubPlan 2).col1))", null, BuiltInTypes.Int4, null)]
    [InlineData("(a.id = (SubPlan 2).col1)", null, BuiltInTypes.Int4, null)]
    [InlineData("MERGE_ACTION()", null, null, false)]
    [InlineData("merge_action()", null, null, null)]
    [InlineData("\"MERGE_ACTION\"()", null, null, null)]
    [InlineData("MERGE_ACTION(1)", null, null, null)]
    [InlineData("EXISTS(SubPlan 1) AND true", null, null, null)]
    [InlineData("EXISTS(hashed SubPlan 1)", null, null, null)]
    [InlineData("ARRAY(SubPlan 1)", null, null, null)]
    [InlineData("(rescan SubPlan 1)", BuiltInTypes.Int8, null, null)]
    public void NullableOf_SubPlanForms_AreReadThroughTheirCallbacks(
        string entry,
        long? scalar,
        long? column,
        bool? expected
    ) => PlanExpression.NullableOf(entry, SubPlans(scalar, column)).ShouldBe(expected);

    [Theory]
    [InlineData("(SubPlan 3)", BuiltInTypes.Int8)]
    [InlineData("EXISTS(SubPlan 1)", BuiltInTypes.Bool)]
    [InlineData("(ANY (a.id = (hashed SubPlan 2).col1))", BuiltInTypes.Bool)]
    [InlineData("MERGE_ACTION()", BuiltInTypes.Text)]
    public void ProvedTypeOf_SubPlanForm_IsItsType(string entry, long expected) =>
        PlanExpression.ProvedTypeOf(entry, SubPlans(BuiltInTypes.Int8, BuiltInTypes.Int4)).ShouldBe(expected);

    // An ANY reads an operator, which is the catalog's only when the session finds pg_catalog first; the rest are
    // syntax.
    [Theory]
    [InlineData("(ANY (a.id = (hashed SubPlan 2).col1))", null)]
    [InlineData("(SubPlan 3)", false)]
    [InlineData("EXISTS(SubPlan 1)", false)]
    [InlineData("MERGE_ACTION()", false)]
    public void NullableOf_SubPlanFormWhenTheCatalogIsNotFirst_IsReadOnlyWithoutAnOperator(
        string entry,
        bool? expected
    ) =>
        PlanExpression
            .NullableOf(entry, SubPlans(BuiltInTypes.Int8, BuiltInTypes.Int4, catalogFunctions: false))
            .ShouldBe(expected);

    // The walk's typed proof of the references of the tests of the names only pg_catalog holds.
    private static long? Polymorphic(OutputReference reference) =>
        (reference.Alias is null ? reference.Column : reference.Alias + "." + reference.Column) switch
        {
            "o.id" => BuiltInTypes.Int8,
            "u.id" => BuiltInTypes.Int4,
            "during" or "b.during" => BuiltInTypes.Tstzrange,
            "u.name" => BuiltInTypes.Varchar,
            _ => null,
        };

    // Every name of CatalogOverloads held by pg_catalog alone.
    private static readonly CatalogNames All = new(
        CatalogOverloads.FunctionNames.ToFrozenSet(StringComparer.Ordinal),
        CatalogOverloads.OperatorNames.ToFrozenSet(StringComparer.Ordinal)
    );

    private static ExpressionContext Alone(
        CatalogNames names,
        bool catalogFunctions = true,
        bool groupsHaveRows = false
    ) => new(catalogFunctions, Polymorphic, GroupsHaveRows: groupsHaveRows, CatalogAlone: names);

    private static readonly string[] CountsOfAnArgument =
    [
        "count(o.id)",
        "count(DISTINCT status)",
        "count(email) FILTER (WHERE (id > 1))",
        "count(email) OVER (?)",
        "count(email) OVER w1",
        "(count(o.id))",
    ];

    public static TheoryData<string> CountTexts => [.. CountsOfAnArgument];

    [Theory]
    [MemberData(nameof(CountTexts))]
    public void NullableOf_CatalogsAloneCount_IsNotNull(string entry) =>
        PlanExpression.NullableOf(entry, Alone(All)).ShouldBe(false);

    // With no name held by pg_catalog alone, or with the names but another schema's found first, count may be a user's.
    [Theory]
    [MemberData(nameof(CountTexts))]
    public void NullableOf_CountThatMayBeAnotherSchemas_IsUnknown(string entry)
    {
        PlanExpression.NullableOf(entry, Alone(CatalogNames.None)).ShouldBeNull();
        PlanExpression.NullableOf(entry, Alone(All, catalogFunctions: false)).ShouldBeNull();
    }

    [Fact]
    public void NullableOf_CountOfAStarWhenNoNameIsTheCatalogsAlone_IsNotNull() =>
        PlanExpression.NullableOf("count(*)", Alone(CatalogNames.None)).ShouldBe(false);

    [Theory]
    [InlineData("array_agg(DISTINCT p.id ORDER BY p.id)")]
    [InlineData("array_agg(o.note)")]
    public void NullableOf_ArrayAggOverAGroupWithARow_IsNotNull(string entry) =>
        PlanExpression.NullableOf(entry, Alone(All, groupsHaveRows: true)).ShouldBe(false);

    // A group may have no row, a FILTER may leave it none, a window's frame may be empty, and array_agg may be a
    // user's.
    [Theory]
    [InlineData("array_agg(o.note)", false, true)]
    [InlineData("array_agg(DISTINCT p.id ORDER BY p.id)", false, true)]
    [InlineData("array_agg(o.note) FILTER (WHERE (o.id > 1))", true, true)]
    [InlineData("array_agg(o.note) OVER w1", true, true)]
    [InlineData("array_agg(o.note)", true, false)]
    public void NullableOf_ArrayAggThatMayBeNull_IsUnknown(string entry, bool groupsHaveRows, bool catalogAlone) =>
        PlanExpression
            .NullableOf(entry, Alone(catalogAlone ? All : CatalogNames.None, groupsHaveRows: groupsHaveRows))
            .ShouldBeNull();

    [Theory]
    [InlineData("jsonb_build_object('id', id, 'email', email)", false)]
    [InlineData("json_build_object()", false)]
    [InlineData("jsonb_build_object(VARIADIC $1)", null)]
    public void NullableOf_JsonBuildObject_IsNotNullButForVariadic(string entry, bool? expected) =>
        PlanExpression.NullableOf(entry, Alone(All)).ShouldBe(expected);

    [Theory]
    [InlineData("tstzrange($1, $2)", false)]
    [InlineData("int4range(1, 2, '[]'::text)", false)]
    [InlineData("(during * tstzrange($1, $2))", false)]
    [InlineData("tstzrange($1)", null)]
    [InlineData("(during * (u.id)::numeric)", null)]
    public void NullableOf_RangeConstructorAndIntersection_AreNotNull(string entry, bool? expected) =>
        PlanExpression.NullableOf(entry, Alone(All)).ShouldBe(expected);

    // Another schema's * may be the one the server chose for two ranges.
    [Fact]
    public void NullableOf_IntersectionWhenAnotherSchemaHoldsTheOperator_IsUnknown() =>
        PlanExpression
            .NullableOf("(during * tstzrange($1, $2))", Alone(new CatalogNames(All.Functions, FrozenSet<string>.Empty)))
            .ShouldBeNull();

    // With no name held by pg_catalog alone, or with the names but another schema's found first, a call of the name
    // may be a user's function or aggregate.
    [Theory]
    [InlineData("jsonb_build_object('id', id, 'email', email)")]
    [InlineData("json_build_object()")]
    [InlineData("tstzrange($1, $2)")]
    [InlineData("int4range(1, 2, '[]'::text)")]
    [InlineData("array_agg(o.note)")]
    public void NullableOf_NameThatMayBeAnotherSchemas_IsUnknown(string entry)
    {
        PlanExpression.NullableOf(entry, Alone(CatalogNames.None, groupsHaveRows: true)).ShouldBeNull();
        PlanExpression.NullableOf(entry, Alone(All, catalogFunctions: false, groupsHaveRows: true)).ShouldBeNull();
    }

    [Theory]
    [InlineData("count(o.id)", BuiltInTypes.Int8)]
    [InlineData("count(email) OVER w1", BuiltInTypes.Int8)]
    [InlineData("jsonb_build_object('id', id, 'email', email)", BuiltInTypes.Jsonb)]
    [InlineData("json_build_object()", BuiltInTypes.Json)]
    [InlineData("tstzrange($1, $2)", BuiltInTypes.Tstzrange)]
    [InlineData("int4range(1, 2, '[]'::text)", BuiltInTypes.Int4range)]
    [InlineData("(during * tstzrange($1, $2))", BuiltInTypes.Tstzrange)]
    public void ProvedTypeOf_CatalogsAloneForm_IsItsType(string entry, long expected) =>
        PlanExpression.ProvedTypeOf(entry, Alone(All)).ShouldBe(expected);

    // upper and lower of a range are NULL for an infinite or an empty one, and json_agg and jsonb_agg are in no table.
    [Theory]
    [InlineData("upper(during)")]
    [InlineData("lower(during)")]
    [InlineData("json_agg(o.note)")]
    [InlineData("jsonb_agg(o.note)")]
    public void NullableOf_PolymorphicNameNotInTheTable_IsUnknown(string entry) =>
        PlanExpression.NullableOf(entry, Alone(All, groupsHaveRows: true)).ShouldBeNull();

    // A column of a domain over numeric.
    private const long Amount = 16_400;

    // The references of the tests of the type of COALESCE, GREATEST, LEAST and CASE: Polymorphic's, a numeric, an
    // integer and a column of a domain, each bare.
    private static long? CommonTyped(OutputReference reference) =>
        reference switch
        {
            { Alias: null, Column: "total" } => BuiltInTypes.Numeric,
            { Alias: null, Column: "quantity" } => BuiltInTypes.Int4,
            { Alias: null, Column: "amount" } => Amount,
            _ => Polymorphic(reference),
        };

    private static ExpressionContext CommonTypes(bool catalogFunctions = true, bool groupsHaveRows = false) =>
        new(catalogFunctions, CommonTyped, GroupsHaveRows: groupsHaveRows);

    // The parser casts every argument and every result to the common type, and the plan prints the casts: a form that
    // is not NULL is of the one type its arguments proved of a known type have.
    [Theory]
    [InlineData("COALESCE(total, '0'::numeric)", BuiltInTypes.Numeric)]
    [InlineData("COALESCE(o.note, 'x'::text)", BuiltInTypes.Text)]
    [InlineData("GREATEST(quantity, 1)", BuiltInTypes.Int4)]
    [InlineData("LEAST(o.id, '5'::bigint)", BuiltInTypes.Int8)]
    [InlineData("CASE WHEN (quantity > 1) THEN '1'::numeric ELSE 2.5 END", BuiltInTypes.Numeric)]
    [InlineData("COALESCE(o.id, (u.id)::bigint)", BuiltInTypes.Int8)]
    [InlineData("COALESCE(COALESCE(o.note, 'x'::text), o.note)", BuiltInTypes.Text)]
    [InlineData("(COALESCE(total, '0'::numeric))", BuiltInTypes.Numeric)]
    [InlineData("COALESCE(o.note, o.note)", null)]
    [InlineData("COALESCE(amount, '1'::numeric)", null)]
    [InlineData("COALESCE(o.id, u.id)", null)]
    [InlineData("CASE WHEN (quantity > 1) THEN o.note ELSE 'x'::text END", null)]
    [InlineData("CASE WHEN (quantity > 1) THEN 'x'::text END", null)]
    public void ProvedTypeOf_CommonTypeForm_IsItsArgumentsType(string entry, long? expected) =>
        PlanExpression.ProvedTypeOf(entry, CommonTypes()).ShouldBe(expected);

    // An argument proved of no known type is no argument of the common type.
    [Theory]
    [InlineData("COALESCE(u.id, 1)", BuiltInTypes.Int4)]
    [InlineData("COALESCE(u.id, u.name)", null)]
    [InlineData("CASE WHEN (u.id > 1) THEN u.id ELSE 'x'::text END", BuiltInTypes.Text)]
    public void ProvedTypeOf_ArgumentOfNoKnownType_IsSkipped(string entry, long? expected) =>
        PlanExpression.ProvedTypeOf(entry, Untyped(catalogFunctions: true, AllProved)).ShouldBe(expected);

    // Every form of a type is read only when the session finds pg_catalog first.
    [Theory]
    [InlineData("COALESCE(total, '0'::numeric)")]
    [InlineData("GREATEST(quantity, quantity)")]
    [InlineData("CASE WHEN (quantity > 1) THEN quantity ELSE quantity END")]
    public void ProvedTypeOf_CommonTypeFormWhenTheCatalogIsNotFirst_IsNull(string entry) =>
        PlanExpression.ProvedTypeOf(entry, CommonTypes(catalogFunctions: false)).ShouldBeNull();

    // A form of a type feeds an aggregate, an operator and a cast.
    [Theory]
    [InlineData("sum(COALESCE(total, '0'::numeric))")]
    [InlineData("max(COALESCE(o.id, (u.id)::bigint))")]
    [InlineData("sum(CASE WHEN (quantity > 1) THEN '1'::numeric ELSE 2.5 END)")]
    [InlineData("(COALESCE(quantity, 0) + 1)")]
    [InlineData("(GREATEST(quantity, 1))::bigint")]
    public void NullableOf_FormsOverACommonType_AreRead(string entry) =>
        PlanExpression.NullableOf(entry, CommonTypes(groupsHaveRows: true)).ShouldBe(false);

    // A domain's column is proved of the domain, which no row of the tables takes, and a form of no known type feeds
    // nothing.
    [Theory]
    [InlineData("sum(COALESCE(amount, '1'::numeric))")]
    [InlineData("(COALESCE(amount, amount) + '1'::numeric)")]
    [InlineData("(COALESCE(o.note, o.note) + 1)")]
    [InlineData("sum(COALESCE(total, '0'::numeric)) FILTER (WHERE (quantity > 1))")]
    public void NullableOf_FormsOverNoCommonType_AreUnknown(string entry) =>
        PlanExpression.NullableOf(entry, CommonTypes(groupsHaveRows: true)).ShouldBeNull();

    // The walk's answer at a full join of "a", its outer side, and "r", its inner one.
    private static (bool OuterSide, long Type)? SideOf(OutputReference reference) =>
        (reference.Alias, reference.Column) switch
        {
            ("a", "id") => (true, BuiltInTypes.Int4),
            ("r", "account_id") => (false, BuiltInTypes.Int4),
            ("r", "id") => (false, BuiltInTypes.Int8),
            _ => null,
        };

    private static ExpressionContext AtAFullJoin(bool catalogFunctions = true) =>
        new(catalogFunctions, static _ => null, ProvedOnFullJoinSide: SideOf);

    // A row of a full join has a row of one side at least, and COALESCE, GREATEST and LEAST are NULL only when every
    // argument is: over a plain reference to each side the walk proves but for the join, the form is not NULL, of
    // their type when they share one.  Both of one side, a cast of a side's column, a reference in parentheses, a CASE
    // and an operator prove nothing.
    [Theory]
    [InlineData("COALESCE(a.id, r.account_id)", false, BuiltInTypes.Int4)]
    [InlineData("GREATEST(a.id, r.account_id)", false, BuiltInTypes.Int4)]
    [InlineData("LEAST(r.account_id, a.id)", false, BuiltInTypes.Int4)]
    [InlineData("COALESCE(a.id, r.account_id, NULL::integer)", false, BuiltInTypes.Int4)]
    [InlineData("COALESCE(a.id, r.id)", false, null)]
    [InlineData("COALESCE(a.id, a.id)", null, null)]
    [InlineData("COALESCE(a.id, (r.account_id)::bigint)", null, null)]
    [InlineData("COALESCE((a.id), r.account_id)", null, null)]
    [InlineData("COALESCE(a.id, x.id)", null, null)]
    [InlineData("CASE WHEN (a.id > 1) THEN a.id ELSE r.account_id END", null, null)]
    [InlineData("(a.id + r.account_id)", null, null)]
    public void NullableOf_CoalesceOverBothSidesOfAFullJoin_IsNotNull(string entry, bool? expected, long? type)
    {
        PlanExpression.NullableOf(entry, AtAFullJoin()).ShouldBe(expected);
        PlanExpression.ProvedTypeOf(entry, AtAFullJoin()).ShouldBe(type);
    }

    // At a node that is no full join, and where the session does not find pg_catalog first, the form is not read.
    [Fact]
    public void NullableOf_CoalesceOverBothSidesWithoutTheFullJoin_IsUnknown()
    {
        PlanExpression.NullableOf("COALESCE(a.id, r.account_id)", new(true, static _ => null)).ShouldBeNull();
        PlanExpression.NullableOf("COALESCE(a.id, r.account_id)", AtAFullJoin(catalogFunctions: false)).ShouldBeNull();
    }

    [Theory]
    [InlineData("count(*)", new string[0], new string[0])]
    [InlineData("(count(o.id) + count(*))", new[] { "count" }, new string[0])]
    [InlineData("(during * tstzrange($1, $2))", new[] { "tstzrange" }, new[] { "*" })]
    [InlineData("jsonb_build_object('a'::text, (x * 2))", new[] { "jsonb_build_object" }, new[] { "*" })]
    [InlineData("(count(a.id) + count(DISTINCT a.id))", new[] { "count" }, new string[0])]
    [InlineData("upper(during)", new string[0], new string[0])]
    [InlineData("'count(x)'::text", new string[0], new string[0])]
    [InlineData("shadow.count(id)", new string[0], new string[0])]
    [InlineData("\"count\"(id)", new string[0], new string[0])]
    [InlineData("count(o.id", new string[0], new string[0])]
    public void CatalogNamesIn_Entry_GivesTheNamesItCalls(string entry, string[] functions, string[] operators)
    {
        var (foundFunctions, foundOperators) = PlanExpression.CatalogNamesIn(entry);

        foundFunctions.ShouldBe(functions);
        foundOperators.ShouldBe(operators);
    }

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
