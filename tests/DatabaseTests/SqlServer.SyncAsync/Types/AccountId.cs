#pragma warning disable IDE0130 // The end-to-end project's namespace, which the mirrored queries name.
namespace SqlSource.DatabaseTests.SqlServer.EndToEnd.Types;

#pragma warning restore IDE0130

// This project's copy of tests/DatabaseTests/SqlServer/EndToEnd/Types/AccountId.cs, in its namespace, since a test
// project compiles no file of another: the mirrored queries name it.  A change to the original changes this copy.
//
// The id of an account, which the column types of Queries/Notes.sql give dbo.accounts.id and dbo.notes.account_id, and
// a -- param: marker a parameter: a method reads it from the int that SqlClient gives an int and sends it as that int,
// through its converters.
internal readonly record struct AccountId(int Value)
{
    [SqlSourceConverter]
    public static AccountId FromDatabase(int value) => new(value);

    [SqlSourceConverter]
    public static int ToDatabase(AccountId accountId) => accountId.Value;
}
