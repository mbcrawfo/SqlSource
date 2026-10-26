-- The queries of the row buffer, claimed by RowBufferQueries: the numbers from 1 to a count, into one shared model, in
-- each collection type that reads its rows through the pooled buffer, by the marker inside a query, and the same four
-- under no-array-pool, which read them without the pool.

-- name: NumbersAsArray
-- summary: The numbers from 1 to a count, in an array.
-- collection-type: Array
-- output-model: CountedNumber
SELECT n FROM generate_series(1, @count) AS n ORDER BY n;

-- name: NumbersAsIEnumerable
-- summary: The numbers from 1 to a count, in an IEnumerable.
-- collection-type: IEnumerable
-- output-model: CountedNumber
SELECT n FROM generate_series(1, @count) AS n ORDER BY n;

-- name: NumbersAsIReadOnlyCollection
-- summary: The numbers from 1 to a count, in an IReadOnlyCollection.
-- collection-type: IReadOnlyCollection
-- output-model: CountedNumber
SELECT n FROM generate_series(1, @count) AS n ORDER BY n;

-- name: NumbersAsIReadOnlyList
-- summary: The numbers from 1 to a count, in an IReadOnlyList.
-- collection-type: IReadOnlyList
-- output-model: CountedNumber
SELECT n FROM generate_series(1, @count) AS n ORDER BY n;

-- name: NumbersNoPoolAsArray
-- summary: The numbers from 1 to a count, in an array, read without the array pool.
-- collection-type: Array
-- generator: no-array-pool
-- output-model: CountedNumber
SELECT n FROM generate_series(1, @count) AS n ORDER BY n;

-- name: NumbersNoPoolAsIEnumerable
-- summary: The numbers from 1 to a count, in an IEnumerable, read without the array pool.
-- collection-type: IEnumerable
-- generator: no-array-pool
-- output-model: CountedNumber
SELECT n FROM generate_series(1, @count) AS n ORDER BY n;

-- name: NumbersNoPoolAsIReadOnlyCollection
-- summary: The numbers from 1 to a count, in an IReadOnlyCollection, read without the array pool.
-- collection-type: IReadOnlyCollection
-- generator: no-array-pool
-- output-model: CountedNumber
SELECT n FROM generate_series(1, @count) AS n ORDER BY n;

-- name: NumbersNoPoolAsIReadOnlyList
-- summary: The numbers from 1 to a count, in an IReadOnlyList, read without the array pool.
-- collection-type: IReadOnlyList
-- generator: no-array-pool
-- output-model: CountedNumber
SELECT n FROM generate_series(1, @count) AS n ORDER BY n;
