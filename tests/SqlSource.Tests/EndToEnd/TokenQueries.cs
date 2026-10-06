namespace SqlSource.Tests.EndToEnd;

// A folder of queries with tokens, nested mode: each is a method of the private Sql class.
[SqlQueries(Path = "Tokens")]
internal static partial class TokenQueries
{
    public static string Search(string columns, string table, string filter) => Sql.Search(columns, table, filter);

    public static string Checked(string table, string filter) => Sql.Checked(table, filter);
}
