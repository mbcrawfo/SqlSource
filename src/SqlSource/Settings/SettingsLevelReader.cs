using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using SqlSource.Settings.ColumnTypes;
using SqlSource.Settings.Libraries;
using SqlSource.Settings.TypeOverrides;

namespace SqlSource.Settings;

/// <summary>
/// Reads a <see cref="SettingsLevel" /> from a source that is not a marker.  This is the one place that says which
/// members such a level has, what makes a text, a list of generator parameters, a type override and a column type
/// valid, in each form a source writes them, and that a text which is empty or white space is not set.  A choice is
/// decoded by its source, which alone knows how one is written.
/// </summary>
internal static class SettingsLevelReader
{
    private delegate bool Validator(ReadOnlySpan<char> value);

    /// <summary>
    /// Reads one level.  A value that is not valid is given to the source and is not set, and every setting is
    /// still read, so that all the problems of a source are found together.  The shared empty level is returned
    /// when nothing is set.
    /// </summary>
    public static SettingsLevel Read<TSource>(ref TSource source)
        where TSource : struct, ISettingSource
    {
        // In the order of SettingId: a source reports what is not valid in the order it is asked.
        var level = new SettingsLevel
        {
            Parameters = ReadParameters(ref source),
            Output = source.Choice<OutputKind>(SettingId.Output),
            Database = ReadText(ref source, SettingId.Database, SettingValue.IsDatabaseName),
            InputModelSuffix = ReadText(ref source, SettingId.InputModelSuffix, SettingValue.IsSuffix),
            OutputModelSuffix = ReadText(ref source, SettingId.OutputModelSuffix, SettingValue.IsSuffix),
            ModelNamespace = ReadText(ref source, SettingId.ModelNamespace, SettingValue.IsQualifiedName),
            InputModelType = source.Choice<ModelKind>(SettingId.InputModelType),
            OutputModelType = source.Choice<ModelKind>(SettingId.OutputModelType),
            CollectionType = source.Choice<CollectionKind>(SettingId.CollectionType),
            TypeOverrides = ReadTypeOverrides(ref source, out var sqlServerTypeOverrides),
            SqlServerTypeOverrides = sqlServerTypeOverrides,
            ColumnTypes = ReadColumnTypes(ref source),
            LibraryTypes = ReadLibraryTypes(ref source),
            NestedClass = ReadText(ref source, SettingId.NestedClass, SettingValue.IsTypeName),
        };

        // A source that sets nothing shares the empty level.
        return level.Equals(SettingsLevel.None) ? SettingsLevel.None : level;
    }

    // A word that is no generator parameter is recorded, and the other words apply.  The elements of an array win over
    // a text; an array with no word is the empty list, which is set.
    private static GeneratorParameters? ReadParameters<TSource>(ref TSource source)
        where TSource : struct, ISettingSource
    {
        if (source.Elements(SettingId.Parameters) is { } elements)
        {
            var indexes = new List<int>();
            var fromElements = GeneratorParameterList.ReadElements(elements, indexes);
            ReportParts(ref source, SettingId.Parameters, indexes);
            return fromElements;
        }

        if (source.Text(SettingId.Parameters) is not { } list || string.IsNullOrWhiteSpace(list))
        {
            return null;
        }

        var words = new List<string>();
        var parameters = GeneratorParameterList.Parse(list, words);
        foreach (var word in words)
        {
            source.Invalid(SettingId.Parameters, word);
        }

        return parameters;
    }

    // An entry that is not valid is recorded by its index, and the other entries apply.  The value serves both
    // engines: PostgreSQL's entries are returned and SQL Server's given in sqlServer.  A list with no valid entry is
    // not set.
    private static EquatableArray<TypeOverride>? ReadTypeOverrides<TSource>(
        ref TSource source,
        out EquatableArray<TypeOverride>? sqlServer
    )
        where TSource : struct, ISettingSource
    {
        sqlServer = null;
        if (source.Members(SettingId.TypeOverrides) is not { } members)
        {
            return null;
        }

        var (indexes, repeated) = (new List<int>(), new List<(int Index, int First)>());
        var overrides = TypeOverrideGrammar.ReadMembers(members, source.Place, indexes, repeated);
        ReportParts(ref source, SettingId.TypeOverrides, indexes, repeated);

        sqlServer = overrides.SqlServer.Count == 0 ? null : overrides.SqlServer;
        return overrides.PostgreSql.Count == 0 ? null : overrides.PostgreSql;
    }

