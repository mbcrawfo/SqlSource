using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.Text;

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
            ? new Parser(text, fileName, lexed.Lexemes, headerEnd).Run()
            : new SqlFileParseResult(
                EquatableArray<SqlBlock>.Empty,
                new EquatableArray<SqlParseError>(ImmutableArray.Create(lexed.Error))
            );
    }

    private sealed class Parser(string text, string fileName, EquatableArray<SqlLexeme> lexemes, int headerEnd)
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
                    )
                )
                : new SqlFileParseResult(
                    new EquatableArray<SqlBlock>(_blocks.ToImmutableArray()),
                    EquatableArray<SqlParseError>.Empty
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

            ReadBlock(name, FileStart, new SqlGeneratorParameterScope(), 0, lexemes.Count);
        }

        private void ReadNamedBlocks(List<(int Index, SqlMarker Marker)> nameMarkers)
        {
            var preamble = ReadPreamble(nameMarkers[0].Index);
            for (var position = 0; position < nameMarkers.Count; position++)
            {
                var (index, marker) = nameMarkers[position];
                var end = position + 1 < nameMarkers.Count ? nameMarkers[position + 1].Index : lexemes.Count;
                var name = text.Substring(marker.ValueSpan.Start, marker.ValueSpan.Length);
                var nameSpan = name.Length == 0 ? marker.Span : marker.ValueSpan;
                if (!SqlIdentifier.IsUsableName(name))
                {
                    AddError(SqlParseErrorKind.InvalidName, nameSpan, name);
                }
                else if (!_names.Add(name))
                {
                    AddError(SqlParseErrorKind.DuplicateName, nameSpan, name);
                }

                // A marker with an unusable name still starts a block, so that what follows is checked as a block.
                ReadBlock(name, nameSpan, preamble, index + 1, end);
            }
        }

        private SqlGeneratorParameterScope ReadPreamble(int end)
        {
            var scope = new SqlGeneratorParameterScope();
            var sqlReported = false;
            for (var index = 0; index < end; index++)
            {
                var lexeme = lexemes[index];
                var marker = SqlMarkerReader.Read(text, lexeme);
                if (marker is { Kind: SqlMarkerKind.GeneratorParameters })
                {
                    scope.Read(text, marker.Value, _errors);
                }
                else if (marker is { Kind: SqlMarkerKind.Dialect })
                {
                    ReadDialect(marker.Value);
                }
                else if (marker is { Kind: SqlMarkerKind.Summary })
                {
                    AddError(SqlParseErrorKind.SummaryBeforeFirstName, marker.Value.Span);
                }
                else if (!sqlReported && lexeme.GetContentSpan(text) is { } content)
                {
                    AddError(SqlParseErrorKind.SqlBeforeFirstName, content);
                    sqlReported = true;
                }
            }

            return scope;
        }

        private void ReadBlock(string name, TextSpan nameSpan, SqlGeneratorParameterScope inherited, int start, int end)
        {
            var scope = new SqlGeneratorParameterScope();
            var summary = new List<string>();
            var lastContent = FindLastContent(start, end);
            for (var index = start; index < end; index++)
            {
                var marker = SqlMarkerReader.Read(text, lexemes[index]);
                if (marker is not null && lastContent >= 0 && index > lastContent)
                {
                    // A marker comes before the SQL it describes.  One after the block's last SQL would be taken by a
                    // reader to belong to the next block, so it is rejected and not applied.  A dialect marker there
                    // is past the header by definition, and is reported as misplaced too: it belongs at the top of
                    // the file, not above the next SQL.
                    AddError(SqlParseErrorKind.MarkerAtEndOfBlock, marker.Value.Span);
                    if (marker.Value.Kind == SqlMarkerKind.Dialect)
                    {
                        ReadDialect(marker.Value);
                    }
                }
                else if (marker is { Kind: SqlMarkerKind.GeneratorParameters })
                {
                    scope.Read(text, marker.Value, _errors);
                }
                else if (marker is { Kind: SqlMarkerKind.Dialect })
                {
                    ReadDialect(marker.Value);
                }
                else if (marker is { Kind: SqlMarkerKind.Summary, ValueSpan.IsEmpty: false })
                {
                    summary.Add(text.Substring(marker.Value.ValueSpan.Start, marker.Value.ValueSpan.Length));
                }
            }

            if (lastContent < 0)
            {
                AddError(SqlParseErrorKind.EmptyBlock, nameSpan);
                return;
            }

            var keepComments = inherited.KeepComments || scope.KeepComments;
            var sql = SqlTextBuilder.Build(text, lexemes, start, end, keepComments);
            HashSet<string> ignoredTokens = [.. inherited.IgnoredTokens, .. scope.IgnoredTokens];
            var scanned = TokenScanner.Scan(sql.Text, ignoredTokens);
            foreach (var error in scanned.Errors)
            {
                _errors.Add(error with { Span = sql.ToSourceSpan(error.Span) });
            }

            _blocks.Add(
                new SqlBlock(
                    name,
                    nameSpan,
                    summary.Count == 0 ? null : string.Join(" ", summary),
                    keepComments,
                    scope.TokenValidation ?? inherited.TokenValidation,
                    scanned.Segments
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
