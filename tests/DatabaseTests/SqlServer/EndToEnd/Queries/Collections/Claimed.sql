-- The query of the collection types, claimed by a class of CollectionQueries.cs for each of the attribute and of the
-- project's settings file, which the query says none of itself, and the copies of the query, one for each collection
-- type, which each say theirs by its marker.

-- name: ListCollectedAccounts
-- summary: The id and name of every account, in the collection type of the class that claims it.
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
