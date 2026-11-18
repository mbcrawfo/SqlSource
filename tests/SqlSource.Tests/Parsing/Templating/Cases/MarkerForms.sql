-- dialect: postgres
-- name: Q
-- token: {{a:x}}
-- just a note
SELECT {{a}} -- summary: inline
    , {{f:a = 1 -- summary: inside
    AND b = 2}}
FROM t;