    // An entry that is not valid is recorded by its index, and the other entries apply.  A list with no valid entry is
    // not set.  The source says which member is written with the JSON opt-in.
    private static EquatableArray<ColumnType>? ReadColumnTypes<TSource>(ref TSource source)
        where TSource : struct, ISettingSource
    {
        if (source.Members(SettingId.ColumnTypes) is not { } members)
        {
            return null;
        }

        var json = new bool[members.Count];
        for (var index = 0; index < json.Length; index++)
        {
            json[index] = source.IsJsonMember(SettingId.ColumnTypes, index);
        }

        var (indexes, repeated) = (new List<int>(), new List<(int Index, int First)>());
        var columnTypes = ColumnTypeGrammar.ReadMembers(members, source.Place, indexes, repeated, json);
        ReportParts(ref source, SettingId.ColumnTypes, indexes, repeated);

        return columnTypes.Count == 0 ? null : columnTypes;
    }

    // A member whose name is no library or whose value is no state is recorded by its index, and the other members
    // apply.  A member for a library the entries already have is recorded as a repeat of the member that put it, and
    // the first wins.  A list with no valid member is not set.  The source is told which member gives each entry, and
    // keeps where it is apart from the level.
    private static EquatableArray<LibrarySetting>? ReadLibraryTypes<TSource>(ref TSource source)
        where TSource : struct, ISettingSource
    {
        if (source.Members(SettingId.LibraryTypes) is not { } members)
        {
            return null;
        }

        var (invalid, repeated) = (new List<int>(), new List<(int Index, int First)>());
        // Beside the entries, the index of the member that put each of them.
        var (entries, entryMembers) = (new List<LibrarySetting>(), new List<int>());
        for (var index = 0; index < members.Count; index++)
        {
            if (
                !SettingValue.TryReadChoice(members[index].Key.AsSpan().Trim(), out SqlLibrary library)
                || !SettingValue.TryReadChoice(members[index].Value.AsSpan().Trim(), out LibraryState state)
            )
            {
                invalid.Add(index);
                continue;
            }

            var first = IndexOfLibrary(entries, library);
            if (first >= 0)
            {
                repeated.Add((index, entryMembers[first]));
                continue;
            }

            entries.Add(new LibrarySetting(library, state, source.Place));
            entryMembers.Add(index);
            source.LibraryEntry(library, index);
        }

        ReportParts(ref source, SettingId.LibraryTypes, invalid, repeated);

        entries.Sort(static (a, b) => a.Library.CompareTo(b.Library));
        return entries.Count == 0 ? null : new EquatableArray<LibrarySetting>(entries.ToImmutableArray());
    }

    private static int IndexOfLibrary(List<LibrarySetting> entries, SqlLibrary library)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            if (entries[index].Library == library)
            {
                return index;
            }
        }

        return -1;
    }

    // Gives the source the elements or members that are not valid, then those whose key is repeated.
    private static void ReportParts<TSource>(
        ref TSource source,
        SettingId setting,
        List<int> invalid,
        List<(int Index, int First)>? repeated = null
    )
        where TSource : struct, ISettingSource
    {
        foreach (var index in invalid)
        {
            source.InvalidPart(setting, index);
        }

        if (repeated is null)
        {
            return;
        }

        foreach (var (index, first) in repeated)
        {
            source.RepeatedKey(setting, index, first);
        }
    }

    private static string? ReadText<TSource>(ref TSource source, SettingId setting, Validator isValid)
        where TSource : struct, ISettingSource
    {
        if (source.Text(setting) is not { } written || string.IsNullOrWhiteSpace(written))
        {
            return null;
        }

        var value = written.Trim();
        if (isValid(value.AsSpan()))
        {
            return value;
        }

        source.Invalid(setting, value);
        return null;
    }
}
