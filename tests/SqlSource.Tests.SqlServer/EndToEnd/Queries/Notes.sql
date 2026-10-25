-- The queries of the notes, claimed by NoteQueries.  The schema's alias type dbo.Email is read and sent as a C# type of
-- the project, through its converters, and time as a TimeOnly.
-- type-override: dbo.Email = SqlSource.Tests.SqlServer.EndToEnd.Types.Email
-- type-override: time = System.TimeOnly

-- name: ListAccountNotes
-- summary: Every account with its notes, and an account with none once, with NULL for the note.
SELECT a.id, a.name, n.id AS note_id, n.kind, n.body, n.remind_at
FROM dbo.accounts AS a
LEFT JOIN dbo.notes AS n ON n.account_id = a.id
ORDER BY a.id, n.id;

-- name: CountAccountNotes
-- summary: The number of notes of each account, which the tool shows is never null.
SELECT a.id, COUNT(n.id) AS note_count
FROM dbo.accounts AS a
LEFT JOIN dbo.notes AS n ON n.account_id = a.id
GROUP BY a.id
ORDER BY a.id;

-- name: ListAccountMonths
-- summary: The last day of the month each account opened in.
-- SQL Server treats EOMONTH as possibly null, and the tool cannot show otherwise, so the column would be nullable
-- without its marker.
-- column: month_end not null
SELECT a.id, EOMONTH(a.opened_on) AS month_end
FROM dbo.accounts AS a
ORDER BY a.id;

-- name: ListMarkedNames
-- summary: The names of the accounts, with their nullability set by the marks of their aliases.
-- A mark is written in brackets and in double quotes.  SQL Server treats UPPER of a column as possibly null, so the
-- column would be nullable without its mark.
SELECT a.id AS [id?], UPPER(a.name) AS "upper_name!"
FROM dbo.accounts AS a
ORDER BY a.id;

-- name: ListNoteKinds
-- summary: The kind of each note and the email of its account.
SELECT n.kind, a.email
FROM dbo.notes AS n
JOIN dbo.accounts AS a ON a.id = n.account_id
ORDER BY n.id;

-- name: ListReminders
-- summary: The notes with a reminder, with time given back to the type map by the query's override.
-- type-override: sys.time = default
SELECT n.id, n.remind_at
FROM dbo.notes AS n
WHERE n.remind_at IS NOT NULL
ORDER BY n.id;

-- name: FindAccountByEmail -> one-optional
-- summary: The account that an email names, or none: a parameter that SQL Server describes as the alias type.
SELECT id, name, email
FROM dbo.accounts
WHERE email = @email;

-- name: AddAccount -> one
-- summary: Adds an account, and returns its id and its email: a parameter written to a column of the alias type.
INSERT INTO dbo.accounts (name, email, opened_on)
OUTPUT INSERTED.id, INSERTED.email
VALUES (@name, @email, @openedOn);
