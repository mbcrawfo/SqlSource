-- The queries of the collection types of the markers: IList by the file header, and Array and IImmutableList by a
-- marker of the query, which takes the place of the header's.
-- collection-type: IList

-- name: ListAccountsByHeader
-- summary: The id and name of every account, in the collection type of the file header.
-- output-model: CollectedAccount
SELECT id, name
FROM dbo.accounts
ORDER BY id;

-- name: ListAccountsInArray
-- summary: The id and name of every account, in an array by the query's marker.
-- collection-type: Array
-- output-model: CollectedAccount
SELECT id, name
FROM dbo.accounts
ORDER BY id;

-- name: ListAccountsInImmutableList
-- summary: The id and name of every account, in an IImmutableList by the query's marker.
-- collection-type: IImmutableList
-- output-model: CollectedAccount
SELECT id, name
FROM dbo.accounts
ORDER BY id;
