-- The query that is only SQL, claimed by ConstantQueries: it has no models and no sidecar.
-- output: sql

-- name: CountAccounts
SELECT COUNT(*) FROM dbo.accounts;
