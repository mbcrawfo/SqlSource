-- The queries of the users, at the default output, codegen, which StoreQueries calls.  The type override gives the
-- alias type dbo.Email of the file the type EmailAddress, whose converters the methods call to read and send an email.
-- type-override: dbo.Email=EmailAddress

-- name: FindUserByEmail -> one-optional
-- summary: The user with an email address, or null.
-- one-optional: the method returns the row, or null when there is none.  SQL Server gives a parameter compared with a
-- column of an alias type that alias type, so @email is an EmailAddress with no marker.
SELECT id, name, email
FROM users
WHERE email = @email;

-- name: ChangeUserEmail -> rowcount
-- summary: Changes the email address of one user.
-- rowcount: the method returns the count of rows updated.
UPDATE users
SET email = @email
WHERE id = @userId;

-- name: ListUsersNewestFirst
-- summary: Every user, the newest first.
-- A query that returns rows is many by default, and the marker gives its rows the collection type IReadOnlyList.
-- collection-type: IReadOnlyList
SELECT id, name, email
FROM users
ORDER BY created_at DESC, id;

-- name: StreamUsersOldestFirst
-- summary: Every user, the oldest first, each read as the caller's loop asks for it.
-- The marker gives its rows the collection type IAsyncEnumerable, so its method streams them: it holds no collection,
-- and the connection stays open until the loop ends.
-- collection-type: IAsyncEnumerable
SELECT id, name, email
FROM users
ORDER BY created_at, id;

-- name: ListUsersSortedBy
-- summary: Every user, in the order the caller chooses.
-- The token orderBy is text that the caller's code chooses, never a user's; its default is the sample the tool
-- describes the query with.
SELECT id, name
FROM users
ORDER BY {{orderBy:name}};
