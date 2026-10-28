namespace SqlSource.DatabaseTests.SqlServer.Realistic.Generation;

// Each realistic case with models is a Models query of the project, claimed here by a class of its own: a Path that
// names a folder searches none of the folders below it, and the attribute is not AllowMultiple, so no one class can
// claim the query.sql of every folder of Cases.  Cases/sqlsource.json gives these files the database realistic.  A
// case that the describer or the generator refuses has no class, and its case.json says so.  RealisticMethods.cs
// claims each of these cases a second time, at CodeGen.  RealisticClaimsTests fails on a case with models and no
// class, or a class of a case without them.

[SqlSourceGenerate(
    Path = "../Cases/activity-feed-union-all/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ActivityFeedUnionAllQuery;

[SqlSourceGenerate(
    Path = "../Cases/big-customers-having/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class BigCustomersHavingQuery;

[SqlSourceGenerate(
    Path = "../Cases/cart-by-id/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CartByIdQuery;

[SqlSourceGenerate(
    Path = "../Cases/categories-right-join-products/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CategoriesRightJoinProductsQuery;

[SqlSourceGenerate(
    Path = "../Cases/category-parent-self-join/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CategoryParentSelfJoinQuery;

[SqlSourceGenerate(
    Path = "../Cases/category-tree-recursive/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CategoryTreeRecursiveQuery;

[SqlSourceGenerate(
    Path = "../Cases/contact-fallbacks/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ContactFallbacksQuery;

[SqlSourceGenerate(
    Path = "../Cases/contacts-union/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ContactsUnionQuery;

[SqlSourceGenerate(
    Path = "../Cases/cte-feeding-update/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CteFeedingUpdateQuery;

[SqlSourceGenerate(
    Path = "../Cases/customer-json-object/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CustomerJsonObjectQuery;

[SqlSourceGenerate(
    Path = "../Cases/customer-order-counts/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CustomerOrderCountsQuery;

[SqlSourceGenerate(
    Path = "../Cases/customers-cross-apply-latest-order/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CustomersCrossApplyLatestOrderQuery;

[SqlSourceGenerate(
    Path = "../Cases/customers-distinct-from/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CustomersDistinctFromQuery;

[SqlSourceGenerate(
    Path = "../Cases/customers-exists-flags/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CustomersExistsFlagsQuery;

[SqlSourceGenerate(
    Path = "../Cases/customers-first-order-value/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CustomersFirstOrderValueQuery;

[SqlSourceGenerate(
    Path = "../Cases/customers-last-payment-subquery/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CustomersLastPaymentSubqueryQuery;

[SqlSourceGenerate(
    Path = "../Cases/customers-search-paged/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CustomersSearchPagedQuery;

[SqlSourceGenerate(
    Path = "../Cases/customers-with-orders-and-carts/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class CustomersWithOrdersAndCartsQuery;

[SqlSourceGenerate(
    Path = "../Cases/delete-cart-items-output/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class DeleteCartItemsOutputQuery;

[SqlSourceGenerate(
    Path = "../Cases/insert-customer-output/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class InsertCustomerOutputQuery;

[SqlSourceGenerate(
    Path = "../Cases/insert-invoice-next-value/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class InsertInvoiceNextValueQuery;

[SqlSourceGenerate(
    Path = "../Cases/invoices-due-this-month/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class InvoicesDueThisMonthQuery;

[SqlSourceGenerate(
    Path = "../Cases/invoices-full-join-payments/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class InvoicesFullJoinPaymentsQuery;

[SqlSourceGenerate(
    Path = "../Cases/invoices-outer-apply-latest-payment/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class InvoicesOuterApplyLatestPaymentQuery;

[SqlSourceGenerate(
    Path = "../Cases/last-orders-view-joined/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class LastOrdersViewJoinedQuery;

[SqlSourceGenerate(
    Path = "../Cases/latest-order-per-customer/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class LatestOrderPerCustomerQuery;

[SqlSourceGenerate(
    Path = "../Cases/merge-stock-upsert/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class MergeStockUpsertQuery;

[SqlSourceGenerate(
    Path = "../Cases/order-gaps-lag-lead/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class OrderGapsLagLeadQuery;

[SqlSourceGenerate(
    Path = "../Cases/order-note-optimistic-update/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class OrderNoteOptimisticUpdateQuery;

[SqlSourceGenerate(
    Path = "../Cases/order-skus-string-agg/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class OrderSkusStringAggQuery;

[SqlSourceGenerate(
    Path = "../Cases/order-status-labels/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class OrderStatusLabelsQuery;

[SqlSourceGenerate(
    Path = "../Cases/orders-grouping-sets/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class OrdersGroupingSetsQuery;

[SqlSourceGenerate(
    Path = "../Cases/orders-in-customer-time-zone/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class OrdersInCustomerTimeZoneQuery;

[SqlSourceGenerate(
    Path = "../Cases/orders-in-statuses/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class OrdersInStatusesQuery;

[SqlSourceGenerate(
    Path = "../Cases/orders-keyset-page/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class OrdersKeysetPageQuery;

[SqlSourceGenerate(
    Path = "../Cases/orders-optional-filters/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class OrdersOptionalFiltersQuery;

[SqlSourceGenerate(
    Path = "../Cases/orders-with-invoice/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class OrdersWithInvoiceQuery;

[SqlSourceGenerate(
    Path = "../Cases/orders-with-lines-for-json/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class OrdersWithLinesForJsonQuery;

[SqlSourceGenerate(
    Path = "../Cases/payment-reference-try-convert/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class PaymentReferenceTryConvertQuery;

[SqlSourceGenerate(
    Path = "../Cases/payments-running-total/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class PaymentsRunningTotalQuery;

[SqlSourceGenerate(
    Path = "../Cases/price-as-of/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class PriceAsOfQuery;

[SqlSourceGenerate(
    Path = "../Cases/price-bounds-greatest/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class PriceBoundsGreatestQuery;

[SqlSourceGenerate(
    Path = "../Cases/price-history-all/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class PriceHistoryAllQuery;

[SqlSourceGenerate(
    Path = "../Cases/product-attributes-openjson/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ProductAttributesOpenjsonQuery;

[SqlSourceGenerate(
    Path = "../Cases/product-color-json-value/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ProductColorJsonValueQuery;

[SqlSourceGenerate(
    Path = "../Cases/product-specs-json-column/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ProductSpecsJsonColumnQuery;

[SqlSourceGenerate(
    Path = "../Cases/products-like-escape/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ProductsLikeEscapeQuery;

[SqlSourceGenerate(
    Path = "../Cases/products-never-ordered/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ProductsNeverOrderedQuery;

[SqlSourceGenerate(
    Path = "../Cases/products-ranked-in-category/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ProductsRankedInCategoryQuery;

[SqlSourceGenerate(
    Path = "../Cases/sales-by-category-rollup/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class SalesByCategoryRollupQuery;

[SqlSourceGenerate(
    Path = "../Cases/shipping-label-concat/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class ShippingLabelConcatQuery;

[SqlSourceGenerate(
    Path = "../Cases/top-with-ties-best-sellers/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class TopWithTiesBestSellersQuery;

[SqlSourceGenerate(
    Path = "../Cases/update-orders-from-join-output/query.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class UpdateOrdersFromJoinOutputQuery;
