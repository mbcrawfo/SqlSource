-- dialect: postgres
-- name: Filtered
-- token: {{f:a /* note */ = 1}}
SELECT * FROM t WHERE {{f}};
