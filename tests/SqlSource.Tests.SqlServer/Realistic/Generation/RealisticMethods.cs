namespace SqlSource.Tests.SqlServer.Realistic.Generation;

// Each realistic case with models is claimed a second time here, at CodeGen, so that a generated method runs its
// query; RealisticQueries.cs claims it at Models.  The classes are in the same namespace as that file's, so the two
// claims share the models.  A CodeGen query has no constant, so these classes set no SqlLocation.
// RealisticClaimsTests fails on a case with models that either file lacks, or a class of a case without them.

[SqlSourceGenerate(Path = "../Cases/activity-feed-union-all/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class ActivityFeedUnionAllMethod;

[SqlSourceGenerate(Path = "../Cases/big-customers-having/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class BigCustomersHavingMethod;

[SqlSourceGenerate(Path = "../Cases/cart-by-id/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CartByIdMethod;

[SqlSourceGenerate(Path = "../Cases/categories-right-join-products/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CategoriesRightJoinProductsMethod;

[SqlSourceGenerate(Path = "../Cases/category-parent-self-join/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CategoryParentSelfJoinMethod;

[SqlSourceGenerate(Path = "../Cases/category-tree-recursive/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CategoryTreeRecursiveMethod;

[SqlSourceGenerate(Path = "../Cases/contact-fallbacks/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class ContactFallbacksMethod;

[SqlSourceGenerate(Path = "../Cases/contacts-union/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class ContactsUnionMethod;

[SqlSourceGenerate(Path = "../Cases/cte-feeding-update/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CteFeedingUpdateMethod;

[SqlSourceGenerate(Path = "../Cases/customer-json-object/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CustomerJsonObjectMethod;

[SqlSourceGenerate(Path = "../Cases/customer-order-counts/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CustomerOrderCountsMethod;

[SqlSourceGenerate(Path = "../Cases/customers-cross-apply-latest-order/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CustomersCrossApplyLatestOrderMethod;

[SqlSourceGenerate(Path = "../Cases/customers-distinct-from/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CustomersDistinctFromMethod;

[SqlSourceGenerate(Path = "../Cases/customers-exists-flags/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CustomersExistsFlagsMethod;

[SqlSourceGenerate(Path = "../Cases/customers-first-order-value/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CustomersFirstOrderValueMethod;

[SqlSourceGenerate(Path = "../Cases/customers-last-payment-subquery/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CustomersLastPaymentSubqueryMethod;

[SqlSourceGenerate(Path = "../Cases/customers-search-paged/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CustomersSearchPagedMethod;

[SqlSourceGenerate(Path = "../Cases/customers-with-orders-and-carts/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class CustomersWithOrdersAndCartsMethod;

[SqlSourceGenerate(Path = "../Cases/delete-cart-items-output/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class DeleteCartItemsOutputMethod;

[SqlSourceGenerate(Path = "../Cases/insert-customer-output/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class InsertCustomerOutputMethod;

[SqlSourceGenerate(Path = "../Cases/insert-invoice-next-value/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class InsertInvoiceNextValueMethod;

[SqlSourceGenerate(Path = "../Cases/invoices-due-this-month/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class InvoicesDueThisMonthMethod;

[SqlSourceGenerate(Path = "../Cases/invoices-full-join-payments/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class InvoicesFullJoinPaymentsMethod;

[SqlSourceGenerate(Path = "../Cases/invoices-outer-apply-latest-payment/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class InvoicesOuterApplyLatestPaymentMethod;

[SqlSourceGenerate(Path = "../Cases/last-orders-view-joined/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class LastOrdersViewJoinedMethod;

[SqlSourceGenerate(Path = "../Cases/latest-order-per-customer/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class LatestOrderPerCustomerMethod;

[SqlSourceGenerate(Path = "../Cases/merge-stock-upsert/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class MergeStockUpsertMethod;

[SqlSourceGenerate(Path = "../Cases/order-gaps-lag-lead/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class OrderGapsLagLeadMethod;

[SqlSourceGenerate(Path = "../Cases/order-note-optimistic-update/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class OrderNoteOptimisticUpdateMethod;

[SqlSourceGenerate(Path = "../Cases/order-skus-string-agg/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class OrderSkusStringAggMethod;

[SqlSourceGenerate(Path = "../Cases/order-status-labels/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class OrderStatusLabelsMethod;

[SqlSourceGenerate(Path = "../Cases/orders-grouping-sets/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class OrdersGroupingSetsMethod;

[SqlSourceGenerate(Path = "../Cases/orders-in-customer-time-zone/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class OrdersInCustomerTimeZoneMethod;

[SqlSourceGenerate(Path = "../Cases/orders-in-statuses/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class OrdersInStatusesMethod;

[SqlSourceGenerate(Path = "../Cases/orders-keyset-page/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class OrdersKeysetPageMethod;

[SqlSourceGenerate(Path = "../Cases/orders-optional-filters/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class OrdersOptionalFiltersMethod;

[SqlSourceGenerate(Path = "../Cases/orders-with-invoice/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class OrdersWithInvoiceMethod;

[SqlSourceGenerate(Path = "../Cases/orders-with-lines-for-json/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class OrdersWithLinesForJsonMethod;

[SqlSourceGenerate(Path = "../Cases/payment-reference-try-convert/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class PaymentReferenceTryConvertMethod;

[SqlSourceGenerate(Path = "../Cases/payments-running-total/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class PaymentsRunningTotalMethod;

[SqlSourceGenerate(Path = "../Cases/price-as-of/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class PriceAsOfMethod;

[SqlSourceGenerate(Path = "../Cases/price-bounds-greatest/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class PriceBoundsGreatestMethod;

[SqlSourceGenerate(Path = "../Cases/price-history-all/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class PriceHistoryAllMethod;

[SqlSourceGenerate(Path = "../Cases/product-attributes-openjson/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class ProductAttributesOpenjsonMethod;

[SqlSourceGenerate(Path = "../Cases/product-color-json-value/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class ProductColorJsonValueMethod;

[SqlSourceGenerate(Path = "../Cases/product-specs-json-column/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class ProductSpecsJsonColumnMethod;

[SqlSourceGenerate(Path = "../Cases/products-like-escape/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class ProductsLikeEscapeMethod;

[SqlSourceGenerate(Path = "../Cases/products-never-ordered/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class ProductsNeverOrderedMethod;

[SqlSourceGenerate(Path = "../Cases/products-ranked-in-category/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class ProductsRankedInCategoryMethod;

[SqlSourceGenerate(Path = "../Cases/sales-by-category-rollup/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class SalesByCategoryRollupMethod;

[SqlSourceGenerate(Path = "../Cases/shipping-label-concat/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class ShippingLabelConcatMethod;

[SqlSourceGenerate(Path = "../Cases/top-with-ties-best-sellers/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class TopWithTiesBestSellersMethod;

[SqlSourceGenerate(Path = "../Cases/update-orders-from-join-output/query.sql", Output = GeneratorOutput.CodeGen)]
internal static partial class UpdateOrdersFromJoinOutputMethod;
