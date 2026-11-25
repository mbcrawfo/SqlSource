using System;
using System.Collections.Generic;
using System.Text;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Describing.Postgres.Statements;

/// <summary>
/// The names under which the plan of a statement may print PostgreSQL 18's old and new row of a <c>RETURNING</c>
/// list, read from the SQL and the server's major: <see cref="None" />, a set of names, or
/// <see cref="Unknown" />.
/// </summary>
/// <remarks>
/// <para>
/// The rows are named <c>old</c> and <c>new</c>, and <c>RETURNING WITH (OLD AS o, NEW AS n)</c> renames them for its
/// list.  The plan prints them as <c>old.id</c> or <c>o.id</c>, as it prints a table's alias, and an alias of an inner
/// scope may have the same name.  Before 18 there are no such rows, and a statement that does not name them,
/// by <see cref="OuterJoinGate.NamesOldOrNew" />, has none either.
/// </para>
/// <para>
/// The set holds <c>old</c>, <c>new</c> and every name each <c>RETURNING WITH</c> of the statement gives, together.  A
/// renamed row loses its default name only within its own list, and the walk cannot tell which list is a
/// <c>ModifyTable</c>'s, so the set may hold a name that names no row there.  That costs an alias of that name the
/// heuristic and is never a wrong answer.  A name is compared as the server keeps it: a word folded to lower case, a
/// quoted name exactly, a <c>U&amp;"..."</c> one decoded, each cut to 63 bytes (<see cref="PostgresIdentifiers" />).
/// A clause this reader cannot follow, a name it cannot decode, or a name with a character above U+007F is
/// <see cref="Unknown" />: a server of a single-byte encoding folds such a character by its locale and cuts a name
/// by its own bytes, so only an ASCII name is read as every server reads it.
/// </para>
/// </remarks>
internal sealed class OldAndNewNames
{
    /// <summary>The first major whose <c>RETURNING</c> list has an old and a new row.</summary>
    public const int FirstMajor = 18;

    /// <summary>No reference can be the old or the new row.</summary>
    public static readonly OldAndNewNames None = new(mayBeNamed: false, names: null);

    /// <summary>A reference may be the old or the new row under any name.</summary>
    public static readonly OldAndNewNames Unknown = new(mayBeNamed: true, names: null);

    private readonly IReadOnlySet<string>? _names;

    private OldAndNewNames(bool mayBeNamed, IReadOnlySet<string>? names)
    {
        MayBeNamed = mayBeNamed;
        _names = names;
    }

    private enum TokenKind
    {
        Word,
        Name,
        Punctuation,
        Other,
    }

    /// <summary>Whether a reference may be the old or the new row.</summary>
    public bool MayBeNamed { get; }

    /// <summary>Whether an alias the plan prints may be the old or the new row's name.</summary>
    /// <param name="alias">The alias, as <see cref="Walk.OutputReference" /> reads it.</param>
    public bool MayName(string alias) => MayBeNamed && (_names is null || _names.Contains(alias));

    /// <summary>The names of the statement's old and new row.</summary>
    /// <param name="sql">The SQL of the query.</param>
    /// <param name="major">The server's major, or null when its version does not say, read as 18 or later.</param>
    /// <exception cref="InvalidOperationException">
    /// The SQL does not lex: a bug of the plan, whose parser read the same text.
    /// </exception>
    public static OldAndNewNames Of(string sql, int? major)
    {
        if (major < FirstMajor || !OuterJoinGate.NamesOldOrNew(sql))
        {
            return None;
        }

        var names = new HashSet<string>(StringComparer.Ordinal) { "old", "new" };
        var tokens = TokensOf(sql);
        for (var at = 0; at + 1 < tokens.Count; at++)
        {
            if (
                IsKeyword(tokens[at], "RETURNING")
                && IsKeyword(tokens[at + 1], "WITH")
                && !TryReadOptions(tokens, at + 2, names)
            )
            {
                return Unknown;
            }
        }

        return new OldAndNewNames(mayBeNamed: true, names);
    }

