-- The queries of the settings of query methods, claimed by MethodQueries: each result shape, what rowcount counts, a
-- token, and the method's suffix, run on the seed's accounts and notes; and one that waits on a lock before its last
-- row, for a cancellation during the read.  A copy named with Sync after its original is the original under
-- method-type=sync, which gives it a synchronous method in place of the asynchronous one, and GetAccountInRangeBoth is
-- GetAccountInRange under both, which gives it the two.

-- name: TouchAccount
-- summary: Updates an account without changing it, as rowcount by default, since it returns no rows.
UPDATE accounts SET name = name WHERE id = @accountId;

-- name: TouchAccountSync
-- summary: TouchAccount, by a synchronous method.
-- generator: method-type=sync
UPDATE accounts SET name = name WHERE id = @accountId;

-- name: TouchAccountQuietly -> none
-- summary: The same update, with nothing returned.
UPDATE accounts SET name = name WHERE id = @accountId;

-- name: TouchAccountQuietlySync -> none
-- summary: TouchAccountQuietly, by a synchronous method.
-- generator: method-type=sync
UPDATE accounts SET name = name WHERE id = @accountId;

-- name: GetAccountInRange -> one
-- summary: The one account whose id is in a range.
SELECT id, name FROM accounts WHERE id BETWEEN @fromId AND @toId ORDER BY id;

-- name: GetAccountInRangeSync -> one
-- summary: GetAccountInRange, by a synchronous method.
-- generator: method-type=sync
SELECT id, name FROM accounts WHERE id BETWEEN @fromId AND @toId ORDER BY id;

-- name: GetAccountInRangeBoth -> one
-- summary: GetAccountInRange, by a synchronous method and an asynchronous one, which is named with Async after it.
-- generator: method-type=both
SELECT id, name FROM accounts WHERE id BETWEEN @fromId AND @toId ORDER BY id;

-- name: FindAccountInRange -> one-optional
-- summary: The account whose id is in a range, if there is one.
SELECT id, name FROM accounts WHERE id BETWEEN @fromId AND @toId ORDER BY id;

-- name: FindAccountInRangeSync -> one-optional
-- summary: FindAccountInRange, by a synchronous method.
-- generator: method-type=sync
SELECT id, name FROM accounts WHERE id BETWEEN @fromId AND @toId ORDER BY id;

-- name: CountNotesOf -> one
-- summary: The number of notes of an account, by a method named with Async after it.
-- generator: async-method-suffix
SELECT count(*) AS note_count FROM notes WHERE account_id = @accountId;

-- name: ListAccountsWhere
-- summary: The accounts that a filter keeps, whose sample filters by a parameter that another filter may leave out.
-- param: @accountId integer
SELECT id, name FROM accounts WHERE {{filter:id = @accountId}} ORDER BY id;

-- name: ListAccountsWhereSync
-- summary: ListAccountsWhere, by a synchronous method, which takes the token as the asynchronous one does.
-- param: @accountId integer
-- generator: method-type=sync
SELECT id, name FROM accounts WHERE {{filter:id = @accountId}} ORDER BY id;

-- name: AddNote -> rowcount
-- summary: Adds a note to an account, counting the row of its RETURNING and reading none.
INSERT INTO notes (account_id, kind, body, written_at) VALUES (@accountId, 'call', @body, now()) RETURNING id;

-- name: ListAccountsAsRowCount -> rowcount
-- summary: Every account, run as rowcount, which a SELECT counts as -1.
SELECT id, name FROM accounts ORDER BY id;

-- name: RenameInCte -> rowcount
-- summary: An update inside a WITH whose last statement is a SELECT, which counts as -1, as a SELECT does.
WITH u AS (UPDATE accounts SET name = name WHERE id = @accountId RETURNING id) SELECT id FROM u;

-- name: ListThenWait
-- summary: The numbers from one to a count, which waits for a shared advisory lock before the last, so that the server
-- summary: has sent most of the rows before it when the wait starts.
-- A SELECT with no FROM computes its list only for a row its WHERE keeps, so the last row alone takes the lock.
SELECT n FROM generate_series(1, @count) AS n
LEFT JOIN LATERAL (SELECT pg_advisory_xact_lock_shared(@lockKey) WHERE n = @count) AS w ON true;

-- name: ListThenRaise
-- summary: The accounts into an array, and then a division by zero after them, which the method throws.
-- A PostgreSQL query is one statement, so the error after the rows is in its second branch: the UNION ALL sends the
-- rows of the first, and then fails on the first row of the second.  The tests of this and the four below pin that
-- every shape throws it, as the same batches of several statements do on SQL Server.  The server cannot say that id
-- is never NULL through the UNION, so the marker says it, as the model it shares with Collections.sql has it.
-- column: id not null
-- output-model: CollectedAccount
(SELECT id, name FROM accounts ORDER BY id)
UNION ALL
(SELECT id / 0, name FROM accounts WHERE id = 1);

-- name: GetThenRaise -> one
-- summary: The first account, and then a division by zero after it, which the method throws.
-- ListThenRaise under one, whose second read, to see that there is no second row, meets the error.
-- column: id not null
-- output-model: CollectedAccount
(SELECT id, name FROM accounts WHERE id = 1)
UNION ALL
(SELECT id / 0, name FROM accounts WHERE id = 1);

-- name: GetThenRaiseSync -> one
-- summary: GetThenRaise, by a synchronous method.
-- column: id not null
-- output-model: CollectedAccount
-- generator: method-type=sync
(SELECT id, name FROM accounts WHERE id = 1)
UNION ALL
(SELECT id / 0, name FROM accounts WHERE id = 1);

-- name: FindThenRaise -> one-optional
-- summary: The first account, and then a division by zero after it, which the method throws.
-- GetThenRaise under one-optional.
-- column: id not null
-- output-model: CollectedAccount
(SELECT id, name FROM accounts WHERE id = 1)
UNION ALL
(SELECT id / 0, name FROM accounts WHERE id = 1);

-- name: RunThenRaise -> none
-- summary: The accounts, and then a division by zero after them, which a method that reads no rows throws.
(SELECT id, name FROM accounts ORDER BY id)
UNION ALL
(SELECT id / 0, name FROM accounts WHERE id = 1);

-- name: CountThenRaise -> rowcount
-- summary: The accounts, and then a division by zero after them, which a method that reads no rows throws.
-- RunThenRaise under rowcount.
(SELECT id, name FROM accounts ORDER BY id)
UNION ALL
(SELECT id / 0, name FROM accounts WHERE id = 1);
