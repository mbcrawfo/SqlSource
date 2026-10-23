using System;
using Microsoft.CodeAnalysis.CSharp;

namespace SqlSource.Parsing.Tokens;

/// <summary>
/// Finds the token whose default is open at each of a series of offsets of one SQL text, as
/// <see cref="TokenScanner.FindOpenDefault" /> does for one, reading each character once over the series.
/// </summary>
/// <param name="sql">The SQL.</param>
internal sealed class OpenDefaultFinder(string sql)
{
    // Every character before this offset has been read, but the last: a "}}" that starts there ends after the
    // offset of the last call, and may end at or before the next.
    private int _position;

    // The name of the first opener of a default after the last "}}" read, or null when there is none.
    private string? _open;

    // The offset of the last call.
    private int _offset;

    /// <summary>
    /// Gives the name of the token whose default is open at <paramref name="offset" />, or null.  The offsets of
    /// successive calls must not decrease.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The offset is less than that of the last call.</exception>
    public string? At(int offset)
    {
        if (offset < _offset)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "The offsets must not decrease.");
        }

        _offset = offset;
        for (var index = _position; index < offset; index++)
        {
            if (index + 2 <= offset && sql[index] == '}' && sql[index + 1] == '}')
            {
                _open = null;
            }
            else
            {
                _open ??= ReadOpener(index);
            }
        }

        _position = offset > _position ? offset - 1 : _position;
        return _open;
    }

    // The name of the opener of a default that starts at the index, or null.
    private string? ReadOpener(int index)
    {
        if (
            !TokenScanner.TryReadOpener(sql, index, out var nameSpan, out var after)
            || SqlText.CharAt(sql, after) != ':'
        )
        {
            return null;
        }

        // A name that cannot start an identifier is not valid, and is rejected before a string is made for it.
        if (nameSpan.IsEmpty || !SyntaxFacts.IsIdentifierStartCharacter(sql[nameSpan.Start]))
        {
            return null;
        }

        var name = sql.Substring(nameSpan.Start, nameSpan.Length);
        return SqlIdentifier.IsValid(name) ? name : null;
    }
}