    // Reads "(OLD AS name, NEW AS name)" from the index into the names: false for anything else, a name that cannot be
    // decoded, or one with a character above U+007F, which a server of another encoding than UTF-8 may fold and cut
    // otherwise than the tool does.
    private static bool TryReadOptions(List<Token> tokens, int at, HashSet<string> names)
    {
        if (!IsPunctuation(tokens, at, '('))
        {
            return false;
        }

        at++;
        while (true)
        {
            if (
                at + 2 >= tokens.Count
                || !(IsKeyword(tokens[at], "OLD") || IsKeyword(tokens[at], "NEW"))
                || !IsKeyword(tokens[at + 1], "AS")
                || tokens[at + 2] is not { Kind: TokenKind.Word or TokenKind.Name, Name: { } name }
                || !Ascii.IsValid(name)
            )
            {
                return false;
            }

            _ = names.Add(name);
            at += 3;
            if (IsPunctuation(tokens, at, ')'))
            {
                return true;
            }

            if (!IsPunctuation(tokens, at, ','))
            {
                return false;
            }

            at++;
        }
    }

    private static bool IsKeyword(Token token, string keyword) =>
        token.Kind == TokenKind.Word && string.Equals(token.Text, keyword, StringComparison.OrdinalIgnoreCase);

    private static bool IsPunctuation(List<Token> tokens, int at, char character) =>
        at < tokens.Count && tokens[at] is { Kind: TokenKind.Punctuation, Text: [var found] } && found == character;

    // The tokens of the SQL without its comments: each word with the name it stands for, each quoted identifier with
    // its name, null when it cannot be decoded, and each other character of text as one token.  The U& before a
    // Unicode-escaped identifier and its UESCAPE clause are the identifier's.
    private static List<Token> TokensOf(string sql)
    {
        var lexemes = DescriberSql.Lex(sql, SqlDialectRules.PostgreSql);
        var tokens = new List<Token>();
        var index = 0;
        while (index < lexemes.Count)
        {
            var lexeme = lexemes[index];
            if (PostgresIdentifiers.TryReadQuotedName(sql, lexemes, index, out var name, out var end))
            {
                tokens.Add(new Token(TokenKind.Name, string.Empty, name));
                index = end;
                continue;
            }

            if (lexeme.Kind == SqlLexemeKind.Text)
            {
                var text = sql.AsSpan(lexeme.Span.Start, lexeme.Span.Length);
                if (index + 1 < lexemes.Count && PostgresIdentifiers.OpensUnicodeEscape(sql, lexemes, index + 1))
                {
                    text = text[..^2];
                }

                AddText(text, tokens);
            }
            else if (!DescriberSql.IsCommentOrHint(lexeme.Kind))
            {
                tokens.Add(new Token(TokenKind.Other, string.Empty, null));
            }

            index++;
        }

        return tokens;
    }

    private static void AddText(ReadOnlySpan<char> text, List<Token> tokens)
    {
        var at = 0;
        while (at < text.Length)
        {
            var character = text[at];
            if (char.IsWhiteSpace(character))
            {
                at++;
            }
            else if (PostgresIdentifiers.IsWordStart(character))
            {
                var end = PostgresIdentifiers.WordEnd(text, at);
                var word = text[at..end];
                tokens.Add(new Token(TokenKind.Word, word.ToString(), PostgresIdentifiers.FoldedWord(word)));
                at = end;
            }
            else if (PostgresIdentifiers.IsWordPart(character))
            {
                // A number, or a $ that starts no word: no name.
                var end = PostgresIdentifiers.WordEnd(text, at);
                tokens.Add(new Token(TokenKind.Other, text[at..end].ToString(), null));
                at = end;
            }
            else
            {
                tokens.Add(new Token(TokenKind.Punctuation, character.ToString(), null));
                at++;
            }
        }
    }

    private readonly record struct Token(TokenKind Kind, string Text, string? Name);
}
