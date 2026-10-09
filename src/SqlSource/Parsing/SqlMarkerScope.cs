using System;
using System.Collections.Generic;
using SqlSource.Settings;

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

    /// <summary>The names of a scope that has none.  Shared, and never changed.</summary>
    public static readonly ISet<string> NoNames = new HashSet<string>();

    private List<SqlTokenDefault>? _tokenDefaults;

    private HashSet<string>? _ignoredTokens;

    private List<SqlParameterDeclaration>? _declarations;

    private OutputKind? _output;

    private string? _database;

    private string? _inputModelSuffix;

    private string? _outputModelSuffix;

    private string? _modelNamespace;

    private ModelKind? _inputModelType;

    private ModelKind? _outputModelType;

    private CollectionKind? _collectionType;

    private string? _inputModel;

    private string? _outputModel;

    /// <summary>The scope's generator parameters.</summary>
    public SqlGeneratorParameterScope Generator { get; } = new();

    /// <summary>
    /// What the scope's markers say about the settings.  The shared empty level when they say nothing, and one
    /// instance for every read until another marker is applied, so that the queries of a file share their preamble's.
    /// </summary>
    public SettingsLevel Level
    {
        get => field ??= BuildLevel();
        private set;
    }

    /// <summary>The name a <c>-- input-model:</c> marker gives, with the first such marker, or null.</summary>
    public (string Name, SqlMarker Marker)? InputModel { get; private set; }

    /// <summary>The name an <c>-- output-model:</c> marker gives, or null.</summary>
    public string? OutputModel => _outputModel;

    /// <summary>The defaults the scope's <c>-- token:</c> markers give, in marker order, each name once.</summary>
    public IReadOnlyList<SqlTokenDefault> TokenDefaults => _tokenDefaults ?? (IReadOnlyList<SqlTokenDefault>)[];

    /// <summary>The names the scope's <c>-- token-ignore:</c> markers give, as written.</summary>
    public ISet<string> IgnoredTokens => _ignoredTokens ?? NoNames;

    /// <summary>What the scope's <c>-- param:</c> markers declare, in marker order, each parameter once.</summary>
    public IReadOnlyList<SqlParameterDeclaration> Declarations =>
        _declarations ?? (IReadOnlyList<SqlParameterDeclaration>)[];

    /// <summary>
    /// Reads one marker.  <paramref name="inQuery" /> and <paramref name="inPreamble" /> say what the scope is; both
    /// are true for a file with no <c>-- name:</c> marker, which is one query and its own preamble.
    /// </summary>
    public void Read(SqlMarker marker, bool inQuery, bool inPreamble)
    {
        Level = null!; // Built again at the next read of Level.
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
        else if (marker.Kind == SqlMarkerKind.TokenIgnore)
        {
            ReadTokenIgnore(marker);
        }
        else if (marker.Kind == SqlMarkerKind.Param)
        {
            ReadParam(marker);
        }
        else if (marker.Kind == SqlMarkerKind.Output)
        {
            SetChoice(ref _output, marker);
        }
        else if (marker.Kind == SqlMarkerKind.Database)
        {
            SetText(ref _database, marker, SettingValue.IsDatabaseName(Value(marker)));
        }
        else
        {
            ReadModelSetting(marker);
        }
    }

    private void ReadModelSetting(SqlMarker marker)
    {
        if (marker.Kind == SqlMarkerKind.InputModelSuffix)
        {
            SetText(ref _inputModelSuffix, marker, SettingValue.IsSuffix(Value(marker)));
        }
        else if (marker.Kind == SqlMarkerKind.OutputModelSuffix)
        {
            SetText(ref _outputModelSuffix, marker, SettingValue.IsSuffix(Value(marker)));
        }
        else if (marker.Kind == SqlMarkerKind.ModelNamespace)
        {
            SetText(ref _modelNamespace, marker, SettingValue.IsNamespace(Value(marker)));
        }
        else if (marker.Kind == SqlMarkerKind.InputModelType)
        {
            SetChoice(ref _inputModelType, marker);
        }
        else if (marker.Kind == SqlMarkerKind.OutputModelType)
        {
            SetChoice(ref _outputModelType, marker);
        }
        else if (marker.Kind == SqlMarkerKind.CollectionType)
        {
            SetChoice(ref _collectionType, marker);
        }
        else if (marker.Kind == SqlMarkerKind.InputModel)
        {
            SetText(ref _inputModel, marker, SettingValue.IsTypeName(Value(marker)));
            if (_inputModel is { } inputModel && InputModel is null)
            {
                InputModel = (inputModel, marker);
            }
        }
        else if (marker.Kind == SqlMarkerKind.OutputModel)
        {
            SetText(ref _outputModel, marker, SettingValue.IsTypeName(Value(marker)));
        }
    }

    private static bool IsAllowedInQuery(SqlMarkerKind kind) =>
        kind
            is SqlMarkerKind.GeneratorParameters
                or SqlMarkerKind.Token
                or SqlMarkerKind.TokenIgnore
                or SqlMarkerKind.Param
                or SqlMarkerKind.Database
                or SqlMarkerKind.Output
                or SqlMarkerKind.InputModelType
                or SqlMarkerKind.OutputModelType
                or SqlMarkerKind.CollectionType
                or SqlMarkerKind.InputModel
                or SqlMarkerKind.OutputModel;

    private static bool IsAllowedInPreamble(SqlMarkerKind kind) =>
        kind
            is SqlMarkerKind.GeneratorParameters
                or SqlMarkerKind.Database
                or SqlMarkerKind.Output
                or SqlMarkerKind.InputModelSuffix
                or SqlMarkerKind.OutputModelSuffix
                or SqlMarkerKind.ModelNamespace
                or SqlMarkerKind.InputModelType
                or SqlMarkerKind.OutputModelType
                or SqlMarkerKind.CollectionType;

    private SettingsLevel BuildLevel() =>
        Generator.Parameters is null
        && _output is null
        && _database is null
        && _inputModelSuffix is null
        && _outputModelSuffix is null
        && _modelNamespace is null
        && _inputModelType is null
        && _outputModelType is null
        && _collectionType is null
            ? SettingsLevel.None
            : new SettingsLevel
            {
                Parameters = Generator.Parameters,
                Output = _output,
                Database = _database,
                InputModelSuffix = _inputModelSuffix,
                OutputModelSuffix = _outputModelSuffix,
                ModelNamespace = _modelNamespace,
                InputModelType = _inputModelType,
                OutputModelType = _outputModelType,
                CollectionType = _collectionType,
            };

    private ReadOnlySpan<char> Value(SqlMarker marker) => text.AsSpan(marker.ValueSpan.Start, marker.ValueSpan.Length);

    // A value from a fixed list.  The same value twice is fine; another value is a conflict, and the first stands.
    private void SetChoice<T>(ref T? field, SqlMarker marker)
        where T : struct, Enum
    {
        if (!SettingValue.TryReadChoice<T>(Value(marker), out var value))
        {
            AddInvalid(marker);
        }
        else if (field is { } existing && !EqualityComparer<T>.Default.Equals(existing, value))
        {
            AddConflict(marker);
        }
        else
        {
            field = value;
        }
    }

    // A value that is text, taken as written.  It is compared as written, so "billing" and "Billing" are two.
    private void SetText(ref string? field, SqlMarker marker, bool isValid)
    {
        if (!isValid)
        {
            AddInvalid(marker);
            return;
        }

        var value = Value(marker).ToString();
        if (field is not null && !string.Equals(field, value, StringComparison.Ordinal))
        {
            AddConflict(marker);
        }
        else
        {
            field = value;
        }
    }

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

    // The value is one name, as a C# identifier is written.  A keyword is allowed: it is a token that is not one.
    private void ReadTokenIgnore(SqlMarker marker)
    {
        var name = text.Substring(marker.ValueSpan.Start, marker.ValueSpan.Length);
        if (!SqlIdentifier.IsValid(name))
        {
            AddInvalid(marker);
            return;
        }

        _ignoredTokens ??= [];
        _ = _ignoredTokens.Add(name);
    }

    // "@name [type] [null | not null]".  The type is what stands between the name and those words, as written.
    private void ReadParam(SqlMarker marker)
    {
        var start = marker.ValueSpan.Start;
        var end = marker.ValueSpan.End;
        var nameEnd = start + 1;
        while (nameEnd < end && SqlLexer.IsParameterNameCharacter(text[nameEnd]))
        {
            nameEnd++;
        }

        if (
            start == end
            || text[start] != rules.ParameterPrefix
            || nameEnd == start + 1
            || (nameEnd < end && !char.IsWhiteSpace(text[nameEnd]))
        )
        {
            AddInvalid(marker);
            return;
        }

        var name = text.Substring(start + 1, nameEnd - start - 1);
        var rest = text.AsSpan(nameEnd, end - nameEnd).Trim();
        bool? nullable = null;
        if (TryTakeLastWord(ref rest, "null"))
        {
            nullable = !TryTakeLastWord(ref rest, "not");
        }

        var declaration = new SqlParameterDeclaration(name, rest.IsEmpty ? null : rest.ToString(), nullable, marker);
        _declarations ??= [];
        foreach (var existing in _declarations)
        {
            if (!string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (
                !string.Equals(existing.Type, declaration.Type, StringComparison.Ordinal)
                || existing.Nullable != nullable
            )
            {
                AddConflict(marker);
            }

            return;
        }

        _declarations.Add(declaration);
    }

    // Takes word off the end of rest when it stands there as a word: alone, or after white space.
    private static bool TryTakeLastWord(ref ReadOnlySpan<char> rest, string word)
    {
        if (!rest.EndsWith(word.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var before = rest.Length - word.Length;
        if (before > 0 && !char.IsWhiteSpace(rest[before - 1]))
        {
            return false;
        }

        rest = rest.Slice(0, before).TrimEnd();
        return true;
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
