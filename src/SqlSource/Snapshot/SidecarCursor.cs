using System;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Snapshot;

/// <summary>
/// Walks a text that <see cref="SidecarReader" /> has found to be one JSON object, and keeps the first error.  It
/// checks no grammar: after a key comes a colon, and after a value a comma or the end of its container.
/// </summary>
/// <remarks>
/// Once an error is recorded <see cref="Next" /> returns the end of the text, so every loop above ends by itself and
/// no caller needs to check after each step.
/// </remarks>
internal sealed class SidecarCursor(string text)
{
    private SidecarTokenizer _tokens = new(text);

    public string Text { get; } = text;

    /// <summary>The first error, or null.</summary>
    public SidecarError? Error { get; private set; }

    /// <summary>The span of the key that <see cref="NextKey" /> returned last, quotes included.</summary>
    public TextSpan KeySpan { get; private set; }

    /// <summary>
    /// The offset after the last token.  Setting it moves the cursor: a value may be read out of order.
    /// </summary>
    public int Position
    {
        get => _tokens.Position;
        set => _tokens = new SidecarTokenizer(Text, value);
    }

    public SidecarToken Next() =>
        Error is null ? _tokens.Next() : new SidecarToken(SidecarTokenKind.End, new TextSpan(Text.Length, 0));

    /// <summary>Records an error, unless there is one already.</summary>
    public void Fail(SidecarErrorKind kind, TextSpan span, string? argument = null) =>
        Error ??= new SidecarError(kind, span, argument);

    public string GetString(SidecarToken token) => SidecarTokenizer.GetString(Text, token);

    /// <summary>Moves past the value that starts with <paramref name="first" /> and returns its span.</summary>
    public TextSpan Skip(SidecarToken first)
    {
        if (Error is not null)
        {
            return default;
        }

        // The grammar and the depth were checked before any cursor was made.
        _ = _tokens.TrySkipValue(first, 0, out _);
        return TextSpan.FromBounds(first.Span.Start, _tokens.Position);
    }

    /// <summary>
    /// Reads the next key of the object being read, and its colon.  False at the object's end.
    /// </summary>
    public bool NextMember(out SidecarToken key)
    {
        key = Next();
        if (key.Kind == SidecarTokenKind.Comma)
        {
            key = Next();
        }

        if (key.Kind != SidecarTokenKind.String)
        {
            return false;
        }

        _ = Next();
        return true;
    }

    /// <summary>
    /// Reads to the next key of the object that is one of <paramref name="keys" />, skipping every other key with
    /// its value, and returns it; null at the object's end.  A key found twice is an error.
    /// <paramref name="seen" /> has a bit for each key found so far, by its index in <paramref name="keys" />.
    /// </summary>
    public string? NextKey(string[] keys, ref int seen)
    {
        while (NextMember(out var key))
        {
            var index = IndexOf(keys, key);
            if (index < 0)
            {
                _ = Skip(Next());
                continue;
            }

            if ((seen & (1 << index)) != 0)
            {
                Fail(SidecarErrorKind.DuplicateKey, key.Span, keys[index]);
                return null;
            }

            seen |= 1 << index;
            KeySpan = key.Span;
            return keys[index];
        }

        return null;
    }

    /// <summary>Reads the first token of the next element of the array being read.  False at the array's end.</summary>
    public bool NextElement(out SidecarToken first)
    {
        first = Next();
        if (first.Kind == SidecarTokenKind.Comma)
        {
            first = Next();
        }

        return first.Kind is not (SidecarTokenKind.ArrayEnd or SidecarTokenKind.End);
    }

    /// <summary>Whether <see cref="NextKey" /> found <paramref name="key" />, by the bits it keeps.</summary>
    public static bool Has(string[] keys, int seen, string key) => (seen & (1 << Array.IndexOf(keys, key))) != 0;

    /// <summary>Records <see cref="SidecarErrorKind.MissingKey" /> unless <paramref name="key" /> was found.</summary>
    public void Require(string[] keys, int seen, TextSpan brace, string key)
    {
        if (!Has(keys, seen, key))
        {
            Fail(SidecarErrorKind.MissingKey, brace, key);
        }
    }

    /// <summary>Records <see cref="SidecarErrorKind.WrongType" /> at the value that starts with the token.</summary>
    public void WrongType(SidecarToken first, string key) => Fail(SidecarErrorKind.WrongType, Skip(first), key);

    /// <summary>Reads a string, or <c>null</c> where <paramref name="orNull" /> allows it.</summary>
    public string? ReadString(string key, bool orNull)
    {
        var token = Next();
        if (token.Kind == SidecarTokenKind.String)
        {
            return GetString(token);
        }

        if (!(orNull && token.Kind == SidecarTokenKind.Null))
        {
            WrongType(token, key);
        }

        return null;
    }

    /// <summary>Reads a 32-bit integer, or <c>null</c> where <paramref name="orNull" /> allows it.</summary>
    public int? ReadInt32(string key, bool orNull)
    {
        var token = Next();
        if (SidecarTokenizer.TryGetInt32(Text, token, out var value))
        {
            return value;
        }

        if (!(orNull && token.Kind == SidecarTokenKind.Null))
        {
            WrongType(token, key);
        }

        return null;
    }

    /// <summary>Reads <c>true</c>, <c>false</c> or <c>null</c>.</summary>
    public bool? ReadBoolean(string key)
    {
        var token = Next();
        if (token.Kind is SidecarTokenKind.True or SidecarTokenKind.False)
        {
            return token.Kind == SidecarTokenKind.True;
        }

        if (token.Kind != SidecarTokenKind.Null)
        {
            WrongType(token, key);
        }

        return null;
    }

    /// <summary>
    /// Reads the opening brace of an object, whose members are then read with <see cref="NextKey" />.  False for
    /// <c>null</c> where <paramref name="orNull" /> allows it, and for any other value, which is an error.
    /// </summary>
    public bool BeginObject(string key, bool orNull, out TextSpan brace) =>
        Begin(SidecarTokenKind.ObjectStart, key, orNull, out brace);

    /// <summary>Reads the opening bracket of an array, as <see cref="BeginObject" /> does a brace.</summary>
    public bool BeginArray(string key, bool orNull) => Begin(SidecarTokenKind.ArrayStart, key, orNull, out _);

    /// <summary>
    /// Moves past an array without reading it and returns the offset to read it from later, with
    /// <see cref="BeginArray" /> first; -1, and an error, for any other value.
    /// </summary>
    public int DeferArray(string key)
    {
        var position = Position;
        var first = Next();
        if (first.Kind != SidecarTokenKind.ArrayStart)
        {
            WrongType(first, key);
            return -1;
        }

        _ = Skip(first);
        return position;
    }

    private bool Begin(SidecarTokenKind kind, string key, bool orNull, out TextSpan brace)
    {
        var token = Next();
        brace = token.Span;
        if (token.Kind == kind)
        {
            return true;
        }

        if (!(orNull && token.Kind == SidecarTokenKind.Null))
        {
            WrongType(token, key);
        }

        return false;
    }

    private int IndexOf(string[] keys, SidecarToken key)
    {
        for (var index = 0; index < keys.Length; index++)
        {
            if (SidecarTokenizer.StringEquals(Text, key, keys[index]))
            {
                return index;
            }
        }

        return -1;
    }
}
