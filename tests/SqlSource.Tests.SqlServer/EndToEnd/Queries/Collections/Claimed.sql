-- The query of the collection types of the attribute and of the project's property, claimed by a class of
-- CollectionQueries.cs for each: it says none of its own.

-- name: ListCollectedAccounts
-- summary: The id and name of every account, in the collection type of the class that claims it.
-- output-model: CollectedAccount
SELECT id, name
FROM dbo.accounts
ORDER BY id;
