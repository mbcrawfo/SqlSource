-- The query of the methods' locations, claimed by LocationQueries, and its copies, one for each other place, which each
-- set theirs by its markers and share the query's model, and its copy of the suffix, ListAccountNamesWithSuffix.
-- ListAccountNames and ListAccountNamesWithSuffix have the default, an extension method of the connection in
-- LocationQueriesExtensions.

-- name: ListAccountNames
-- summary: The id and name of every account, through an extension method of the connection.
SELECT id, name
FROM dbo.accounts
ORDER BY id;

-- name: ListAccountNamesPublic
-- summary: ListAccountNames again, as a public static method of the type.
-- member-location: class
-- visibility: public
-- output-model: ListAccountNamesDto
SELECT id, name
FROM dbo.accounts
ORDER BY id;

-- name: ListAccountNamesInternal
-- summary: ListAccountNames again, as an internal static method of the type.
-- member-location: class
-- visibility: internal
-- output-model: ListAccountNamesDto
SELECT id, name
FROM dbo.accounts
ORDER BY id;

-- name: ListAccountNamesPrivate
-- summary: ListAccountNames again, as a private static method of the type, which only the type's own code calls.
-- member-location: class
-- visibility: private
-- output-model: ListAccountNamesDto
SELECT id, name
FROM dbo.accounts
ORDER BY id;

-- name: ListAccountNamesNested
-- summary: ListAccountNames again, as a static method of the type's nested class Sql, which is internal.
-- member-location: nested-class
-- visibility: internal
-- output-model: ListAccountNamesDto
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
