-- dialect: postgres
-- name: ByIds
SELECT * FROM t
WHERE id IN /* c */ @ids
    AND kind NOT IN @kinds
    AND tag IN -- c
        @tags
    AND position('@' IN @email) > 0;
