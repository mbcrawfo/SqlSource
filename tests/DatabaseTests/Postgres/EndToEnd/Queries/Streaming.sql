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

-- name: StreamNumbersFailingHalfway
-- summary: The numbers from 1 to a million, each with 10 divided by its distance from 500,000, which fails at that row.
-- The division is by zero at the row of 500,000, which the server sends after the rows before it, so a loop that stops
-- before that row leaves it to the rows still to come.  The query sorts a million numbers, as StreamNumbers does, so
-- the server is still sending when the loop stops.  The numbers are sorted in a subquery, and the division is above
-- it: PostgreSQL computes the columns of a query before its own sort, so a division beside the ORDER BY would fail
-- before the first row is sent.
SELECT n, 10 / (n - 500000) AS quotient
FROM (SELECT n FROM generate_series(1, 1000000) AS n ORDER BY n) AS numbers
ORDER BY n;
