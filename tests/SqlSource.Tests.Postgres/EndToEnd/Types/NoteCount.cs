namespace SqlSource.Tests.Postgres.EndToEnd.Types;

// The number of notes of an account, which a -- column: marker of Queries/Notes.sql gives a count: a method reads it
// from the long that Npgsql gives a bigint, through its converters.
internal readonly record struct NoteCount(long Value)
{
    [SqlSourceConverter]
    public static NoteCount FromDatabase(long value) => new(value);

    [SqlSourceConverter]
    public static long ToDatabase(NoteCount noteCount) => noteCount.Value;
}
