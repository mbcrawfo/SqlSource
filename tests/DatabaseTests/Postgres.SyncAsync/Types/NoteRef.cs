#pragma warning disable IDE0130 // The end-to-end project's namespace, which the mirrored queries name.
namespace SqlSource.DatabaseTests.Postgres.EndToEnd.Types;

#pragma warning restore IDE0130

// This project's copy of tests/DatabaseTests/Postgres/EndToEnd/Types/NoteRef.cs, in its namespace, since a test project
// compiles no file of another: the mirrored queries name it.  A change to the original changes this copy.
//
// The id that the schema's domain note_ref is read as, by the type override of Queries/Notes.sql: a value type, which
// a method reads from the long that Npgsql gives a domain over bigint and sends as that long, through its converters.
internal readonly record struct NoteRef(long Value)
{
    [SqlSourceConverter]
    public static NoteRef FromDatabase(long value) => new(value);

    [SqlSourceConverter]
    public static long ToDatabase(NoteRef noteRef) => noteRef.Value;
}
