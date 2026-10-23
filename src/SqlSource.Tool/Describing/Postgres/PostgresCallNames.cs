namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// The name of each call the describer asks through the exchange.  <see cref="PostgresEvents" /> names a call's event
/// after it.
/// </summary>
/// <remarks>
/// A value never changes: every capture records the name of each call it holds, and a recorded answer is found by it
/// (<c>docs/architecture/postgres-describer.md#the-calls-and-their-records</c>).  A call is added and never renamed,
/// as <c>src/SqlSource.Tool/AGENTS.md</c> says.
/// </remarks>
internal static class PostgresCallNames
{
    public const string Open = "open";
    public const string Prepare = "prepare";
    public const string Columns = "columns";
    public const string Tables = "tables";
    public const string Types = "types";
    public const string Spellings = "spellings";
    public const string Check = "check";
    public const string Explain = "explain";
    public const string Partitions = "partitions";
    public const string Relations = "relations";
    public const string Overloads = "overloads";
    public const string Deallocate = "deallocate";
}
