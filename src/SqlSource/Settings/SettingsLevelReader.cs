using System;
using System.Collections.Generic;
using SqlSource.Settings.ColumnTypes;
using SqlSource.Settings.TypeOverrides;

namespace SqlSource.Settings;

/// <summary>
/// Reads a <see cref="SettingsLevel" /> from a source that is not a marker.  This is the one place that says which
/// members such a level has, what makes a text, a list of generator parameters, a type override and a column type
/// valid, and that a text which is empty or white space is not set.  A choice is decoded by its source, which alone
/// knows how one is written.
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
            NestedClass = ReadText(ref source, SettingId.NestedClass, SettingValue.IsTypeName),
        };

        // A source that sets nothing shares the empty level.
        return level.Equals(SettingsLevel.None) ? SettingsLevel.None : level;
    }

    // A word that is no generator parameter is recorded, and the other words apply.
    private static GeneratorParameters? ReadParameters<TSource>(ref TSource source)
        where TSource : struct, ISettingSource
    {
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

    // An entry that is not valid is recorded as written, and the other entries apply.  The value serves both engines:
    // PostgreSQL's entries are returned and SQL Server's given in sqlServer.  A list with no valid entry is not set.
    private static EquatableArray<TypeOverride>? ReadTypeOverrides<TSource>(
        ref TSource source,
        out EquatableArray<TypeOverride>? sqlServer
    )
        where TSource : struct, ISettingSource
    {
        sqlServer = null;
        if (source.Text(SettingId.TypeOverrides) is not { } list || string.IsNullOrWhiteSpace(list))
        {
            return null;
        }

        var entries = new List<string>();
        var overrides = TypeOverrideGrammar.ReadList(list, source.Place, entries);
        foreach (var entry in entries)
        {
            source.Invalid(SettingId.TypeOverrides, entry);
        }

        sqlServer = overrides.SqlServer.Count == 0 ? null : overrides.SqlServer;
        return overrides.PostgreSql.Count == 0 ? null : overrides.PostgreSql;
    }

    // An entry that is not valid is recorded as written, and the other entries apply.  A list with no valid entry is
    // not set.
    private static EquatableArray<ColumnType>? ReadColumnTypes<TSource>(ref TSource source)
        where TSource : struct, ISettingSource
    {
        if (source.Text(SettingId.ColumnTypes) is not { } list || string.IsNullOrWhiteSpace(list))
        {
            return null;
        }

        var invalid = new List<string>();
        var columnTypes = ColumnTypeGrammar.ReadList(list, source.Place, invalid);
        foreach (var entry in invalid)
        {
            source.Invalid(SettingId.ColumnTypes, entry);
        }

        return columnTypes.Count == 0 ? null : columnTypes;
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
