namespace SqlSource.Tool.Describing;

/// <summary>
/// The connection of one selected database, or that it has none.
/// </summary>
/// <param name="Database">The database, as the plan spells it.</param>
/// <param name="Variable">The environment variable of its name.</param>
/// <param name="Value">The connection, or null.  It goes to the describer and to nothing else.</param>
/// <param name="Source">
/// Where the value came from, which is all the tool may say of it: <c>--connection</c>, or a variable's name.
/// </param>
/// <param name="SharesVariableWith">
/// Another selected database whose name gives the same variable, when either of the two has no value on the command
/// line (<c>SQLSRC214</c>).  Such a database has no connection.
/// </param>
internal sealed record DatabaseConnection(
    string Database,
    string Variable,
    string? Value,
    string? Source,
    string? SharesVariableWith
)
{
    /// <summary>The connection without its value, which is a secret.</summary>
    public override string ToString() => $"DatabaseConnection {{ Database = {Database}, Source = {Source ?? "none"} }}";
}
