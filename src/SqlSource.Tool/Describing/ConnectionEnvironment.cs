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

    private ConnectionEnvironment(
        Func<string, string?> getEnvironmentVariable,
        DotEnvFile file,
        string unitFolder,
        string? lookedFor
    )
    {
        _getEnvironmentVariable = getEnvironmentVariable;
        _file = file;
        UnitFolder = unitFolder;
        LookedFor = lookedFor;
    }

    /// <summary>Why the file could not be read, or the lines of it that could not, in order.</summary>
    public ImmutableArray<ToolDiagnostic> Errors => _file.Errors;

    /// <summary>The full path of the file read, or null when the run reads none or finds none there.</summary>
    public string? FilePath => _file.Path;

    /// <summary>The full path of the file the run looks for, or null when it reads none.</summary>
    public string? LookedFor { get; }

    /// <summary>The folder of the run's unit.</summary>
    public string UnitFolder { get; }

    /// <summary>The lookup of the environment and a file that was read already.</summary>
    /// <param name="getEnvironmentVariable">The host's.</param>
    /// <param name="file">The run's file.</param>
    /// <param name="unitFolder">The folder of the run's unit.</param>
    /// <param name="lookedFor">The full path of the file the run looks for, or null when it reads none.</param>
    public static ConnectionEnvironment Of(
        Func<string, string?> getEnvironmentVariable,
        DotEnvFile file,
        string unitFolder,
        string? lookedFor
    ) => new(getEnvironmentVariable, file, unitFolder, lookedFor);

    /// <summary>
    /// The lookup of a run: the host's environment, and the file at <paramref name="envFile" />, read through the
    /// host now.
    /// </summary>
    /// <param name="unitFolder">The folder of the run's unit.</param>
    /// <param name="envFile">The full path of the run's <c>.env</c> file, or null when it reads none.</param>
    /// <param name="host">The environment and the files.</param>
    public static ConnectionEnvironment Load(string unitFolder, string? envFile, ToolHost host) =>
        Of(host.GetEnvironmentVariable, DotEnvSearch.Read(envFile, host.ReadTextFile), unitFolder, envFile);

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
        $"ConnectionEnvironment {{ UnitFolder = {UnitFolder}, LookedFor = {LookedFor}, FilePath = {FilePath}, "
        + $"Errors = {Errors.Length} }}";

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
