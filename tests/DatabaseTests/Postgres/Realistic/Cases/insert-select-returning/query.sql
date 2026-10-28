-- name: CreateRenewalOrders
INSERT INTO orders (account_id, placed_at, note, coupon)
SELECT a.id, @placed_at, @note, CASE WHEN 'vip' = ANY (a.tags) THEN 'VIP10' END
FROM accounts a
WHERE a.status = 'active'
RETURNING id, account_id, placed_at, shipped_at, note, coupon;
