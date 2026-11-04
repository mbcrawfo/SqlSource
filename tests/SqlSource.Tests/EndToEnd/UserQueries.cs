namespace SqlSource.Tests.EndToEnd;

// The folder, Source = ".": every .sql file next to this source file, in a private class named Sql, but for
// CountUsers.sql, whose marker puts its constant on the type.
[SqlSourceGenerate(Source = ".")]
internal static partial class UserQueries
{
    public static string GetUserSql => Sql.GetUser;

    public static string ListUsersSql => Sql.ListUsers;

    public static string CountUsersSql => CountUsers;
}
