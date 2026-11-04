namespace SqlSource.Tests.EndToEnd;

// A folder, on a generic type.  The folder's file is shared with OrderQueries, and its header puts the members on the
// type.
[SqlSourceGenerate(Source = "Orders")]
internal sealed partial class Repository<TKey>(TKey key)
{
    public string Describe() => $"{GetOrder} -- {key}";
}
