-- name: ImportProducts
INSERT INTO products (id, sku, name, price, category_id)
SELECT u.id, u.sku, u.name, u.price, c.id
FROM unnest(@ids::int[], @skus::text[], @names::text[], @prices::numeric[], @category_slugs::text[])
    AS u (id, sku, name, price, category_slug)
LEFT JOIN categories c ON c.slug = u.category_slug
RETURNING id, sku, name, price, category_id, discontinued_on;
