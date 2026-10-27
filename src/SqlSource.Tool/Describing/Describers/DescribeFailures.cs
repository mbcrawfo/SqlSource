using SqlSource.Diagnostics;

namespace SqlSource.Tool.Describing.Describers;

/// <summary>
/// The failures that both describers build the same way: what an engine passes in is its own help, its server's
/// lines and the spelling of its minimum version, and the descriptor, the order of the arguments and the step are
/// kept here once.
/// </summary>
internal static class DescribeFailures
{
    /// <summary>The reason of <c>SQLSRC228</c> for a type the database lacks.</summary>
    public const string NoSuchType = "the database has no such type";

    /// <summary>
    /// The reason of <c>SQLSRC228</c> for a declaration that is not written as a type name; for SQL Server it is the
    /// head of the reason, before <c>": "</c> and what was found.
    /// </summary>
    public const string NotATypeName = "it is not written as a type name";

    /// <summary>
    /// <c>SQLSRC224</c>: no session.  Every failure of <c>open</c> is this one, whatever the server's error: a
    /// refused login and a missing database are not the server rejecting a query.
    /// </summary>
    /// <param name="database">The database.</param>
    /// <param name="serverLines">
    /// The engine's code of the error and the describer's fixed text, never the server's message.
    /// </param>
    /// <param name="help">How to mend it.</param>
    public static DescribeFailure Open(string database, EquatableArray<string> serverLines, string help) =>
        new(ToolDiagnostics.DatabaseCannotBeOpened, new EquatableArray<string>([database]), null, serverLines, help);

    /// <summary>
    /// <c>SQLSRC235</c>: the server is older than the engine's minimum.  Nothing of a query is sent, and the failure
    /// holds no server line: the server said nothing.
    /// </summary>
    /// <param name="database">The database.</param>
    /// <param name="engine">How the message names the engine.</param>
    /// <param name="version">The server's version, as it is recorded.</param>
    /// <param name="minimum">The least version the engine supports, as the message spells it.</param>
    /// <param name="help">How to mend it.</param>
    public static DescribeFailure VersionNotSupported(
        string database,
        string engine,
        string version,
        string minimum,
        string help
    ) =>
        new(
            ToolDiagnostics.ServerVersionNotSupported,
            new EquatableArray<string>([database, engine, version, minimum]),
            null,
            EquatableArray<string>.Empty,
            help
        );

    /// <summary>
    /// <c>SQLSRC228</c>: the type a <c>-- param:</c> marker declares cannot be used; the last argument is why.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="writtenParameter">The parameter as the SQL writes one, such as <c>@id</c>.</param>
    /// <param name="type">The type as the marker writes it, or null when it writes none.</param>
    /// <param name="reason">Why the type cannot be used.</param>
    /// <param name="step">The step that found out.</param>
    /// <param name="serverLines">What the server said, verbatim, or none.</param>
    /// <param name="help">How to mend it.</param>
    public static DescribeFailure DeclaredType(
        DescribeRequest query,
        string writtenParameter,
        string? type,
        string reason,
        DescribeStep step,
        EquatableArray<string> serverLines,
        string help
    ) =>
        new(
            ToolDiagnostics.DeclaredTypeNotValid,
            new EquatableArray<string>([type ?? string.Empty, writtenParameter, query.Name, reason]),
            step,
            serverLines,
            help
        );
}
