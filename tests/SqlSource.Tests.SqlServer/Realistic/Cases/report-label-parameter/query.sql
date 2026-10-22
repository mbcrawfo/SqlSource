-- name: SummarizeOrdersSince
-- param: @since datetime2(0)
SELECT
    @report_label AS report_label,
    COUNT(*) AS order_count,
    MIN(o.placed_at) AS first_placed_at,
    MAX(o.placed_at) AS last_placed_at
FROM dbo.orders AS o
WHERE o.placed_at >= @since;
