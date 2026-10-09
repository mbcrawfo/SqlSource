-- Queries with tokens.  The test project turns token validation off, so only Checked, whose own list is the default,
-- checks its arguments.

-- name: Search -> many
-- summary: Finds rows of a table.
-- param: @pattern text not null
-- token: {{filter:name LIKE @pattern}}
SELECT {{columns}}
FROM {{ table:users }}
WHERE {{filter}}
ORDER BY {{table}}.id;

-- name: Checked
-- generator: default
SELECT id FROM {{table}} WHERE {{filter}};
