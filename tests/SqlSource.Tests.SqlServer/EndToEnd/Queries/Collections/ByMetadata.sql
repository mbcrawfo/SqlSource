-- The query of the collection type of the file header, ImmutableArray.
-- collection-type: ImmutableArray

-- name: ListAccountsByMetadata
-- summary: The id and name of every account, in the collection type of the file header.
-- output-model: CollectedAccount
SELECT id, name
FROM dbo.accounts
ORDER BY id;
