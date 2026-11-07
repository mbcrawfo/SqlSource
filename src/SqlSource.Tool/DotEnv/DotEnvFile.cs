using System.Collections.Immutable;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.DotEnv;

/// <summary>
/// The <c>.env</c> file that a run reads.
/// </summary>
/// <param name="Path">The full path of the file read, or null when the run reads none or finds none there.</param>
/// <param name="Values">
/// The value of each of the tool's keys, by key.  Empty when the file has an error: no value of a file that could
/// not be read in full is used.  A value is a connection and is never printed.
/// </param>
/// <param name="Errors">Why the file could not be read, or the lines of it that could not.</param>
internal sealed record DotEnvFile(
    string? Path,
    ImmutableDictionary<string, string> Values,
    ImmutableArray<ToolDiagnostic> Errors
)
{
    /// <summary>The file without its values, which are secrets.</summary>
    public override string ToString() =>
        $"DotEnvFile {{ Path = {Path}, Values = {Values.Count}, Errors = {Errors.Length} }}";
}
