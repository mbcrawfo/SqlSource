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
    /// The reason of <c>SQLSRC228</c> for a declaration that is not written as a type name.  The tool's own refusal
    /// gives it as the head of the reason, before <c>": "</c> and what was found (<see cref="TypeNameFound" />); a
    /// refusal by the server gives it alone.
    /// </summary>
    public const string NotATypeName = "it is not written as a type name";

    /// <summary>
    /// What both engines' readers of a declared type say they found where a text leaves the grammar of a type name;
    /// what is only one engine's, such as the shape of its type names, is its reader's own.
    /// </summary>
    internal static class TypeNameFound
    {
        /// <summary>A <c>;</c>.</summary>
        public const string Semicolon = "a ; is not part of a type name";

        /// <summary>A <c>--</c> or a <c>/*</c>.</summary>
        public const string Comment = "a comment is not part of a type name";

        /// <summary>A dot with no part on one side of it, or a third part.</summary>
        public const string Parts = "a type name has one or two parts";

        /// <summary>A line break.</summary>
        public const string OneLine = "a type name is on one line";

        /// <summary>
        /// The reason of <c>SQLSRC228</c>: <see cref="NotATypeName" />, <c>": "</c> and what was found.
        /// </summary>
        /// <param name="found">What was found.</param>
        public static string Reason(string found) => NotATypeName + ": " + found;

        /// <summary>
        /// What stands at a place where the grammar was left: a <c>;</c>, a comment or a line break by name, and the
        /// end or anything else as the shape the reader expected there.
        /// </summary>
        /// <param name="text">The declared type.</param>
        /// <param name="position">The place in it, which may be its end.</param>
        /// <param name="shape">What the reader says of anything else.</param>
        public static string At(string text, int position, string shape)
        {
            if (position == text.Length)
            {
                return shape;
            }

            var next = position + 1 < text.Length ? text[position + 1] : '\0';
            return text[position] switch
            {
                ';' => Semicolon,
                '-' when next == '-' => Comment,
                '/' when next == '*' => Comment,
                var value when IsLineBreak(value) => OneLine,
                _ => shape,
            };
        }

        /// <summary>A line break of any kind a reader may take for one.</summary>
        /// <param name="value">The character.</param>
        public static bool IsLineBreak(char value) => value is '\r' or '\n' or '\u0085' or '\u2028' or '\u2029';
    }

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
    /// <param name="parameter">The parameter as the SQL writes it, and the type its marker declares.</param>
    /// <param name="reason">Why the type cannot be used.</param>
    /// <param name="step">The step that found out.</param>
    /// <param name="serverLines">What the server said, verbatim, or none.</param>
    /// <param name="help">How to mend it.</param>
    public static DescribeFailure DeclaredType(
        DescribeRequest query,
        DeclaredParameter parameter,
        string reason,
        DescribeStep step,
        EquatableArray<string> serverLines,
        string help
    ) =>
        new(
            ToolDiagnostics.DeclaredTypeNotValid,
            new EquatableArray<string>([parameter.Type ?? string.Empty, parameter.Written, query.Name, reason]),
            step,
            serverLines,
            help
        );
}
