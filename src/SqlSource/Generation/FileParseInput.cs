using Microsoft.CodeAnalysis;
using SqlSource.Generation.Chains;
using SqlSource.Generation.MSBuild;
using SqlSource.Generation.Paths;
using SqlSource.Parsing;
using SqlSource.Settings;

namespace SqlSource.Generation;

/// <summary>
/// A <c>.sql</c> file with everything outside it that its parse depends on.  This is stage one of the settings:
/// what must be known before the file is parsed.  Everything else joins after a type's queries are selected.
/// </summary>
/// <param name="File">
/// The file.  Compared by reference: the compiler hands out the same object until the file changes.
/// </param>
/// <param name="NormalizedPath">The file's path as <see cref="SqlPath.Normalize" /> gives it, or null.</param>
/// <param name="Dialect">
/// The dialect the file is parsed with, unless a marker in it names another: the chain of its folder's, or else its own
/// metadata, or else the project's property, or else <see cref="SqlDialect.Ansi" />.
/// </param>
/// <param name="InvalidDialect">
/// The file's metadata as written when it is not a dialect, and null otherwise, whether or not the chain sets one.
/// </param>
/// <param name="CommentsWanted">
/// Whether a level the parser cannot see may ask for <c>keep-comments</c>: the first list of the chain's and the
/// metadata's, or the property's when neither gives one, or the attribute of a type that claims the file.  It may be
/// true where no query ends up keeping its comments; it is never false where one does, apart from two paths that
/// differ only by case, with the metadata on the later one: that is <c>SQLSRC013</c>, and the emitter falls back to
/// the stripped form.
/// </param>
internal sealed record FileParseInput(
    AdditionalText File,
    string? NormalizedPath,
    SqlDialectChoice Dialect,
    string? InvalidDialect,
    bool CommentsWanted
)
{
    /// <summary>
    /// The chain of the file's folder comes before its own metadata, and the metadata before the project's property,
    /// whether or not it is valid: a file with metadata that is not a dialect, and no dialect in its chain, is read as
    /// <see cref="SqlDialect.Ansi" />, and its value is reported either way.
    /// </summary>
    public static FileParseInput Resolve(
        FileMetadata metadata,
        SettingsChain chain,
        DialectSetting projectDialect,
        bool projectKeepsComments,
        EquatableArray<string> commentPaths
    )
    {
        var path = SqlPath.Normalize(metadata.File.Path);
        var commentsWanted =
            (
                (chain.Level.Parameters ?? metadata.Settings?.Level.Parameters) is { } list
                    ? (list & GeneratorParameters.KeepComments) != 0
                    : projectKeepsComments
            ) || (path is not null && SqlPath.Contains(commentPaths, path));
        var dialect = chain.Dialect ?? metadata.Dialect.Dialect ?? projectDialect.Dialect ?? default;
        return new FileParseInput(metadata.File, path, dialect, metadata.Dialect.InvalidValue, commentsWanted);
    }
}
