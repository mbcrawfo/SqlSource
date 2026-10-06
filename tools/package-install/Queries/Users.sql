-- name: GetUser
SELECT id, name FROM users WHERE id = @id;

-- name: ListUsers
SELECT id, name FROM {{table}} {{whereClause}};

-- name: ListChecked
-- SqlSource: token-validation
SELECT id, name FROM {{table}} {{whereClause}};
