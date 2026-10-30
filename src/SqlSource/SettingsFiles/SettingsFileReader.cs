using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Parsing;
using SqlSource.Settings;
using SqlSource.Snapshot.Reading;

namespace SqlSource.SettingsFiles;

/// <summary>
/// Reads a settings file: JSON with comments and trailing commas, whose top level is one object of the keys of
/// <see cref="SettingsFileKeys" />.  It never throws, whatever the text: a file a user can write must not cost every
/// type its generated code.
/// </summary>
/// <remarks>
/// Two passes, as <see cref="SidecarReader" /> reads a sidecar.  The first checks that the text is one JSON value
/// nested at most <see cref="SidecarTokenizer.MaxDepth" /> levels; a text that is not, or whose value is not an object,
/// gives that one error and nothing else.  The second walks the object's members, trusting the grammar, and keeps the
/// raw value of each setting for <see cref="SettingsLevelReader" />, which alone says whether a value is valid.  The
/// errors are sorted by position, whatever order they are found in.
/// </remarks>
internal static class SettingsFileReader
{
    // The number of containers around a value of the top level: the object.
    private const int MemberDepth = 1;

    private static readonly int SettingCount = Enum.GetValues(typeof(SettingId)).Length;

    /// <summary>
    /// Reads <paramref name="text" />, the text of a settings file without a byte order mark.  The entries of the type
    /// override, the column types and the library types carry <paramref name="place" />.
    /// </summary>
    public static SettingsFile Read(string text, SettingPlace place)
    {
        if (CheckGrammar(text) is { } invalid)
        {
            return new SettingsFile(
                null,
                null,
                SettingsLevel.None,
                EquatableArray<LibraryMember>.Empty,
                new EquatableArray<SettingsFileError>(ImmutableArray.Create(invalid))
            );
        }

        var errors = new List<SettingsFileError>();
        var values = new SettingsFileValue?[SettingCount];
        var walk = new Walk(text, errors, values);
        walk.ReadMembers();

        var libraryMembers = new List<LibraryMember>();
        var source = new SettingsFileSource(values, text, place, errors, libraryMembers);
        var level = SettingsLevelReader.Read(ref source);
        var sorted =
            errors.Count == 0
                ? EquatableArray<SettingsFileError>.Empty
                : new EquatableArray<SettingsFileError>(
                    errors.OrderBy(static error => error.Span.Start).ToImmutableArray()
                );
        var members = new EquatableArray<LibraryMember>(libraryMembers.ToImmutableArray());
        return new SettingsFile(walk.Root, walk.Dialect, level, members, sorted);
    }

    private static SettingsFileError? CheckGrammar(string text)
    {
        var tokens = new SidecarTokenizer(text, lenient: true);
        var first = tokens.Next();
        if (!tokens.TrySkipValue(first, 0, out var problem))
        {
            return Unreadable(SettingsFileErrorKind.InvalidJson, problem);
        }

        var end = tokens.Position;
        var after = tokens.Next();
        if (after.Kind != SidecarTokenKind.End)
        {
            return Unreadable(SettingsFileErrorKind.InvalidJson, after.Span);
        }

        return first.Kind == SidecarTokenKind.ObjectStart
            ? null
            : Unreadable(SettingsFileErrorKind.NotAnObject, TextSpan.FromBounds(first.Span.Start, end));
    }

    private static SettingsFileError Unreadable(SettingsFileErrorKind kind, TextSpan span) =>
        new(kind, span, null, null, null);

    // The second pass, over a text the first has passed: after a key comes a colon, and after a value a comma or the
    // end of its container.
    private sealed class Walk(string text, List<SettingsFileError> errors, SettingsFileValue?[] values)
    {
        private SidecarTokenizer _tokens = new(text, lenient: true);

        public bool Root { get; private set; }

        public SqlDialectChoice? Dialect { get; private set; }

        // Reads every member of the top level.  A key seen before is reported and its value skipped, whether or not
        // it is a key of the table.
        public void ReadMembers()
        {
            _ = _tokens.Next();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (NextMember(out var keyToken))
            {
                var key = SidecarTokenizer.GetString(text, keyToken);
                var first = _tokens.Next();
                if (!seen.Add(key))
                {
                    _ = Skip(first);
                    errors.Add(
                        new SettingsFileError(SettingsFileErrorKind.DuplicateKey, keyToken.Span, null, key, null)
                    );
                }
                else if (!SettingsFileKeys.All.Contains(key))
                {
                    _ = Skip(first);
                    errors.Add(new SettingsFileError(SettingsFileErrorKind.UnknownKey, keyToken.Span, null, key, null));
                }
                else if (!HasType(first.Kind, SettingsFileKeys.TypeOf(key)))
                {
                    WrongType(first, key);
                }
                else
                {
                    ReadValue(key, first);
                }
            }
        }

        // A value of the key's JSON type.  $schema is a string, read for nothing.
        private void ReadValue(string key, SidecarToken first)
        {
            if (SettingsFileKeys.TryGetSetting(key, out var setting))
            {
                values[(int)setting] = ReadSetting(key, first);
            }
            else if (key == SettingsFileKeys.Root)
            {
                Root = first.Kind == SidecarTokenKind.True;
            }
            else if (key == SettingsFileKeys.Dialect)
            {
                ReadDialect(first);
            }
        }

        private SettingsFileValue? ReadSetting(string key, SidecarToken first)
        {
            if (first.Kind == SidecarTokenKind.ArrayStart)
            {
                return ReadArray(key);
            }

            return first.Kind == SidecarTokenKind.ObjectStart
                ? ReadObject(key)
                : SettingsFileValue.String(SidecarTokenizer.GetString(text, first), first.Span);
        }

