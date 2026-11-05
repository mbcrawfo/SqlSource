using Microsoft.CodeAnalysis;
using SqlSource.Generation.Chains;
using SqlSource.Generation.Paths;
using SqlSource.Parsing;

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
/// The dialect the file is parsed with, unless a marker in it names another: the chain of its folder's, or else
/// <see cref="SqlDialect.Ansi" />.
/// </param>
/// <param name="CommentsWanted">
/// Whether the level the parser cannot see, the chain of the file's folder, turns <c>keep-comments</c> on.  A query
/// keeps its comments when it does, unless the query's markers or its file header turn it off.
/// </param>
internal sealed record FileParseInput(
    AdditionalText File,
    string? NormalizedPath,
    SqlDialectChoice Dialect,
    bool CommentsWanted
)
{
    /// <summary>
    /// The input of <paramref name="file" />, whose path <see cref="SqlPath.Normalize" /> gives as
    /// <paramref name="normalizedPath" />, under <paramref name="chain" />, the chain of its folder.
    /// </summary>
    public static FileParseInput Resolve(AdditionalText file, string? normalizedPath, SettingsChain chain) =>
        new(file, normalizedPath, chain.Dialect ?? default, chain.Level.KeepsComments);
}
