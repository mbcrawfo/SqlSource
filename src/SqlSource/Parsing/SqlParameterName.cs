using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// The one place that takes a parameter's prefix off its name, for a parameter in the SQL and for one a server or a
/// driver reports.
/// </summary>
internal static class SqlParameterName
{
    /// <summary>
    /// The span of a parameter lexeme's name: the lexeme less its one-character prefix.  The lexer gives a parameter
    /// as the dialect's prefix and at least one name character, so this never fails for a lexeme of that kind.
    /// </summary>
    /// <param name="parameter">The span of a <see cref="SqlLexemeKind.Parameter" /> lexeme.</param>
    public static TextSpan Of(TextSpan parameter) => new(parameter.Start + 1, parameter.Length - 1);

    /// <summary>
    /// A name as a server or a driver reported it, without its first character when that is one of
    /// <paramref name="prefixes" />, and as it is otherwise.
    /// </summary>
    /// <param name="name">The reported name.</param>
    /// <param name="prefixes">The characters that may start it as a prefix.</param>
    public static string WithoutPrefix(string name, string prefixes) =>
        name.Length > 0 && prefixes.IndexOf(name[0]) >= 0 ? name.Substring(1) : name;
}
