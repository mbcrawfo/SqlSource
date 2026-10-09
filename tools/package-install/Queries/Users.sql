-- name: GetUser
SELECT id, name FROM users /* by key */ WHERE id = @id;

-- name: ListUsers
SELECT id, name FROM {{table}} {{whereClause}};

-- name: ListChecked
-- generator: default
SELECT id, name FROM {{table}} {{whereClause}};
