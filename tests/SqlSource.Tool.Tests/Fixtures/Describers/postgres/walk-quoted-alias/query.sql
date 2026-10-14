-- name: WalkQuotedAlias
SELECT u.id, "Order".total
FROM users u LEFT JOIN orders "Order" ON "Order".user_id = u.id;
