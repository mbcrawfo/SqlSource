-- name: MergeProductCatalog
MERGE INTO products p
USING unnest(@ids::int[], @skus::text[], @names::text[], @prices::numeric[]) AS s (id, sku, name, price)
ON s.sku = p.sku
WHEN MATCHED AND p.discontinued_on IS NOT NULL THEN
    DO NOTHING
WHEN MATCHED THEN
    UPDATE SET name = s.name, price = s.price
WHEN NOT MATCHED THEN
    INSERT (id, sku, name, price) VALUES (s.id, s.sku, s.name, s.price)
RETURNING merge_action() AS action, p.id, p.sku, p.name, p.price, p.category_id, p.discontinued_on;
