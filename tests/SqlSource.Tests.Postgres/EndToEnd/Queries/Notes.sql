-- The queries of the notes, claimed by NoteQueries.  The schema's enum and domain are read as C# types of the project.
-- type-override: public.note_kind = SqlSource.Tests.Postgres.EndToEnd.Types.NoteKind
-- type-override: public.email = SqlSource.Tests.Postgres.EndToEnd.Types.Email

-- name: ListAccountNotes
-- summary: Every account with its notes, and an account with none once, with NULL for the note.
SELECT a.id, a.name, n.id AS note_id, n.kind, n.body
FROM accounts AS a
LEFT JOIN notes AS n ON n.account_id = a.id
ORDER BY a.id, n.id;

-- name: CountAccountNotes
-- summary: The number of notes of each account.
-- column: note_count not null
SELECT a.id, count(n.id) AS note_count
FROM accounts AS a
LEFT JOIN notes AS n ON n.account_id = a.id
GROUP BY a.id
ORDER BY a.id;

-- name: ListMarkedNames
-- summary: The names of the accounts, with their nullability set by the marks of their aliases.
SELECT a.id AS "id?", upper(a.name) AS "upper_name!"
FROM accounts AS a
ORDER BY a.id;

-- name: ListNoteKinds
-- summary: The kind of each note and the email of its account.
SELECT n.kind, a.email
FROM notes AS n
JOIN accounts AS a ON a.id = n.account_id
ORDER BY n.id;
