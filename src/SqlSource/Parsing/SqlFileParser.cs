using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Parsing.Markers;
using SqlSource.Parsing.Tokens;
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
    /// header replaces it whole for the text after the marker.  <paramref name="commentsWanted" /> says that a level
    /// the parser cannot see, a property, the metadata of the file's item or the attribute of a type that claims the
    /// file, may ask for <c>keep-comments</c>: the SQL of a query with no list of its own is then built with its
    /// comments too.
    /// </summary>
    public static SqlFileParseResult Parse(
        string text,
        string fileName,
        SqlDialectChoice dialect = default,
        bool commentsWanted = false
    )
    {
        var lexer = new SqlLexer(text, SqlDialectRules.For(dialect));
        var headerEnd = SqlDialectMarker.Apply(lexer, text);
        var lexed = lexer.ReadToEnd();
        return lexed.Error is null
            ? new Parser(text, fileName, lexed.Lexemes, headerEnd, lexer.Rules, commentsWanted).Run()
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
        SqlDialectRules rules,
        bool commentsWanted
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
                    SqlMarkerReader.Describe(text, marker),
                    string.Empty
                );
            }

            return (TextSpan.FromBounds(value.Start, nameEnd), shape);
        }

        private SqlMarkerScope ReadPreamble(int end)
        {
            var scope = new SqlMarkerScope(text, rules, SqlScopeKind.Preamble, _errors);
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
                        scope.Read(marker);
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

        // preamble is null for a file with no name marker: the file is one query and its own preamble.  The three
        // steps and the two checks between them stay in this order: an error is added where it is found, and two
        // errors at one place are reported in the order they were added.
        private void ReadBlock(
            string name,
            TextSpan nameSpan,
            ResultShape? shape,
            SqlMarkerScope? preamble,
            int start,
            int end
        )
        {
            var lastContent = FindLastContent(start, end);
            var (scope, summary) = ReadBlockMarkers(
                preamble is null ? SqlScopeKind.WholeFile : SqlScopeKind.Query,
                start,
                end,
                lastContent
            );
            if (lastContent < 0)
            {
                AddError(SqlParseErrorKind.EmptyBlock, nameSpan);
                return;
            }

            var query = BuildQuery(scope, start, end);
            if (scope?.InputModel is { } inputModel && query.Parameters.Count == 0)
            {
                AddError(SqlParseErrorKind.InputModelWithoutParameters, inputModel.Marker.Span);
            }

            var (markers, kept, keptErrors) = ResolveSettings(scope, preamble, query, start, end);
            _blocks.Add(
                new SqlBlock(
                    name,
                    nameSpan,
                    summary,
                    shape,
                    query.Segments,
                    kept,
                    query.Tokens,
                    query.Parameters,
                    markers,
                    ModelNameOf(scope?.InputModel),
                    ModelNameOf(scope?.OutputModel),
                    ColumnMarkersOf(scope),
                    keptErrors
                )
            );
        }

        // Step one: the block's markers.  The scope is the query's, or null when no marker was handed to one, as
        // for most queries; the summary is its lines joined, or null.
        private (SqlMarkerScope? Scope, string? Summary) ReadBlockMarkers(
            SqlScopeKind kind,
            int start,
            int end,
            int lastContent
        )
        {
            SqlMarkerScope? scope = null;
            var summary = new List<string>();
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
                    (scope ??= new SqlMarkerScope(text, rules, kind, _errors)).Read(marker);
                }
            }

            return (scope, summary.Count == 0 ? null : string.Join(" ", summary));
        }

        // Step two: the query.  The stripped SQL is what the query is: its tokens, its parameters and its hash come
        // from it.
        private BuiltQuery BuildQuery(SqlMarkerScope? scope, int start, int end)
        {
            var sql = SqlTextBuilder.Build(text, lexemes, start, end, keepComments: false);
            var scanned = TokenScanner.Scan(sql.Text, scope?.IgnoredTokens ?? SqlMarkerScope.NoNames);
            foreach (var error in scanned.Errors)
            {
                _errors.Add(error with { Span = sql.ToSourceSpan(error.Span) });
            }

            FindHiddenTokenEnds(sql, start, end);

            // A query without markers has no scope, and so no defaults and no declarations.
            var tokenDefaults = scope?.TokenDefaults ?? [];
            var tokens = SqlTokenList.Create(text, sql, scanned.Occurrences, tokenDefaults, _errors);

            // A variable is written in the SQL where it is declared, so SQL without parameters declares none.
            var variables =
                sql.Parameters.Length == 0 ? null : SqlDeclaredVariables.Find(text, lexemes, start, end, rules);
            var parameters = SqlParameterList.Create(
                rules,
                new SqlScannedQuery(sql, scanned.Occurrences, tokens, variables),
                tokenDefaults,
                scope?.Declarations ?? [],
                _errors
            );

            return new BuiltQuery(sql, scanned.Segments, scanned.Occurrences, tokens, parameters);
        }

        // A comment is gone from the SQL that the tokens are found in, and so is any "}}" it holds: in a default, that
        // is not the end of the token.  Only a comment that holds "}}" is looked at further, and most hold none.  The
        // comments come in order, and so do their places in the SQL, so one finder reads the SQL once for them all.
        private void FindHiddenTokenEnds(SqlBlockText sql, int start, int end)
        {
            OpenDefaultFinder? finder = null;
            for (var index = start; index < end; index++)
            {
                var lexeme = lexemes[index];
                if (
                    lexeme.Kind is SqlLexemeKind.LineComment or SqlLexemeKind.BlockComment
                    && text.IndexOf("}}", lexeme.Span.Start, lexeme.Span.Length, StringComparison.Ordinal) >= 0
                    && (finder ??= new OpenDefaultFinder(sql.Text)).At(sql.ToOutput(lexeme.Span.Start)) is { } name
                )
                {
                    AddError(SqlParseErrorKind.TokenCrossesComment, lexeme.Span, name);
                }
            }
        }

        // Step three: what the parser can resolve of the settings.  A query that has a list uses it whole, so it
        // alone says whether the comments can be wanted.  A query without one may get the parameter from a level the
        // parser does not see.
        private (
            SettingsLevel Markers,
            EquatableArray<SqlSegment>? Kept,
            EquatableArray<SqlParseError> KeptErrors
        ) ResolveSettings(SqlMarkerScope? scope, SqlMarkerScope? preamble, BuiltQuery query, int start, int end)
        {
            var markers = (scope?.Level ?? SettingsLevel.None).Over(preamble?.Level ?? SettingsLevel.None);
            var keepsComments = markers.Parameters is { } list
                ? (list & GeneratorParameters.KeepComments) != 0
                : commentsWanted;

            if (!keepsComments)
            {
                return (markers, null, EquatableArray<SqlParseError>.Empty);
            }

            var (kept, keptErrors) = BuildKept(start, end, query, scope?.IgnoredTokens ?? SqlMarkerScope.NoNames);
            return (markers, kept, keptErrors);
        }

        // The SQL with its comments, or null when it is the stripped SQL: one form then serves.  A token can stand
        // in a comment, so this form is scanned too, and a comment can open a default that the SQL after it closes or
        // stand in a token's head.
        // These errors are not the file's: a type that claims the file may not keep the comments, so they go with the
        // block, for the type that emits this form to report.  An error of the stripped form is the file's already,
        // and a file with one has no blocks, so only the comments' own errors are seen.
        private (EquatableArray<SqlSegment>? Segments, EquatableArray<SqlParseError> Errors) BuildKept(
            int start,
            int end,
            BuiltQuery query,
            ISet<string> ignoredTokens
        )
        {
            var kept = SqlTextBuilder.Build(text, lexemes, start, end, keepComments: true);
            if (string.Equals(kept.Text, query.Sql.Text, StringComparison.Ordinal))
            {
                return (null, EquatableArray<SqlParseError>.Empty);
            }

            var scanned = TokenScanner.Scan(kept.Text, ignoredTokens);

            // Created at the first error: most kept forms have none.
            ImmutableArray<SqlParseError>.Builder? errors = null;
            foreach (var error in scanned.Errors)
            {
                (errors ??= ImmutableArray.CreateBuilder<SqlParseError>()).Add(
                    error with
                    {
                        Span = kept.ToSourceSpan(error.Span),
                    }
                );
            }

            FindCommentsInHeads(query, ignoredTokens, start, end, ref errors);
            FindOpenedDefaults(kept, start, end, ref errors);
            return (
                scanned.Segments,
                errors is null
                    ? EquatableArray<SqlParseError>.Empty
                    : new EquatableArray<SqlParseError>(errors.ToImmutable())
            );
        }

        // A comment in a token's head, from its braces to its colon or closing braces, is a blank where comments are
        // stripped, which a head may hold, and is text that ends the head where they are kept, so the token is gone
        // there.  An ignored token is read to its closing braces as any other is, so the text it takes in differs
        // too; it has no occurrence, so its heads are found by reading the SQL as the scanner does, and only for a
        // query that ignores a name.
        private void FindCommentsInHeads(
            BuiltQuery query,
            ISet<string> ignoredTokens,
            int start,
            int end,
            ref ImmutableArray<SqlParseError>.Builder? errors
        )
        {
            var sql = query.Sql;
            foreach (var occurrence in query.Occurrences)
            {
                _ = TokenScanner.TryReadOpener(sql.Text, occurrence.Span.Start, out _, out var after);
                FindCommentsInHead(sql, occurrence.Span.Start, after, occurrence.Name, start, end, ref errors);
            }

            if (ignoredTokens.Count == 0)
            {
                return;
            }

            var noCloseFrom = int.MaxValue;
            var index = sql.Text.IndexOf("{{", StringComparison.Ordinal);
            while (index >= 0)
            {
                var next = index + 1;
                if (TokenScanner.TryReadToken(sql.Text, index, ref noCloseFrom, out var name, out _, out var tokenEnd))
                {
                    if (ignoredTokens.Contains(name))
                    {
                        _ = TokenScanner.TryReadOpener(sql.Text, index, out _, out var after);
                        FindCommentsInHead(sql, index, after, name, start, end, ref errors);
                    }

                    next = tokenEnd;
                }

                index = next < sql.Text.Length ? sql.Text.IndexOf("{{", next, StringComparison.Ordinal) : -1;
            }
        }

        // The head runs from tokenStart up to after in the SQL.  A stripped comment always makes a head longer in the
        // file than in the SQL, so only such a head has the block's lexemes searched for the comment.
        private void FindCommentsInHead(
            SqlBlockText sql,
            int tokenStart,
            int after,
            string name,
            int start,
            int end,
            ref ImmutableArray<SqlParseError>.Builder? errors
        )
        {
            var headStart = sql.ToSourceSpan(new TextSpan(tokenStart, 1)).Start;
            var headEnd = sql.ToSourceSpan(new TextSpan(after, 1)).Start;
            if (headEnd - headStart == after - tokenStart)
            {
                return;
            }

            for (var index = start; index < end && lexemes[index].Span.Start < headEnd; index++)
            {
                var lexeme = lexemes[index];
                if (
                    lexeme.Kind is SqlLexemeKind.LineComment or SqlLexemeKind.BlockComment
                    && lexeme.Span.Start >= headStart
                )
                {
                    (errors ??= ImmutableArray.CreateBuilder<SqlParseError>()).Add(
                        SqlParseError.Create(SqlParseErrorKind.TokenCrossesComment, lexeme.Span, name)
                    );
                }
            }
        }

        // The mirror of FindHiddenTokenEnds: a comment that opens a default which the SQL after it closes is a token
        // only where the comments are kept, and its default takes the SQL up to those braces.  A default open at the
        // comment's end whose opener is before the comment is the same token in both forms.  Only a comment that holds
        // "{{" is looked at further, and the comments' places in the kept SQL only grow, so one finder reads it once.
        private void FindOpenedDefaults(
            SqlBlockText kept,
            int start,
            int end,
            ref ImmutableArray<SqlParseError>.Builder? errors
        )
        {
            OpenDefaultFinder? finder = null;
            for (var index = start; index < end; index++)
            {
                var lexeme = lexemes[index];
                if (
                    lexeme.Kind is not (SqlLexemeKind.LineComment or SqlLexemeKind.BlockComment)
                    || text.IndexOf("{{", lexeme.Span.Start, lexeme.Span.Length, StringComparison.Ordinal) < 0
                )
                {
                    continue;
                }

                var commentEnd = kept.ToOutput(lexeme.Span.End);
                if (
                    (finder ??= new OpenDefaultFinder(kept.Text)).At(commentEnd) is { } name
                    && finder.OpenerStart >= kept.ToOutput(lexeme.Span.Start)
                    && kept.Text.IndexOf("}}", commentEnd, StringComparison.Ordinal) >= 0
                )
                {
                    (errors ??= ImmutableArray.CreateBuilder<SqlParseError>()).Add(
                        SqlParseError.Create(SqlParseErrorKind.TokenCrossesComment, lexeme.Span, name)
                    );
                }
            }
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
                AddError(
                    SqlParseErrorKind.InvalidMarkerValue,
                    place,
                    SqlMarkerReader.Describe(text, marker),
                    string.Empty
                );
            }
            else if (_dialect is { } existing && existing != dialect)
            {
                AddError(SqlParseErrorKind.ConflictingSettings, place, SqlMarkerReader.Describe(text, marker));
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

        // The record is made only for a query that has the marker: the parser's allocation budget holds.
        private static SqlModelName? ModelNameOf((string Name, SqlMarker Marker)? model) =>
            model is { } found ? new SqlModelName(found.Name, found.Marker.Span) : null;

        // The array is made only for a query that has the marker: the parser's allocation budget holds.
        private static EquatableArray<SqlColumnMarker> ColumnMarkersOf(SqlMarkerScope? scope)
        {
            if (scope is null || scope.Columns.Count == 0)
            {
                return EquatableArray<SqlColumnMarker>.Empty;
            }

            var markers = ImmutableArray.CreateBuilder<SqlColumnMarker>(scope.Columns.Count);
            foreach (var column in scope.Columns)
            {
                markers.Add(new SqlColumnMarker(column.Name, column.IsNullable, column.Marker.Span));
            }

            return new EquatableArray<SqlColumnMarker>(markers.MoveToImmutable());
        }

        private void AddError(SqlParseErrorKind kind, TextSpan span, params string[] arguments) =>
            _errors.Add(SqlParseError.Create(kind, span, arguments));

        // What step two gives.
        private readonly record struct BuiltQuery(
            SqlBlockText Sql,
            EquatableArray<SqlSegment> Segments,
            EquatableArray<SqlTokenOccurrence> Occurrences,
            EquatableArray<SqlToken> Tokens,
            EquatableArray<SqlQueryParameter> Parameters
        );
    }
}
