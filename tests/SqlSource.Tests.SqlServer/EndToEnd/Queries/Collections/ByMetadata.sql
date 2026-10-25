-- The query of the collection type of the item metadata, ImmutableArray, which the project gives this file alone.

-- name: ListAccountsByMetadata
-- summary: The id and name of every account, in the collection type of the file's metadata.
-- output-model: CollectedAccount
SELECT id, name
FROM dbo.accounts
ORDER BY id;
