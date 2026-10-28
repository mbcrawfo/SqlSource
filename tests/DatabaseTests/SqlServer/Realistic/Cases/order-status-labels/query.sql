-- name: ListOrderStatusLabels
SELECT
    o.id,
    o.placed_at,
    o.status,
    CASE o.status
        WHEN 'pending' THEN N'Awaiting payment'
        WHEN 'shipped' THEN N'On its way'
        WHEN 'delivered' THEN N'Delivered'
        WHEN 'cancelled' THEN N'Cancelled'
        ELSE N'Unknown'
    END AS status_label,
    CASE o.status
        WHEN 'pending' THEN N'Can be changed'
        WHEN 'shipped' THEN N'Can be tracked'
    END AS action_hint,
    CASE WHEN o.shipped_at IS NOT NULL THEN DATEDIFF(day, o.placed_at, o.shipped_at) END AS days_to_ship,
    CASE WHEN o.ship_city IS NULL THEN N'(none)' ELSE o.ship_city END AS ship_to,
    CAST(CASE WHEN o.status IN ('pending', 'shipped') THEN 1 ELSE 0 END AS bit) AS is_open
FROM dbo.orders AS o
ORDER BY o.placed_at DESC, o.id DESC;
