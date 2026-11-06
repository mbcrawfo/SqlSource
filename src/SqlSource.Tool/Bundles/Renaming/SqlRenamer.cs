using System;
using System.Collections.Generic;
using System.Text;
using SqlSource.Parsing;
using SqlSource.Parsing.Tokens;

namespace SqlSource.Tool.Bundles.Renaming;

/// <summary>
/// Renames SQL lexeme by lexeme, by the generator's lexer and the engine's word rule: a word, a quoted name, a
/// parameter and a literal through the mapping, a comment emptied, a number, white space and punctuation kept.
/// </summary>
/// <remarks>
/// A word, quoted name or parameter that <see cref="IEngineRenaming.IsEngineWord" /> keeps stays verbatim wherever it
/// stands, a user's name spelled as the engine's word too.  A refusal is reported and the renaming goes on, so that
/// every refusal of a text is reported; the text of a renaming that refused is not to be used.
/// </remarks>
internal sealed partial class SqlRenamer(
    IEngineRenaming engine,
    NameMapping mapping,
    IReadOnlySet<string> queryParameters
)
{
    /// <summary>
    /// The ASCII operators and delimiters, the only characters kept that are no word, number or white space.
    /// </summary>
    internal const string Punctuation = "!%&()*+,-./:;<=>?@[]^`{|}~\\$#'\"";

    /// <summary>
    /// The letters that, right before a quoted region, are a literal's prefix: <c>E''</c>, <c>N''</c>, <c>B''</c>,
    /// <c>X''</c> and <c>U&amp;''</c>.
    /// </summary>
    internal const string LiteralPrefixes = "EeNnBbXxUu";

    /// <summary>
    /// PostgreSQL's name of a column it names itself, which is kept whole and holds no nullability mark.
    /// </summary>
    internal const string UnnamedColumn = "?column?";

    /// <summary>
    /// The end of the run that the ASCII digit at <paramref name="start" /> begins, before <paramref name="end" />: the
    /// engine's word parts, <c>.</c>, and a sign right after <c>e</c> or <c>E</c> and before a digit.  The run is kept
    /// when <see cref="IsNumber" /> says it is a number, and refused whole otherwise, since an engine may read the
    /// rest as a name, as PostgreSQL before 15 reads <c>1users</c> as <c>1 AS users</c>, or as a value, as a hex or
    /// binary constant holds one.  The SQL renamer and the token check both split a run by this.
    /// </summary>
    internal static int NumberRunEnd(IEngineRenaming engine, string text, int start, int end)
    {
        var at = start + 1;
        while (at < end)
        {
            var character = text[at];
            var exponentSign =
                character is '+' or '-'
                && text[at - 1] is 'e' or 'E'
                && at + 1 < end
                && char.IsAsciiDigit(text[at + 1]);
            if (!engine.IsWordPart(character) && character != '.' && !exponentSign)
            {
                break;
            }

            at++;
        }

        return at;
    }

    /// <summary>
    /// Whether a text is a number, the one rule of the SQL renamer and the token check: ASCII digits, then optionally
    /// <c>.</c> and digits, then optionally <c>e</c> or <c>E</c>, a sign and digits, the sign optional.  Each group of
    /// digits may hold one <c>_</c> between two digits, as PostgreSQL 16 reads <c>1_000</c>; the digits after
    /// <c>.</c> may be none, as in <c>1.</c>.
    /// </summary>
    internal static bool IsNumber(ReadOnlySpan<char> text)
    {
        var at = DigitsEnd(text, 0);
        if (at == 0)
        {
            return false;
        }

        if (at < text.Length && text[at] == '.')
        {
            at = DigitsEnd(text, at + 1);
        }

        if (at < text.Length && text[at] is 'e' or 'E')
        {
            at++;
            if (at < text.Length && text[at] is '+' or '-')
            {
                at++;
            }

            var digits = DigitsEnd(text, at);
            if (digits == at)
            {
                return false;
            }

            at = digits;
        }

        return at == text.Length;
    }

    // The end of the digits from the index, each _ between two of them; the index itself when no digit is there.
    private static int DigitsEnd(ReadOnlySpan<char> text, int at)
    {
        if (at >= text.Length || !char.IsAsciiDigit(text[at]))
        {
            return at;
        }

        at++;
        while (at < text.Length)
        {
            if (char.IsAsciiDigit(text[at]))
            {
                at++;
            }
            else if (text[at] == '_' && at + 1 < text.Length && char.IsAsciiDigit(text[at + 1]))
            {
                at += 2;
            }
            else
            {
                break;
            }
        }

        return at;
    }

    private readonly IEngineRenaming _engine = engine;
    private readonly NameMapping _mapping = mapping;
    private readonly SqlDialectRules _rules = SqlDialectRules.For(engine.Dialect);

    /// <summary>
    /// The SQL renamed.  A new word is of the class given, or a function before <c>(</c> unless the class is
    /// <see cref="NameClass.Type" />.  SQL that does not lex, a character that is no word, number, white space or
    /// punctuation, or a run from a digit that is no number, is a refusal of <see cref="RefusalStep.Token" /> at the
    /// place.
    /// </summary>
    /// <param name="sql">The SQL.</param>
    /// <param name="newWords">The class of a name that the mapping does not hold yet.</param>
    /// <param name="place">Where the SQL is, as a refusal names it.</param>
    /// <param name="refuse">Takes each refusal.</param>
    public RenamedText Rename(string sql, NameClass newWords, string place, Action<RenamingRefusal> refuse) =>
        new Pass(this, sql, place, refuse, queryFile: false).Run(newWords);

    /// <summary>
    /// A query file renamed: as <see cref="Rename" />, new words being aliases, and each marker kept with its value
    /// renamed, and each token placeholder with its name renamed as a token.  A marker that a bundle's
    /// <c>query.sql</c> does not hold, or one that does not read, is a refusal at the file.
    /// </summary>
    /// <param name="text">The file's text.</param>
    /// <param name="refuse">Takes each refusal.</param>
    public RenamedText RenameQueryFile(string text, Action<RenamingRefusal> refuse) =>
        new Pass(this, text, BundleFiles.Query, refuse, queryFile: true).Run(NameClass.Alias);

    /// <summary>A name, kept when it is the engine's word, else the mapping's replacement of it.</summary>
    internal string NameOf(string spelling, bool quoted, NameClass cls) =>
        _engine.IsEngineWord(spelling, quoted) ? spelling : _mapping.Name(spelling, quoted, cls);

    /// <summary>
    /// A parameter's name, without its prefix: a copy of one of the query's through its parameter, else a name of its
    /// own, kept when it is the engine's word.
    /// </summary>
    internal string ParameterName(string name)
    {
        if (!queryParameters.Contains(name) && _engine.CopyOf(name, queryParameters) is var (parameter, suffix))
        {
            return NameOf(parameter, quoted: false, NameClass.Parameter) + suffix;
        }

        return NameOf(name, quoted: false, NameClass.Parameter);
    }

    // One renaming of one text.
    private sealed partial class Pass(
        SqlRenamer renamer,
        string text,
        string place,
        Action<RenamingRefusal> refuse,
        bool queryFile
    )
    {
        private readonly RenamedTextBuilder _into = new(text);

        public RenamedText Run(NameClass newWords)
        {
            var lexed = SqlLexer.Lex(text, renamer._rules);
            if (lexed.Error is { } error)
            {
                Refuse(text.Substring(error.Span.Start, error.Span.Length));
                return new RenamedTextBuilder(string.Empty).Build();
            }

            var lexemes = lexed.Lexemes;
            for (var index = 0; index < lexemes.Count; index++)
            {
                var lexeme = lexemes[index];
                var (start, end) = (lexeme.Span.Start, lexeme.Span.End);
                switch (lexeme.Kind)
                {
                    case SqlLexemeKind.Text:
                        var quoteFollows = index + 1 < lexemes.Count && lexemes[index + 1].Kind == SqlLexemeKind.Quoted;
                        Text(start, end, quoteFollows, newWords);
                        break;
                    case SqlLexemeKind.Quoted:
                        Quoted(start, end);
                        break;
                    case SqlLexemeKind.Parameter:
                        var name = lexeme.ParameterName;
                        _into.CopyTo(name.Start);
                        _into.ReplaceTo(name.End, renamer.ParameterName(text.Substring(name.Start, name.Length)));
                        break;
                    case SqlLexemeKind.LineComment:
                        if (queryFile)
                        {
                            LineCommentOfQueryFile(lexeme);
                        }
                        else
                        {
                            _into.ReplaceTo(end, "--");
                        }

                        break;
                    case SqlLexemeKind.BlockComment:
                        _into.ReplaceTo(end, "/*" + LineBreaksOf(start, end) + "*/");
                        break;
                    case SqlLexemeKind.Hint:
                        Hint(start, end, newWords);
                        break;
                    default:
                        throw UnexpectedEnumValueException.Of(lexeme.Kind);
                }
            }

            return _into.Build();
        }

        private void Refuse(string token) => refuse(new RenamingRefusal(RefusalStep.Token, place, token));

        private string LineBreaksOf(int start, int end)
        {
            var breaks = new StringBuilder();
            for (var index = start; index < end; index++)
            {
                if (SqlText.IsLineTerminator(text[index]))
                {
                    _ = breaks.Append(text[index]);
                }
            }

            return breaks.ToString();
        }

        // A hint keeps its opener, /*+, /*! or /*M!, or a line's --+, and its */, and its words are renamed.
        private void Hint(int start, int end, NameClass newWords)
        {
            var isLine = text[start] == '-';
            var opener = !isLine && text[start + 2] == 'M' ? 4 : 3;
            var closer = isLine ? 0 : 2;
            _into.CopyTo(start + opener);
            Text(start + opener, end - closer, quoteFollows: false, newWords);
            _into.CopyTo(end);
        }

        private void Text(int start, int end, bool quoteFollows, NameClass newWords)
        {
            var engine = renamer._engine;
            var at = start;
            while (at < end)
            {
                var character = text[at];
                if (queryFile && character == '{' && TryToken(at, end, out var after))
                {
                    at = after;
                }
                else if (char.IsAsciiDigit(character))
                {
                    var runEnd = NumberRunEnd(engine, text, at, end);
                    if (!IsNumber(text.AsSpan(at, runEnd - at)))
                    {
                        Refuse(text[at..runEnd]);
                    }

                    at = runEnd;
                    _into.CopyTo(at);
                }
                else if (engine.IsWordStart(character))
                {
                    var wordStart = at;
                    at++;
                    while (at < end && engine.IsWordPart(text[at]))
                    {
                        at++;
                    }

                    Word(wordStart, at, end, quoteFollows, newWords);
                }
                else if (char.IsWhiteSpace(character) || Punctuation.Contains(character, StringComparison.Ordinal))
                {
                    at++;
                    _into.CopyTo(at);
                }
                else
                {
                    var length = char.IsSurrogatePair(text, at) ? 2 : 1;
                    Refuse(text.Substring(at, length));
                    at += length;
                    _into.CopyTo(at);
                }
            }
        }

        // A word of plain text.  A literal's one-letter prefix is kept, and so is SQL Server's @@ function and the @
        // and # that start a name.
        private void Word(int start, int end, int textEnd, bool quoteFollows, NameClass newWords)
        {
            var rest = textEnd - end;
            if (
                quoteFollows
                && end - start == 1
                && LiteralPrefixes.Contains(text[start], StringComparison.Ordinal)
                && (rest == 0 || (rest == 1 && text[end] == '&'))
            )
            {
                _into.CopyTo(end);
                return;
            }

            if (string.CompareOrdinal(text, start, "@@", 0, 2) == 0)
            {
                _into.CopyTo(end);
                return;
            }

            var nameStart = start;
            while (nameStart < end && text[nameStart] is '@' or '#')
            {
                nameStart++;
            }

            _into.CopyTo(nameStart);
            if (nameStart < end)
            {
                var cls = newWords != NameClass.Type && IsCall(end, textEnd) ? NameClass.Function : newWords;
                _into.ReplaceTo(end, renamer.NameOf(text[nameStart..end], quoted: false, cls));
            }
        }

        // Whether "(" is the next thing after the white space that follows a word.
        private bool IsCall(int after, int end)
        {
            while (after < end && char.IsWhiteSpace(text[after]))
            {
                after++;
            }

            return after < end && text[after] == '(';
        }

        // A quoted region: a literal keeps its form with its content replaced, a quoted name its delimiters around
        // the name's replacement.
        private void Quoted(int start, int end)
        {
            var opener = text[start];
            switch (opener)
            {
                case '\'':
                    var content = text[(start + 1)..(end - 1)].Replace("''", "'", StringComparison.Ordinal);
                    _into.ReplaceTo(end, "'" + renamer._mapping.Literal(content) + "'");
                    break;
                case '$':
                    var tag = text[start..(text.IndexOf('$', start + 1) + 1)];
                    var inner = text[(start + tag.Length)..(end - tag.Length)];
                    _into.ReplaceTo(end, tag + renamer._mapping.Literal(inner) + tag);
                    break;
                case '"':
                    QuotedName(start, end, "\"");
                    break;
                case '[':
                    QuotedName(start, end, "]");
                    break;
                default:
                    Refuse(opener.ToString());
                    _into.ReplaceTo(end, string.Empty);
                    break;
            }
        }

        // A name keeps its delimiters, and a nullability mark after its stem; PostgreSQL's ?column? is kept whole.
        private void QuotedName(int start, int end, string closer)
        {
            var opener = text[start];
            var name = text[(start + 1)..(end - 1)].Replace(closer + closer, closer, StringComparison.Ordinal);
            if (name == UnnamedColumn || renamer._engine.IsEngineWord(name, quoted: true))
            {
                _into.CopyTo(end);
                return;
            }

            var mark = name.Length > 1 && name[^1] is '!' or '?' ? name[^1..] : string.Empty;
            var stem = renamer.NameOf(name[..^mark.Length], quoted: true, NameClass.Alias);
            _into.ReplaceTo(
                end,
                opener + stem.Replace(closer, closer + closer, StringComparison.Ordinal) + mark + closer
            );
        }

        // A token placeholder, {{name}} or {{name:default}}, as the generator reads one: its name is renamed as a
        // token, and the rest goes on as SQL.
        private bool TryToken(int at, int end, out int after)
        {
            after = at;
            if (!TokenScanner.TryReadToken(text, at, out _, out _, out _))
            {
                return false;
            }

            _ = TokenScanner.TryReadOpener(text, at, out var name, out _);
            if (name.End > end)
            {
                return false;
            }

            _into.CopyTo(name.Start);
            _into.ReplaceTo(name.End, renamer.NameOf(text.Substring(name.Start, name.Length), false, NameClass.Token));
            after = name.End;
            return true;
        }
    }
}
