-- The queries of the list parameters, claimed by ListQueries: a parameter written after IN or NOT IN with no
-- parentheses is a list, which the method sends as one parameter for each value.  Every query reads the seed's
-- accounts into one shared model.

-- name: ListAccountsByIds
-- summary: The accounts whose ids are in a list.
-- output-model: ListedAccount
SELECT id, name, status
FROM accounts
WHERE id IN @ids
ORDER BY id;

-- name: ListAccountsExcept
-- summary: The accounts whose ids are not in a list.
-- output-model: ListedAccount
SELECT id, name, status
FROM accounts
WHERE id NOT IN @ids
ORDER BY id;

-- name: ListAccountsOptional
-- summary: The accounts whose ids are in a list, or every account when the list is null.
-- output-model: ListedAccount
SELECT id, name, status
FROM accounts
WHERE (@ids IS NULL OR id IN @ids)
ORDER BY id;

-- name: ListAccountsByIdsAndStatuses
-- summary: The accounts whose ids are in one list and whose statuses, of the enum account_status, are in another.
-- output-model: ListedAccount
SELECT id, name, status
FROM accounts
WHERE id IN @ids AND status IN @statuses
ORDER BY id;

-- name: ListAccountsUsedTwice
-- summary: The accounts whose ids, or whose ids plus one, are in a list, which the query uses twice.
-- output-model: ListedAccount
SELECT id, name, status
FROM accounts
WHERE id IN @ids OR id + 1 IN @ids
ORDER BY id;

-- name: StreamAccountsByIds
-- summary: ListAccountsByIds, streamed, by its own collection type.
-- collection-type: IAsyncEnumerable
-- output-model: ListedAccount
SELECT id, name, status
FROM accounts
WHERE id IN @ids
ORDER BY id;

-- name: ListAccountsByIdsNoPadding
-- summary: ListAccountsByIds, with each list sent at its own count.
-- generator: list-padding=off
-- output-model: ListedAccount
SELECT id, name, status
FROM accounts
WHERE id IN @ids
ORDER BY id;

-- name: ListAccountNotesExcept
-- summary: Each account and the body of each of its notes whose id is not in a list, and NULL for an account without
-- summary: one, which an empty list keeps.
SELECT a.id, a.name, n.body
FROM accounts a
LEFT JOIN notes n ON n.account_id = a.id
WHERE n.id NOT IN @noteIds
ORDER BY a.id, n.id;
