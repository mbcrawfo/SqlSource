-- name: ListPurchasedProductsByAccount
SELECT
    o.account_id,
    array_agg(DISTINCT p.id ORDER BY p.id) AS product_ids,
    string_agg(DISTINCT p.name, ', ' ORDER BY p.name) AS product_names,
    array_agg(DISTINCT c.slug ORDER BY c.slug) FILTER (WHERE c.slug IS NOT NULL) AS category_slugs,
    string_agg(DISTINCT o.coupon, ',') AS coupons,
    sum(l.quantity) AS units
FROM orders o
JOIN order_lines l ON l.order_id = o.id
JOIN products p ON p.id = l.product_id
LEFT JOIN categories c ON c.id = p.category_id
GROUP BY o.account_id
ORDER BY o.account_id;
