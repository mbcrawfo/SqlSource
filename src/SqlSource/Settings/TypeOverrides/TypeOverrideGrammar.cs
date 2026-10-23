using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using SqlSource.Settings.ClrTypes;

namespace SqlSource.Settings.TypeOverrides;

/// <summary>
/// Reads the type override, in an MSBuild value and in a <c>-- type-override:</c> marker alike.  An entry is
/// <c>key=type</c>, where the key is one that an engine reads, by <see cref="TypeKeys.TryNormalizePostgres" /> for
/// PostgreSQL and <see cref="TypeKeys.TryNormalizeSqlServer" /> for SQL Server, and the type one that
/// <see cref="ClrTypeName.TryParse" /> reads, or <c>default</c>, ignoring case, which takes the key out of the map.
/// </summary>
/// <remarks>
/// An MSBuild value is a list of entries separated by white space, line breaks included, outside <c>&lt;&gt;</c>,
/// <c>[]</c> and double quotes, and serves both engines.  A marker holds one entry, with white space allowed around its
/// <c>=</c>, and is read by its file's engine.  The <c>=</c> of an entry is the first one outside double quotes.  A
/// quoted part of a key may be written <c>U&amp;"..."</c>, with escapes such as <c>\000A</c> for a line break; it keeps
/// <c>""</c> for a quote, so the quotes of a key still pair, and <see cref="TypeKeys" /> reads it.
/// </remarks>
internal static class TypeOverrideGrammar
{
    private const string Default = "default";

    private const string NotInBrackets = ".  A key is not written in brackets: write ";

    /// <summary>
    /// Reads one entry, its key by <paramref name="engine" />'s rules.  False, with a null <paramref name="entry" />,
    /// for a text that is not one or a key that the engine does not read, and never an exception.
    /// </summary>
    /// <param name="text">The entry and nothing else.</param>
    /// <param name="spacesAroundEquals">
    /// Whether white space may stand on either side of the <c>=</c>, as in a marker.
    /// </param>
    /// <param name="place">Where the entry was written.</param>
    /// <param name="engine">The engine whose rules read the key.</param>
    /// <param name="entry">The entry that was read.</param>
    public static bool TryReadEntry(
        ReadOnlySpan<char> text,
        bool spacesAroundEquals,
        SettingPlace place,
        DatabaseEngine engine,
        out TypeOverride? entry
    )
    {
        entry = null;
        if (
            !TrySplitEntry(text, spacesAroundEquals, out var key, out var type)
            || !TryNormalize(key, engine, out var normalized)
        )
        {
            return false;
        }

        entry = new TypeOverride(normalized, type, place);
        return true;
    }

    /// <summary>
    /// Reads an MSBuild value, which serves both engines: each entry goes in the list of each engine that reads its
    /// key, and an entry that neither reads, or that is not one, is added to <paramref name="invalid" /> as written.
    /// The other entries apply.  Of two entries for one key of a list, the last wins, so that a project can add to a
    /// value it was given.  Each list is in the order of <see cref="TypeOverrideMap" />, and empty when it has no
    /// entry.
    /// </summary>
    public static TypeOverrideLists ReadList(string text, SettingPlace place, List<string> invalid)
    {
        List<TypeOverride>? postgreSql = null;
        List<TypeOverride>? sqlServer = null;
        var rest = text.AsSpan();
        while (NextEntry(ref rest) is { IsEmpty: false } written)
        {
            if (!TrySplitEntry(written, spacesAroundEquals: false, out var key, out var type))
            {
                invalid.Add(written.ToString());
                continue;
            }

            if (!TypeKeys.TryNormalizeEach(key, out var postgreSqlKey, out var sqlServerKey))
            {
                invalid.Add(written.ToString());
                continue;
            }

            if (postgreSqlKey is not null)
            {
                Put(postgreSql ??= [], new TypeOverride(postgreSqlKey, type, place));
            }

            if (sqlServerKey is not null)
            {
                Put(sqlServer ??= [], new TypeOverride(sqlServerKey, type, place));
            }
        }

        return new TypeOverrideLists(ToArray(postgreSql), ToArray(sqlServer));
    }

