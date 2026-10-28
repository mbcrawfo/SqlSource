-- name: ListCategoryTree
WITH tree AS (
    SELECT c.id, c.parent_id, c.name, CAST(c.name AS nvarchar(400)) AS path, 1 AS depth
    FROM dbo.categories AS c
    WHERE c.parent_id IS NULL
    UNION ALL
    SELECT c.id, c.parent_id, c.name, CAST(t.path + N' > ' + c.name AS nvarchar(400)), t.depth + 1
    FROM dbo.categories AS c
    JOIN tree AS t ON t.id = c.parent_id
)
SELECT t.id, t.parent_id, t.name, t.path, t.depth, COUNT(p.id) AS product_count
FROM tree AS t
LEFT JOIN dbo.products AS p ON p.category_id = t.id
GROUP BY t.id, t.parent_id, t.name, t.path, t.depth
ORDER BY t.path;
