namespace SqlSource.DatabaseTests.SqlServer.Realistic.Generation;

// Each realistic case with models is claimed a second time here, at CodeGen, so that a generated method runs its
// query; RealisticQueries.cs claims it at Models.  The classes are in the same namespace as that file's, so the two
// claims share the models.  A CodeGen query has no constant, so these classes set no SqlLocation.
// RealisticClaimsTests fails on a case with models that either file lacks, or a class of a case without them.

[SqlSourceGenerate(Source = "../Cases/activity-feed-union-all/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class ActivityFeedUnionAllMethod;

[SqlSourceGenerate(Source = "../Cases/big-customers-having/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class BigCustomersHavingMethod;

[SqlSourceGenerate(Source = "../Cases/cart-by-id/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CartByIdMethod;

[SqlSourceGenerate(Source = "../Cases/categories-right-join-products/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CategoriesRightJoinProductsMethod;

[SqlSourceGenerate(Source = "../Cases/category-parent-self-join/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CategoryParentSelfJoinMethod;

[SqlSourceGenerate(Source = "../Cases/category-tree-recursive/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CategoryTreeRecursiveMethod;

[SqlSourceGenerate(Source = "../Cases/contact-fallbacks/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class ContactFallbacksMethod;

[SqlSourceGenerate(Source = "../Cases/contacts-union/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class ContactsUnionMethod;

[SqlSourceGenerate(Source = "../Cases/cte-feeding-update/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CteFeedingUpdateMethod;

[SqlSourceGenerate(Source = "../Cases/customer-json-object/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CustomerJsonObjectMethod;

[SqlSourceGenerate(Source = "../Cases/customer-order-counts/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CustomerOrderCountsMethod;

[SqlSourceGenerate(Source = "../Cases/customers-cross-apply-latest-order/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CustomersCrossApplyLatestOrderMethod;

[SqlSourceGenerate(Source = "../Cases/customers-distinct-from/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CustomersDistinctFromMethod;

[SqlSourceGenerate(Source = "../Cases/customers-exists-flags/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CustomersExistsFlagsMethod;

[SqlSourceGenerate(Source = "../Cases/customers-first-order-value/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CustomersFirstOrderValueMethod;

[SqlSourceGenerate(Source = "../Cases/customers-last-payment-subquery/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CustomersLastPaymentSubqueryMethod;

[SqlSourceGenerate(Source = "../Cases/customers-search-paged/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CustomersSearchPagedMethod;

[SqlSourceGenerate(Source = "../Cases/customers-with-orders-and-carts/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CustomersWithOrdersAndCartsMethod;

[SqlSourceGenerate(Source = "../Cases/delete-cart-items-output/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class DeleteCartItemsOutputMethod;

[SqlSourceGenerate(Source = "../Cases/insert-customer-output/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class InsertCustomerOutputMethod;

[SqlSourceGenerate(Source = "../Cases/insert-invoice-next-value/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class InsertInvoiceNextValueMethod;

[SqlSourceGenerate(Source = "../Cases/invoices-due-this-month/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class InvoicesDueThisMonthMethod;

[SqlSourceGenerate(Source = "../Cases/invoices-full-join-payments/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class InvoicesFullJoinPaymentsMethod;

[SqlSourceGenerate(Source = "../Cases/invoices-outer-apply-latest-payment/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class InvoicesOuterApplyLatestPaymentMethod;

[SqlSourceGenerate(Source = "../Cases/last-orders-view-joined/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class LastOrdersViewJoinedMethod;

[SqlSourceGenerate(Source = "../Cases/latest-order-per-customer/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class LatestOrderPerCustomerMethod;

[SqlSourceGenerate(Source = "../Cases/merge-stock-upsert/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class MergeStockUpsertMethod;

[SqlSourceGenerate(Source = "../Cases/order-gaps-lag-lead/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class OrderGapsLagLeadMethod;

[SqlSourceGenerate(Source = "../Cases/order-note-optimistic-update/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class OrderNoteOptimisticUpdateMethod;

[SqlSourceGenerate(Source = "../Cases/order-skus-string-agg/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class OrderSkusStringAggMethod;

[SqlSourceGenerate(Source = "../Cases/order-status-labels/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class OrderStatusLabelsMethod;

[SqlSourceGenerate(Source = "../Cases/orders-grouping-sets/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class OrdersGroupingSetsMethod;

[SqlSourceGenerate(Source = "../Cases/orders-in-customer-time-zone/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class OrdersInCustomerTimeZoneMethod;

[SqlSourceGenerate(Source = "../Cases/orders-in-statuses/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class OrdersInStatusesMethod;

[SqlSourceGenerate(Source = "../Cases/orders-keyset-page/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class OrdersKeysetPageMethod;

[SqlSourceGenerate(Source = "../Cases/orders-optional-filters/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class OrdersOptionalFiltersMethod;

[SqlSourceGenerate(Source = "../Cases/orders-with-invoice/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class OrdersWithInvoiceMethod;

[SqlSourceGenerate(Source = "../Cases/orders-with-lines-for-json/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class OrdersWithLinesForJsonMethod;

[SqlSourceGenerate(Source = "../Cases/payment-reference-try-convert/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class PaymentReferenceTryConvertMethod;

[SqlSourceGenerate(Source = "../Cases/payments-running-total/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class PaymentsRunningTotalMethod;

[SqlSourceGenerate(Source = "../Cases/price-as-of/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class PriceAsOfMethod;

[SqlSourceGenerate(Source = "../Cases/price-bounds-greatest/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class PriceBoundsGreatestMethod;

[SqlSourceGenerate(Source = "../Cases/price-history-all/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class PriceHistoryAllMethod;

[SqlSourceGenerate(Source = "../Cases/product-attributes-openjson/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class ProductAttributesOpenjsonMethod;

[SqlSourceGenerate(Source = "../Cases/product-color-json-value/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class ProductColorJsonValueMethod;

[SqlSourceGenerate(Source = "../Cases/product-specs-json-column/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class ProductSpecsJsonColumnMethod;

[SqlSourceGenerate(Source = "../Cases/products-like-escape/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class ProductsLikeEscapeMethod;

[SqlSourceGenerate(Source = "../Cases/products-never-ordered/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class ProductsNeverOrderedMethod;

[SqlSourceGenerate(Source = "../Cases/products-ranked-in-category/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class ProductsRankedInCategoryMethod;

[SqlSourceGenerate(Source = "../Cases/sales-by-category-rollup/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class SalesByCategoryRollupMethod;

[SqlSourceGenerate(Source = "../Cases/shipping-label-concat/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class ShippingLabelConcatMethod;

[SqlSourceGenerate(Source = "../Cases/top-with-ties-best-sellers/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class TopWithTiesBestSellersMethod;

[SqlSourceGenerate(Source = "../Cases/update-orders-from-join-output/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class UpdateOrdersFromJoinOutputMethod;
