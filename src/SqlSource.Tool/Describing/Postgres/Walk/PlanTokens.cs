using System;
using System.Collections.Generic;

namespace SqlSource.Tool.Describing.Postgres.Walk;

/// <summary>
/// An entry of a plan's <c>Output</c> as tokens of the plan's quoting: what <see cref="PlanExpression" /> reads its
/// forms from, and what <see cref="PlanPartitions" /> finds an alias's names by.
/// </summary>
internal static class PlanTokens
{
    internal enum Kind
    {
        Word,
        QuotedName,
        String,
        Number,
        Open,
        Close,
        OpenBracket,
        CloseBracket,
        Comma,
        Dot,
        Cast,
        Other,
    }

    internal readonly record struct Token(Kind Kind, int Start, int End);

    // The entry as tokens, or null when a quote does not close or a parenthesis or a bracket does not balance.  A
    // string is '...' with '' for a quote, and a quoted name "..." with "" for a quote; a space only separates.
    public static List<Token>? Read(string entry)
    {
        var tokens = new List<Token>();
        var depth = new Stack<Kind>();
        var at = 0;
        while (at < entry.Length)
        {
            var character = entry[at];
            var start = at;
            if (char.IsWhiteSpace(character))
            {
                at++;
                continue;
            }

            Kind kind;
            if (character is '\'' or '"')
            {
                if (!PostgresIdentifiers.SkipQuoted(entry, at, out at))
                {
                    return null;
                }

                kind = character == '\'' ? Kind.String : Kind.QuotedName;
            }
            else if (PostgresIdentifiers.IsWordStart(character))
            {
                while (at < entry.Length && PostgresIdentifiers.IsWordPart(entry[at]))
                {
                    at++;
                }

                kind = Kind.Word;
            }
            else if (char.IsAsciiDigit(character))
            {
                kind = Number(entry, ref at);
            }
            else if (character == ':' && at + 1 < entry.Length && entry[at + 1] == ':')
            {
                at += 2;
                kind = Kind.Cast;
            }
            else if (Punctuation(character) is { } punctuation)
            {
                at++;
                kind = punctuation;
                if (!Balances(depth, kind))
                {
                    return null;
                }
            }
            else
            {
                while (at < entry.Length && IsSymbol(entry[at]))
                {
                    at++;
                }

                at = Math.Max(at, start + 1);
                kind = Kind.Other;
            }

            tokens.Add(new Token(kind, start, at));
        }

        return depth.Count == 0 ? tokens : null;
    }

    // Digits with an optional fraction; one that runs on into a letter is no number this class reads.
    private static Kind Number(string entry, ref int at)
    {
        while (at < entry.Length && char.IsAsciiDigit(entry[at]))
        {
            at++;
        }

        if (at + 1 < entry.Length && entry[at] == '.' && char.IsAsciiDigit(entry[at + 1]))
        {
            at++;
            while (at < entry.Length && char.IsAsciiDigit(entry[at]))
            {
                at++;
            }
        }

        if (at < entry.Length && (PostgresIdentifiers.IsWordPart(entry[at]) || entry[at] == '.'))
        {
            while (at < entry.Length && (PostgresIdentifiers.IsWordPart(entry[at]) || entry[at] == '.'))
            {
                at++;
            }

            return Kind.Other;
        }

        return Kind.Number;
    }

    private static bool Balances(Stack<Kind> depth, Kind kind)
    {
        if (kind is Kind.Open or Kind.OpenBracket)
        {
            depth.Push(kind);
            return true;
        }

        if (kind is not (Kind.Close or Kind.CloseBracket))
        {
            return true;
        }

        var opens = kind == Kind.Close ? Kind.Open : Kind.OpenBracket;
        return depth.TryPop(out var open) && open == opens;
    }

    private static Kind? Punctuation(char character) =>
        character switch
        {
            '(' => Kind.Open,
            ')' => Kind.Close,
            '[' => Kind.OpenBracket,
            ']' => Kind.CloseBracket,
            ',' => Kind.Comma,
            '.' => Kind.Dot,
            _ => null,
        };

    // A "$" is one, as in "+$1"; a character that starts a word is not.
    private static bool IsSymbol(char character) =>
        !char.IsWhiteSpace(character)
        && !PostgresIdentifiers.IsWordStart(character)
        && !char.IsAsciiDigit(character)
        && character is not ('\'' or '"' or ':')
        && Punctuation(character) is null;
}
