namespace SqlSource.Tests.EndToEnd;

// A folder of queries with tokens, nested location: each is a method of the private Sql class.
[SqlSourceGenerate(Path = "Tokens")]
internal static partial class TokenQueries
{
    public static string Search(string columns, string table, string filter) => Sql.Search(columns, table, filter);

    public static string Checked(string table, string filter) => Sql.Checked(table, filter);

    public static string Whole(string sql) => Sql.Whole(sql);

    public static string Eight(string a, string b, string c, string d, string e, string f, string g, string h) =>
        Sql.Eight(a, b, c, d, e, f, g, h);
}
