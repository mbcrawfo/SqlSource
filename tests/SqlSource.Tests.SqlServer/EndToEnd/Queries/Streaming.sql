-- The queries of the streaming method, claimed by StreamingQueries: every one is a many query that the header's
-- collection type gives an IAsyncEnumerable, so that the method yields each row as the reader reads it.
-- collection-type: IAsyncEnumerable

-- name: StreamAccounts
-- summary: Every account, streamed.
-- output-model: CollectedAccount
SELECT id, name
FROM dbo.accounts
ORDER BY id;

-- name: StreamAccountsNamedFrom
-- summary: The accounts from an id on, streamed, for a method that takes parameters.
-- output-model: CollectedAccount
SELECT id, name
FROM dbo.accounts
WHERE id >= @firstId
ORDER BY id;

-- name: StreamNullAtThird
-- summary: Every account, with a NULL for the name of the third, which the marker says is never NULL.
-- column: name not null
SELECT id, CASE WHEN id = 3 THEN NULL ELSE name END AS name
FROM dbo.accounts
ORDER BY id;

-- name: StreamNumbers
-- summary: The numbers from 1 to a count, for a break and a cancellation partway through many rows.
-- The cross join of six digits is a million numbers, which SQL Server must sort before it sends the first, so that it
-- is still sending when a loop stops partway and a stream that cancelled its command would show.  It runs on every
-- supported version, where GENERATE_SERIES needs 2022.
WITH digits (v) AS (SELECT v FROM (VALUES (0), (1), (2), (3), (4), (5), (6), (7), (8), (9)) AS d (v))
SELECT n
FROM (
    SELECT d1.v + 10 * d2.v + 100 * d3.v + 1000 * d4.v + 10000 * d5.v + 100000 * d6.v + 1 AS n
    FROM digits AS d1
    CROSS JOIN digits AS d2
    CROSS JOIN digits AS d3
    CROSS JOIN digits AS d4
    CROSS JOIN digits AS d5
    CROSS JOIN digits AS d6
) AS numbers
WHERE n <= @count
ORDER BY n;

-- name: StreamFromTable
-- summary: The ids of the rows of a table that the caller names, for a method that takes a token.
SELECT id
FROM {{table:dbo.accounts}}
ORDER BY id;

-- name: StreamThenSelect
-- summary: The accounts, and then a second result set that the stream does not read.
-- output-model: CollectedAccount
SELECT id, name
FROM dbo.accounts
ORDER BY id;
SELECT 1;

-- name: StreamThenRaise
-- summary: The accounts, a mark on the session, and then an error that the method does not report.
-- output-model: CollectedAccount
-- SET CONTEXT_INFO keeps its value for the session, which a test reads from the same connection: the mark is there when
-- the batch ran as far as the THROW.
SELECT id, name
FROM dbo.accounts
ORDER BY id;
SET CONTEXT_INFO 0x02;
THROW 50000, 'after the rows', 1;

-- name: StreamNumbersThenMark
-- summary: The numbers from 1 to a count, and then a statement that marks the session once the numbers are read.
-- This is StreamNumbers with the mark after it, and the two are meant to match.
-- SET CONTEXT_INFO keeps its value for the session, which a test reads from the same connection: the mark is there only
-- when the batch ran to its end, and a command that was cancelled partway through the numbers never reaches it.
WITH digits (v) AS (SELECT v FROM (VALUES (0), (1), (2), (3), (4), (5), (6), (7), (8), (9)) AS d (v))
SELECT n
FROM (
    SELECT d1.v + 10 * d2.v + 100 * d3.v + 1000 * d4.v + 10000 * d5.v + 100000 * d6.v + 1 AS n
    FROM digits AS d1
    CROSS JOIN digits AS d2
    CROSS JOIN digits AS d3
    CROSS JOIN digits AS d4
    CROSS JOIN digits AS d5
    CROSS JOIN digits AS d6
) AS numbers
WHERE n <= @count
ORDER BY n;
SET CONTEXT_INFO 0x01;

-- name: StreamNumbersFailingHalfway
-- summary: The numbers from 1 to a million, each with 10 divided by its distance from 500,000, which fails at that row.
-- The division is by zero at the row of 500,000, which the server sends after the rows before it, so a loop that stops
-- before that row leaves it to the rows still to come.  It is StreamNumbers' cross join of six digits, which SQL Server
-- must sort before it sends the first number, so the server is still sending when the loop stops, and it runs on every
-- supported version.  The division is by the number's place in that order, which is the number itself: SQL Server
-- would compute a division by the number before its sort, and fail before the first row is sent, while a place in the
-- order is known only after it.
WITH digits (v) AS (SELECT v FROM (VALUES (0), (1), (2), (3), (4), (5), (6), (7), (8), (9)) AS d (v))
SELECT n, 10 / (CAST(ROW_NUMBER() OVER (ORDER BY n) AS int) - 500000) AS quotient
FROM (
    SELECT d1.v + 10 * d2.v + 100 * d3.v + 1000 * d4.v + 10000 * d5.v + 100000 * d6.v + 1 AS n
    FROM digits AS d1
    CROSS JOIN digits AS d2
    CROSS JOIN digits AS d3
    CROSS JOIN digits AS d4
    CROSS JOIN digits AS d5
    CROSS JOIN digits AS d6
) AS numbers
ORDER BY n;
