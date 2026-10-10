using System;
using System.Collections.Immutable;
using System.Linq;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Planning;

namespace SqlSource.Tool.Describing;

/// <summary>
/// Makes the entry of a sidecar from what the plan, the session and the description each know of a query.
/// </summary>
internal static class EntryBuilder
{
    /// <summary>
    /// The entry.  Its parameters are the query's list in the list's order: the name and the nullability are the
    /// list's, the ordinal is the index, and the type with its source is what the description gives for that name,
    /// compared ignoring case, or null.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The description holds a parameter that the query's list does not: a bug of the describer.
    /// </exception>
    public static SidecarEntry Build(
        PlannedQuery query,
        SqlDialect dialect,
        string database,
        ServerInfo server,
        QueryDescription description
    )
    {
        var listed = query.Query.Parameters;
        var unknown = description.Parameters.FirstOrDefault(described => Find(listed, described.Name) < 0);
        if (unknown is not null)
        {
            throw new InvalidOperationException(
                $"The describer gave the parameter '{unknown.Name}', which the query '{query.Query.Name}' "
                    + "does not have."
            );
        }

        var parameters = ImmutableArray.CreateBuilder<SidecarParameter>(listed.Count);
        for (var ordinal = 0; ordinal < listed.Count; ordinal++)
        {
            var parameter = listed[ordinal];
            var described = Find(description.Parameters, parameter.Name);
            parameters.Add(
                new SidecarParameter(
                    parameter.Name,
                    ordinal,
                    described?.Type,
                    parameter.Nullable,
                    described?.TypeSource
                )
            );
        }

        return new SidecarEntry(
            query.Query.Name,
            default,
            query.Hash ?? throw new InvalidOperationException($"The query '{query.Query.Name}' needs no entry."),
            SqlDialectName.Canonical(dialect),
            database,
            server.Version,
            description.ResultKind,
            description.MatchesTable,
            description.Plan,
            description.TableMatch,
            new EquatableArray<SidecarParameter>(parameters.MoveToImmutable()),
            description.Columns
        );
    }

    private static int Find(EquatableArray<SqlQueryParameter> listed, string name)
    {
        for (var index = 0; index < listed.Count; index++)
        {
            if (string.Equals(listed[index].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static DescribedParameter? Find(EquatableArray<DescribedParameter> described, string name)
    {
        return described.FirstOrDefault(parameter =>
            string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase)
        );
    }
}
