namespace SqlSource.DatabaseTests.SqlServer.Realistic.Generation;

// Each realistic case with models is a Models query of the project, claimed here by a class of its own: a Source that
// names a folder searches none of the folders below it, and the attribute is not AllowMultiple, so no one class can
// claim the query.sql of every folder of Cases.  Cases/sqlsource.json gives these files the database realistic.  A
// case that the describer or the generator refuses has no class, and its case.json says so.  RealisticMethods.cs
// claims each of these cases a second time, at CodeGen.  RealisticClaimsTests fails on a case with models and no
// class, or a class of a case without them.

[SqlSourceGenerate(
    Source = "../Cases/activity-feed-union-all/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ActivityFeedUnionAllQuery;

[SqlSourceGenerate(
    Source = "../Cases/big-customers-having/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class BigCustomersHavingQuery;

[SqlSourceGenerate(
    Source = "../Cases/cart-by-id/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CartByIdQuery;

[SqlSourceGenerate(
    Source = "../Cases/categories-right-join-products/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CategoriesRightJoinProductsQuery;

[SqlSourceGenerate(
    Source = "../Cases/category-parent-self-join/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CategoryParentSelfJoinQuery;

[SqlSourceGenerate(
    Source = "../Cases/category-tree-recursive/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CategoryTreeRecursiveQuery;

[SqlSourceGenerate(
    Source = "../Cases/contact-fallbacks/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ContactFallbacksQuery;

[SqlSourceGenerate(
    Source = "../Cases/contacts-union/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ContactsUnionQuery;

[SqlSourceGenerate(
    Source = "../Cases/cte-feeding-update/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CteFeedingUpdateQuery;

[SqlSourceGenerate(
    Source = "../Cases/customer-json-object/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CustomerJsonObjectQuery;

[SqlSourceGenerate(
    Source = "../Cases/customer-order-counts/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CustomerOrderCountsQuery;

[SqlSourceGenerate(
    Source = "../Cases/customers-cross-apply-latest-order/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CustomersCrossApplyLatestOrderQuery;

[SqlSourceGenerate(
    Source = "../Cases/customers-distinct-from/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CustomersDistinctFromQuery;

[SqlSourceGenerate(
    Source = "../Cases/customers-exists-flags/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CustomersExistsFlagsQuery;

[SqlSourceGenerate(
    Source = "../Cases/customers-first-order-value/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CustomersFirstOrderValueQuery;

[SqlSourceGenerate(
    Source = "../Cases/customers-last-payment-subquery/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CustomersLastPaymentSubqueryQuery;

[SqlSourceGenerate(
    Source = "../Cases/customers-search-paged/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CustomersSearchPagedQuery;

[SqlSourceGenerate(
    Source = "../Cases/customers-with-orders-and-carts/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CustomersWithOrdersAndCartsQuery;

[SqlSourceGenerate(
    Source = "../Cases/delete-cart-items-output/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class DeleteCartItemsOutputQuery;

[SqlSourceGenerate(
    Source = "../Cases/insert-customer-output/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class InsertCustomerOutputQuery;

[SqlSourceGenerate(
    Source = "../Cases/insert-invoice-next-value/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class InsertInvoiceNextValueQuery;

[SqlSourceGenerate(
    Source = "../Cases/invoices-due-this-month/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class InvoicesDueThisMonthQuery;

[SqlSourceGenerate(
    Source = "../Cases/invoices-full-join-payments/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class InvoicesFullJoinPaymentsQuery;

[SqlSourceGenerate(
    Source = "../Cases/invoices-outer-apply-latest-payment/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class InvoicesOuterApplyLatestPaymentQuery;

[SqlSourceGenerate(
    Source = "../Cases/last-orders-view-joined/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class LastOrdersViewJoinedQuery;

[SqlSourceGenerate(
    Source = "../Cases/latest-order-per-customer/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class LatestOrderPerCustomerQuery;

[SqlSourceGenerate(
    Source = "../Cases/merge-stock-upsert/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class MergeStockUpsertQuery;

[SqlSourceGenerate(
    Source = "../Cases/order-gaps-lag-lead/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class OrderGapsLagLeadQuery;

[SqlSourceGenerate(
    Source = "../Cases/order-note-optimistic-update/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class OrderNoteOptimisticUpdateQuery;

[SqlSourceGenerate(
    Source = "../Cases/order-skus-string-agg/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class OrderSkusStringAggQuery;

[SqlSourceGenerate(
    Source = "../Cases/order-status-labels/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class OrderStatusLabelsQuery;

[SqlSourceGenerate(
    Source = "../Cases/orders-grouping-sets/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class OrdersGroupingSetsQuery;

[SqlSourceGenerate(
    Source = "../Cases/orders-in-customer-time-zone/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class OrdersInCustomerTimeZoneQuery;

[SqlSourceGenerate(
    Source = "../Cases/orders-in-statuses/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class OrdersInStatusesQuery;

[SqlSourceGenerate(
    Source = "../Cases/orders-keyset-page/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class OrdersKeysetPageQuery;

[SqlSourceGenerate(
    Source = "../Cases/orders-optional-filters/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class OrdersOptionalFiltersQuery;

[SqlSourceGenerate(
    Source = "../Cases/orders-with-invoice/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class OrdersWithInvoiceQuery;

[SqlSourceGenerate(
    Source = "../Cases/orders-with-lines-for-json/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class OrdersWithLinesForJsonQuery;

[SqlSourceGenerate(
    Source = "../Cases/payment-reference-try-convert/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class PaymentReferenceTryConvertQuery;

[SqlSourceGenerate(
    Source = "../Cases/payments-running-total/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class PaymentsRunningTotalQuery;

[SqlSourceGenerate(
    Source = "../Cases/price-as-of/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class PriceAsOfQuery;

[SqlSourceGenerate(
    Source = "../Cases/price-bounds-greatest/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class PriceBoundsGreatestQuery;

[SqlSourceGenerate(
    Source = "../Cases/price-history-all/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class PriceHistoryAllQuery;

[SqlSourceGenerate(
    Source = "../Cases/product-attributes-openjson/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ProductAttributesOpenjsonQuery;

[SqlSourceGenerate(
    Source = "../Cases/product-color-json-value/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ProductColorJsonValueQuery;

[SqlSourceGenerate(
    Source = "../Cases/product-specs-json-column/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ProductSpecsJsonColumnQuery;

[SqlSourceGenerate(
    Source = "../Cases/products-like-escape/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ProductsLikeEscapeQuery;

[SqlSourceGenerate(
    Source = "../Cases/products-never-ordered/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ProductsNeverOrderedQuery;

[SqlSourceGenerate(
    Source = "../Cases/products-ranked-in-category/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ProductsRankedInCategoryQuery;

[SqlSourceGenerate(
    Source = "../Cases/sales-by-category-rollup/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class SalesByCategoryRollupQuery;

[SqlSourceGenerate(
    Source = "../Cases/shipping-label-concat/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ShippingLabelConcatQuery;

[SqlSourceGenerate(
    Source = "../Cases/top-with-ties-best-sellers/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class TopWithTiesBestSellersQuery;

[SqlSourceGenerate(
    Source = "../Cases/update-orders-from-join-output/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class UpdateOrdersFromJoinOutputQuery;
