-- The queries of the list parameters, claimed by ListQueries: a parameter written after IN or NOT IN with no
-- parentheses is a list, which the method sends as one parameter for each value.  Every query reads the seed's
-- accounts into one shared model.

-- name: ListAccountsByIds
-- summary: The accounts whose ids are in a list.
-- output-model: ListedAccount
SELECT id, name, status
FROM dbo.accounts
WHERE id IN @ids
ORDER BY id;

-- name: ListAccountsExcept
-- summary: The accounts whose ids are not in a list.
-- output-model: ListedAccount
SELECT id, name, status
FROM dbo.accounts
WHERE id NOT IN @ids
ORDER BY id;

-- name: ListAccountsOptional
-- summary: The accounts whose ids are in a list, or every account when the list is null.
-- output-model: ListedAccount
SELECT id, name, status
FROM dbo.accounts
WHERE (@ids IS NULL OR id IN @ids)
ORDER BY id;

-- name: ListAccountsByIdsAndStatuses
-- summary: The accounts whose ids are in one list and whose statuses are in another.
-- output-model: ListedAccount
SELECT id, name, status
FROM dbo.accounts
WHERE id IN @ids AND status IN @statuses
ORDER BY id;

-- name: ListAccountsUsedTwice
-- summary: The accounts whose ids, or whose ids plus one, are in a list, which the query uses twice.
-- output-model: ListedAccount
SELECT id, name, status
FROM dbo.accounts
WHERE id IN @ids OR id + 1 IN @ids
ORDER BY id;

-- name: StreamAccountsByIds
-- summary: ListAccountsByIds, streamed, by its own collection type.
-- collection-type: IAsyncEnumerable
-- output-model: ListedAccount
SELECT id, name, status
FROM dbo.accounts
WHERE id IN @ids
ORDER BY id;

-- name: ListAccountsByIdsNoPadding
-- summary: ListAccountsByIds, with each list sent at its own count.
-- generator: list-padding=off
-- output-model: ListedAccount
SELECT id, name, status
FROM dbo.accounts
WHERE id IN @ids
ORDER BY id;

-- name: CountAccountsWithStatuses
-- summary: How many accounts have a status in a list, the test inside an aggregate, which SQL Server refuses for an
-- summary: empty or a null list.
SELECT SUM(CASE WHEN status IN @statuses THEN 1 ELSE 0 END) AS matching
FROM dbo.accounts;

-- name: CountAccountsWithStatusesTestedFirst
-- summary: CountAccountsWithStatuses, with the test in a WITH query and the aggregate over its result, which SQL Server
-- summary: runs for an empty list too.
WITH tested AS (
    SELECT CASE WHEN status IN @statuses THEN 1 ELSE 0 END AS hit
    FROM dbo.accounts
)
SELECT SUM(hit) AS matching
FROM tested;
