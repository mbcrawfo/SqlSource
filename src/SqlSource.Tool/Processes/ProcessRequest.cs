using System.Collections.Immutable;

namespace SqlSource.Tool.Processes;

/// <summary>
/// A program to run.
/// </summary>
/// <param name="Program">The program: a path, or a name that is looked for on the path.</param>
/// <param name="Arguments">Its arguments, each as the program is to see it: nothing is quoted or split.</param>
/// <param name="WorkingDirectory">The full path of the directory it runs in.</param>
/// <param name="SetVariables">Environment variables it gets beside the ones of this process.</param>
/// <param name="RemovedVariables">Environment variables of this process that it does not get.</param>
internal sealed record ProcessRequest(
    string Program,
    ImmutableArray<string> Arguments,
    string WorkingDirectory,
    ImmutableDictionary<string, string> SetVariables,
    ImmutableArray<string> RemovedVariables
);
