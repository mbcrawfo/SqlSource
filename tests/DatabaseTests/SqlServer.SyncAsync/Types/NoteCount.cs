#pragma warning disable IDE0130 // The end-to-end project's namespace, which the mirrored queries name.
namespace SqlSource.DatabaseTests.SqlServer.EndToEnd.Types;

#pragma warning restore IDE0130

// This project's copy of tests/DatabaseTests/SqlServer/EndToEnd/Types/NoteCount.cs, in its namespace, since a test
// project compiles no file of another: the mirrored queries name it.  A change to the original changes this copy.
//
// The number of notes of an account, which a -- column: marker of Queries/Notes.sql gives a count: a method reads it
// from the int that SqlClient gives the int of COUNT, through its converters.
internal readonly record struct NoteCount(int Value)
{
    [SqlSourceConverter]
    public static NoteCount FromDatabase(int value) => new(value);

    [SqlSourceConverter]
    public static int ToDatabase(NoteCount noteCount) => noteCount.Value;
}
