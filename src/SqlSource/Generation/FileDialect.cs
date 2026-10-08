using Microsoft.CodeAnalysis;
using SqlSource.Parsing;

namespace SqlSource.Generation;

/// <summary>
/// A <c>.sql</c> file with the dialect that MSBuild gives it: its own metadata, or else the project's property, or
/// else <see cref="SqlDialect.Ansi" />.
/// </summary>
/// <param name="File">
/// The file.  Compared by reference: the compiler hands out the same object until the file changes.
/// </param>
/// <param name="Dialect">The dialect the file is parsed with, unless a marker in it names another.</param>
/// <param name="InvalidValue">The file's metadata as written when it is not a dialect, and null otherwise.</param>
internal sealed record FileDialect(AdditionalText File, SqlDialectChoice Dialect, string? InvalidValue)
{
    /// <summary>
    /// A file's own metadata comes before the project's property, whether or not it is valid: a file with metadata
    /// that is not a dialect is read as <see cref="SqlDialect.Ansi" />, and its value is reported.
    /// </summary>
    public static FileDialect Resolve(AdditionalText file, DialectSetting metadata, DialectSetting project) =>
        metadata.Dialect is { } dialect
            ? new FileDialect(file, dialect, metadata.InvalidValue)
            : new FileDialect(file, project.Dialect ?? default, null);
}
