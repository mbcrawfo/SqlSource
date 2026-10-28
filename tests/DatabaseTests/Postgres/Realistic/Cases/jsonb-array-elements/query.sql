-- name: ListPurchasedItems
SELECT
    e.id AS event_id,
    e.account_id,
    e.at,
    li.item ->> 'sku' AS sku,
    (li.item ->> 'qty')::integer AS quantity,
    p.name AS product_name,
    p.price
FROM events e
CROSS JOIN LATERAL jsonb_array_elements(e.payload -> 'items') AS li(item)
LEFT JOIN products p ON p.sku = li.item ->> 'sku'
WHERE e.kind = 'purchase' AND e.payload ? 'items'
ORDER BY e.at, sku;
