using System;
using System.Collections.Immutable;
using SqlSource.Tool.DotEnv;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Describing;

/// <summary>
/// Where a run looks up the variables that hold a connection: the environment, and then the run's <c>.env</c> file.
/// </summary>
/// <remarks>
/// It caches nothing: a run that reads its connections again, as each pass of <c>--watch</c> does, makes a new one.
/// It holds the values of the file, which are secrets, so it is a class that prints none of them.
/// </remarks>
internal sealed class ConnectionEnvironment
{
    private readonly Func<string, string?> _getEnvironmentVariable;
    private readonly DotEnvFile _file;

    private ConnectionEnvironment(Func<string, string?> getEnvironmentVariable, DotEnvFile file, string unitFolder)
    {
        _getEnvironmentVariable = getEnvironmentVariable;
        _file = file;
        UnitFolder = unitFolder;
    }

    /// <summary>Why the file could not be read, or the lines of it that could not, in order.</summary>
    public ImmutableArray<ToolDiagnostic> Errors => _file.Errors;

    /// <summary>The full path of the file read, or null when no folder searched holds one.</summary>
    public string? FilePath => _file.Path;

    /// <summary>The folder of the run's unit, where the search for the file starts.</summary>
    public string UnitFolder { get; }

    /// <summary>The lookup of the environment and a file that was read already.</summary>
    /// <param name="getEnvironmentVariable">The host's.</param>
    /// <param name="file">The run's file.</param>
    /// <param name="unitFolder">The folder of the run's unit.</param>
    public static ConnectionEnvironment Of(
        Func<string, string?> getEnvironmentVariable,
        DotEnvFile file,
        string unitFolder
    ) => new(getEnvironmentVariable, file, unitFolder);

    /// <summary>
    /// The lookup of a run: the host's environment, and the <c>.env</c> file nearest to the unit's folder, read
    /// through the host now.
    /// </summary>
    public static ConnectionEnvironment Load(string unitFolder, ToolHost host) =>
        Of(host.GetEnvironmentVariable, DotEnvSearch.Read(unitFolder, host.ReadTextFile), unitFolder);

    /// <summary>
    /// The value of a variable and where it came from: the environment's when it is not blank, with the variable as
    /// its source; else the file's when it is not blank, with the variable and the file's path; else null.  A value
    /// that is empty, or holds only white space, is not set: redacting a space would turn every space of a
    /// describer's text into the mark.
    /// </summary>
    public (string Value, string Source)? Find(string variable)
    {
        if (NullIfBlank(_getEnvironmentVariable(variable)) is { } fromEnvironment)
        {
            return (fromEnvironment, variable);
        }

        return _file.Values.TryGetValue(variable, out var fromFile) && NullIfBlank(fromFile) is { } value
            ? (value, $"{variable} in {_file.Path}")
            : null;
    }

    /// <summary>The lookup without its values, which are secrets.</summary>
    public override string ToString() =>
        $"ConnectionEnvironment {{ UnitFolder = {UnitFolder}, FilePath = {FilePath}, Errors = {Errors.Length} }}";

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
