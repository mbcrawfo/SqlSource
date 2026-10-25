using SqlSource.Generation.MSBuild;
using SqlSource.Generation.Paths;
using SqlSource.Generation.Sidecars;
using SqlSource.Settings;

namespace SqlSource.Generation.Types;

/// <summary>
/// One claimed file with everything its types read.  It compares equal until one of them changes, so an edit to
/// another file, to another sidecar or to a setting other than the type override and the column types types this one
/// again only when its own value changed.
/// </summary>
/// <param name="Check">What the checks of the file's sidecar gave, its usable entries among them.</param>
/// <param name="File">
/// The parsed file, or null when no entry of it is usable.  Such a file carries nothing else but the probes, since it
/// has nothing to type.
/// </param>
/// <param name="Metadata">
/// The type override and the column types of the metadata of the file's item, as a level that says nothing else.
/// </param>
/// <param name="Property">
/// The type override and the column types of the project's properties, as a level that says nothing else.
/// </param>
/// <param name="NpgsqlProbe">The Npgsql the project references, which chooses the column of PostgreSQL's map.</param>
/// <param name="SqlClientProbe">
/// The Microsoft.Data.SqlClient the project references, which chooses the column of SQL Server's map.
/// </param>
/// <param name="MappedTypes">
/// The C# types that the type override and the column types name at any level, and that the <c>-- param:</c> and
/// <c>-- column:</c> markers give, resolved.
/// </param>
internal sealed record FileTypesInput(
    SidecarCheckResult Check,
    ParsedSqlFile? File,
    SettingsLevel Metadata,
    SettingsLevel Property,
    NpgsqlProbe NpgsqlProbe,
    SqlClientProbe SqlClientProbe,
    EquatableArray<MappedType> MappedTypes
)
{
    /// <summary>
    /// Finds the parsed file and the metadata of <paramref name="check" />'s file.  Both lists are sorted by
    /// <see cref="SqlPath.Comparer" />.  <paramref name="property" /> is the properties' type override and column types
    /// alone, as <see cref="TypeSettingsOf" /> gives them.
    /// </summary>
    public static FileTypesInput Gather(
        SidecarCheckResult check,
        EquatableArray<ParsedSqlFile> files,
        EquatableArray<FileSettings> filesSettings,
        SettingsLevel property,
        NpgsqlProbe npgsqlProbe,
        SqlClientProbe sqlClientProbe,
        EquatableArray<MappedType> mappedTypes
    )
    {
        var fileIndex =
            check.UsableEntries.Count == 0
                ? -1
                : SqlPath.IndexOf(files, check.NormalizedPath, static file => file.NormalizedPath);
        if (fileIndex < 0)
        {
            return new FileTypesInput(
                check,
                null,
                SettingsLevel.None,
                SettingsLevel.None,
                npgsqlProbe,
                sqlClientProbe,
                EquatableArray<MappedType>.Empty
            );
        }

        var settingsIndex = SqlPath.IndexOf(filesSettings, check.NormalizedPath, static file => file.NormalizedPath);
        return new FileTypesInput(
            check,
            files[fileIndex],
            settingsIndex < 0 ? SettingsLevel.None : TypeSettingsOf(filesSettings[settingsIndex].Level),
            property,
            npgsqlProbe,
            sqlClientProbe,
            mappedTypes
        );
    }

    /// <summary>
    /// The type override of <paramref name="level" />, each engine's entries, and its column types, as a level that
    /// says nothing else, so that a change to another setting gives an equal level.  <see cref="SettingsLevel.None" />
    /// when it has none of them.
    /// </summary>
    public static SettingsLevel TypeSettingsOf(SettingsLevel level) =>
        level.TypeOverrides is null && level.SqlServerTypeOverrides is null && level.ColumnTypes is null
            ? SettingsLevel.None
            : new SettingsLevel
            {
                TypeOverrides = level.TypeOverrides,
                SqlServerTypeOverrides = level.SqlServerTypeOverrides,
                ColumnTypes = level.ColumnTypes,
            };
}
