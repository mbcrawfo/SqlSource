using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.Parsing.Quoting;
using SqlSource.Tool.Describing.SqlServer;

namespace SqlSource.Tool.Bundles.Renaming.SqlServer;

/// <summary>
/// The renaming of a message of SQL Server's, or of the describer's fixed text in its place: the parameters it names,
/// renamed, and nothing else.
/// </summary>
/// <remarks>
/// No decision of the pipeline compares a message's text.  <c>SqlServerErrors.NamedIn</c> reads only the
/// <c>@name</c>s of a message, a copy of a parameter as the parameter, so the renamed message holds those names alone,
/// each read as it reads them, and a text of <see cref="ConnectionErrors" />, which names none, becomes empty.
/// </remarks>
internal static class SqlServerMessages
{
    private const char Prefix = '@';

    /// <summary>
    /// The message renamed: each <c>@name</c> it holds, renamed through the scope and joined by one space.  A global
    /// variable, <c>@@ROWCOUNT</c>, and an <c>@</c> that no name follows, are left out.
    /// </summary>
    /// <param name="message">The message as captured.</param>
    /// <param name="scope">The scope of the message's place.</param>
    /// <param name="isEngineWord">Whether a spelling is the engine's word, unquoted.</param>
    public static string Rename(string message, IRenamingScope scope, Func<string, bool> isEngineWord)
    {
        List<string> names = [];
        var at = message.IndexOf(Prefix, StringComparison.Ordinal);
        while (at >= 0)
        {
            var end = at + 1;
            while (end < message.Length && QuoteReader.IsNameCharacterOfSqlServer(message[end]))
            {
                end++;
            }

            var name = message[(at + 1)..end];
            if (name.Length > 0 && name[0] != Prefix)
            {
                names.Add(Prefix + scope.Parameter(Named(name, scope, isEngineWord)));
            }

            at = end < message.Length ? message.IndexOf(Prefix, end) : -1;
        }

        return string.Join(' ', names);
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

    // The name a message's name is written as: its own when the mapping or the engine knows it, else the stem of a
    // copy, <stem><one or more _><digits>, that the mapping or the engine knows, else its own.
    private static string Named(string name, IRenamingScope scope, Func<string, bool> isEngineWord)
    {
        if (scope.IsMapped(name, quoted: false) || isEngineWord(name))
        {
            return name;
        }

        var digits = name.Length;
        while (digits > 0 && char.IsAsciiDigit(name[digits - 1]))
        {
            digits--;
        }

        var stem = digits;
        while (stem > 0 && name[stem - 1] == '_')
        {
            stem--;
        }

        if (digits == name.Length || stem == digits || stem == 0)
        {
            return name;
        }

        var parameter = name[..stem];
        return scope.IsMapped(parameter, quoted: false) || isEngineWord(parameter) ? parameter : name;
    }
}
