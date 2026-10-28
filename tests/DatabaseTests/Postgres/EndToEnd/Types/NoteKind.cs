namespace SqlSource.DatabaseTests.Postgres.EndToEnd.Types;

// The C# enum that the schema's note_kind is read as, by the type override of Queries/Notes.sql.
internal enum NoteKind
{
    Call,
    Email,
    Visit,
}
