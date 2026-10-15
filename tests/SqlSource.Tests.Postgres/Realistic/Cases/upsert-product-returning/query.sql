-- name: UpsertProduct
-- param: @category_id int null
-- param: @discontinued_on date null
INSERT INTO products (id, category_id, sku, name, price, discontinued_on)
VALUES (@id, @category_id, @sku, @name, @price, @discontinued_on)
ON CONFLICT (sku) DO UPDATE
SET category_id = EXCLUDED.category_id,
    name = EXCLUDED.name,
    price = EXCLUDED.price,
    discontinued_on = EXCLUDED.discontinued_on
RETURNING id, category_id, sku, name, price, discontinued_on, (xmax = 0) AS inserted;
