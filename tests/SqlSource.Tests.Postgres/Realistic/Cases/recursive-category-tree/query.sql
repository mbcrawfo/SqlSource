-- name: ListCategoryTree
WITH RECURSIVE tree AS (
    SELECT c.id, c.parent_id, c.name, c.name AS path, 1 AS depth
    FROM categories c
    WHERE c.parent_id IS NULL
    UNION ALL
    SELECT c.id, c.parent_id, c.name, t.path || ' / ' || c.name, t.depth + 1
    FROM categories c
    JOIN tree t ON c.parent_id = t.id
)
SELECT t.id, t.parent_id, t.name, t.path, t.depth, count(p.id) AS product_count
FROM tree t
LEFT JOIN products p ON p.category_id = t.id
GROUP BY t.id, t.parent_id, t.name, t.path, t.depth
ORDER BY t.path;
