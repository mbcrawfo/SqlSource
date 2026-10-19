using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using SqlSource.Diagnostics;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Paths;

namespace SqlSource.Generation.Models;

/// <summary>
/// Gathers the requests of every type by the full name of their model, and gives one model for each full name.
/// </summary>
internal static class ModelCollector
{
    // A stable sort, so that two requests of one place, from two types whose names differ only by arity, keep the
    // order of the compilation.
    private static readonly IComparer<ModelSource> SourceOrder = Comparer<ModelSource>.Create(ModelSource.Comparison);

    /// <summary>
    /// The requests grouped by full name, compared ordinally, each group's in the order of
    /// <see cref="ModelSource.Comparison" />, and the groups in the ordinal order of their full names, so that the
    /// result does not depend on the order of the compilation's types or files.
    /// </summary>
    public static EquatableArray<ModelGroup> Group(ImmutableArray<ModelRequest> requests) =>
        new(
            requests
                .GroupBy(static request => request.FullName, StringComparer.Ordinal)
                .OrderBy(static group => group.Key, StringComparer.Ordinal)
                .Select(static group => new ModelGroup(
                    group.Key,
                    new EquatableArray<ModelRequest>(
                        group.OrderBy(static request => request.Source, SourceOrder).ToImmutableArray()
                    )
                ))
                .ToImmutableArray()
        );

    /// <summary>
    /// One model for each group, written from the group's first request and named after each query of the group.
    /// The hint names are made unique ignoring case, since the compiler throws on two that are equal so.
    /// </summary>
    public static ModelSet Resolve(EquatableArray<ModelGroup> groups)
    {
        if (groups.Count == 0)
        {
            return new ModelSet(EquatableArray<Model>.Empty, EquatableArray<DiagnosticInfo>.Empty);
        }

        var hintNames = groups.Select(static group => HintName.ForModel(group.FullName)).ToImmutableArray();
        var ambiguous = HintName.FindAmbiguous(hintNames);
        var models = ImmutableArray.CreateBuilder<Model>(groups.Count);
        var text = new StringBuilder();
        for (var index = 0; index < groups.Count; index++)
        {
            var requests = groups[index].Requests;
            var first = requests[0];
            models.Add(
                new Model(
                    HintName.MakeUnique(hintNames[index], ambiguous),
                    first.NamespaceText,
                    first.Name,
                    first.Role,
                    first.Kind,
                    first.IsPublic,
                    first.Properties,
                    SummaryXml(first.Role, requests, text)
                )
            );
        }

        return new ModelSet(new EquatableArray<Model>(models.ToImmutable()), EquatableArray<DiagnosticInfo>.Empty);
    }

    // The first query's -- summary:, escaped, then on a line of its own the sentence that says what the model is and
    // names each query once, in order:
    // "A row of the queries <c>A</c> from <c>F.sql</c> and <c>B</c> from <c>G.sql</c>."
    // Two types that claim one query ask for its model twice, and the requests of a query are next to each other.
    private static string SummaryXml(ModelRole role, EquatableArray<ModelRequest> requests, StringBuilder text)
    {
        var queries = new List<ModelSource>(requests.Count);
        foreach (var source in requests.Select(static request => request.Source))
        {
            if (queries.Count == 0 || !IsSameQuery(queries[queries.Count - 1], source))
            {
                queries.Add(source);
            }
        }

        _ = text.Clear();
        if (requests[0].Source.Summary is { } summary)
        {
            _ = text.Append(XmlDocWriter.Escape(summary)).Append('\n');
        }

        _ = text.Append(role == ModelRole.Input ? "The parameters of " : "A row of ")
            .Append(queries.Count == 1 ? "the query " : "the queries ");
        for (var index = 0; index < queries.Count; index++)
        {
            if (index > 0)
            {
                _ = text.Append(index == queries.Count - 1 ? " and " : ", ");
            }

            _ = text.Append("<c>")
                .Append(queries[index].QueryName)
                .Append("</c> from <c>")
                .Append(XmlDocWriter.Escape(queries[index].FileName))
                .Append("</c>");
        }

        return text.Append('.').ToString();
    }

    private static bool IsSameQuery(ModelSource left, ModelSource right) =>
        left.QueryIndex == right.QueryIndex && SqlPath.Comparer.Equals(left.NormalizedPath, right.NormalizedPath);
}
