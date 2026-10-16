-- name: ListAccountsWithCouponCodes
SELECT a.id, a.name, oc.order_id, oc.coupon_code, oc.channel
FROM accounts a
LEFT JOIN (
    SELECT o.account_id, o.id AS order_id, coalesce(o.coupon, 'none') AS coupon_code, 'web' AS channel
    FROM orders o
) oc ON oc.account_id = a.id
ORDER BY a.id, oc.order_id;
