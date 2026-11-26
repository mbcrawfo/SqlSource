-- name: ListReadingsAtLevels
-- param: @levels _reading_level[]
SELECT r.id
FROM readings AS r
WHERE @levels IS NOT NULL;
