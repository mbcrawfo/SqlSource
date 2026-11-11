-- The queries of the users, at the folder's output, methods, which UserQueries calls.  The type override gives the
-- domain email of the file the type EmailAddress, whose converters the methods call to read and send an email.
-- type-override: public.email=EmailAddress

-- name: FindUserByEmail -> one-optional
-- summary: The user with an email address, or null.
-- one-optional: the method returns the row, or null when there is none.  PostgreSQL gives a parameter compared with a
-- domain column the domain's base type, text, so the marker declares @email as the domain, which the type override
-- reaches.
-- param: @email public.email
SELECT id, name, email
FROM users
WHERE email = @email;

-- name: ChangeUserEmail -> rowcount
-- summary: Changes the email address of one user.
-- rowcount: the method returns the count of rows updated.  A parameter written to a domain column has the domain's
-- type, so @email is an EmailAddress with no marker.
UPDATE users
SET email = @email
WHERE id = @userId;

-- name: AddUser -> one
-- summary: Adds a user and returns their id.
-- one: the method returns the new user's id.  The generator parameter collection-method also gives the query a
-- collection method, an overload of AddUser that takes a collection of AddUserParams, adds every user in one
-- transaction of its own and returns their ids, in the order of the items.
-- generator: collection-method
INSERT INTO users (name, email)
VALUES (@name, @email)
RETURNING id;

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
