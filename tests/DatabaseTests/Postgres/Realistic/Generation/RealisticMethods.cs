namespace SqlSource.DatabaseTests.Postgres.Realistic.Generation;

// Each realistic case with models is claimed a second time here, at CodeGen, so that a generated method runs its
// query; RealisticQueries.cs claims it at Models.  The classes are in the same namespace as that file's, so the two
// claims share the models.  A CodeGen query has no constant, so these classes set no SqlLocation.
// RealisticClaimsTests fails on a case with models that either file lacks, or a class of a case without them.

[SqlSourceGenerate(Path = "../Cases/aggregate-cte-joined-inner/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class AggregateCteJoinedInnerMethod;

[SqlSourceGenerate(Path = "../Cases/aggregate-cte-left-joined/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class AggregateCteLeftJoinedMethod;

[SqlSourceGenerate(Path = "../Cases/any-ids-and-enum-array/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class AnyIdsAndEnumArrayMethod;

[SqlSourceGenerate(Path = "../Cases/array-and-string-agg/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class ArrayAndStringAggMethod;

[SqlSourceGenerate(Path = "../Cases/category-parent-self-join/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CategoryParentSelfJoinMethod;

[SqlSourceGenerate(Path = "../Cases/coalesce-case-greatest/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CoalesceCaseGreatestMethod;

[SqlSourceGenerate(Path = "../Cases/count-filter-statuses/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CountFilterStatusesMethod;

[SqlSourceGenerate(Path = "../Cases/cte-chain/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CteChainMethod;

[SqlSourceGenerate(Path = "../Cases/cte-materialized/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CteMaterializedMethod;

[SqlSourceGenerate(Path = "../Cases/cte-not-materialized/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CteNotMaterializedMethod;

[SqlSourceGenerate(Path = "../Cases/cte-used-twice/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CteUsedTwiceMethod;

[SqlSourceGenerate(Path = "../Cases/data-modifying-cte-archive/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class DataModifyingCteArchiveMethod;

[SqlSourceGenerate(Path = "../Cases/delete-using-returning/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class DeleteUsingReturningMethod;

[SqlSourceGenerate(Path = "../Cases/distinct-derived-table/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class DistinctDerivedTableMethod;

[SqlSourceGenerate(Path = "../Cases/distinct-on-latest-per-account/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class DistinctOnLatestPerAccountMethod;

[SqlSourceGenerate(Path = "../Cases/except-accounts-without-reservations/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class ExceptAccountsWithoutReservationsMethod;

[SqlSourceGenerate(Path = "../Cases/exists-as-column/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class ExistsAsColumnMethod;

[SqlSourceGenerate(Path = "../Cases/full-join-accounts-reservations/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class FullJoinAccountsReservationsMethod;

[SqlSourceGenerate(Path = "../Cases/group-by-having/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class GroupByHavingMethod;

[SqlSourceGenerate(Path = "../Cases/in-subquery-buyers/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class InSubqueryBuyersMethod;

[SqlSourceGenerate(Path = "../Cases/insert-from-unnest-arrays/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class InsertFromUnnestArraysMethod;

[SqlSourceGenerate(Path = "../Cases/insert-multirow-values/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class InsertMultirowValuesMethod;

[SqlSourceGenerate(Path = "../Cases/insert-select-returning/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class InsertSelectReturningMethod;

[SqlSourceGenerate(
    Path = "../Cases/intersect-accounts-with-orders-and-events/query.sql",
    Output = GeneratorOutput.CodeGen
)]
internal static partial class IntersectAccountsWithOrdersAndEventsMethod;

[SqlSourceGenerate(Path = "../Cases/json-build-and-agg/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class JsonBuildAndAggMethod;

[SqlSourceGenerate(Path = "../Cases/jsonb-array-elements/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class JsonbArrayElementsMethod;

[SqlSourceGenerate(Path = "../Cases/jsonb-containment-and-key/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class JsonbContainmentAndKeyMethod;

[SqlSourceGenerate(Path = "../Cases/lateral-latest-order/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class LateralLatestOrderMethod;

[SqlSourceGenerate(Path = "../Cases/left-joined-subquery-coalesce/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class LeftJoinedSubqueryCoalesceMethod;

[SqlSourceGenerate(Path = "../Cases/left-lateral-grouped-count/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class LeftLateralGroupedCountMethod;

[SqlSourceGenerate(Path = "../Cases/left-lateral-latest-order/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class LeftLateralLatestOrderMethod;

[SqlSourceGenerate(Path = "../Cases/merge-returning/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class MergeReturningMethod;

[SqlSourceGenerate(Path = "../Cases/order-report-four-tables/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class OrderReportFourTablesMethod;

[SqlSourceGenerate(Path = "../Cases/parameter-in-cte/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class ParameterInCteMethod;

[SqlSourceGenerate(Path = "../Cases/partition-nullable-side/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class PartitionNullableSideMethod;

[SqlSourceGenerate(Path = "../Cases/partition-preserved-side/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class PartitionPreservedSideMethod;

[SqlSourceGenerate(Path = "../Cases/recursive-category-tree/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class RecursiveCategoryTreeMethod;

[SqlSourceGenerate(Path = "../Cases/reservations-in-range/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class ReservationsInRangeMethod;

[SqlSourceGenerate(Path = "../Cases/right-join-orders-accounts/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class RightJoinOrdersAccountsMethod;

[SqlSourceGenerate(Path = "../Cases/rollup-by-status/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class RollupByStatusMethod;

[SqlSourceGenerate(Path = "../Cases/row-number-dedupe/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class RowNumberDedupeMethod;

[SqlSourceGenerate(Path = "../Cases/running-total-lag-rank/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class RunningTotalLagRankMethod;

[SqlSourceGenerate(Path = "../Cases/scalar-subquery-last-order/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class ScalarSubqueryLastOrderMethod;

[SqlSourceGenerate(Path = "../Cases/search-ilike-optional-filter-paged/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class SearchIlikeOptionalFilterPagedMethod;

[SqlSourceGenerate(Path = "../Cases/table-statement/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class TableStatementMethod;

[SqlSourceGenerate(Path = "../Cases/union-all-activity-feed/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class UnionAllActivityFeedMethod;

[SqlSourceGenerate(Path = "../Cases/union-branch-literal/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class UnionBranchLiteralMethod;

[SqlSourceGenerate(Path = "../Cases/union-distinct-search-ordered-limited/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class UnionDistinctSearchOrderedLimitedMethod;

[SqlSourceGenerate(Path = "../Cases/union-in-cte-joined/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class UnionInCteJoinedMethod;

[SqlSourceGenerate(Path = "../Cases/union-mixed-types/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class UnionMixedTypesMethod;

[SqlSourceGenerate(Path = "../Cases/unnest-tags-with-ordinality/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class UnnestTagsWithOrdinalityMethod;

[SqlSourceGenerate(Path = "../Cases/update-from-aggregate-returning/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class UpdateFromAggregateReturningMethod;

[SqlSourceGenerate(Path = "../Cases/upsert-product-returning/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class UpsertProductReturningMethod;

[SqlSourceGenerate(Path = "../Cases/values-statement/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class ValuesStatementMethod;

[SqlSourceGenerate(Path = "../Cases/view-joined-left/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class ViewJoinedLeftMethod;
