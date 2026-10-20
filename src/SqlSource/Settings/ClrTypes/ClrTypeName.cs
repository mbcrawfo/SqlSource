using System;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace SqlSource.Settings.ClrTypes;

/// <summary>
/// A C# type as the type override names it: a keyword, or a name qualified from the global namespace, with its type
/// arguments and the ranks of an array.
/// </summary>
/// <remarks>
/// <para>
/// The grammar is <c>["global::"] name ("." name)* rank*</c> or <c>keyword rank*</c>, where a name is an identifier
/// with optional type arguments, <c>&lt;arg ("," arg)*&gt;</c>, an argument is a type with an optional <c>?</c>, and
/// a rank is <c>[</c>, commas and <c>]</c>, after an optional <c>?</c>.  A name of one part needs <c>global::</c>:
/// the generator cannot see a file's usings.  The keywords are those of the built-in types that a property can have,
/// <c>dynamic</c> and <c>void</c> not among them.
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
internal sealed record ClrTypeName(string? Keyword, EquatableArray<ClrNamePart> Parts, EquatableArray<ClrRank> Ranks)
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

    /// <summary>
    /// The type as generated code writes it: <c>global::</c> before a name, a keyword bare, <c>, </c> between type
    /// arguments, each <c>?</c> and rank as written, and no other white space.  Two texts of one type give one text.
    /// </summary>
    public string Text
    {
        get
        {
            var builder = new StringBuilder();
            Write(builder);
            return builder.ToString();
        }
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

    private void Write(StringBuilder builder)
    {
        if (Keyword is not null)
        {
            _ = builder.Append(Keyword);
        }
        else
        {
            _ = builder.Append(GlobalPrefix);
            for (var index = 0; index < Parts.Count; index++)
            {
                if (index > 0)
                {
                    _ = builder.Append('.');
                }

                WritePart(builder, Parts[index]);
            }
        }

        foreach (var rank in Ranks)
        {
            _ = builder.Append(rank.NullableElement ? "?[" : "[").Append(',', rank.Dimensions - 1).Append(']');
        }
    }

    private static void WritePart(StringBuilder builder, ClrNamePart part)
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

            part.Arguments[index].Type.Write(builder);
            if (part.Arguments[index].Nullable)
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
            else if (ReadName(first, nesting) is { } name && (qualified || name.Length > 1))
            {
                parts = name;
            }
            else
            {
                return null;
            }

            return ReadRanks() is { } ranks
                ? new ClrTypeName(keyword, new EquatableArray<ClrNamePart>(parts), new EquatableArray<ClrRank>(ranks))
                : null;
        }

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
