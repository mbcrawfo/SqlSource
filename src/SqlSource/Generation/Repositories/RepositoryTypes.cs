using SqlSource.Settings;

namespace SqlSource.Generation.Repositories;

/// <summary>
/// Which types are repositories: those that claim a query whose output is <see cref="OutputKind.Repository" />.  The
/// <c>RepositoryShapes</c> step reads the shape of these types alone, and <c>MethodsInput</c> gives one only to them,
/// by this one rule, so that the two cannot disagree.
/// </summary>
internal static class RepositoryTypes
{
    /// <summary>
    /// The type when a query of one of its files has the output <see cref="OutputKind.Repository" />, by its markers
    /// and its file's level, and null otherwise.
    /// </summary>
    public static TargetType? Of(TypeQueries type) => ClaimsRepositoryQuery(type) ? type.Type.Type : null;

    /// <summary>
    /// Whether a query of one of the type's files has the output <see cref="OutputKind.Repository" />, by its markers
    /// and its file's level.
    /// </summary>
    public static bool ClaimsRepositoryQuery(TypeQueries type)
    {
        for (var index = 0; index < type.Files.Count; index++)
        {
            foreach (var query in type.Files[index].Queries)
            {
                if (QuerySettings.ResolveOutput(query.Markers, type.FileSettings[index]) == OutputKind.Repository)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
