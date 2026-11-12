#pragma warning disable IDE0130 // The end-to-end project's namespace, which the mirrored queries name.
namespace SqlSource.DatabaseTests.Postgres.EndToEnd.Types;

#pragma warning restore IDE0130

// This project's copy of tests/DatabaseTests/Postgres/EndToEnd/Types/NoteCount.cs, in its namespace, since a test
// project compiles no file of another: the mirrored queries name it.  A change to the original changes this copy.
//
// The number of notes of an account, which a -- column: marker of Queries/Notes.sql gives a count: a method reads it
// from the long that Npgsql gives a bigint, through its converters.
internal readonly record struct NoteCount(long Value)
{
    [SqlSourceConverter]
    public static NoteCount FromDatabase(long value) => new(value);

    [SqlSourceConverter]
    public static long ToDatabase(NoteCount noteCount) => noteCount.Value;
}
