-- The queries of the collection types, claimed by CollectionQueries: one query for each, by the marker inside it,
-- every one of the seed's accounts into the model they share.

-- name: ListAsArray
-- summary: Every account, in an array.
-- collection-type: Array
-- output-model: CollectedAccount
SELECT id, name FROM accounts ORDER BY id;

-- name: ListAsIEnumerable
-- summary: Every account, in an IEnumerable.
-- collection-type: IEnumerable
-- output-model: CollectedAccount
SELECT id, name FROM accounts ORDER BY id;

-- name: ListAsIReadOnlyCollection
-- summary: Every account, in an IReadOnlyCollection.
-- collection-type: IReadOnlyCollection
-- output-model: CollectedAccount
SELECT id, name FROM accounts ORDER BY id;

-- name: ListAsIReadOnlyList
-- summary: Every account, in an IReadOnlyList.
-- collection-type: IReadOnlyList
-- output-model: CollectedAccount
SELECT id, name FROM accounts ORDER BY id;

-- name: ListAsICollection
-- summary: Every account, in an ICollection.
-- collection-type: ICollection
-- output-model: CollectedAccount
SELECT id, name FROM accounts ORDER BY id;

-- name: ListAsIList
-- summary: Every account, in an IList.
-- collection-type: IList
-- output-model: CollectedAccount
SELECT id, name FROM accounts ORDER BY id;

-- name: ListAsList
-- summary: Every account, in a List.
-- collection-type: List
-- output-model: CollectedAccount
SELECT id, name FROM accounts ORDER BY id;

-- name: ListAsImmutableArray
-- summary: Every account, in an ImmutableArray.
-- collection-type: ImmutableArray
-- output-model: CollectedAccount
SELECT id, name FROM accounts ORDER BY id;

-- name: ListAsImmutableList
-- summary: Every account, in an ImmutableList.
-- collection-type: ImmutableList
-- output-model: CollectedAccount
SELECT id, name FROM accounts ORDER BY id;

-- name: ListAsIImmutableList
-- summary: Every account, in an IImmutableList.
-- collection-type: IImmutableList
-- output-model: CollectedAccount
SELECT id, name FROM accounts ORDER BY id;
