using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.Tool.Describing.SqlServer;

namespace SqlSource.Tool.Bundles.Renaming.SqlServer;

/// <summary>
/// The renaming of a message of SQL Server's, or of the describer's fixed text in its place: the parameters it names,
/// renamed, and nothing else.
/// </summary>
/// <remarks>
/// No decision of the pipeline compares a message's text.  <c>SqlServerErrors.NamesIn</c>, which the describer reads a
/// message through, gives only the <c>@name</c>s of a message, a copy of a parameter as the parameter, so the renamed
/// message holds those names alone, each read as the describer reads them, and a text of
/// <see cref="ConnectionErrors" />, which names none, becomes empty.
/// </remarks>
internal static class SqlServerMessages
{
    private const char Prefix = '@';

    /// <summary>
    /// The message renamed: each <c>@name</c> it holds, a copy that the capture records as its parameter, renamed
    /// through the scope and joined by one space.  A global variable, <c>@@ROWCOUNT</c>, and an <c>@</c> that no name
    /// follows, are left out.
    /// </summary>
    /// <param name="message">The message as captured.</param>
    /// <param name="scope">The scope of the message's place.</param>
    public static string Rename(string message, IRenamingScope scope)
    {
        return string.Join(
            ' ',
            SqlServerErrors.NamesIn(message).Select(name => Prefix + scope.Parameter(scope.ParameterOf(name)))
        );
    }

    /// <summary>
    /// The words of a renamed message for the token check: the <c>@</c> and the name of each piece.  A piece with no
    /// <c>@</c>, which the renaming does not write, is given whole, so that the check sees it.
    /// </summary>
    public static IEnumerable<string> WordsOf(string renamed) =>
        renamed
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(static piece =>
                piece.Length > 1 && piece[0] == Prefix ? [Prefix.ToString(), piece[1..]] : (IEnumerable<string>)[piece]
            );
}
