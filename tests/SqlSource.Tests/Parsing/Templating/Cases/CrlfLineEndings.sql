-- dialect: postgres
-- name: A
SELECT {{t:users}}

-- name: B
SELECT * FROM t WHERE 1 = 1 {{f:AND a = 1
    AND b = 2}}
    AND id = @id
