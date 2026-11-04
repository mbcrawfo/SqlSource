-- The query of the methods' locations, claimed by LocationQueries, and its copies, one for each other place, which each
-- set theirs by its markers and share the query's models.  GetLocatedName has the default, an extension method of the
-- connection in LocationQueriesExtensions.

-- name: GetLocatedName -> one
-- summary: The name of an account, by its id.
SELECT id, name FROM accounts WHERE id = @accountId;

-- name: GetLocatedNameInternal -> one
-- summary: GetLocatedName again, as an internal static method of the type, which any code of the project calls.
-- member-location: class
-- visibility: internal
-- input-model: GetLocatedNameArgs
-- output-model: GetLocatedNameDto
SELECT id, name FROM accounts WHERE id = @accountId;

-- name: GetLocatedNamePrivate -> one
-- summary: GetLocatedName again, as a private static method of the type, which only the type's own code calls.
-- member-location: class
-- visibility: private
-- input-model: GetLocatedNameArgs
-- output-model: GetLocatedNameDto
SELECT id, name FROM accounts WHERE id = @accountId;

-- name: GetLocatedNameNested -> one
-- summary: GetLocatedName again, as a static method of the type's nested class Sql, which is internal.
-- member-location: nested-class
-- visibility: internal
-- input-model: GetLocatedNameArgs
-- output-model: GetLocatedNameDto
SELECT id, name FROM accounts WHERE id = @accountId;
