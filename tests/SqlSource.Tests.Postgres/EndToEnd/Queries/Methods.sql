-- The queries of the settings of query methods, claimed by MethodQueries: each result shape, what rowcount counts, a
-- token, and the method's suffix.  Each is run on the seed's accounts and notes.

-- name: TouchAccount
-- summary: Updates an account without changing it, as rowcount by default, since it returns no rows.
UPDATE accounts SET name = name WHERE id = @accountId;

-- name: TouchAccountQuietly -> none
-- summary: The same update, with nothing returned.
UPDATE accounts SET name = name WHERE id = @accountId;

-- name: GetAccountInRange -> one
-- summary: The one account whose id is in a range.
SELECT id, name FROM accounts WHERE id BETWEEN @fromId AND @toId ORDER BY id;

-- name: FindAccountInRange -> one-optional
-- summary: The account whose id is in a range, if there is one.
SELECT id, name FROM accounts WHERE id BETWEEN @fromId AND @toId ORDER BY id;

-- name: CountNotesOf -> one
-- summary: The number of notes of an account, by a method named with Async after it.
-- generator: async-method-suffix
SELECT count(*) AS note_count FROM notes WHERE account_id = @accountId;

-- name: ListAccountsWhere
-- summary: The accounts that a filter keeps, whose sample filters by a parameter that another filter may leave out.
-- param: @accountId integer
SELECT id, name FROM accounts WHERE {{filter:id = @accountId}} ORDER BY id;

-- name: AddNote -> rowcount
-- summary: Adds a note to an account, counting the row of its RETURNING and reading none.
INSERT INTO notes (account_id, kind, body, written_at) VALUES (@accountId, 'call', @body, now()) RETURNING id;

-- name: ListAccountsAsRowCount -> rowcount
-- summary: Every account, run as rowcount, which a SELECT counts as -1.
SELECT id, name FROM accounts ORDER BY id;

-- name: RenameInCte -> rowcount
-- summary: An update inside a WITH whose last statement is a SELECT, which counts as one.
WITH u AS (UPDATE accounts SET name = name WHERE id = @accountId RETURNING id) SELECT id FROM u;
