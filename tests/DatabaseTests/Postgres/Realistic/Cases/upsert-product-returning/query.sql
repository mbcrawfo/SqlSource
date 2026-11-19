-- name: UpsertProduct
-- param: @category_id int null
-- param: @discontinued_on date null
INSERT INTO products (id, category_id, sku, name, price, discontinued_on)
VALUES (@id, @category_id, @sku, @name, @price, @discontinued_on)
ON CONFLICT (sku) DO UPDATE
    SET
        category_id = excluded.category_id,
        name = excluded.name,
        price = excluded.price,
        discontinued_on = excluded.discontinued_on
RETURNING id, category_id, sku, name, price, discontinued_on, (xmax = 0) AS inserted;
