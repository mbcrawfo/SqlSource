-- name: ListCategoriesWithParent
SELECT
    c.id,
    c.name,
    c.parent_id,
    parent.name AS parent_name,
    COALESCE(parent.name + N' > ', N'') + c.name AS breadcrumb
FROM dbo.categories AS c
LEFT JOIN dbo.categories AS parent ON parent.id = c.parent_id
ORDER BY breadcrumb;
