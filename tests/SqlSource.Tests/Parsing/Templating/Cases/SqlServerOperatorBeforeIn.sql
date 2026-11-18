-- dialect: mssql
-- name: ByOperators
SELECT * FROM t
WHERE x =#IN @a
    OR x =@@IN @b
    OR x <>#in @c
    OR x `@@IN @d
    OR y IN @e;
