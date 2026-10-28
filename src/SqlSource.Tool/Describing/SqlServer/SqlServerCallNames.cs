namespace SqlSource.Tool.Describing.SqlServer;

/// <summary>The name of each call the describer asks through the exchange.</summary>
/// <remarks>
/// A value never changes: every capture records the name of each call it holds, and a recorded answer is found by it.
/// A call is added and never renamed, as <c>src/SqlSource.Tool/Describing/SqlServer/AGENTS.md</c> says.
/// </remarks>
internal static class SqlServerCallNames
{
    public const string Open = "open";
    public const string Parameters = "parameters";
    public const string Probe = "probe";
    public const string Declared = "declared";
    public const string Columns = "columns";
    public const string Operands = "operands";
    public const string Table = "table";
}
