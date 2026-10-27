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
/// Whether a level the parser cannot see may ask for <c>keep-comments</c>: the chain's list, or the attribute of a
/// type that claims the file.  It may be true where no query ends up keeping its comments; it is never false where one
/// does.
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
    /// <paramref name="commentPaths" /> are the files of the types whose attribute asks for comments, sorted by
    /// <see cref="SqlPath.Comparer" />.
    /// </summary>
    public static FileParseInput Resolve(
        AdditionalText file,
        string? normalizedPath,
        SettingsChain chain,
        EquatableArray<string> commentPaths
    )
    {
        var commentsWanted =
            chain.Level.KeepsComments || (normalizedPath is not null && SqlPath.Contains(commentPaths, normalizedPath));
        return new FileParseInput(file, normalizedPath, chain.Dialect ?? default, commentsWanted);
    }
}
