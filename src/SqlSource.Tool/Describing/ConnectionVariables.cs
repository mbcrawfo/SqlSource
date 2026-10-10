using System.Text;

namespace SqlSource.Tool.Describing;

/// <summary>
/// The environment variables that hold a connection.
/// </summary>
internal static class ConnectionVariables
{
    /// <summary>The variable with no name, for a run that has exactly one selected database.</summary>
    public const string Unnamed = "SQLSOURCE_CONNECTION";

    /// <summary>
    /// The variable of a database: its name in upper case, in the invariant culture, with every character that is
    /// not a letter or a digit written as <c>_</c>.  Two names can give one variable, which is <c>SQLSRC214</c>.
    /// </summary>
    public static string For(string database)
    {
        var variable = new StringBuilder(Unnamed).Append('_');
        foreach (var character in database.ToUpperInvariant())
        {
            _ = variable.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        return variable.ToString();
    }
}
