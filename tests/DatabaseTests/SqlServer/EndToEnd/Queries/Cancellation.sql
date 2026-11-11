-- The query of the cancellation during the read, claimed by CancellationQueries: a file of its own, since its type
-- override names Types/CancelOnRead.cs, a type of this project alone, and the SqlClient 6.1 project links Methods.sql.

-- name: ListAccountIdsCancellingOnRead
-- summary: The ids of the accounts, each read as a CancelOnRead, whose converter cancels the token of its test, so
-- that the read of the second row sees it cancelled.
-- type-override: int = SqlSource.DatabaseTests.SqlServer.EndToEnd.Types.CancelOnRead
SELECT id
FROM dbo.accounts
ORDER BY id;
