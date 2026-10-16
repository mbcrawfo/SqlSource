-- name: ListCategoriesWithAncestors
SELECT
    c.id,
    c.name,
    c.slug,
    parent.id AS parent_id,
    parent.name AS parent_name,
    grandparent.name AS grandparent_name,
    count(p.id) AS product_count
FROM categories c
LEFT JOIN categories parent ON parent.id = c.parent_id
LEFT JOIN categories grandparent ON grandparent.id = parent.parent_id
LEFT JOIN products p ON p.category_id = c.id
GROUP BY c.id, c.name, c.slug, parent.id, parent.name, grandparent.name
ORDER BY c.id;