        private static bool HasType(SidecarTokenKind kind, SettingsFileValueType type) =>
            type switch
            {
                SettingsFileValueType.String => kind == SidecarTokenKind.String,
                SettingsFileValueType.Boolean => kind is SidecarTokenKind.True or SidecarTokenKind.False,
                SettingsFileValueType.ArrayOfStrings => kind == SidecarTokenKind.ArrayStart,
                SettingsFileValueType.ObjectOfStrings => kind == SidecarTokenKind.ObjectStart,
                _ => false,
            };

        // Blank sets nothing, as for every other text.
        private void ReadDialect(SidecarToken first)
        {
            var written = SidecarTokenizer.GetString(text, first);
            if (string.IsNullOrWhiteSpace(written))
            {
                return;
            }

            var value = written.Trim();
            if (SqlDialectName.TryParse(value, out var dialect))
            {
                Dialect = dialect;
                return;
            }

            errors.Add(
                new SettingsFileError(
                    SettingsFileErrorKind.InvalidValue,
                    first.Span,
                    SettingsFileKeys.Dialect,
                    value,
                    null
                )
            );
        }

        // An element that is not a string is reported and left out.  An array whose every element is left out is not
        // the empty list but sets nothing, since a wrong JSON type never sets a setting; [] stays the empty list.
        private SettingsFileValue? ReadArray(string key)
        {
            var (elements, spans) = (new List<string>(), new List<TextSpan>());
            var wrongTypes = 0;
            while (NextElement(out var element))
            {
                if (element.Kind != SidecarTokenKind.String)
                {
                    WrongType(element, key);
                    wrongTypes++;
                    continue;
                }

                elements.Add(SidecarTokenizer.GetString(text, element));
                spans.Add(element.Span);
            }

            return elements.Count == 0 && wrongTypes > 0 ? null : SettingsFileValue.Array(elements, spans);
        }

        // A member whose value is not a string is reported and left out, but for a member of the column types written
        // as an object, which is kept as the text of its type, its span the object's.  Two members with one key are
        // both kept: the setting's map says whether two keys are one.
        private SettingsFileValue ReadObject(string key)
        {
            var members = new List<KeyValuePair<string, string>>();
            var (keySpans, valueSpans) = (new List<TextSpan>(), new List<TextSpan>());
            var json = key == SettingsFileKeys.ColumnTypes ? new List<bool>() : null;
            while (NextMember(out var memberKey))
            {
                var value = _tokens.Next();
                string written;
                var isJson = false;
                TextSpan span;
                if (value.Kind == SidecarTokenKind.String)
                {
                    written = SidecarTokenizer.GetString(text, value);
                    span = value.Span;
                }
                else if (json is not null && value.Kind == SidecarTokenKind.ObjectStart)
                {
                    written = ReadColumnTypeObject(out isJson);
                    span = TextSpan.FromBounds(value.Span.Start, _tokens.Position);
                }
                else
                {
                    WrongType(value, key);
                    continue;
                }

                members.Add(new KeyValuePair<string, string>(SidecarTokenizer.GetString(text, memberKey), written));
                keySpans.Add(memberKey.Span);
                valueSpans.Add(span);
                json?.Add(isJson);
            }

            return SettingsFileValue.Object(members, keySpans, valueSpans, json);
        }

        // The object form of a column type, after its {: the text of its type member, and whether its json member is
        // true.  An object with no type, a type that is not a string, a json that is not true or false, a member twice
        // or any other member gives the empty string, which no type is, so that the level reader finds the member not
        // valid, as the setting's other problems are found.
        private string ReadColumnTypeObject(out bool isJson)
        {
            (string? type, bool? json, var isValid) = (null, null, true);
            while (NextMember(out var memberKey))
            {
                var value = _tokens.Next();
                var name = SidecarTokenizer.GetString(text, memberKey);
                if (name == SettingsFileKeys.TypeMember && type is null && value.Kind == SidecarTokenKind.String)
                {
                    type = SidecarTokenizer.GetString(text, value);
                }
                else if (
                    name == SettingsFileKeys.JsonMember
                    && json is null
                    && value.Kind is SidecarTokenKind.True or SidecarTokenKind.False
                )
                {
                    json = value.Kind == SidecarTokenKind.True;
                }
                else
                {
                    isValid = false;
                    _ = Skip(value);
                }
            }

            isJson = isValid && json == true;
            return isValid && type is not null ? type : string.Empty;
        }

        private void WrongType(SidecarToken first, string key) =>
            errors.Add(new SettingsFileError(SettingsFileErrorKind.WrongType, Skip(first), key, null, null));

        // Reads the next key of the object being read, and its colon.  False at the object's end.
        private bool NextMember(out SidecarToken key)
        {
            key = _tokens.Next();
            if (key.Kind == SidecarTokenKind.Comma)
            {
                key = _tokens.Next();
            }

            if (key.Kind != SidecarTokenKind.String)
            {
                return false;
            }

            _ = _tokens.Next();
            return true;
        }

        // Reads the first token of the next element of the array being read.  False at the array's end.
        private bool NextElement(out SidecarToken first)
        {
            first = _tokens.Next();
            if (first.Kind == SidecarTokenKind.Comma)
            {
                first = _tokens.Next();
            }

            return first.Kind is not (SidecarTokenKind.ArrayEnd or SidecarTokenKind.End);
        }

        // Moves past the value that starts with first and returns its span.  The first pass checked its grammar and
        // depth, and MemberDepth is no more than the depth of any value the walk skips, so the skip cannot fail.
        private TextSpan Skip(SidecarToken first)
        {
            _ = _tokens.TrySkipValue(first, MemberDepth, out _);
            return TextSpan.FromBounds(first.Span.Start, _tokens.Position);
        }
    }
}
