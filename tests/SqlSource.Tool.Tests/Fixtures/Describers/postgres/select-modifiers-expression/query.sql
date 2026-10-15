-- name: ModifierExpressions
SELECT
    '1 day'::interval day to second(3) AS span,
    CASE m.names[1] WHEN '' THEN m.names[2] ELSE m.names[1] END AS first_name
FROM modifiers AS m;
