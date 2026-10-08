using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Settings;

namespace SqlSource.Parsing;

/// <summary>
/// Turns the text of one <c>.sql</c> file into its named SQL blocks, or into a list of errors.
/// </summary>
/// <remarks>
/// The parser reads no files and compares text ordinally, and its result has value equality.
/// </remarks>
internal static class SqlFileParser
{
    /// <summary>
    /// Parses <paramref name="text" />.  <paramref name="fileName" /> is the file's name with its extension and without
    /// a directory; it names the block of a file that has no <c>-- name:</c> marker.  <paramref name="dialect" /> is
    /// the dialect, with its options, that the project gives the file.  A <c>-- dialect:</c> marker in the file's
    /// header replaces it whole for the text after the marker.
    /// </summary>
    public static SqlFileParseResult Parse(string text, string fileName, SqlDialectChoice dialect = default)
    {
        var lexer = new SqlLexer(text, SqlDialectRules.For(dialect));
        var headerEnd = SqlDialectMarker.Apply(lexer, text);
        var lexed = lexer.ReadToEnd();
        return lexed.Error is null
            ? new Parser(text, fileName, lexed.Lexemes, headerEnd, lexer.Rules).Run()
            : new SqlFileParseResult(
                EquatableArray<SqlBlock>.Empty,
                new EquatableArray<SqlParseError>(ImmutableArray.Create(lexed.Error)),
                lexer.Rules.Dialect
            );
    }

