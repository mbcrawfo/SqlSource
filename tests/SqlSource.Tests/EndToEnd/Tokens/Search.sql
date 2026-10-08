-- Queries with tokens.  The test project turns token validation off, so only Checked checks its arguments.

-- name: Search
-- summary: Finds rows of a table.
SELECT {{columns}}
FROM {{ table }}
WHERE {{filter}}
ORDER BY {{table}}.id;

-- name: Checked
-- generator: token-validation
SELECT id FROM {{table}} WHERE {{filter}};
