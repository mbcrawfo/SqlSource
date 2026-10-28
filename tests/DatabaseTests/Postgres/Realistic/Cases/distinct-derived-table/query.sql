-- name: ListCategoriesBoughtPerAccount
SELECT x.account_id, x.name, x.category_id, c.name AS category
FROM (
    SELECT DISTINCT a.id AS account_id, a.name, p.category_id
    FROM accounts a
    LEFT JOIN orders o ON o.account_id = a.id
    LEFT JOIN order_lines l ON l.order_id = o.id
    LEFT JOIN products p ON p.id = l.product_id
) x
LEFT JOIN categories c ON c.id = x.category_id
ORDER BY x.name, category;
