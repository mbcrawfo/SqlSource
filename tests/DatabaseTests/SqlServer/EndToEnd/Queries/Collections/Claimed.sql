-- The query of the collection types, claimed by CollectionQueries alone, which sets none, so that the query takes the
-- project's settings file's, and its copies, one for each collection type, which each set theirs by its marker.  The
-- header puts every method on the type, public.
-- member-location: class
-- visibility: public

-- name: ListCollectedAccounts
-- summary: The id and name of every account, in the collection type of the project's settings file.
-- output-model: CollectedAccount
SELECT id, name
FROM dbo.accounts
ORDER BY id;

-- name: ListCollectedAccountsAsIEnumerable
-- summary: ListCollectedAccounts again, with the collection type IEnumerable.
-- output-model: CollectedAccount
-- collection-type: IEnumerable
SELECT id, name
FROM dbo.accounts
ORDER BY id;

-- name: ListCollectedAccountsAsICollection
-- summary: ListCollectedAccounts again, with the collection type ICollection.
-- output-model: CollectedAccount
-- collection-type: ICollection
SELECT id, name
FROM dbo.accounts
ORDER BY id;

-- name: ListCollectedAccountsAsIReadOnlyCollection
-- summary: ListCollectedAccounts again, with the collection type IReadOnlyCollection.
-- output-model: CollectedAccount
-- collection-type: IReadOnlyCollection
SELECT id, name
FROM dbo.accounts
ORDER BY id;

-- name: ListCollectedAccountsAsList
-- summary: ListCollectedAccounts again, with the collection type List.
-- output-model: CollectedAccount
-- collection-type: List
SELECT id, name
FROM dbo.accounts
ORDER BY id;

-- name: ListCollectedAccountsAsImmutableList
-- summary: ListCollectedAccounts again, with the collection type ImmutableList.
-- output-model: CollectedAccount
-- collection-type: ImmutableList
SELECT id, name
FROM dbo.accounts
ORDER BY id;
