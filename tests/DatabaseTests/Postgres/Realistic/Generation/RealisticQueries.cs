namespace SqlSource.DatabaseTests.Postgres.Realistic.Generation;

// Each realistic case with models is a Models query of the project, claimed here by a class of its own: a Source that
// names a folder searches none of the folders below it, and the attribute is not AllowMultiple, so no one class can
// claim the query.sql of every folder of Cases.  Cases/sqlsource.json gives these files the database realistic.  A
// case that the describer or the generator refuses has no class, and its case.json says so.  RealisticMethods.cs
// claims each of these cases a second time, at CodeGen.  RealisticClaimsTests fails on a case with models and no
// class, or a class of a case without them.

[SqlSourceGenerate(
    Source = "../Cases/aggregate-cte-joined-inner/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class AggregateCteJoinedInnerQuery;

[SqlSourceGenerate(
    Source = "../Cases/aggregate-cte-left-joined/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class AggregateCteLeftJoinedQuery;

[SqlSourceGenerate(
    Source = "../Cases/any-ids-and-enum-array/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class AnyIdsAndEnumArrayQuery;

[SqlSourceGenerate(
    Source = "../Cases/array-and-string-agg/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ArrayAndStringAggQuery;

[SqlSourceGenerate(
    Source = "../Cases/category-parent-self-join/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CategoryParentSelfJoinQuery;

[SqlSourceGenerate(
    Source = "../Cases/coalesce-case-greatest/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CoalesceCaseGreatestQuery;

[SqlSourceGenerate(
    Source = "../Cases/count-filter-statuses/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CountFilterStatusesQuery;

[SqlSourceGenerate(
    Source = "../Cases/cte-chain/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CteChainQuery;

[SqlSourceGenerate(
    Source = "../Cases/cte-materialized/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CteMaterializedQuery;

[SqlSourceGenerate(
    Source = "../Cases/cte-not-materialized/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CteNotMaterializedQuery;

[SqlSourceGenerate(
    Source = "../Cases/cte-used-twice/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CteUsedTwiceQuery;

[SqlSourceGenerate(
    Source = "../Cases/data-modifying-cte-archive/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class DataModifyingCteArchiveQuery;

[SqlSourceGenerate(
    Source = "../Cases/delete-using-returning/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class DeleteUsingReturningQuery;

[SqlSourceGenerate(
    Source = "../Cases/distinct-derived-table/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class DistinctDerivedTableQuery;

[SqlSourceGenerate(
    Source = "../Cases/distinct-on-latest-per-account/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class DistinctOnLatestPerAccountQuery;

[SqlSourceGenerate(
    Source = "../Cases/except-accounts-without-reservations/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ExceptAccountsWithoutReservationsQuery;

[SqlSourceGenerate(
    Source = "../Cases/exists-as-column/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ExistsAsColumnQuery;

[SqlSourceGenerate(
    Source = "../Cases/full-join-accounts-reservations/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class FullJoinAccountsReservationsQuery;

[SqlSourceGenerate(
    Source = "../Cases/group-by-having/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class GroupByHavingQuery;

[SqlSourceGenerate(
    Source = "../Cases/in-subquery-buyers/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class InSubqueryBuyersQuery;

[SqlSourceGenerate(
    Source = "../Cases/insert-from-unnest-arrays/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class InsertFromUnnestArraysQuery;

[SqlSourceGenerate(
    Source = "../Cases/insert-multirow-values/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class InsertMultirowValuesQuery;

[SqlSourceGenerate(
    Source = "../Cases/insert-select-returning/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class InsertSelectReturningQuery;

[SqlSourceGenerate(
    Source = "../Cases/intersect-accounts-with-orders-and-events/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class IntersectAccountsWithOrdersAndEventsQuery;

[SqlSourceGenerate(
    Source = "../Cases/json-build-and-agg/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class JsonBuildAndAggQuery;

[SqlSourceGenerate(
    Source = "../Cases/jsonb-array-elements/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class JsonbArrayElementsQuery;

[SqlSourceGenerate(
    Source = "../Cases/jsonb-containment-and-key/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class JsonbContainmentAndKeyQuery;

[SqlSourceGenerate(
    Source = "../Cases/lateral-latest-order/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class LateralLatestOrderQuery;

[SqlSourceGenerate(
    Source = "../Cases/left-joined-subquery-coalesce/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class LeftJoinedSubqueryCoalesceQuery;

[SqlSourceGenerate(
    Source = "../Cases/left-lateral-grouped-count/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class LeftLateralGroupedCountQuery;

[SqlSourceGenerate(
    Source = "../Cases/left-lateral-latest-order/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class LeftLateralLatestOrderQuery;

[SqlSourceGenerate(
    Source = "../Cases/merge-returning/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class MergeReturningQuery;

[SqlSourceGenerate(
    Source = "../Cases/order-report-four-tables/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class OrderReportFourTablesQuery;

[SqlSourceGenerate(
    Source = "../Cases/parameter-in-cte/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ParameterInCteQuery;

[SqlSourceGenerate(
    Source = "../Cases/partition-nullable-side/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class PartitionNullableSideQuery;

[SqlSourceGenerate(
    Source = "../Cases/partition-preserved-side/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class PartitionPreservedSideQuery;

[SqlSourceGenerate(
    Source = "../Cases/recursive-category-tree/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class RecursiveCategoryTreeQuery;

[SqlSourceGenerate(
    Source = "../Cases/reservations-in-range/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ReservationsInRangeQuery;

[SqlSourceGenerate(
    Source = "../Cases/right-join-orders-accounts/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class RightJoinOrdersAccountsQuery;

[SqlSourceGenerate(
    Source = "../Cases/rollup-by-status/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class RollupByStatusQuery;

[SqlSourceGenerate(
    Source = "../Cases/row-number-dedupe/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class RowNumberDedupeQuery;

[SqlSourceGenerate(
    Source = "../Cases/running-total-lag-rank/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class RunningTotalLagRankQuery;

[SqlSourceGenerate(
    Source = "../Cases/scalar-subquery-last-order/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ScalarSubqueryLastOrderQuery;

[SqlSourceGenerate(
    Source = "../Cases/search-ilike-optional-filter-paged/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class SearchIlikeOptionalFilterPagedQuery;

[SqlSourceGenerate(
    Source = "../Cases/table-statement/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class TableStatementQuery;

[SqlSourceGenerate(
    Source = "../Cases/union-all-activity-feed/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class UnionAllActivityFeedQuery;

[SqlSourceGenerate(
    Source = "../Cases/union-branch-literal/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class UnionBranchLiteralQuery;

[SqlSourceGenerate(
    Source = "../Cases/union-distinct-search-ordered-limited/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class UnionDistinctSearchOrderedLimitedQuery;

[SqlSourceGenerate(
    Source = "../Cases/union-in-cte-joined/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class UnionInCteJoinedQuery;

[SqlSourceGenerate(
    Source = "../Cases/union-mixed-types/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class UnionMixedTypesQuery;

[SqlSourceGenerate(
    Source = "../Cases/unnest-tags-with-ordinality/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class UnnestTagsWithOrdinalityQuery;

[SqlSourceGenerate(
    Source = "../Cases/update-from-aggregate-returning/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class UpdateFromAggregateReturningQuery;

[SqlSourceGenerate(
    Source = "../Cases/upsert-product-returning/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class UpsertProductReturningQuery;

[SqlSourceGenerate(
    Source = "../Cases/values-statement/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ValuesStatementQuery;

[SqlSourceGenerate(
    Source = "../Cases/view-joined-left/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ViewJoinedLeftQuery;
