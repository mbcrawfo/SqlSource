namespace SqlSource.DatabaseTests.Postgres.EndToEnd.Types;

// The id that the schema's domain note_ref is read as, by the type override of Queries/Notes.sql: a value type, which
// a method reads from the long that Npgsql gives a domain over bigint and sends as that long, through its converters.
internal readonly record struct NoteRef(long Value)
{
    [SqlSourceConverter]
    public static NoteRef FromDatabase(long value) => new(value);

    [SqlSourceConverter]
    public static long ToDatabase(NoteRef noteRef) => noteRef.Value;
}
