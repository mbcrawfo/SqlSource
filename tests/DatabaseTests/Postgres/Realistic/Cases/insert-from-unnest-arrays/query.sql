-- name: ImportProducts
INSERT INTO products (id, sku, name, price, category_id)
SELECT
    u.id,
    u.sku,
    u.name,
    u.price,
    -- The insert's two id columns, of the product and of its category, are what this case describes.
    c.id  -- noqa: AL08
FROM
    unnest(@ids::int[], @skus::text[], @names::text[], @prices::numeric[], @category_slugs::text[])
        AS u (id, sku, name, price, category_slug)
LEFT JOIN categories c ON c.slug = u.category_slug
RETURNING id, sku, name, price, category_id, discontinued_on;
