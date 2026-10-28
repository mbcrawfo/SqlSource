namespace SqlSource.DatabaseTests.Postgres.EndToEnd.Types;

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
