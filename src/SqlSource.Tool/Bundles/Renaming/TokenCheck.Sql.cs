using System;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Parsing;
using SqlSource.Parsing.Markers;
using SqlSource.Parsing.Tokens;
using SqlSource.Settings;

namespace SqlSource.Tool.Bundles.Renaming;

internal sealed partial class TokenCheck
{
    // SQL, lexeme by lexeme and word by word as the SQL renamer reads it.  In query.sql a marker's value is checked by
    // its kind.
    private void Sql(string text, string place, bool queryFile)
    {
        var lexed = SqlLexer.Lex(text, _rules);
        if (lexed.Error is { } error)
        {
            Refuse(place, text.Substring(error.Span.Start, error.Span.Length));
            return;
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
                    Words(text, start, end, quoteFollows, place);
                    break;
                case SqlLexemeKind.Quoted:
                    Quoted(text, start, end, place);
                    break;
                case SqlLexemeKind.Parameter:
                    var name = text.Substring(lexeme.ParameterName.Start, lexeme.ParameterName.Length);
                    Whole(IsParameter(name), place, name);
                    break;
                case SqlLexemeKind.LineComment:
                    if (queryFile && SqlMarkerReader.Read(text, lexeme) is { } marker)
                    {
                        Marker(text, marker, place);
                    }
                    else
                    {
                        Words(text, start + (text[start] == '-' ? 2 : 1), end, quoteFollows: false, place);
                    }

                    break;
                case SqlLexemeKind.BlockComment:
                    Words(text, start + 2, end - 2, quoteFollows: false, place);
                    break;
                case SqlLexemeKind.Hint:
                    var isLine = text[start] == '-';
                    var opener = SqlRenamer.HintOpenerLength(text, start);
                    Words(text, start + opener, end - (isLine ? 0 : 2), quoteFollows: false, place);
                    break;
                default:
                    throw UnexpectedEnumValueException.Of(lexeme.Kind);
            }
        }
    }

    // A marker's value, by its kind; the rest of its line as plain text.
    private void Marker(string text, SqlMarker marker, string place)
    {
        var value = text.Substring(marker.ValueSpan.Start, marker.ValueSpan.Length);
        switch (marker.Kind)
        {
            case SqlMarkerKind.Name:
                // The result's shape after the arrow is a closed word, which must read as the generator reads it.
                var (nameSpan, arrow) = SqlMarkerReader.SplitName(text, marker.ValueSpan);
                var name = text.Substring(nameSpan.Start, nameSpan.Length);
                Whole(IsSafe(name, quoted: false), place, name);
                if (arrow >= 0)
                {
                    var shape = text[(arrow + 2)..marker.ValueSpan.End];
                    Whole(SettingValue.TryReadChoice<ResultShape>(shape.AsSpan(), out _), place, shape.Trim());
                }

                break;
            case SqlMarkerKind.Dialect:
                // A dialect's name, which must read as the generator reads it.
                Whole(SqlDialectMarker.TryRead(text, marker, out _), place, value);
                break;
            case SqlMarkerKind.Database:
                Name(value, NameClass.Database, place);
                break;
            case SqlMarkerKind.Param:
            case SqlMarkerKind.Token:
                Sql(value, place, queryFile: false);
                break;
            case SqlMarkerKind.TokenIgnore:
                // A token's name, renamed as quoted.
                Whole(IsSafe(value, quoted: true), place, value);
                break;
            case SqlMarkerKind.Summary:
            case SqlMarkerKind.GeneratorParameters:
            case SqlMarkerKind.Output:
            case SqlMarkerKind.InputModelSuffix:
            case SqlMarkerKind.OutputModelSuffix:
            case SqlMarkerKind.ModelNamespace:
            case SqlMarkerKind.InputModelType:
            case SqlMarkerKind.OutputModelType:
            case SqlMarkerKind.InputModel:
            case SqlMarkerKind.OutputModel:
            case SqlMarkerKind.CollectionType:
            case SqlMarkerKind.TypeOverride:
            case SqlMarkerKind.Column:
            case SqlMarkerKind.LibraryTypes:
            case SqlMarkerKind.MemberLocation:
            case SqlMarkerKind.Visibility:
            case SqlMarkerKind.NestedClass:
                Refuse(place, "-- " + SqlMarkerReader.WordOf(marker.Kind) + ":");
                break;
            default:
                throw UnexpectedEnumValueException.Of(marker.Kind);
        }

        Words(text, marker.ValueSpan.End, marker.Span.End, quoteFollows: false, place);
    }

    // Plain text: numbers, white space and punctuation pass, a run a number starts that is no number is refused, and
    // each word must be safe.  A token placeholder's name must be safe as quoted, as the SQL renamer reads one in every
    // text.
    private void Words(string text, int start, int end, bool quoteFollows, string place)
    {
        var at = start;
        while (at < end)
        {
            var character = text[at];
            if (character == '{' && TokenNameAt(text, at, end) is { } name)
            {
                var token = text.Substring(name.Start, name.Length);
                Whole(IsSafe(token, quoted: true), place, token);
                at = name.End;
            }
            else if (SqlRenamer.NumberAt(_engine, text, at, end) is (var runEnd, var isNumber))
            {
                if (!isNumber)
                {
                    Refuse(place, text[at..runEnd]);
                }

                at = runEnd;
            }
            else if (_engine.IsWordStart(character))
            {
                var wordStart = at;
                at++;
                while (at < end && _engine.IsWordPart(text[at]))
                {
                    at++;
                }

                Word(text, wordStart, at, end, quoteFollows, place);
            }
            else if (
                SqlRenamer.IsSpace(character) || SqlRenamer.Punctuation.Contains(character, StringComparison.Ordinal)
            )
            {
                at++;
            }
            else
            {
                var length = char.IsSurrogatePair(text, at) ? 2 : 1;
                Refuse(place, text.Substring(at, length));
                at += length;
            }
        }
    }

    // The name of the token placeholder at the index, as the SQL renamer reads one, or null when none is there.
    private static TextSpan? TokenNameAt(string text, int at, int end)
    {
        if (!TokenScanner.TryReadToken(text, at, out _, out _, out _))
        {
            return null;
        }

        _ = TokenScanner.TryReadOpener(text, at, out var name, out _);
        return name.End <= end ? name : null;
    }

    // A word: a literal's one-letter prefix passes, and SQL Server's @ and # before a name are punctuation.
    private void Word(string text, int start, int end, int textEnd, bool quoteFollows, string place)
    {
        var rest = textEnd - end;
        if (
            quoteFollows
            && end - start == 1
            && SqlRenamer.LiteralPrefixes.Contains(text[start], StringComparison.Ordinal)
            && (rest == 0 || (rest == 1 && text[end] == '&'))
        )
        {
            return;
        }

        var word = text[start..end];
        var name = word.TrimStart('@', '#');
        Whole(name.Length == 0 || IsSafe(word, quoted: false) || IsSafe(name, quoted: false), place, word);
    }

    // A quoted region: a literal holds a replacement, and a quoted name is a replacement or the engine's.
    private void Quoted(string text, int start, int end, string place)
    {
        switch (text[start])
        {
            case '\'':
                var content = SqlRenamer.ContentOf(text, start, end);
                Whole(_mapping.IsReplacement(content), place, content);
                break;
            case '$':
                var inner = SqlRenamer.ContentOf(text, start, end);
                Whole(_mapping.IsReplacement(inner), place, inner);
                break;
            case '"':
                QuotedName(SqlRenamer.ContentOf(text, start, end), place);
                break;
            case '[':
                QuotedName(SqlRenamer.ContentOf(text, start, end), place);
                break;
            default:
                Refuse(place, text[start].ToString());
                break;
        }
    }

    private void QuotedName(string name, string place) =>
        Whole(
            name == SqlRenamer.UnnamedColumn
                || _engine.IsEngineWord(name, quoted: true)
                || IsSafe(RenamingRun.MarkOf(name).Stem, quoted: true),
            place,
            name
        );
}
