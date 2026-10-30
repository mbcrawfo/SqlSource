using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// The one place that takes a parameter's prefix off its name, for a parameter in the SQL and for one a server or a
/// driver reports, and the one rule for comparing two parameters' names, which the generator and the tool share.
/// </summary>
/// <remarks>
/// SqlSource reads two names that differ only in case as one parameter, so a name is compared ordinal ignoring case
/// wherever a parameter is matched by its name.  The drivers bind such names as one, and so does a SQL Server whose
/// instance collation ignores case; one whose collation is case-sensitive reads them as two variables (TD-0092).
/// </remarks>
internal static class SqlParameterName
{
    /// <summary>The one comparer of parameters' names, for a set or a dictionary keyed by them.</summary>
    public static StringComparer Comparer => StringComparer.OrdinalIgnoreCase;

    /// <summary>The one comparison of parameters' names, for <c>string.Equals</c> and a span's <c>Equals</c>.</summary>
    public const StringComparison Comparison = StringComparison.OrdinalIgnoreCase;

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

    /// <summary>
    /// The index in <paramref name="parameters" /> of the parameter named <paramref name="name" />, by
    /// <see cref="Comparison" />, or -1.  A query's list holds a name once, so the first match is the one.  The list
    /// is a type argument so that a struct such as <see cref="EquatableArray{T}" /> is searched without boxing.
    /// </summary>
    /// <param name="parameters">A query's parameters, or the list a parse is building.</param>
    /// <param name="name">The name without its prefix.</param>
    public static int IndexOf<TList>(TList parameters, ReadOnlySpan<char> name)
        where TList : IReadOnlyList<SqlQueryParameter>
    {
        for (var index = 0; index < parameters.Count; index++)
        {
            if (name.Equals(parameters[index].Name.AsSpan(), Comparison))
            {
                return index;
            }
        }

        return -1;
    }
}
