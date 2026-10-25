namespace SqlSource.Tests.SqlServer.EndToEnd.Types;

// The number of notes of an account, which a -- column: marker of Queries/Notes.sql gives a count: a method reads it
// from the int that SqlClient gives the int of COUNT, through its converters.
internal readonly record struct NoteCount(int Value)
{
    [SqlSourceConverter]
    public static NoteCount FromDatabase(int value) => new(value);

    [SqlSourceConverter]
    public static int ToDatabase(NoteCount noteCount) => noteCount.Value;
}
