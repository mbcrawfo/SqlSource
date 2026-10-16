-- name: ListActiveAccountsFreeDuring
SELECT a.id, a.name, a.email, a.created_at
FROM accounts a
WHERE a.status = 'active'
EXCEPT
SELECT a.id, a.name, a.email, a.created_at
FROM accounts a
JOIN reservations r ON r.account_id = a.id
WHERE r.during && tstzrange(@from, @to)
ORDER BY name;