    /// <summary>
    /// The help of an entry whose key is written in brackets, as T-SQL writes a name, to end the message that says it
    /// is not valid: the key in the forms that are read.  A qualified key has two, plain, which matches ignoring case,
    /// and quoted, which matches it exactly: <c>[dbo].[Email]=X</c> gives <c>dbo.Email</c> and
    /// <c>"dbo"."Email"</c>.  An unqualified key has the plain form alone, <c>[INT]=long</c> gives <c>INT</c>, since an
    /// unqualified quoted key must be a name in lower case.  A key with a part that cannot be plain has the quoted form
    /// alone.  Empty for a key with no part in brackets, or one whose brackets are not well formed.
    /// </summary>
    /// <param name="entry">The entry as written, white space around its <c>=</c> allowed.</param>
    public static string BracketHelp(ReadOnlySpan<char> entry)
    {
        if (entry.IndexOf('[') < 0 || !TryRewriteBrackets(entry, out var plain, out var quoted, out var isQualified))
        {
            return string.Empty;
        }

        if (plain is null)
        {
            return NotInBrackets + quoted;
        }

        return isQualified
            ? NotInBrackets + plain + ", or " + quoted + " to match its case exactly"
            : NotInBrackets + plain;
    }

    // The key and the C# type of an entry, null for default, or false for a text with no = or a type that is not
    // valid.  The key is not read here.
    private static bool TrySplitEntry(
        ReadOnlySpan<char> text,
        bool spacesAroundEquals,
        out ReadOnlySpan<char> key,
        out string? type
    )
    {
        key = default;
        type = null;
        var equals = IndexOfEquals(text);
        if (equals < 0)
        {
            return false;
        }

        key = text.Slice(0, equals);
        var written = text.Slice(equals + 1);
        if (spacesAroundEquals)
        {
            key = key.TrimEnd();
            written = written.TrimStart();
        }

        if (written.Equals(Default.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!ClrTypeName.TryParse(written, out var name))
        {
            return false;
        }

        type = name!.Text;
        return true;
    }

    private static bool TryNormalize(ReadOnlySpan<char> key, DatabaseEngine engine, out string normalized) =>
        engine == DatabaseEngine.SqlServer
            ? TypeKeys.TryNormalizeSqlServer(key, out normalized)
            : TypeKeys.TryNormalizePostgres(key, out normalized);

    // Puts an entry in its place in a list kept in the order of TypeOverrideMap, over an entry of its key.
    private static void Put(List<TypeOverride> entries, TypeOverride entry)
    {
        var at = TypeOverrideMap.IndexOf(entries, entry.Key);
        if (at >= 0)
        {
            entries[at] = entry;
        }
        else
        {
            entries.Insert(~at, entry);
        }
    }

    private static EquatableArray<TypeOverride> ToArray(List<TypeOverride>? entries) =>
        entries is null
            ? EquatableArray<TypeOverride>.Empty
            : new EquatableArray<TypeOverride>(entries.ToImmutableArray());

    // The key at the start of an entry, written with at least one part in brackets, in the plain form, null when a
    // bracketed part cannot be plain, and in the quoted form.  A part in brackets, in which ]] is a ], is written as
    // its text, and quoted; a plain, quoted or escaped part is kept as written.  The key is followed by [] for an
    // array, white space, and the = of the entry, or the end of it.  False for anything else.
    private static bool TryRewriteBrackets(
        ReadOnlySpan<char> entry,
        out string? plain,
        out string quoted,
        out bool isQualified
    )
    {
        plain = null;
        quoted = string.Empty;
        isQualified = false;
        var plainKey = new StringBuilder();
        var quotedKey = new StringBuilder();
        var canBePlain = true;
        var hasBrackets = false;
        var index = 0;
        while (true)
        {
            if (index >= entry.Length)
            {
                return false;
            }

            if (entry[index] == '[')
            {
                if (!TryReadBracketedPart(entry, ref index, out var text))
                {
                    return false;
                }

                hasBrackets = true;
                canBePlain &= IsPlain(text);
                _ = plainKey.Append(text);
                _ = quotedKey.Append('"').Append(text.Replace("\"", "\"\"")).Append('"');
            }
            else
            {
                var start = index;
                if (!TypeKeys.TryReadPart(entry, start, out index))
                {
                    return false;
                }

                var part = entry.Slice(start, index - start).ToString();
                _ = plainKey.Append(part);
                _ = quotedKey.Append(part);
            }

            if (index < entry.Length && entry[index] == '.')
            {
                _ = plainKey.Append('.');
                _ = quotedKey.Append('.');
                isQualified = true;
                index++;
                continue;
            }

            break;
        }

        var rest = entry.Slice(index);
        if (rest.StartsWith("[]".AsSpan()))
        {
            _ = plainKey.Append("[]");
            _ = quotedKey.Append("[]");
            rest = rest.Slice(2);
        }

        rest = rest.TrimStart();
        if (!hasBrackets || (!rest.IsEmpty && rest[0] != '='))
        {
            return false;
        }

        plain = canBePlain ? plainKey.ToString() : null;
        quoted = quotedKey.ToString();
        return true;
    }

    // Reads [text] from entry[index], with ]] for a ], and moves past it.  The text is not empty.
    private static bool TryReadBracketedPart(ReadOnlySpan<char> entry, ref int index, out string text)
    {
        text = string.Empty;
        var builder = new StringBuilder();
        var at = index + 1;
        while (at < entry.Length)
        {
            if (entry[at] != ']')
            {
                _ = builder.Append(entry[at]);
                at++;
            }
            else if (at + 1 < entry.Length && entry[at + 1] == ']')
            {
                _ = builder.Append(']');
                at += 2;
            }
            else
            {
                index = at + 1;
                text = builder.ToString();
                return text.Length > 0;
            }
        }

        return false;
    }

    private static bool IsPlain(string text) => text.All(TypeKeys.IsPlainCharacter);

    // The first = outside double quotes, or -1.  A " toggles being inside quotes, so a doubled one inside a quoted part
    // closes and opens it again.
    private static int IndexOfEquals(ReadOnlySpan<char> text)
    {
        var quoted = false;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '"')
            {
                quoted = !quoted;
            }
            else if (text[index] == '=' && !quoted)
            {
                return index;
            }
        }

        return -1;
    }

    // The next run of text outside white space, where white space inside <>, [] or double quotes belongs to the run,
    // and a bracket inside double quotes is not counted.  Only the key opens a double quote.  Empty at the end.
    private static ReadOnlySpan<char> NextEntry(ref ReadOnlySpan<char> rest)
    {
        var start = 0;
        while (start < rest.Length && char.IsWhiteSpace(rest[start]))
        {
            start++;
        }

        var end = start;
        var open = 0;
        var quoted = false;
        var inType = false;
        while (end < rest.Length && (open > 0 || quoted || !char.IsWhiteSpace(rest[end])))
        {
            // Quotes belong to the key: once its = is read, a " is a character of the type, which is invalid.
            if (rest[end] == '"' && !inType)
            {
                quoted = !quoted;
            }
            else if (!quoted && rest[end] == '=')
            {
                inType = true;
            }
            else if (!quoted && rest[end] is '<' or '[')
            {
                open++;
            }
            else if (!quoted && rest[end] is '>' or ']' && open > 0)
            {
                open--;
            }

            end++;
        }

        var entry = rest.Slice(start, end - start);
        rest = rest.Slice(end);
        return entry;
    }
}
