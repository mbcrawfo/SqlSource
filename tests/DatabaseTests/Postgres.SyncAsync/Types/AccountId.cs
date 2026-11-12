#pragma warning disable IDE0130 // The end-to-end project's namespace, which the mirrored queries name.
namespace SqlSource.DatabaseTests.Postgres.EndToEnd.Types;

#pragma warning restore IDE0130

// This project's copy of tests/DatabaseTests/Postgres/EndToEnd/Types/AccountId.cs, in its namespace, since a test
// project compiles no file of another: the mirrored queries name it.  A change to the original changes this copy.
//
// The id of an account, which the column types of Queries/Notes.sql give accounts.id and notes.account_id, and a
// -- param: marker a parameter: a method reads it from the int that Npgsql gives an integer and sends it as that int,
// through its converters.
internal readonly record struct AccountId(int Value)
{
    [SqlSourceConverter]
    public static AccountId FromDatabase(int value) => new(value);

    [SqlSourceConverter]
    public static int ToDatabase(AccountId accountId) => accountId.Value;
}
