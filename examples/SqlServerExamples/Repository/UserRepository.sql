-- The queries of the users, at the default output, repository: each query gets its models, an instance method of
-- UserRepository, which takes its connection from the repository's connection string, and a member of the interface
-- IUserRepository.  They are the running example of the manual's Get started pages, in their words.

-- name: GetUser -> one-optional
-- summary: One user, or null.
-- one-optional: the method returns the row, or null when there is none.
SELECT
    id,
    name,
    email
FROM users
WHERE id = @userId;

-- name: ListUserNames
-- summary: The name of every user, in the order of their ids.
-- A query that returns rows is many by default, and its method returns an array.
SELECT
    id,
    name
FROM users
ORDER BY id;

-- name: RenameUser -> rowcount
-- summary: Changes the name of one user.
-- rowcount: the method returns the count of rows updated.
UPDATE users
SET name = @name
WHERE id = @userId;

-- name: FindUsers
-- summary: Finds the users with a name, or every user when the name is null.
-- The param marker gives @name its type and says that it can be null, so FindUsersParams.Name is a string?.
-- param: @name nvarchar(100) null
SELECT
    id,
    name,
    email
FROM users
WHERE @name IS NULL OR name = @name
ORDER BY name;

-- name: ListUsersByIds
-- summary: The users whose ids are in the list.
-- A parameter directly after IN is a list, so ListUsersByIdsParams.Ids is an IEnumerable<int>.
SELECT
    id,
    name
FROM users
WHERE id IN @ids
ORDER BY id;

-- name: ListDisplayNames
-- summary: The name of every user, in upper case.
-- The column marker says that display_name, the result of a function, is never null, so it is a string.
-- column: display_name not null
SELECT
    id,
    UPPER(name) AS display_name
FROM users
ORDER BY id;
