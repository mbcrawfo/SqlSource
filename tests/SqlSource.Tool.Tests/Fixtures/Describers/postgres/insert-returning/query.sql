-- name: PlaceOrder
INSERT INTO orders (user_id, total, note) VALUES (@user_id, @total, @note) RETURNING id, placed_at;
