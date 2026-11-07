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
-- summary: How many accounts have a status in a list, the test inside an aggregate.
SELECT SUM(CASE WHEN status IN @statuses THEN 1 ELSE 0 END) AS matching
FROM dbo.accounts;

-- name: CountAccountsGroupedByStatuses
-- summary: The first id and the count of the accounts whose status is in a list, and of those whose status is not, the
-- summary: test in a GROUP BY.
SELECT MIN(id) AS first_id, COUNT(*) AS accounts
FROM dbo.accounts
GROUP BY CASE WHEN status IN @statuses THEN 1 ELSE 0 END
ORDER BY first_id;

-- name: MarkAccountsWithIds
-- summary: Sets each account's status to itself, and returns each id with 1 when it is in a list and 0 when it is not,
-- summary: the test in an OUTPUT clause.
UPDATE dbo.accounts
SET status = status
OUTPUT INSERTED.id, CASE WHEN INSERTED.id IN @ids THEN 1 ELSE 0 END AS listed;

-- name: CountAccountsExceptAndBefore
-- summary: How many accounts have an id that is not in a list, and how many of those have the next id in it: the list
-- summary: after a NOT and inside an aggregate.
SELECT COUNT(*) AS outside, SUM(CASE WHEN id + 1 IN @ids THEN 1 ELSE 0 END) AS before_listed
FROM dbo.accounts
WHERE id NOT IN @ids;

-- name: CountAccountsWithStatusesTestedFirst
-- summary: CountAccountsWithStatuses, with the test in a WITH query and the aggregate over its result, which SQL Server
-- summary: runs for an empty list too.
WITH tested AS (
    SELECT CASE WHEN status IN @statuses THEN 1 ELSE 0 END AS hit
    FROM dbo.accounts
)
SELECT SUM(hit) AS matching
FROM tested;
