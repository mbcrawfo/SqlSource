namespace SqlSource.Tests.EndToEnd;

// A folder, on a generic type.  The folder's file is shared with OrderQueries.
[SqlSourceGenerate(Source = "Orders")]
internal sealed partial class Repository<TKey>(TKey key)
{
    public string Describe() => $"{Sql.GetOrder} -- {key}";
}
