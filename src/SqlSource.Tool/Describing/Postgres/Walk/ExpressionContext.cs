using System;

namespace SqlSource.Tool.Describing.Postgres.Walk;

/// <summary>What the walk tells <see cref="PlanExpression" /> of the plan an entry is read in.</summary>
/// <param name="CatalogFunctions">
/// Whether the session finds <c>pg_catalog</c> first (<see cref="PostgresSearchPath" />), so that a function, an
/// operator or a type name the plan prints bare is the catalog's.
/// </param>
/// <param name="ProvedTypeOf">
/// The declared type of the column a reference that is part of the entry names, when the walk proves it is not
/// <c>NULL</c>; <see cref="TotalFunctions.UnknownType" /> for one proved of no known type; or null.  A reference that
/// is the whole entry is never asked.
/// </param>
/// <param name="GroupPreserved">
/// Whether a span in parentheses inside the entry, given as the text inside them, may be read as what it says, or null
/// when every one may.  The walk says no for a value a node under a join that null-extends computed, which the node
/// that computes the entry takes from it and prints in parentheses: such a span is unknown, whatever its form.  The
/// entry as a whole is never asked.
/// </param>
/// <param name="GroupFromChild">
/// Whether a span in parentheses of the entry, given as the text inside them, is a value a child of the node that
/// computes the entry printed, or null when none is.  No aggregate inside such a span is read, since the child
/// computed it under its own grouping.
/// </param>
/// <param name="GroupsHaveRows">
/// Whether the node that computes the entry is an <c>Aggregate</c> each of whose groups has a row: one with a
/// <c>Group Key</c> and no <c>Grouping Sets</c>.
/// </param>
/// <param name="ScalarSubPlanTypeOf">
/// The type of the value of the <c>SubPlan</c> of a name, <c>SubPlan 1</c>, when its plan always returns one row whose
/// value the walk proves not <c>NULL</c>; or null.  The walk answers it only for a subplan that is the whole entry, of
/// the column's type.
/// </param>
/// <param name="SubPlanColumnTypeOf">
/// The type of a column, by its number from 1, of the root of the <c>SubPlan</c> of a name, when the walk proves it
/// not <c>NULL</c>; or null.
/// </param>
/// <param name="CatalogAlone">
/// The names of <see cref="CatalogOverloads" /> that no schema but <c>pg_catalog</c> holds a function or an operator
/// of, or null for none.  A call of such a name is read by its name, and only when
/// <paramref name="CatalogFunctions" /> holds too; a name it leaves out is read as no form of that table.
/// </param>
/// <param name="ProvedOnFullJoinSide">
/// Where the node that computes the entry is a <c>Full</c> join that no join above null-extends: for a reference to an
/// instance under one of its sides, under which no join null-extends, whether that side is the join's <c>Outer</c>
/// child, and the declared type of the column, when the walk would prove the column but for that join's extension;
/// or null.  It is null at any other node.
/// </param>
internal sealed record ExpressionContext(
    bool CatalogFunctions,
    Func<OutputReference, long?> ProvedTypeOf,
    Func<string, bool>? GroupPreserved = null,
    Func<string, bool>? GroupFromChild = null,
    bool GroupsHaveRows = false,
    Func<string, long?>? ScalarSubPlanTypeOf = null,
    Func<string, int, long?>? SubPlanColumnTypeOf = null,
    CatalogNames? CatalogAlone = null,
    Func<OutputReference, (bool OuterSide, long Type)?>? ProvedOnFullJoinSide = null
);
