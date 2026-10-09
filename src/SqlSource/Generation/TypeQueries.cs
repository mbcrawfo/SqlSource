using SqlSource.Settings;

namespace SqlSource.Generation;

/// <summary>
/// A type with its own parsed files: the whole input of <see cref="TypeEmitter" /> but for the project's settings.
/// It compares equal until the type, one of its files or the metadata of one changes, so a change to another type's
/// file does not emit this one again.
/// </summary>
/// <param name="Type">The type and the paths of its files.</param>
/// <param name="Files">The type's files, in member order.</param>
/// <param name="FileSettings">
/// What the metadata of each file's item says about the settings: one level for each of <paramref name="Files" />.
/// </param>
/// <param name="HintName">The name of the type's generated file.  Unique, ignoring case, in the compilation.</param>
internal sealed record TypeQueries(
    TypeFiles Type,
    EquatableArray<ParsedSqlFile> Files,
    EquatableArray<SettingsLevel> FileSettings,
    string HintName
);
