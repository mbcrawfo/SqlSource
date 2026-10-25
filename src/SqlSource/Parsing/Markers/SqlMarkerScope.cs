using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Parsing.Tokens;
using SqlSource.Settings;
using SqlSource.Settings.ClrTypes;
using SqlSource.Settings.TypeOverrides;

namespace SqlSource.Parsing.Markers;

/// <summary>
/// What the markers of one scope give: a file's preamble, or one query.  <see cref="SqlFileParser" /> hands it each
/// marker but <c>-- name:</c>, <c>-- summary:</c> and <c>-- dialect:</c>, which it reads itself.
/// </summary>
/// <remarks>
/// This is the one place that knows which marker is allowed where, and what a valid value of each is.  A marker that
/// is not allowed, not valid or in conflict with an earlier one of the scope is reported and not applied.
/// </remarks>
internal sealed class SqlMarkerScope(
    string text,
    SqlDialectRules rules,
    SqlScopeKind scopeKind,
    List<SqlParseError> errors
)
{
    private const string InsideAQuery = "inside a query";

    private const string BeforeTheFirstQuery = "before the file's first query";

    // The word that takes a key out of the map of the levels below, ignoring case, as the type override reads it.
    private const string Default = "default";

    /// <summary>The names of a scope that has none.  Shared by every parse, so it cannot be written to.</summary>
    public static readonly ISet<string> NoNames = ImmutableHashSet<string>.Empty;

    private List<SqlTokenDefault>? _tokenDefaults;

    private HashSet<string>? _ignoredTokens;

    private List<SqlParameterDeclaration>? _declarations;

    private List<SqlColumnDeclaration>? _columns;

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

    // The entries of the file's engine, keyed by its rules: sorted by key as TypeOverrideMap keeps a map, each key
    // once.
    private List<TypeOverride>? _typeOverrides;

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

    /// <summary>
    /// The name an <c>-- input-model:</c> marker gives, with the first marker that set it, or null.
    /// </summary>
    public (string Name, SqlMarker Marker)? InputModel { get; private set; }

    /// <summary>
    /// The name an <c>-- output-model:</c> marker gives, with the first marker that set it, or null.
    /// </summary>
    public (string Name, SqlMarker Marker)? OutputModel { get; private set; }

    /// <summary>The defaults the scope's <c>-- token:</c> markers give, in marker order, each name once.</summary>
    public IReadOnlyList<SqlTokenDefault> TokenDefaults => _tokenDefaults ?? (IReadOnlyList<SqlTokenDefault>)[];

    /// <summary>The names the scope's <c>-- token-ignore:</c> markers give, as written.</summary>
    public ISet<string> IgnoredTokens => _ignoredTokens ?? NoNames;

    /// <summary>What the scope's <c>-- param:</c> markers declare, in marker order, each parameter once.</summary>
    public IReadOnlyList<SqlParameterDeclaration> Declarations =>
        _declarations ?? (IReadOnlyList<SqlParameterDeclaration>)[];

    /// <summary>What the scope's <c>-- column:</c> markers say, in marker order, each column once.</summary>
    public IReadOnlyList<SqlColumnDeclaration> Columns => _columns ?? (IReadOnlyList<SqlColumnDeclaration>)[];

    /// <summary>
    /// Reads one marker.  The scope takes it or reports it by what it is the scope of.
    /// </summary>
    public void Read(SqlMarker marker)
    {
        Level = null!; // Built again at the next read of Level.
        if (!IsAllowed(marker.Kind))
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

        switch (marker.Kind)
        {
            case SqlMarkerKind.GeneratorParameters:
                Generator.Read(text, marker, errors);
                break;
            case SqlMarkerKind.Token:
                ReadToken(marker);
                break;
            case SqlMarkerKind.TokenIgnore:
                ReadTokenIgnore(marker);
                break;
            case SqlMarkerKind.Param:
                ReadParam(marker);
                break;
            case SqlMarkerKind.Column:
                ReadColumn(marker);
                break;
            case SqlMarkerKind.Output:
                SetChoice(ref _output, marker);
                break;
            case SqlMarkerKind.Database:
                SetText(ref _database, marker, SettingValue.IsDatabaseName(Value(marker)));
                break;
            case SqlMarkerKind.TypeOverride:
                ReadTypeOverride(marker);
                break;
            case SqlMarkerKind.InputModelSuffix:
                SetText(ref _inputModelSuffix, marker, SettingValue.IsSuffix(Value(marker)));
                break;
            case SqlMarkerKind.OutputModelSuffix:
                SetText(ref _outputModelSuffix, marker, SettingValue.IsSuffix(Value(marker)));
                break;
            case SqlMarkerKind.ModelNamespace:
                SetText(ref _modelNamespace, marker, SettingValue.IsQualifiedName(Value(marker)));
                break;
            case SqlMarkerKind.InputModelType:
                SetChoice(ref _inputModelType, marker);
                break;
            case SqlMarkerKind.OutputModelType:
                SetChoice(ref _outputModelType, marker);
                break;
            case SqlMarkerKind.CollectionType:
                SetChoice(ref _collectionType, marker);
                break;
            case SqlMarkerKind.InputModel:
                SetText(ref _inputModel, marker, SettingValue.IsQualifiedName(Value(marker)));
                if (_inputModel is { } inputModel && InputModel is null)
                {
                    InputModel = (inputModel, marker);
                }

                break;
            case SqlMarkerKind.OutputModel:
                SetText(ref _outputModel, marker, SettingValue.IsQualifiedName(Value(marker)));
                if (_outputModel is { } outputModel && OutputModel is null)
                {
                    OutputModel = (outputModel, marker);
                }

                break;
            case SqlMarkerKind.Name:
            case SqlMarkerKind.Summary:
            case SqlMarkerKind.Dialect:
            default:
                // The parser reads the first three itself and no scope allows them, so only an allowed kind with no
                // case reaches this, which a test of every kind catches.
                throw new InvalidOperationException($"No reading of the marker {marker.Kind}.");
        }
    }

    private bool IsAllowed(SqlMarkerKind kind) =>
        scopeKind switch
        {
            SqlScopeKind.Preamble => IsAllowedInPreamble(kind),
            SqlScopeKind.Query => IsAllowedInQuery(kind),
            SqlScopeKind.WholeFile => IsAllowedInQuery(kind) || IsAllowedInPreamble(kind),
            _ => false,
        };

    internal static bool IsAllowedInQuery(SqlMarkerKind kind) =>
        kind
            is SqlMarkerKind.GeneratorParameters
                or SqlMarkerKind.Token
                or SqlMarkerKind.TokenIgnore
                or SqlMarkerKind.Param
                or SqlMarkerKind.Column
                or SqlMarkerKind.Database
                or SqlMarkerKind.Output
                or SqlMarkerKind.InputModelType
                or SqlMarkerKind.OutputModelType
                or SqlMarkerKind.CollectionType
                or SqlMarkerKind.InputModel
                or SqlMarkerKind.OutputModel
                or SqlMarkerKind.TypeOverride;

    internal static bool IsAllowedInPreamble(SqlMarkerKind kind) =>
        kind
            is SqlMarkerKind.GeneratorParameters
                or SqlMarkerKind.Database
                or SqlMarkerKind.Output
                or SqlMarkerKind.InputModelSuffix
                or SqlMarkerKind.OutputModelSuffix
                or SqlMarkerKind.ModelNamespace
                or SqlMarkerKind.InputModelType
                or SqlMarkerKind.OutputModelType
                or SqlMarkerKind.CollectionType
                or SqlMarkerKind.TypeOverride;

    private SettingsLevel BuildLevel()
    {
        if (
            Generator.Parameters is null
            && _output is null
            && _database is null
            && _inputModelSuffix is null
            && _outputModelSuffix is null
            && _modelNamespace is null
            && _inputModelType is null
            && _outputModelType is null
            && _collectionType is null
            && _typeOverrides is null
        )
        {
            return SettingsLevel.None;
        }

        // The entries are the file's engine's alone.
        var typeOverrides = BuildTypeOverrides();
        var isSqlServer = Engine == DatabaseEngine.SqlServer;
        return new SettingsLevel
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
            TypeOverrides = isSqlServer ? null : typeOverrides,
            SqlServerTypeOverrides = isSqlServer ? typeOverrides : null,
        };
    }

    // The engine of the file, whose rules read its type override's keys and whose entries its markers give.
    private DatabaseEngine Engine => SqlDescribableDialects.EngineOf(rules.Dialect);

    private EquatableArray<TypeOverride>? BuildTypeOverrides() =>
        _typeOverrides is null ? null : new EquatableArray<TypeOverride>(_typeOverrides.ToImmutableArray());

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

    // One entry, with white space allowed around its "=", its key read by the file's engine: a key that engine does
    // not read is not valid, though the other reads it.  The entries of a scope add up, one for each key; the same
    // type twice is fine, and another type for a key is a conflict, the first standing.  The types are compared as
    // their text, so int and System.Int32 are two.  A file's header is the place of its preamble's entries.
    private void ReadTypeOverride(SqlMarker marker)
    {
        var place = scopeKind == SqlScopeKind.Preamble ? SettingPlace.FileHeader : SettingPlace.Query;
        if (!TypeOverrideGrammar.TryReadEntry(Value(marker), spacesAroundEquals: true, place, Engine, out var entry))
        {
            AddInvalid(marker, TypeOverrideGrammar.BracketHelp(Value(marker)));
            return;
        }

        _typeOverrides ??= [];
        var at = TypeOverrideMap.IndexOf(_typeOverrides, entry!.Key);
        if (at < 0)
        {
            _typeOverrides.Insert(~at, entry);
        }
        else if (!string.Equals(_typeOverrides[at].Type, entry.Type, StringComparison.Ordinal))
        {
            AddConflict(marker);
        }
    }

    // The value is exactly one token with a default, as the SQL would write it.  The default is lexed alone, as the
    // parameter list reads it later: a quote or a comment that does not close inside it would swallow what follows.
    // Two defaults are compared without their comments, as a default in the SQL is read.
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
        var lexed = SqlLexer.Lex(defaultText, rules);
        if (lexed.Error is not null)
        {
            AddInvalid(marker);
            return;
        }

        var defaultValue = WithoutComments(defaultText, lexed.Lexemes);

        _tokenDefaults ??= [];
        foreach (var existing in _tokenDefaults)
        {
            if (!string.Equals(existing.Name, name, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.Equals(existing.Value, defaultValue, StringComparison.Ordinal))
            {
                AddConflict(marker);
            }

            return;
        }

        _tokenDefaults.Add(
            new SqlTokenDefault(name, defaultText, defaultValue, marker.ValueSpan.Start + place.Start, marker)
        );
    }

    // The default as one in the SQL is read: built without comments by the builder of a query's SQL, then trimmed as
    // the token scanner trims it.  A default without a comment is its own text and costs nothing more.
    private static string WithoutComments(string defaultText, EquatableArray<SqlLexeme> lexemes)
    {
        for (var index = 0; index < lexemes.Count; index++)
        {
            if (lexemes[index].Kind is SqlLexemeKind.LineComment or SqlLexemeKind.BlockComment)
            {
                var built = SqlTextBuilder.Build(defaultText, lexemes, 0, lexemes.Count, keepComments: false).Text;
                var trimmed = SqlText.Trim(built, 0, built.Length);
                return trimmed.Length == built.Length ? built : built.Substring(trimmed.Start, trimmed.Length);
            }
        }

        return defaultText;
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

    // "@name [type] [null | not null] [-> CSharpType]".  The type is what stands between the name and those words, as
    // written.  The C# type is a ClrTypeName, and never default, since no level below a query maps a parameter.
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
        var rest = text.AsSpan(nameEnd, end - nameEnd);
        string? clrType = null;
        if (TrySplitArrow(rest, out var before, out var after))
        {
            if (
                after.Equals(Default.AsSpan(), StringComparison.OrdinalIgnoreCase)
                || !ClrTypeName.TryParse(after, out var parsed)
            )
            {
                AddInvalid(marker);
                return;
            }

            clrType = parsed!.Text;
            rest = before;
        }

        rest = rest.Trim();
        bool? nullable = null;
        if (TryTakeLastWord(ref rest, "null"))
        {
            nullable = !TryTakeLastWord(ref rest, "not");
        }

        var declaration = new SqlParameterDeclaration(
            name,
            rest.IsEmpty ? null : rest.ToString(),
            nullable,
            clrType,
            marker
        );
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
                || !string.Equals(existing.ClrType, clrType, StringComparison.Ordinal)
            )
            {
                AddConflict(marker);
            }

            return;
        }

        _declarations.Add(declaration);
    }

    // "name null" or "name not null".  The name is one word, or a double-quoted name, in which "" is one quote, for a
    // name with white space or one that is a word of nullability.  The same column twice, ignoring case, with the same
    // nullability is fine; with another it is a conflict, the first standing.
    private void ReadColumn(SqlMarker marker)
    {
        var rest = text.AsSpan(marker.ValueSpan.Start, marker.ValueSpan.Length);
        if (!TryTakeLastWord(ref rest, "null"))
        {
            AddInvalid(marker);
            return;
        }

        var nullable = !TryTakeLastWord(ref rest, "not");
        if (ReadColumnName(rest) is not { } name)
        {
            AddInvalid(marker);
            return;
        }

        _columns ??= [];
        foreach (var existing in _columns)
        {
            if (!string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (existing.IsNullable != nullable)
            {
                AddConflict(marker);
            }

            return;
        }

        _columns.Add(new SqlColumnDeclaration(name, nullable, marker));
    }

    // The name a -- column: marker writes, without its quotes, or null when it is empty, has white space or a quote
    // outside quotes, or has a quote inside them that is not doubled.
    private static string? ReadColumnName(ReadOnlySpan<char> written)
    {
        if (written.Length == 0)
        {
            return null;
        }

        if (written[0] != '"')
        {
            foreach (var character in written)
            {
                if (character == '"' || char.IsWhiteSpace(character))
                {
                    return null;
                }
            }

            return written.ToString();
        }

        if (written.Length < 3 || written[written.Length - 1] != '"')
        {
            return null;
        }

        var inner = written.Slice(1, written.Length - 2);
        var quotes = 0;
        var index = 0;
        while (index < inner.Length)
        {
            if (inner[index] != '"')
            {
                index++;
            }
            else if (index + 1 < inner.Length && inner[index + 1] == '"')
            {
                quotes++;
                index += 2;
            }
            else
            {
                return null;
            }
        }

        return quotes == 0 ? inner.ToString() : inner.ToString().Replace("\"\"", "\"");
    }

    // Splits a marker's value at its first "->" that starts it or stands after white space, outside double quotes, so
    // that a quoted name may hold one: before is the text before it, as written, and after the text after it,
    // trimmed.  False, with both empty, for a value without one.
    private static bool TrySplitArrow(
        ReadOnlySpan<char> value,
        out ReadOnlySpan<char> before,
        out ReadOnlySpan<char> after
    )
    {
        var quoted = false;
        for (var index = 0; index < value.Length - 1; index++)
        {
            var character = value[index];
            if (character == '"')
            {
                quoted = !quoted;
            }
            else if (
                !quoted
                && character == '-'
                && value[index + 1] == '>'
                && (index == 0 || char.IsWhiteSpace(value[index - 1]))
            )
            {
                before = value.Slice(0, index);
                after = value.Slice(index + 2).Trim();
                return true;
            }
        }

        before = default;
        after = default;
        return false;
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

    // At the value, or at the whole marker when it has none, with the help that ends the message.
    private void AddInvalid(SqlMarker marker, string help = "") =>
        errors.Add(
            SqlParseError.Create(
                SqlParseErrorKind.InvalidMarkerValue,
                PlaceOf(marker),
                SqlMarkerReader.Describe(text, marker),
                help
            )
        );

    private void AddConflict(SqlMarker marker) =>
        errors.Add(
            SqlParseError.Create(
                SqlParseErrorKind.ConflictingSettings,
                PlaceOf(marker),
                SqlMarkerReader.Describe(text, marker)
            )
        );

    private static TextSpan PlaceOf(SqlMarker marker) => marker.ValueSpan.IsEmpty ? marker.Span : marker.ValueSpan;
}
