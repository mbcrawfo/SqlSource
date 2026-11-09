namespace SqlSource.DatabaseTests.Postgres.Realistic.Generation;

// Each realistic case with models is claimed here once, with Source alone: its output, Methods, is the project's
// sqlsource.json's, which gives the case its models and a generated method that runs its query.  The models are those
// that the Dapper and method tests read, and the Dapper tests' SQL comes from RealisticGeneration, an in-process run at
// Models.  A Methods query has no constant, and its method is an extension method of the connection.
// RealisticClaimsTests fails on a case with models that this file lacks, a class of a case without them, or a claim
// that sets more than Source.

[SqlSourceGenerate(Source = "../Cases/aggregate-cte-joined-inner/query.sql")]
internal static partial class AggregateCteJoinedInnerMethod;

[SqlSourceGenerate(Source = "../Cases/aggregate-cte-left-joined/query.sql")]
internal static partial class AggregateCteLeftJoinedMethod;

[SqlSourceGenerate(Source = "../Cases/any-ids-and-enum-array/query.sql")]
internal static partial class AnyIdsAndEnumArrayMethod;

[SqlSourceGenerate(Source = "../Cases/array-and-string-agg/query.sql")]
internal static partial class ArrayAndStringAggMethod;

[SqlSourceGenerate(Source = "../Cases/category-parent-self-join/query.sql")]
internal static partial class CategoryParentSelfJoinMethod;

[SqlSourceGenerate(Source = "../Cases/coalesce-case-greatest/query.sql")]
internal static partial class CoalesceCaseGreatestMethod;

[SqlSourceGenerate(Source = "../Cases/count-filter-statuses/query.sql")]
internal static partial class CountFilterStatusesMethod;

[SqlSourceGenerate(Source = "../Cases/cte-chain/query.sql")]
internal static partial class CteChainMethod;

[SqlSourceGenerate(Source = "../Cases/cte-materialized/query.sql")]
internal static partial class CteMaterializedMethod;

[SqlSourceGenerate(Source = "../Cases/cte-not-materialized/query.sql")]
internal static partial class CteNotMaterializedMethod;

[SqlSourceGenerate(Source = "../Cases/cte-used-twice/query.sql")]
internal static partial class CteUsedTwiceMethod;

[SqlSourceGenerate(Source = "../Cases/data-modifying-cte-archive/query.sql")]
internal static partial class DataModifyingCteArchiveMethod;

[SqlSourceGenerate(Source = "../Cases/delete-using-returning/query.sql")]
internal static partial class DeleteUsingReturningMethod;

[SqlSourceGenerate(Source = "../Cases/distinct-derived-table/query.sql")]
internal static partial class DistinctDerivedTableMethod;

[SqlSourceGenerate(Source = "../Cases/distinct-on-latest-per-account/query.sql")]
internal static partial class DistinctOnLatestPerAccountMethod;

[SqlSourceGenerate(Source = "../Cases/except-accounts-without-reservations/query.sql")]
internal static partial class ExceptAccountsWithoutReservationsMethod;

[SqlSourceGenerate(Source = "../Cases/exists-as-column/query.sql")]
internal static partial class ExistsAsColumnMethod;

[SqlSourceGenerate(Source = "../Cases/full-join-accounts-reservations/query.sql")]
internal static partial class FullJoinAccountsReservationsMethod;

[SqlSourceGenerate(Source = "../Cases/group-by-having/query.sql")]
internal static partial class GroupByHavingMethod;

[SqlSourceGenerate(Source = "../Cases/in-subquery-buyers/query.sql")]
internal static partial class InSubqueryBuyersMethod;

[SqlSourceGenerate(Source = "../Cases/insert-from-unnest-arrays/query.sql")]
internal static partial class InsertFromUnnestArraysMethod;

[SqlSourceGenerate(Source = "../Cases/insert-multirow-values/query.sql")]
internal static partial class InsertMultirowValuesMethod;

[SqlSourceGenerate(Source = "../Cases/insert-select-returning/query.sql")]
internal static partial class InsertSelectReturningMethod;

[SqlSourceGenerate(Source = "../Cases/intersect-accounts-with-orders-and-events/query.sql")]
internal static partial class IntersectAccountsWithOrdersAndEventsMethod;

[SqlSourceGenerate(Source = "../Cases/json-build-and-agg/query.sql")]
internal static partial class JsonBuildAndAggMethod;

