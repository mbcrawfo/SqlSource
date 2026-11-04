-- The query of the collection type of the file header, ImmutableArray.  The header puts its method on the type, public.
-- collection-type: ImmutableArray
-- member-location: class
-- visibility: public

-- name: ListAccountsByMetadata
-- summary: The id and name of every account, in the collection type of the file header.
-- output-model: CollectedAccount
SELECT id, name
FROM dbo.accounts
ORDER BY id;
