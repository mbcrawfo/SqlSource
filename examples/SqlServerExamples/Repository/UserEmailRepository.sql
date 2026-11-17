-- A second repository of the default output, beside UserRepository, for a query that code which does not await calls.

-- name: GetUserEmail -> one-optional
-- summary: The email address of one user, or null when there is none.
-- The generator parameter method-type=both gives the query a synchronous method, GetUserEmail, for code that does not
-- await: it takes no cancellation token and gets its connection from the same connection string, with the driver's
-- synchronous calls.  Beside it is its asynchronous method, which method-type=both names GetUserEmailAsync, so that the
-- two never share a name.  Both are members of the interface IUserEmailRepository.
-- generator: method-type=both
SELECT email
FROM users
WHERE id = @userId;
