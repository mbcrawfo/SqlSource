-- The queries of the users, at the default output, repository: each query gets its models, an instance method of
-- UserRepository, which takes its connection from the repository's connection string, and a member of the interface
-- IUserRepository.

-- name: GetUser -> one-optional
-- summary: One user, or null.
-- one-optional: the method returns the row, or null when there is none.  The generator parameter method-type=both
-- gives the query a synchronous method, GetUser, for code that does not await, and its asynchronous method beside it,
-- GetUserAsync, each a member of IUserRepository too.  The synchronous one takes no cancellation token and gets its
-- connection from the same connection string, with the driver's synchronous calls.
-- generator: method-type=both
SELECT id, name, email
FROM users
WHERE id = @userId;

-- name: ListUserNames
-- summary: The name of every user, in the order of their ids.
-- A query that returns rows is many by default, and its method returns an array.
SELECT id, name
FROM users
ORDER BY id;

-- name: RenameUser -> rowcount
-- summary: Changes the name of one user.
-- rowcount: the method returns the count of rows updated.
UPDATE users
SET name = @name
WHERE id = @userId;
