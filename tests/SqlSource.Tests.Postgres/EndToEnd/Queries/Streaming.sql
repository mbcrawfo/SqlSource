-- The queries of the streaming method, claimed by StreamingQueries: every one is a many query that the header's
-- collection type gives an IAsyncEnumerable, so that the method yields each row as the reader reads it.
-- collection-type: IAsyncEnumerable

-- name: StreamAccounts
-- summary: Every account, streamed.
-- output-model: CollectedAccount
SELECT id, name FROM accounts ORDER BY id;

-- name: StreamAccountsNamedFrom
-- summary: The accounts from an id on, streamed, for a method that takes parameters.
-- output-model: CollectedAccount
SELECT id, name FROM accounts WHERE id >= @firstId ORDER BY id;

-- name: StreamNullAtThird
-- summary: Every account, with a NULL for the name of the third, which the marker says is never NULL.
-- column: name not null
SELECT id, CASE WHEN id = 3 THEN NULL ELSE name END AS name FROM accounts ORDER BY id;

-- name: StreamNumbers
-- summary: The numbers from 1 to a count, for a break and a cancellation partway through many rows.
SELECT n FROM generate_series(1, @count) AS n ORDER BY n;

-- name: StreamFromTable
-- summary: The ids of the rows of a table that the caller names, for a method that takes a token.
SELECT id FROM {{table:accounts}} ORDER BY id;

-- name: StreamNoteAccounts
-- summary: The accounts that have notes, with the id of each as an AccountId, read through its converters.
-- column: account_id -> AccountId
SELECT DISTINCT account_id FROM notes ORDER BY account_id;
