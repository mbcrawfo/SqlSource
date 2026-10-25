-- The external ids of the accounts, claimed by ExternalRefQueries.  The schema's alias type dbo.ExternalId, over
-- bigint, is read and sent as the value type ExternalId, through its converters.
-- type-override: dbo.ExternalId = SqlSource.Tests.SqlServer.EndToEnd.Types.ExternalId

-- name: GetExternalRef -> one-optional
-- summary: The account that an external id names, with the id, or none.
SELECT r.id, r.account_id, r.external_id
FROM dbo.external_refs AS r
WHERE r.external_id = @externalId;

-- name: AddExternalRef -> one
-- summary: Adds an external id to an account, and returns the row.
INSERT INTO dbo.external_refs (id, account_id, external_id)
OUTPUT INSERTED.id, INSERTED.account_id, INSERTED.external_id
VALUES (@id, @accountId, @externalId);

-- name: ListAccountExternalIds
-- summary: Every account with its external id, and NULL for an account with none, which a LEFT JOIN gives.
SELECT a.id, r.external_id
FROM dbo.accounts AS a
LEFT JOIN dbo.external_refs AS r ON r.account_id = a.id
ORDER BY a.id;
