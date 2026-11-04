-- The query of the methods' locations, claimed by a class of LocationQueries.cs for each, and its copy of the suffix,
-- ListAccountNamesWithSuffix.

-- name: ListAccountNames
-- summary: The id and name of every account, through a method where the class that claims it puts it.
SELECT id, name
FROM dbo.accounts
ORDER BY id;

-- name: ListAccountNamesWithSuffix
-- summary: ListAccountNames again, with the Async suffix on its method.
-- generator: async-method-suffix
-- output-model: ListAccountNamesDto
SELECT id, name
FROM dbo.accounts
ORDER BY id;
