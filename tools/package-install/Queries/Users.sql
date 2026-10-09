-- name: GetUser
SELECT id, name FROM users /* by key */ WHERE id = @id;

-- name: ListUsers -> many
-- param: @since timestamptz not null
-- token: {{whereClause:WHERE created_at > @since}}
SELECT id, name FROM {{table:users}} {{whereClause}};

-- name: ListChecked
-- generator: default
SELECT id, name FROM {{table}} {{whereClause}};
