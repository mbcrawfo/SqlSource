using System;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace SqlSource.Settings.ClrTypes;

/// <summary>
/// A C# type as a setting names it, the type override, the column types or the <c>-&gt;</c> of a <c>-- param:</c> or
/// <c>-- column:</c> marker: a keyword, a name qualified from the global namespace, or a short name, with its type
/// arguments and the ranks of an array.
/// </summary>
/// <remarks>
/// <para>
/// The grammar is <c>["global::"] name ("." name)* rank*</c> or <c>keyword rank*</c>, where a name is an identifier
/// with optional type arguments, <c>&lt;arg ("," arg)*&gt;</c>, an argument is a type with an optional <c>?</c>, and
/// a rank is <c>[</c>, commas and <c>]</c>, after an optional <c>?</c>.  A name of one part written without
/// <c>global::</c> is a short name, <see cref="IsShort" />: the grammar cannot tell which type it is, since the
/// generator cannot see a file's usings, and the compilation resolves it.  The keywords are those of the built-in
/// types that a property can have, <c>dynamic</c> and <c>void</c> not among them.
/// </para>
/// <para>
/// White space is allowed only inside <c>&lt;&gt;</c> and <c>[]</c>, where it means nothing.  A <c>?</c> at the end
/// of the whole type is not valid, since the nullability of a property is its column's; nor is a tuple, a pointer or
/// any text left after the type.
/// </para>
/// </remarks>
/// <param name="Keyword">The keyword, such as <c>int</c>, or null for a name.</param>
/// <param name="Parts">The parts of a name from the first, or empty for a keyword.</param>
/// <param name="Ranks">The ranks of an array, in the order written, or empty.</param>
/// <param name="IsShort">
/// True for a name of one part written without <c>global::</c>.  <c>UserId</c> and <c>global::UserId</c> are two
/// names; a name of several parts is the same with or without <c>global::</c>.
/// </param>
internal sealed record ClrTypeName(
    string? Keyword,
    EquatableArray<ClrNamePart> Parts,
    EquatableArray<ClrRank> Ranks,
    bool IsShort
)
{
    private const string GlobalPrefix = "global::";

    // The deepest nesting of type arguments that is read.  A type nested deeper is not valid, so that no text can
    // run the reader, which calls itself for each argument, out of stack.
    private const int MaxNesting = 32;

    private static readonly string[] Keywords =
    [
        "bool",
        "byte",
        "sbyte",
        "short",
        "ushort",
        "int",
        "uint",
        "long",
        "ulong",
        "nint",
        "nuint",
        "char",
        "float",
        "double",
        "decimal",
        "string",
        "object",
    ];

    // The name in System of each keyword's type, in the order of Keywords.
    private static readonly string[] FrameworkNames =
    [
        "Boolean",
        "Byte",
        "SByte",
        "Int16",
        "UInt16",
        "Int32",
        "UInt32",
        "Int64",
        "UInt64",
        "IntPtr",
        "UIntPtr",
        "Char",
        "Single",
        "Double",
        "Decimal",
        "String",
        "Object",
    ];

    /// <summary>
    /// The type as generated code writes it: <c>global::</c> before a name but a short one, which is bare, a keyword
    /// bare, <c>, </c> between type arguments, each <c>?</c> and rank as written, and no other white space.  Two texts
    /// of one type give one text.
    /// </summary>
    public string Text
    {
        get
        {
            var builder = new StringBuilder();
            Write(builder, keywords: false);
            return builder.ToString();
        }
    }

    /// <summary>
    /// The text that a converter's types are compared with: the <see cref="Text" /> of the type that
    /// <paramref name="text" /> writes, with the name in <c>System</c> of each keyword's type written as the keyword,
    /// <c>global::App.Id&lt;long&gt;</c> of <c>global::App.Id&lt;global::System.Int64&gt;</c>, and each
    /// <c>System.Nullable&lt;T&gt;</c> that is a type argument or an array's element written as <c>T?</c>,
    /// <c>int?[]</c> of <c>global::System.Nullable&lt;global::System.Int32&gt;[]</c>, as a converter's types are
    /// written.  A <c>System.Nullable&lt;T&gt;</c> that is the whole type, already has a <c>?</c>, or whose
    /// <c>T</c> is an array or a nullable, which no property can have, keeps its name, so that the text is a type.
    /// A text with no <c>System.</c> in it, or one that is not a type, is given back as it is.
    /// </summary>
    /// <param name="text">A type as <see cref="Text" /> writes it.</param>
    public static string KeywordTextOf(string text)
    {
        if (text.IndexOf("System.", StringComparison.Ordinal) < 0 || !TryParse(text.AsSpan(), out var name))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        name!.Write(builder, keywords: true);
        return builder.ToString();
    }

    /// <summary>
    /// Reads a whole text as a type.  False, with a null <paramref name="name" />, for a text that is not one, and
    /// never an exception.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> text, out ClrTypeName? name)
    {
        var reader = new Reader(text);
        name = reader.ReadType(0);
        if (name is null || !reader.AtEnd)
        {
            name = null;
            return false;
        }

        return true;
    }

    // Writes the type; with keywords, a keyword's type named from System as the keyword, and an array of
    // System.Nullable<T> as one of T?.
    private void Write(StringBuilder builder, bool keywords)
    {
        if (keywords && Ranks.Count > 0 && !Ranks[0].NullableElement && NullableOf(this) is { } element)
        {
            element.Write(builder, keywords);
            _ = builder.Append('?');
            WriteRanks(builder);
            return;
        }

        if (Keyword is not null)
        {
            _ = builder.Append(Keyword);
        }
        else if (keywords && KeywordOf(Parts) is { } keyword)
        {
            _ = builder.Append(keyword);
        }
        else
        {
            if (!IsShort)
            {
                _ = builder.Append(GlobalPrefix);
            }

            for (var index = 0; index < Parts.Count; index++)
            {
                if (index > 0)
                {
                    _ = builder.Append('.');
                }

                WritePart(builder, Parts[index], keywords);
            }
        }

        WriteRanks(builder);
    }

    private void WriteRanks(StringBuilder builder)
    {
        foreach (var rank in Ranks)
        {
            _ = builder.Append(rank.NullableElement ? "?[" : "[").Append(',', rank.Dimensions - 1).Append(']');
        }
    }

    // The T of System.Nullable<T>, ranks aside, or null: of one argument without a ?, that is neither an array nor a
    // System.Nullable<T> itself, so that T? is a type.
    private static ClrTypeName? NullableOf(ClrTypeName type)
    {
        var parts = type.Parts;
        if (
            type.Keyword is not null
            || parts.Count != 2
            || parts[0].Arguments.Count > 0
            || !parts[0].Identifier.Equals("System", StringComparison.Ordinal)
            || !parts[1].Identifier.Equals("Nullable", StringComparison.Ordinal)
            || parts[1].Arguments.Count != 1
            || parts[1].Arguments[0].Nullable
        )
        {
            return null;
        }

        var argument = parts[1].Arguments[0].Type;
        return argument.Ranks.Count == 0 && NullableOf(argument) is null ? argument : null;
    }

    // The keyword of System.Int64 and the others, or null.
    private static string? KeywordOf(EquatableArray<ClrNamePart> parts)
    {
        if (
            parts.Count != 2
            || parts[0].Arguments.Count > 0
            || parts[1].Arguments.Count > 0
            || !parts[0].Identifier.Equals("System", StringComparison.Ordinal)
        )
        {
            return null;
        }

        var index = Array.IndexOf(FrameworkNames, parts[1].Identifier);
        return index < 0 ? null : Keywords[index];
    }

    private static void WritePart(StringBuilder builder, ClrNamePart part, bool keywords)
    {
        _ = builder.Append(part.Identifier);
        if (part.Arguments.Count == 0)
        {
            return;
        }

        _ = builder.Append('<');
        for (var index = 0; index < part.Arguments.Count; index++)
        {
            if (index > 0)
            {
                _ = builder.Append(", ");
            }

            var argument = part.Arguments[index];
            if (
                keywords
                && !argument.Nullable
                && argument.Type.Ranks.Count == 0
                && NullableOf(argument.Type) is { } inner
            )
            {
                inner.Write(builder, keywords);
                _ = builder.Append('?');
                continue;
            }

            argument.Type.Write(builder, keywords);
            if (argument.Nullable)
            {
                _ = builder.Append('?');
            }
        }

        _ = builder.Append('>');
    }

    // Reads a type from the start of a text.  Each method gives null, or false, at the first thing that is not the
    // grammar's, and what has been read is then not used.
    private ref struct Reader(ReadOnlySpan<char> text)
    {
        private readonly ReadOnlySpan<char> _text = text;

        private int _position;

        // How many brackets are open.  White space is passed over only inside one.
        private int _open;

        public readonly bool AtEnd => _position == _text.Length;

        private static bool IsKeyword(string identifier) => Array.IndexOf(Keywords, identifier) >= 0;

        public ClrTypeName? ReadType(int nesting)
        {
            if (nesting > MaxNesting)
            {
                return null;
            }

            SkipSpace();
            var qualified = TryTake(GlobalPrefix);
            if (ReadIdentifier() is not { } first)
            {
                return null;
            }

            string? keyword = null;
            var parts = ImmutableArray<ClrNamePart>.Empty;
            if (!qualified && IsKeyword(first))
            {
                keyword = first;
            }
            else if (ReadName(first, nesting) is { } name && !IsDynamic(qualified, name))
            {
                parts = name;
            }
            else
            {
                return null;
            }

            return ReadRanks() is { } ranks
                ? new ClrTypeName(
                    keyword,
                    new EquatableArray<ClrNamePart>(parts),
                    new EquatableArray<ClrRank>(ranks),
                    IsShort: !qualified && keyword is null && parts.Length == 1
                )
                : null;
        }

        // dynamic is no type of a property, and a short name must not make it one.
        private static bool IsDynamic(bool qualified, ImmutableArray<ClrNamePart> name) =>
            !qualified && name.Length == 1 && name[0].Identifier.Equals("dynamic", StringComparison.Ordinal);

        // name ("." name)*, after the first identifier.
        private ImmutableArray<ClrNamePart>? ReadName(string first, int nesting)
        {
            var parts = ImmutableArray.CreateBuilder<ClrNamePart>();
            var identifier = first;
            while (true)
            {
                if (!SqlIdentifier.IsUsableName(identifier))
                {
                    return null;
                }

                var arguments = ImmutableArray<ClrTypeArgument>.Empty;
                if (TryTake('<'))
                {
                    if (ReadArguments(nesting) is not { } read)
                    {
                        return null;
                    }

                    arguments = read;
                }

                parts.Add(new ClrNamePart(identifier, new EquatableArray<ClrTypeArgument>(arguments)));
                if (!TryTake('.'))
                {
                    return parts.ToImmutable();
                }

                if (ReadIdentifier() is not { } next)
                {
                    return null;
                }

                identifier = next;
            }
        }

        // arg ("," arg)* ">", after the "<".
        private ImmutableArray<ClrTypeArgument>? ReadArguments(int nesting)
        {
            _open++;
            var arguments = ImmutableArray.CreateBuilder<ClrTypeArgument>();
            do
            {
                if (ReadType(nesting + 1) is not { } type)
                {
                    return null;
                }

                arguments.Add(new ClrTypeArgument(type, TryTake('?')));
            } while (TryTake(','));

            if (!TryTake('>'))
            {
                return null;
            }

            _open--;
            return arguments.ToImmutable();
        }

        // rank*, each "[" ","* "]" after an optional "?".  A "?" that no "[" follows is left for the caller.
        private ImmutableArray<ClrRank>? ReadRanks()
        {
            var ranks = ImmutableArray.CreateBuilder<ClrRank>();
            while (true)
            {
                var start = _position;
                var nullable = TryTake('?');
                if (!TryTake('['))
                {
                    _position = start;
                    return ranks.ToImmutable();
                }

                _open++;
                var dimensions = 1;
                while (TryTake(','))
                {
                    dimensions++;
                }

                if (!TryTake(']'))
                {
                    return null;
                }

                _open--;
                ranks.Add(new ClrRank(nullable, dimensions));
            }
        }

        private string? ReadIdentifier()
        {
            SkipSpace();
            var start = _position;
            if (_position < _text.Length && SyntaxFacts.IsIdentifierStartCharacter(_text[_position]))
            {
                _position++;
                while (_position < _text.Length && SqlIdentifier.IsPartCharacter(_text[_position]))
                {
                    _position++;
                }
            }

            return _position == start ? null : _text.Slice(start, _position - start).ToString();
        }

        private bool TryTake(char character)
        {
            SkipSpace();
            if (_position < _text.Length && _text[_position] == character)
            {
                _position++;
                return true;
            }

            return false;
        }

        private bool TryTake(string word)
        {
            if (!_text.Slice(_position).StartsWith(word.AsSpan(), StringComparison.Ordinal))
            {
                return false;
            }

            _position += word.Length;
            return true;
        }

        private void SkipSpace()
        {
            while (_open > 0 && _position < _text.Length && char.IsWhiteSpace(_text[_position]))
            {
                _position++;
            }
        }
    }
}
