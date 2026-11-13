#pragma warning disable IDE0130 // The end-to-end project's namespace, which the mirrored queries name.
namespace SqlSource.DatabaseTests.Postgres.EndToEnd.Types;

#pragma warning restore IDE0130

// This project's copy of tests/DatabaseTests/Postgres/EndToEnd/Types/NoteKind.cs, in its namespace, since a test
// project compiles no file of another: the mirrored queries name it.  A change to the original changes this copy.
//
// The C# enum that the schema's note_kind is read as, by the type override of Queries/Notes.sql.
internal enum NoteKind
{
    Call,
    Email,
    Visit,
}
