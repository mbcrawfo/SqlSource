using System.Collections.Immutable;

namespace SqlSource.Tool.Describing;

/// <summary>
/// Where a run looks for the connections of its databases, beside the environment: the values of
/// <c>--connection</c>, and the <c>.env</c> file of the unit's folder or of <c>--env-file</c>.
/// </summary>
/// <param name="Given">The values of <c>--connection</c>.</param>
/// <param name="UnitFolder">The full path of the folder of the run's unit, the project or the solution.</param>
/// <param name="EnvFile">The full path of the <c>.env</c> file the run reads, or null when it reads none.</param>
internal sealed record ConnectionSources(ImmutableArray<ConnectionArgument> Given, string UnitFolder, string? EnvFile)
{
    /// <summary>The sources without the values of <c>--connection</c>, which are secrets.</summary>
    public override string ToString() =>
        $"ConnectionSources {{ Given = {Given.Length}, UnitFolder = {UnitFolder}, EnvFile = {EnvFile} }}";
}
