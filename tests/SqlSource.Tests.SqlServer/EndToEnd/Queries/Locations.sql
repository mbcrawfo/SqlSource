-- The query of the methods' locations and of the suffix, claimed by a class of LocationQueries.cs for each.

-- name: ListAccountNames
-- summary: The id and name of every account, through a method where the class that claims it puts it.
SELECT id, name
FROM dbo.accounts
ORDER BY id;
