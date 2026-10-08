namespace SqlSource.Tests.EndToEnd;

// Default folder, nested mode: every .sql file next to this source file, in a private class named Sql.
[SqlSourceGenerate]
internal static partial class UserQueries
{
    public static string GetUserSql => Sql.GetUser;

    public static string ListUsersSql => Sql.ListUsers;

    public static string CountUsersSql => Sql.CountUsers;
}
