-- The queries of the row buffer, claimed by RowBufferQueries: the numbers from 1 to a count, into one shared model, in
-- each collection type that reads its rows through the pooled buffer, by the marker inside a query, and the same four
-- under no-array-pool, which read them without the pool.  SQL Server 2017 has no GENERATE_SERIES, so each query makes
-- the numbers up to 10,000 from a table of the ten digits, joined to itself four times.

-- name: NumbersAsArray
-- summary: The numbers from 1 to a count, in an array.
-- collection-type: Array
-- output-model: CountedNumber
WITH digits (v) AS (SELECT v FROM (VALUES (0), (1), (2), (3), (4), (5), (6), (7), (8), (9)) AS d (v))
SELECT n
FROM (
    SELECT d1.v + 10 * d2.v + 100 * d3.v + 1000 * d4.v + 1 AS n
    FROM digits AS d1
    CROSS JOIN digits AS d2
    CROSS JOIN digits AS d3
    CROSS JOIN digits AS d4
) AS numbers
WHERE n <= @count
ORDER BY n;

-- name: NumbersAsIEnumerable
-- summary: The numbers from 1 to a count, in an IEnumerable.
-- collection-type: IEnumerable
-- output-model: CountedNumber
WITH digits (v) AS (SELECT v FROM (VALUES (0), (1), (2), (3), (4), (5), (6), (7), (8), (9)) AS d (v))
SELECT n
FROM (
    SELECT d1.v + 10 * d2.v + 100 * d3.v + 1000 * d4.v + 1 AS n
    FROM digits AS d1
    CROSS JOIN digits AS d2
    CROSS JOIN digits AS d3
    CROSS JOIN digits AS d4
) AS numbers
WHERE n <= @count
ORDER BY n;

-- name: NumbersAsIReadOnlyCollection
-- summary: The numbers from 1 to a count, in an IReadOnlyCollection.
-- collection-type: IReadOnlyCollection
-- output-model: CountedNumber
WITH digits (v) AS (SELECT v FROM (VALUES (0), (1), (2), (3), (4), (5), (6), (7), (8), (9)) AS d (v))
SELECT n
FROM (
    SELECT d1.v + 10 * d2.v + 100 * d3.v + 1000 * d4.v + 1 AS n
    FROM digits AS d1
    CROSS JOIN digits AS d2
    CROSS JOIN digits AS d3
    CROSS JOIN digits AS d4
) AS numbers
WHERE n <= @count
ORDER BY n;

-- name: NumbersAsIReadOnlyList
-- summary: The numbers from 1 to a count, in an IReadOnlyList.
-- collection-type: IReadOnlyList
-- output-model: CountedNumber
WITH digits (v) AS (SELECT v FROM (VALUES (0), (1), (2), (3), (4), (5), (6), (7), (8), (9)) AS d (v))
SELECT n
FROM (
    SELECT d1.v + 10 * d2.v + 100 * d3.v + 1000 * d4.v + 1 AS n
    FROM digits AS d1
    CROSS JOIN digits AS d2
    CROSS JOIN digits AS d3
    CROSS JOIN digits AS d4
) AS numbers
WHERE n <= @count
ORDER BY n;

-- name: NumbersNoPoolAsArray
-- summary: The numbers from 1 to a count, in an array, read without the array pool.
-- collection-type: Array
-- generator: no-array-pool
-- output-model: CountedNumber
WITH digits (v) AS (SELECT v FROM (VALUES (0), (1), (2), (3), (4), (5), (6), (7), (8), (9)) AS d (v))
SELECT n
FROM (
    SELECT d1.v + 10 * d2.v + 100 * d3.v + 1000 * d4.v + 1 AS n
    FROM digits AS d1
    CROSS JOIN digits AS d2
    CROSS JOIN digits AS d3
    CROSS JOIN digits AS d4
) AS numbers
WHERE n <= @count
ORDER BY n;

-- name: NumbersNoPoolAsIEnumerable
-- summary: The numbers from 1 to a count, in an IEnumerable, read without the array pool.
-- collection-type: IEnumerable
-- generator: no-array-pool
-- output-model: CountedNumber
WITH digits (v) AS (SELECT v FROM (VALUES (0), (1), (2), (3), (4), (5), (6), (7), (8), (9)) AS d (v))
SELECT n
FROM (
    SELECT d1.v + 10 * d2.v + 100 * d3.v + 1000 * d4.v + 1 AS n
    FROM digits AS d1
    CROSS JOIN digits AS d2
    CROSS JOIN digits AS d3
    CROSS JOIN digits AS d4
) AS numbers
WHERE n <= @count
ORDER BY n;

-- name: NumbersNoPoolAsIReadOnlyCollection
-- summary: The numbers from 1 to a count, in an IReadOnlyCollection, read without the array pool.
-- collection-type: IReadOnlyCollection
-- generator: no-array-pool
-- output-model: CountedNumber
WITH digits (v) AS (SELECT v FROM (VALUES (0), (1), (2), (3), (4), (5), (6), (7), (8), (9)) AS d (v))
SELECT n
FROM (
    SELECT d1.v + 10 * d2.v + 100 * d3.v + 1000 * d4.v + 1 AS n
    FROM digits AS d1
    CROSS JOIN digits AS d2
    CROSS JOIN digits AS d3
    CROSS JOIN digits AS d4
) AS numbers
WHERE n <= @count
ORDER BY n;

-- name: NumbersNoPoolAsIReadOnlyList
-- summary: The numbers from 1 to a count, in an IReadOnlyList, read without the array pool.
-- collection-type: IReadOnlyList
-- generator: no-array-pool
-- output-model: CountedNumber
WITH digits (v) AS (SELECT v FROM (VALUES (0), (1), (2), (3), (4), (5), (6), (7), (8), (9)) AS d (v))
SELECT n
FROM (
    SELECT d1.v + 10 * d2.v + 100 * d3.v + 1000 * d4.v + 1 AS n
    FROM digits AS d1
    CROSS JOIN digits AS d2
    CROSS JOIN digits AS d3
    CROSS JOIN digits AS d4
) AS numbers
WHERE n <= @count
ORDER BY n;