[SqlSourceGenerate(Source = "../Cases/jsonb-array-elements/query.sql")]
internal static partial class JsonbArrayElementsMethod;

[SqlSourceGenerate(Source = "../Cases/jsonb-containment-and-key/query.sql")]
internal static partial class JsonbContainmentAndKeyMethod;

[SqlSourceGenerate(Source = "../Cases/lateral-latest-order/query.sql")]
internal static partial class LateralLatestOrderMethod;

[SqlSourceGenerate(Source = "../Cases/left-joined-subquery-coalesce/query.sql")]
internal static partial class LeftJoinedSubqueryCoalesceMethod;

[SqlSourceGenerate(Source = "../Cases/left-lateral-grouped-count/query.sql")]
internal static partial class LeftLateralGroupedCountMethod;

[SqlSourceGenerate(Source = "../Cases/left-lateral-latest-order/query.sql")]
internal static partial class LeftLateralLatestOrderMethod;

[SqlSourceGenerate(Source = "../Cases/merge-returning/query.sql")]
internal static partial class MergeReturningMethod;

[SqlSourceGenerate(Source = "../Cases/order-report-four-tables/query.sql")]
internal static partial class OrderReportFourTablesMethod;

[SqlSourceGenerate(Source = "../Cases/parameter-in-cte/query.sql")]
internal static partial class ParameterInCteMethod;

[SqlSourceGenerate(Source = "../Cases/partition-nullable-side/query.sql")]
internal static partial class PartitionNullableSideMethod;

[SqlSourceGenerate(Source = "../Cases/partition-preserved-side/query.sql")]
internal static partial class PartitionPreservedSideMethod;

[SqlSourceGenerate(Source = "../Cases/recursive-category-tree/query.sql")]
internal static partial class RecursiveCategoryTreeMethod;

[SqlSourceGenerate(Source = "../Cases/reservations-in-range/query.sql")]
internal static partial class ReservationsInRangeMethod;

[SqlSourceGenerate(Source = "../Cases/right-join-orders-accounts/query.sql")]
internal static partial class RightJoinOrdersAccountsMethod;

[SqlSourceGenerate(Source = "../Cases/rollup-by-status/query.sql")]
internal static partial class RollupByStatusMethod;

[SqlSourceGenerate(Source = "../Cases/row-number-dedupe/query.sql")]
internal static partial class RowNumberDedupeMethod;

[SqlSourceGenerate(Source = "../Cases/running-total-lag-rank/query.sql")]
internal static partial class RunningTotalLagRankMethod;

[SqlSourceGenerate(Source = "../Cases/scalar-subquery-last-order/query.sql")]
internal static partial class ScalarSubqueryLastOrderMethod;

[SqlSourceGenerate(Source = "../Cases/search-ilike-optional-filter-paged/query.sql")]
internal static partial class SearchIlikeOptionalFilterPagedMethod;

[SqlSourceGenerate(Source = "../Cases/table-statement/query.sql")]
internal static partial class TableStatementMethod;

[SqlSourceGenerate(Source = "../Cases/union-all-activity-feed/query.sql")]
internal static partial class UnionAllActivityFeedMethod;

[SqlSourceGenerate(Source = "../Cases/union-branch-literal/query.sql")]
internal static partial class UnionBranchLiteralMethod;

[SqlSourceGenerate(Source = "../Cases/union-distinct-search-ordered-limited/query.sql")]
internal static partial class UnionDistinctSearchOrderedLimitedMethod;

[SqlSourceGenerate(Source = "../Cases/union-in-cte-joined/query.sql")]
internal static partial class UnionInCteJoinedMethod;

[SqlSourceGenerate(Source = "../Cases/union-mixed-types/query.sql")]
internal static partial class UnionMixedTypesMethod;

[SqlSourceGenerate(Source = "../Cases/unnest-tags-with-ordinality/query.sql")]
internal static partial class UnnestTagsWithOrdinalityMethod;

[SqlSourceGenerate(Source = "../Cases/update-from-aggregate-returning/query.sql")]
internal static partial class UpdateFromAggregateReturningMethod;

[SqlSourceGenerate(Source = "../Cases/upsert-product-returning/query.sql")]
internal static partial class UpsertProductReturningMethod;

[SqlSourceGenerate(Source = "../Cases/values-statement/query.sql")]
internal static partial class ValuesStatementMethod;

[SqlSourceGenerate(Source = "../Cases/view-joined-left/query.sql")]
internal static partial class ViewJoinedLeftMethod;
