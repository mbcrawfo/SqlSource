-- The queries of the row buffer, claimed by RowBufferQueries: the numbers from 1 to a count, into one shared model, in
-- each of the eight collection types that read their rows through the pooled buffer, by the marker inside a query, and
-- the same eight under array-pool=off, which read them without the pool.

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

-- name: NumbersAsList
-- summary: The numbers from 1 to a count, in a List.
-- collection-type: List
-- output-model: CountedNumber
SELECT n FROM generate_series(1, @count) AS n ORDER BY n;

-- name: NumbersAsIList
-- summary: The numbers from 1 to a count, in an IList.
-- collection-type: IList
-- output-model: CountedNumber
SELECT n FROM generate_series(1, @count) AS n ORDER BY n;

-- name: NumbersAsICollection
-- summary: The numbers from 1 to a count, in an ICollection.
-- collection-type: ICollection
-- output-model: CountedNumber
SELECT n FROM generate_series(1, @count) AS n ORDER BY n;

-- name: NumbersAsImmutableArray
-- summary: The numbers from 1 to a count, in an ImmutableArray.
-- collection-type: ImmutableArray
-- output-model: CountedNumber
SELECT n FROM generate_series(1, @count) AS n ORDER BY n;

-- name: NumbersNoPoolAsArray
-- summary: The numbers from 1 to a count, in an array, read without the array pool.
-- collection-type: Array
-- generator: array-pool=off
-- output-model: CountedNumber
SELECT n FROM generate_series(1, @count) AS n ORDER BY n;

-- name: NumbersNoPoolAsIEnumerable
-- summary: The numbers from 1 to a count, in an IEnumerable, read without the array pool.
-- collection-type: IEnumerable
-- generator: array-pool=off
-- output-model: CountedNumber
SELECT n FROM generate_series(1, @count) AS n ORDER BY n;

-- name: NumbersNoPoolAsIReadOnlyCollection
-- summary: The numbers from 1 to a count, in an IReadOnlyCollection, read without the array pool.
-- collection-type: IReadOnlyCollection
-- generator: array-pool=off
-- output-model: CountedNumber
SELECT n FROM generate_series(1, @count) AS n ORDER BY n;

-- name: NumbersNoPoolAsIReadOnlyList
-- summary: The numbers from 1 to a count, in an IReadOnlyList, read without the array pool.
-- collection-type: IReadOnlyList
-- generator: array-pool=off
-- output-model: CountedNumber
SELECT n FROM generate_series(1, @count) AS n ORDER BY n;

-- name: NumbersNoPoolAsList
-- summary: The numbers from 1 to a count, in a List, read without the array pool.
-- collection-type: List
-- generator: array-pool=off
-- output-model: CountedNumber
SELECT n FROM generate_series(1, @count) AS n ORDER BY n;

-- name: NumbersNoPoolAsIList
-- summary: The numbers from 1 to a count, in an IList, read without the array pool.
-- collection-type: IList
-- generator: array-pool=off
-- output-model: CountedNumber
SELECT n FROM generate_series(1, @count) AS n ORDER BY n;

-- name: NumbersNoPoolAsICollection
-- summary: The numbers from 1 to a count, in an ICollection, read without the array pool.
-- collection-type: ICollection
-- generator: array-pool=off
-- output-model: CountedNumber
SELECT n FROM generate_series(1, @count) AS n ORDER BY n;

-- name: NumbersNoPoolAsImmutableArray
-- summary: The numbers from 1 to a count, in an ImmutableArray, read without the array pool.
-- collection-type: ImmutableArray
-- generator: array-pool=off
-- output-model: CountedNumber
SELECT n FROM generate_series(1, @count) AS n ORDER BY n;
