-- name: ListCategoryFilterOptions
SELECT 0 AS id, 'Any category' AS name, NULL AS slug, 1 AS sort_group
UNION ALL
SELECT c.id, c.name, c.slug, 2
FROM categories c
WHERE EXISTS (
    SELECT 1
    FROM products p
    WHERE p.category_id = c.id AND p.discontinued_on IS NULL
)
ORDER BY sort_group, name;
