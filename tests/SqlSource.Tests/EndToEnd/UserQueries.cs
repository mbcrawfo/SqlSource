namespace SqlSource.Tests.EndToEnd;

// The folder, Source = ".", nested location: every .sql file next to this source file, in a private class named Sql.
[SqlSourceGenerate(Source = ".")]
internal static partial class UserQueries
{
    public static string GetUserSql => Sql.GetUser;

    public static string ListUsersSql => Sql.ListUsers;

    public static string CountUsersSql => Sql.CountUsers;
}
