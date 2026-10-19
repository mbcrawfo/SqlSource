namespace SqlSource.Tests.Postgres.Realistic.Generation;

// Each realistic case with models is a Models query of the project, claimed here by a class of its own: a Path that
// names a folder searches none of the folders below it, and the attribute is not AllowMultiple, so no one class can
// claim the query.sql of every folder of Cases.  The csproj gives these files the database realistic.  A case that
// the describer or the generator refuses has no class, and its case.json says so.  RealisticClaimsTests fails on a
// case with models and no class, or a class of a case without them.

[SqlSourceGenerate(
    Path = "../Cases/aggregate-cte-joined-inner/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class AggregateCteJoinedInnerQuery;

[SqlSourceGenerate(
    Path = "../Cases/aggregate-cte-left-joined/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class AggregateCteLeftJoinedQuery;

[SqlSourceGenerate(
    Path = "../Cases/any-ids-and-enum-array/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class AnyIdsAndEnumArrayQuery;

// A class, since Dapper cannot construct a record with an array column (TD-0062).
[SqlSourceGenerate(
    Path = "../Cases/array-and-string-agg/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models,
    OutputModelType = GeneratorModelType.Class
)]
internal static partial class ArrayAndStringAggQuery;

[SqlSourceGenerate(
    Path = "../Cases/category-parent-self-join/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CategoryParentSelfJoinQuery;

[SqlSourceGenerate(
    Path = "../Cases/coalesce-case-greatest/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CoalesceCaseGreatestQuery;

[SqlSourceGenerate(
    Path = "../Cases/count-filter-statuses/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CountFilterStatusesQuery;

[SqlSourceGenerate(
    Path = "../Cases/cte-chain/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CteChainQuery;

[SqlSourceGenerate(
    Path = "../Cases/cte-materialized/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CteMaterializedQuery;

[SqlSourceGenerate(
    Path = "../Cases/cte-not-materialized/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CteNotMaterializedQuery;

[SqlSourceGenerate(
    Path = "../Cases/cte-used-twice/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CteUsedTwiceQuery;

[SqlSourceGenerate(
    Path = "../Cases/data-modifying-cte-archive/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class DataModifyingCteArchiveQuery;

[SqlSourceGenerate(
    Path = "../Cases/delete-using-returning/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class DeleteUsingReturningQuery;

[SqlSourceGenerate(
    Path = "../Cases/distinct-derived-table/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class DistinctDerivedTableQuery;

[SqlSourceGenerate(
    Path = "../Cases/distinct-on-latest-per-account/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class DistinctOnLatestPerAccountQuery;

[SqlSourceGenerate(
    Path = "../Cases/except-accounts-without-reservations/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ExceptAccountsWithoutReservationsQuery;

[SqlSourceGenerate(
    Path = "../Cases/exists-as-column/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ExistsAsColumnQuery;

[SqlSourceGenerate(
    Path = "../Cases/full-join-accounts-reservations/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class FullJoinAccountsReservationsQuery;

[SqlSourceGenerate(
    Path = "../Cases/group-by-having/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class GroupByHavingQuery;

[SqlSourceGenerate(
    Path = "../Cases/in-subquery-buyers/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class InSubqueryBuyersQuery;

[SqlSourceGenerate(
    Path = "../Cases/insert-from-unnest-arrays/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class InsertFromUnnestArraysQuery;

[SqlSourceGenerate(
    Path = "../Cases/insert-multirow-values/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class InsertMultirowValuesQuery;

[SqlSourceGenerate(
    Path = "../Cases/insert-select-returning/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class InsertSelectReturningQuery;

[SqlSourceGenerate(
    Path = "../Cases/intersect-accounts-with-orders-and-events/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class IntersectAccountsWithOrdersAndEventsQuery;

[SqlSourceGenerate(
    Path = "../Cases/json-build-and-agg/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class JsonBuildAndAggQuery;

[SqlSourceGenerate(
    Path = "../Cases/jsonb-array-elements/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class JsonbArrayElementsQuery;

[SqlSourceGenerate(
    Path = "../Cases/jsonb-containment-and-key/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class JsonbContainmentAndKeyQuery;

[SqlSourceGenerate(
    Path = "../Cases/lateral-latest-order/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class LateralLatestOrderQuery;

[SqlSourceGenerate(
    Path = "../Cases/left-joined-subquery-coalesce/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class LeftJoinedSubqueryCoalesceQuery;

[SqlSourceGenerate(
    Path = "../Cases/left-lateral-grouped-count/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class LeftLateralGroupedCountQuery;

[SqlSourceGenerate(
    Path = "../Cases/left-lateral-latest-order/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class LeftLateralLatestOrderQuery;

[SqlSourceGenerate(
    Path = "../Cases/merge-returning/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class MergeReturningQuery;

[SqlSourceGenerate(
    Path = "../Cases/order-report-four-tables/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class OrderReportFourTablesQuery;

[SqlSourceGenerate(
    Path = "../Cases/parameter-in-cte/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ParameterInCteQuery;

[SqlSourceGenerate(
    Path = "../Cases/partition-nullable-side/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class PartitionNullableSideQuery;

[SqlSourceGenerate(
    Path = "../Cases/partition-preserved-side/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class PartitionPreservedSideQuery;

[SqlSourceGenerate(
    Path = "../Cases/recursive-category-tree/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class RecursiveCategoryTreeQuery;

[SqlSourceGenerate(
    Path = "../Cases/reservations-in-range/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ReservationsInRangeQuery;

[SqlSourceGenerate(
    Path = "../Cases/right-join-orders-accounts/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class RightJoinOrdersAccountsQuery;

[SqlSourceGenerate(
    Path = "../Cases/rollup-by-status/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class RollupByStatusQuery;

[SqlSourceGenerate(
    Path = "../Cases/row-number-dedupe/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class RowNumberDedupeQuery;

[SqlSourceGenerate(
    Path = "../Cases/running-total-lag-rank/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class RunningTotalLagRankQuery;

[SqlSourceGenerate(
    Path = "../Cases/scalar-subquery-last-order/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ScalarSubqueryLastOrderQuery;

[SqlSourceGenerate(
    Path = "../Cases/search-ilike-optional-filter-paged/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class SearchIlikeOptionalFilterPagedQuery;

[SqlSourceGenerate(
    Path = "../Cases/table-statement/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class TableStatementQuery;

[SqlSourceGenerate(
    Path = "../Cases/union-all-activity-feed/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class UnionAllActivityFeedQuery;

[SqlSourceGenerate(
    Path = "../Cases/union-branch-literal/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class UnionBranchLiteralQuery;

[SqlSourceGenerate(
    Path = "../Cases/union-distinct-search-ordered-limited/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class UnionDistinctSearchOrderedLimitedQuery;

[SqlSourceGenerate(
    Path = "../Cases/union-in-cte-joined/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class UnionInCteJoinedQuery;

[SqlSourceGenerate(
    Path = "../Cases/union-mixed-types/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class UnionMixedTypesQuery;

[SqlSourceGenerate(
    Path = "../Cases/unnest-tags-with-ordinality/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class UnnestTagsWithOrdinalityQuery;

[SqlSourceGenerate(
    Path = "../Cases/update-from-aggregate-returning/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class UpdateFromAggregateReturningQuery;

[SqlSourceGenerate(
    Path = "../Cases/upsert-product-returning/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class UpsertProductReturningQuery;

[SqlSourceGenerate(
    Path = "../Cases/values-statement/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ValuesStatementQuery;

[SqlSourceGenerate(
    Path = "../Cases/view-joined-left/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ViewJoinedLeftQuery;
