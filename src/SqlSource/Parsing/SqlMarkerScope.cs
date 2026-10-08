using System;
using System.Collections.Generic;

namespace SqlSource.Parsing;

/// <summary>
/// What the markers of one scope give: a file's preamble, or one query.  <see cref="SqlFileParser" /> hands it each
/// marker but <c>-- name:</c>, <c>-- summary:</c> and <c>-- dialect:</c>, which it reads itself.
/// </summary>
/// <remarks>
/// This is the one place that knows which marker is allowed where, and what a valid value of each is.  A marker that
/// is not allowed, not valid or in conflict with an earlier one of the scope is reported and not applied.
/// </remarks>
internal sealed class SqlMarkerScope(string text, SqlDialectRules rules, List<SqlParseError> errors)
{
    private const string InsideAQuery = "inside a query";

    private const string BeforeTheFirstQuery = "before the file's first query";

    private List<SqlTokenDefault>? _tokenDefaults;

    /// <summary>The scope's generator parameters.</summary>
    public SqlGeneratorParameterScope Generator { get; } = new();

    /// <summary>The defaults the scope's <c>-- token:</c> markers give, in marker order, each name once.</summary>
    public IReadOnlyList<SqlTokenDefault> TokenDefaults => _tokenDefaults ?? (IReadOnlyList<SqlTokenDefault>)[];

    /// <summary>
    /// Reads one marker.  <paramref name="inQuery" /> and <paramref name="inPreamble" /> say what the scope is; both
    /// are true for a file with no <c>-- name:</c> marker, which is one query and its own preamble.
    /// </summary>
    public void Read(SqlMarker marker, bool inQuery, bool inPreamble)
    {
        if (!((inQuery && IsAllowedInQuery(marker.Kind)) || (inPreamble && IsAllowedInPreamble(marker.Kind))))
        {
            errors.Add(
                SqlParseError.Create(
                    SqlParseErrorKind.MarkerNotAllowedHere,
                    marker.Span,
                    SqlMarkerReader.WordOf(marker.Kind),
                    IsAllowedInQuery(marker.Kind) ? InsideAQuery : BeforeTheFirstQuery
                )
            );
            return;
        }

        // -- name:, -- summary: and -- dialect: are read by the parser, which never hands them over.
        if (marker.Kind == SqlMarkerKind.GeneratorParameters)
        {
            Generator.Read(text, marker, errors);
        }
        else if (marker.Kind == SqlMarkerKind.Token)
        {
            ReadToken(marker);
        }
    }

    private static bool IsAllowedInQuery(SqlMarkerKind kind) =>
        kind is SqlMarkerKind.GeneratorParameters or SqlMarkerKind.Token;

    private static bool IsAllowedInPreamble(SqlMarkerKind kind) => kind is SqlMarkerKind.GeneratorParameters;

    // The value is exactly one token with a default, as the SQL would write it.  The default is lexed alone, as the
    // parameter list reads it later: a quote or a comment that does not close inside it would swallow what follows.
    private void ReadToken(SqlMarker marker)
    {
        var value = text.Substring(marker.ValueSpan.Start, marker.ValueSpan.Length);
        if (
            !TokenScanner.TryReadToken(value, 0, out var name, out var defaultSpan, out var end)
            || end != value.Length
            || defaultSpan is not { } place
        )
        {
            AddInvalid(marker);
            return;
        }

        if (SqlIdentifier.IsReservedKeyword(name))
        {
            errors.Add(SqlParseError.Create(SqlParseErrorKind.ReservedTokenName, marker.ValueSpan, name));
            return;
        }

        var defaultText = value.Substring(place.Start, place.Length);
        if (SqlLexer.Lex(defaultText, rules).Error is not null)
        {
            AddInvalid(marker);
            return;
        }

        _tokenDefaults ??= [];
        foreach (var existing in _tokenDefaults)
        {
            if (!string.Equals(existing.Name, name, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.Equals(existing.Text, defaultText, StringComparison.Ordinal))
            {
                AddConflict(marker);
            }

            return;
        }

        _tokenDefaults.Add(new SqlTokenDefault(name, defaultText, marker.ValueSpan.Start + place.Start, marker));
    }

    // At the value, or at the whole marker when it has none.
    private void AddInvalid(SqlMarker marker) => Add(SqlParseErrorKind.InvalidMarkerValue, marker);

    private void AddConflict(SqlMarker marker) => Add(SqlParseErrorKind.ConflictingSettings, marker);

    private void Add(SqlParseErrorKind kind, SqlMarker marker) =>
        errors.Add(
            SqlParseError.Create(
                kind,
                marker.ValueSpan.IsEmpty ? marker.Span : marker.ValueSpan,
                SqlMarkerReader.Describe(text, marker)
            )
        );
}
