-- dialect: postgres
-- name: ByA
-- token: {{cond:a = 1}}
SELECT * FROM t WHERE {{cond}};

-- name: ByB
SELECT * FROM t WHERE 1 = 1 {{cond:AND b = 2}};

-- name: ByC
SELECT * FROM t WHERE {{cond}};