    private sealed class Parser(
        string text,
        string fileName,
        EquatableArray<SqlLexeme> lexemes,
        int headerEnd,
        SqlDialectRules rules
    )
    {
        private static readonly TextSpan FileStart = new(0, 0);

        private readonly List<SqlBlock> _blocks = [];
        private readonly List<SqlParseError> _errors = [];
        private readonly HashSet<string> _names = [];

        // The dialect that a marker of the file has named so far.  It is already in effect by the time the markers
        // are read here; it is kept to find a second marker that names another.
        private SqlDialectChoice? _dialect;

        public SqlFileParseResult Run()
        {
            var nameMarkers = FindNameMarkers();
            if (nameMarkers.Count == 0)
            {
                ReadUnnamedFile();
            }
            else
            {
                ReadNamedBlocks(nameMarkers);
            }

            return _errors.Count > 0
                ? new SqlFileParseResult(
                    EquatableArray<SqlBlock>.Empty,
                    new EquatableArray<SqlParseError>(
                        _errors.OrderBy(static error => error.Span.Start).ToImmutableArray()
                    ),
                    rules.Dialect
                )
                : new SqlFileParseResult(
                    new EquatableArray<SqlBlock>(_blocks.ToImmutableArray()),
                    EquatableArray<SqlParseError>.Empty,
                    rules.Dialect
                );
        }

        private List<(int Index, SqlMarker Marker)> FindNameMarkers()
        {
            var markers = new List<(int Index, SqlMarker Marker)>();
            for (var index = 0; index < lexemes.Count; index++)
            {
                if (SqlMarkerReader.Read(text, lexemes[index]) is { Kind: SqlMarkerKind.Name } marker)
                {
                    markers.Add((index, marker));
                }
            }

            return markers;
        }

        private void ReadUnnamedFile()
        {
            var lastDot = fileName.LastIndexOf('.');
            var name = lastDot < 0 ? fileName : fileName.Substring(0, lastDot);
            if (!SqlIdentifier.IsUsableName(name))
            {
                AddError(SqlParseErrorKind.InvalidFileName, FileStart, fileName);
            }

            ReadBlock(name, FileStart, null, null, 0, lexemes.Count);
        }

        private void ReadNamedBlocks(List<(int Index, SqlMarker Marker)> nameMarkers)
        {
            var preamble = ReadPreamble(nameMarkers[0].Index);
            for (var position = 0; position < nameMarkers.Count; position++)
            {
                var (index, marker) = nameMarkers[position];
                var end = position + 1 < nameMarkers.Count ? nameMarkers[position + 1].Index : lexemes.Count;
                var (written, shape) = ReadName(marker);
                var name = text.Substring(written.Start, written.Length);
                var nameSpan = name.Length == 0 ? marker.Span : written;
                if (!SqlIdentifier.IsUsableName(name))
                {
                    AddError(SqlParseErrorKind.InvalidName, nameSpan, name);
                }
                else if (!_names.Add(name))
                {
                    AddError(SqlParseErrorKind.DuplicateName, nameSpan, name);
                }

                // A marker with an unusable name still starts a block, so that what follows is checked as a block.
                ReadBlock(name, nameSpan, shape, preamble, index + 1, end);
            }
        }

        // "Name", or "Name -> shape".  What follows the name is reported whole when it is not an arrow and a shape.
        private (TextSpan Name, ResultShape? Shape) ReadName(SqlMarker marker)
        {
            var value = marker.ValueSpan;
            var arrow = text.IndexOf("->", value.Start, value.Length, StringComparison.Ordinal);
            if (arrow < 0)
            {
                return (value, null);
            }

            var nameEnd = arrow;
            while (nameEnd > value.Start && char.IsWhiteSpace(text[nameEnd - 1]))
            {
                nameEnd--;
            }

            ResultShape? shape = null;
            if (SettingValue.TryReadChoice<ResultShape>(text.AsSpan(arrow + 2, value.End - arrow - 2), out var read))
            {
                shape = read;
            }
            else
            {
                AddError(
                    SqlParseErrorKind.InvalidMarkerValue,
                    TextSpan.FromBounds(arrow, value.End),
                    SqlMarkerReader.Describe(text, marker)
                );
            }

            return (TextSpan.FromBounds(value.Start, nameEnd), shape);
        }

        private SqlMarkerScope ReadPreamble(int end)
        {
            var scope = new SqlMarkerScope(text, rules, _errors);
            var sqlReported = false;
            for (var index = 0; index < end; index++)
            {
                var lexeme = lexemes[index];
                if (SqlMarkerReader.Read(text, lexeme) is { } marker)
                {
                    if (marker.Kind == SqlMarkerKind.Dialect)
                    {
                        ReadDialect(marker);
                    }
                    else if (marker.Kind == SqlMarkerKind.Summary)
                    {
                        AddError(SqlParseErrorKind.SummaryBeforeFirstName, marker.Span);
                    }
                    else
                    {
                        scope.Read(marker, inQuery: false, inPreamble: true);
                    }
                }
                else if (!sqlReported && lexeme.GetContentSpan(text) is { } content)
                {
                    AddError(SqlParseErrorKind.SqlBeforeFirstName, content);
                    sqlReported = true;
                }
            }

            return scope;
        }

        // preamble is null for a file with no name marker: the file is one query and its own preamble.
        private void ReadBlock(
            string name,
            TextSpan nameSpan,
            ResultShape? shape,
            SqlMarkerScope? preamble,
            int start,
            int end
        )
        {
            // Created at the first marker the scope takes: most queries have none.
            SqlMarkerScope? scope = null;
            var summary = new List<string>();
            var lastContent = FindLastContent(start, end);
            for (var index = start; index < end; index++)
            {
                if (SqlMarkerReader.Read(text, lexemes[index]) is not { } marker)
                {
                    continue;
                }

                if (lastContent >= 0 && index > lastContent)
                {
                    // A marker comes before the SQL it describes.  One after the block's last SQL would be taken by a
                    // reader to belong to the next block, so it is rejected and not applied.  A dialect marker there
                    // is past the header by definition, and is reported as misplaced too: it belongs at the top of
                    // the file, not above the next SQL.
                    AddError(SqlParseErrorKind.MarkerAtEndOfBlock, marker.Span);
                    if (marker.Kind == SqlMarkerKind.Dialect)
                    {
                        ReadDialect(marker);
                    }
                }
                else if (marker.Kind == SqlMarkerKind.Dialect)
                {
                    ReadDialect(marker);
                }
                else if (marker.Kind == SqlMarkerKind.Summary)
                {
                    if (!marker.ValueSpan.IsEmpty)
                    {
                        summary.Add(text.Substring(marker.ValueSpan.Start, marker.ValueSpan.Length));
                    }
                }
                else if (marker.Kind != SqlMarkerKind.Name)
                {
                    (scope ??= new SqlMarkerScope(text, rules, _errors)).Read(
                        marker,
                        inQuery: true,
                        inPreamble: preamble is null
                    );
                }
            }

            if (lastContent < 0)
            {
                AddError(SqlParseErrorKind.EmptyBlock, nameSpan);
                return;
            }

            // A query that has a list uses it whole.  One that has none uses the preamble's.
            var list = scope?.Generator.Parameters ?? preamble?.Generator.Parameters;
            var keepComments = list is { } given && (given & GeneratorParameters.KeepComments) != 0;
            var sql = SqlTextBuilder.Build(text, lexemes, start, end, keepComments);
            var scanned = TokenScanner.Scan(sql.Text, scope?.IgnoredTokens ?? SqlMarkerScope.NoNames);
            foreach (var error in scanned.Errors)
            {
                _errors.Add(error with { Span = sql.ToSourceSpan(error.Span) });
            }

            // A query without markers has no scope, and so no defaults and no declarations.
            var tokenDefaults = scope?.TokenDefaults ?? [];
            var tokens = SqlTokenList.Create(text, sql, scanned.Occurrences, tokenDefaults, _errors);
            var parameters = SqlParameterList.Create(
                rules,
                sql,
                scanned.Occurrences,
                tokens,
                tokenDefaults,
                scope?.Declarations ?? [],
                _errors
            );
            _blocks.Add(
                new SqlBlock(
                    name,
                    nameSpan,
                    summary.Count == 0 ? null : string.Join(" ", summary),
                    shape,
                    keepComments,
                    list is { } decided ? (decided & GeneratorParameters.NoTokenValidation) == 0 : null,
                    scanned.Segments,
                    tokens,
                    parameters
                )
            );
        }

        // The place is checked first: a marker in the wrong place is reported as that, whatever it names.
        private void ReadDialect(SqlMarker marker)
        {
            if (marker.Span.Start >= headerEnd)
            {
                AddError(SqlParseErrorKind.MisplacedDialect, marker.Span);
                return;
            }

            var place = marker.ValueSpan.IsEmpty ? marker.Span : marker.ValueSpan;
            if (!SqlDialectMarker.TryRead(text, marker, out var dialect))
            {
                AddError(SqlParseErrorKind.InvalidMarkerValue, place, SqlDialectMarker.Describe(text, marker));
            }
            else if (_dialect is { } existing && existing != dialect)
            {
                AddError(SqlParseErrorKind.ConflictingSettings, place, SqlDialectMarker.Describe(text, marker));
            }
            else
            {
                _dialect = dialect;
            }
        }

        // The index of the last lexeme from start up to end that holds SQL, or -1 when none does.
        private int FindLastContent(int start, int end)
        {
            for (var index = end - 1; index >= start; index--)
            {
                if (lexemes[index].GetContentSpan(text) is not null)
                {
                    return index;
                }
            }

            return -1;
        }

        private void AddError(SqlParseErrorKind kind, TextSpan span, params string[] arguments) =>
            _errors.Add(SqlParseError.Create(kind, span, arguments));
    }
}
