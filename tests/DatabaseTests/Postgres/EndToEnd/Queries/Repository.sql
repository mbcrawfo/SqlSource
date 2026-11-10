-- The queries of the repositories of RepositoryQueries.cs, at the output repository by the header, over the project's
-- methods: each type that claims the file gets each query as an instance method that takes its connection from the
-- type's own source, a data source, a connection string, a factory or a borrowed connection, and an interface of them.
-- output: repository

-- name: GetNote -> one-optional
-- summary: The note that an id names, or null.
-- output-model: RepositoryNote
SELECT id, account_id, body, ref
FROM notes
WHERE id = @id;

-- name: ListNotes
-- summary: Every note, streamed, so that a loop that breaks leaves its connection to the method's disposal.
-- collection-type: IAsyncEnumerable
-- output-model: RepositoryNote
SELECT id, account_id, body, ref
FROM notes
ORDER BY id;

-- name: TouchNote -> rowcount
-- summary: Writes the body of the note that an id names as it is, which changes nothing the other tests read.
-- A repository method takes no transaction, so the write is one that leaves the row as it was.
UPDATE notes
SET body = body
WHERE id = @id;
