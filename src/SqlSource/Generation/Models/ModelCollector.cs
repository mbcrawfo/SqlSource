using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using SqlSource.Diagnostics;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Paths;
using SqlSource.Generation.Types;
using SqlSource.Snapshot;

namespace SqlSource.Generation.Models;

/// <summary>
/// Gathers the requests of every type by the full name of their model, and gives one model for each full name that
/// can be generated.
/// </summary>
/// <remarks>
/// <para>
/// The first request of a full name is its model, and each later request that differs from it is <c>SQLSRC257</c> and
/// joins no model.  The model is public when any request that joined it is.
/// </para>
/// <para>
/// A model is not generated, and each query that joined it is told why, when a property has the model's name
/// (<c>SQLSRC256</c>), when it is public and a property's type is not (<c>SQLSRC251</c>), or when its name cannot be
/// used (<c>SQLSRC258</c>): the project has it, it is the namespace of another model, a part of its namespace is
/// another model, or it is lower-case ASCII letters alone, which the compiler warns on.
/// </para>
/// </remarks>
internal static class ModelCollector
{
    private const string TakenByTheModel = "is the name of its model";

    // Each help is given without its period.
    private const string AliasIt = "Give it an alias";

    private const string RenameIt = "Rename it";

    // The second case of SQLSRC251, before the model's full name and after it, and its help.
    private const string NotPublicBefore = "that type is not public, and the model '";

    private const string NotPublicAfter = "' is";

    private const string MakeItPublic = "Make the type public, or make each type that claims the query's file internal";

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

    /// <summary>The groups' full names, in their order: ordinal, and each once.</summary>
    public static EquatableArray<string> FullNamesOf(EquatableArray<ModelGroup> groups) =>
        new(groups.Select(static group => group.FullName).ToImmutableArray());

    /// <summary>
    /// One model for each group that can be generated, written from the group's first request and named after each
    /// query that joined it, and the problems of the others.  The hint names are made unique ignoring case, since the
    /// compiler throws on two that are equal so.
    /// </summary>
    /// <param name="groups">The requests by full name, as <see cref="Group" /> gives them.</param>
    /// <param name="taken">
    /// The full names the project has, as <see cref="ModelNameChecker.FindTaken" /> gives them.
    /// </param>
    public static ModelSet Resolve(EquatableArray<ModelGroup> groups, EquatableArray<TakenName> taken)
    {
        if (groups.Count == 0)
        {
            return new ModelSet(
                EquatableArray<Model>.Empty,
                EquatableArray<ModelPlace>.Empty,
                EquatableArray<DiagnosticInfo>.Empty
            );
        }

        var names = new Names(groups, taken);
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var emitted = new List<(ModelRequest First, List<ModelRequest> Joined, bool IsPublic)>(groups.Count);
        var problems = new List<Problem>();
        foreach (var group in groups)
        {
            var requests = group.Requests;
            var first = requests[0];
            var joined = new List<ModelRequest>(requests.Count) { first };
            for (var index = 1; index < requests.Count; index++)
            {
                var later = requests[index];
                if (FindDifference(first, later) is not { } difference)
                {
                    joined.Add(later);
                    continue;
                }

                AddOnce(
                    diagnostics,
                    DiagnosticInfo.Create(
                        SqlDiagnostics.ModelsDiffer,
                        later.Source.MarkerLocation ?? later.Source.NameLocation,
                        RoleOf(later.Role),
                        group.FullName,
                        later.Source.QueryName,
                        RoleOf(first.Role),
                        QueryOf(first.Source, later.Source),
                        difference
                    )
                );
            }

            var isPublic = joined.Exists(static request => request.IsPublic);
            problems.Clear();
            FindProblems(first, isPublic, names, problems);
            if (problems.Count == 0)
            {
                emitted.Add((first, joined, isPublic));
                continue;
            }

            // Each distinct query once, though two types that claim it ask for its model twice.  Each query is told
            // of its own property, whose column or parameter and type override can differ from the first query's.
            var told = new List<LocationInfo>(joined.Count);
            foreach (var request in joined)
            {
                if (told.Contains(request.Source.NameLocation))
                {
                    continue;
                }

                told.Add(request.Source.NameLocation);
                foreach (var problem in problems)
                {
                    diagnostics.Add(Tell(problem, request, group.FullName));
                }
            }
        }

        var (models, places) = Models(emitted);
        return new ModelSet(models, places, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));
    }

    private static (EquatableArray<Model> Models, EquatableArray<ModelPlace> Places) Models(
        List<(ModelRequest First, List<ModelRequest> Joined, bool IsPublic)> emitted
    )
    {
        var hintNames = emitted.Select(static model => HintName.ForModel(model.First.FullName)).ToImmutableArray();
        var ambiguous = HintName.FindAmbiguous(hintNames);
        var models = ImmutableArray.CreateBuilder<Model>(emitted.Count);
        var places = ImmutableArray.CreateBuilder<ModelPlace>(emitted.Count);
        var text = new StringBuilder();
        for (var index = 0; index < emitted.Count; index++)
        {
            var (first, joined, isPublic) = emitted[index];
            models.Add(
                new Model(
                    HintName.MakeUnique(hintNames[index], ambiguous),
                    first.NamespaceText,
                    first.Name,
                    first.Role,
                    first.Kind,
                    isPublic,
                    first.Properties,
                    SummaryXml(first.Role, joined, text),
                    joined.Exists(static request => request.Properties.Any(static property => property.IsArray))
                )
            );
            places.Add(
                new ModelPlace(
                    first.Source.NormalizedPath,
                    first.Source.QueryIndex,
                    first.Role,
                    first.Source.ClaimingType
                )
            );
        }

        return (
            new EquatableArray<Model>(models.MoveToImmutable()),
            new EquatableArray<ModelPlace>(places.MoveToImmutable())
        );
    }

    // The first difference of the later request from the first, as SQLSRC257 words it, or null when they give one
    // model: the role, the kind, the sorting, the number of properties, then each property's name, type and
    // nullability in turn.  Their documentation changes no C#, so it is not compared.
    private static string? FindDifference(ModelRequest first, ModelRequest later)
    {
        if (first.Role != later.Role)
        {
            return "one is an input model and the other an output model";
        }

        if (first.Kind != later.Kind)
        {
            return "it is a '"
                + ModelKinds.Keyword(later.Kind)
                + "' here and a '"
                + ModelKinds.Keyword(first.Kind)
                + "' there";
        }

        if (first.IsSorted != later.IsSorted)
        {
            return later.IsSorted
                ? "its properties are sorted here and not there"
                : "its properties are sorted there and not here";
        }

        var (here, there) = (later.Properties, first.Properties);
        if (here.Count != there.Count)
        {
            return "it has "
                + here.Count.ToString(CultureInfo.InvariantCulture)
                + (here.Count == 1 ? " property here and " : " properties here and ")
                + there.Count.ToString(CultureInfo.InvariantCulture)
                + " there";
        }

        for (var index = 0; index < here.Count; index++)
        {
            var (mine, theirs) = (here[index], there[index]);
            if (!string.Equals(mine.Name, theirs.Name, StringComparison.Ordinal))
            {
                return "its property "
                    + (index + 1).ToString(CultureInfo.InvariantCulture)
                    + " is '"
                    + mine.Name
                    + "' here and '"
                    + theirs.Name
                    + "' there";
            }

            if (!string.Equals(mine.Type.Text, theirs.Type.Text, StringComparison.Ordinal))
            {
                return "the property '"
                    + mine.Name
                    + "' is '"
                    + PostgresTypeMap.AsWritten(mine.Type.Text)
                    + "' here and '"
                    + PostgresTypeMap.AsWritten(theirs.Type.Text)
                    + "' there";
            }

            if (mine.IsNullable != theirs.IsNullable)
            {
                return "the property '"
                    + mine.Name
                    + (mine.IsNullable ? "' is nullable here and not there" : "' is nullable there and not here");
            }
        }

        return null;
    }

    // The problems that keep the model from being generated, in the order of their ids: each property whose type is
    // not public is one.
    private static void FindProblems(ModelRequest first, bool isPublic, Names names, List<Problem> problems)
    {
        var properties = first.Properties;
        if (isPublic)
        {
            for (var index = 0; index < properties.Count; index++)
            {
                if (!properties[index].Type.IsPublic)
                {
                    problems.Add(new Problem(ProblemKind.TypeNotPublic, index, null));
                }
            }
        }

        // CS0542.  The property names are distinct ignoring case, so one at most has the model's.
        for (var index = 0; index < properties.Count; index++)
        {
            if (string.Equals(properties[index].Name, first.Name, StringComparison.Ordinal))
            {
                problems.Add(new Problem(ProblemKind.NamedAsTheModel, index, null));
                break;
            }
        }

        if (names.FindUnusable(first.FullName, first) is { } reason)
        {
            problems.Add(new Problem(ProblemKind.NameUnusable, -1, reason));
        }
    }

    // The problem as the query of the request is told of it.  Every request that joined a model has its properties'
    // names, types and nullability, so the property at the problem's index is the one the problem is about.
    private static DiagnosticInfo Tell(Problem problem, ModelRequest request, string fullName)
    {
        var at = request.Source.NameLocation;
        if (problem.Kind == ProblemKind.NameUnusable)
        {
            return DiagnosticInfo.Create(
                SqlDiagnostics.ModelNameUnusable,
                at,
                fullName,
                request.Source.QueryName,
                problem.Reason ?? ""
            );
        }

        var property = request.Properties[problem.Property];
        var help = request.Role == ModelRole.Input ? RenameIt : AliasIt;
        return problem.Kind == ProblemKind.TypeNotPublic
            ? DiagnosticInfo.Create(
                SqlDiagnostics.MappedTypeNotAccessible,
                at,
                property.Subject,
                property.Type.Key,
                PostgresTypeMap.AsWritten(property.Type.Text),
                property.Type.Place ?? "",
                NotPublicBefore + fullName + NotPublicAfter,
                MakeItPublic
            )
            : DiagnosticInfo.Create(
                SqlDiagnostics.PropertyNameTaken,
                at,
                property.Subject,
                property.Name,
                TakenByTheModel,
                help
            );
    }

    private static void AddOnce(ImmutableArray<DiagnosticInfo>.Builder diagnostics, DiagnosticInfo diagnostic)
    {
        if (!diagnostics.Contains(diagnostic))
        {
            diagnostics.Add(diagnostic);
        }
    }

    private static string RoleOf(ModelRole role) => role == ModelRole.Input ? "input" : "output";

    // "'GetUser' in Users.sql", and its type too when the later request is of the same query.
    private static string QueryOf(ModelSource first, ModelSource later) =>
        "'"
        + first.QueryName
        + "' in "
        + first.FileName
        + (IsSameQuery(first, later) ? ", for the type " + first.ClaimingTypeName : "");

    // The first query's -- summary:, escaped, then on a line of its own the sentence that says what the model is and
    // names each query that joined it once, in order:
    // "A row of the queries <c>A</c> from <c>F.sql</c> and <c>B</c> from <c>G.sql</c>."
    // A table model says whose rows they are, naming each distinct table once and only the queries whose request has
    // one, then the other queries that joined it in a sentence of their own:
    // "A row of the table <c>public.users</c>, read by the query <c>A</c> from <c>F.sql</c>. Also a row of the query
    // <c>Users</c> from <c>G.sql</c>."
    // Two types that claim one query ask for its model twice, and the requests of a query are next to each other.
    private static string SummaryXml(ModelRole role, List<ModelRequest> requests, StringBuilder text)
    {
        var tables = new List<SidecarTable>();
        var tableQueries = new List<ModelSource>(requests.Count);
        var otherQueries = new List<ModelSource>(requests.Count);
        foreach (var request in requests)
        {
            if (request.Table is { } table)
            {
                if (!tables.Contains(table))
                {
                    tables.Add(table);
                }

                AddQuery(tableQueries, request.Source);
            }
            else
            {
                AddQuery(otherQueries, request.Source);
            }
        }

        _ = text.Clear();
        if (requests[0].Source.Summary is { } summary)
        {
            _ = text.Append(XmlDocWriter.Escape(summary)).Append('\n');
        }

        if (role == ModelRole.Input)
        {
            _ = text.Append("The parameters of ");
            AppendQueries(text, otherQueries);
        }
        else if (tables.Count == 0)
        {
            _ = text.Append("A row of ");
            AppendQueries(text, otherQueries);
        }
        else
        {
            _ = text.Append(tables.Count == 1 ? "A row of the table " : "A row of the tables ");
            for (var index = 0; index < tables.Count; index++)
            {
                AppendSeparator(text, index, tables.Count);
                _ = text.Append("<c>")
                    .Append(XmlDocWriter.Escape(tables[index].Schema ?? ""))
                    .Append('.')
                    .Append(XmlDocWriter.Escape(tables[index].Table ?? ""))
                    .Append("</c>");
            }

            _ = text.Append(", read by ");
            AppendQueries(text, tableQueries);
            if (otherQueries.Count > 0)
            {
                _ = text.Append(" Also a row of ");
                AppendQueries(text, otherQueries);
            }
        }

        return text.ToString();
    }

    // A query once: the requests of a query are next to each other.
    private static void AddQuery(List<ModelSource> queries, ModelSource source)
    {
        if (queries.Count == 0 || !IsSameQuery(queries[queries.Count - 1], source))
        {
            queries.Add(source);
        }
    }

    // "the query <c>A</c> from <c>F.sql</c>." or, for several, "the queries <c>A</c> from <c>F.sql</c> and <c>B</c>
    // from <c>G.sql</c>."
    private static void AppendQueries(StringBuilder text, List<ModelSource> queries)
    {
        _ = text.Append(queries.Count == 1 ? "the query " : "the queries ");
        for (var index = 0; index < queries.Count; index++)
        {
            AppendSeparator(text, index, queries.Count);
            _ = text.Append("<c>")
                .Append(queries[index].QueryName)
                .Append("</c> from <c>")
                .Append(XmlDocWriter.Escape(queries[index].FileName))
                .Append("</c>");
        }

        _ = text.Append('.');
    }

    // Before the item at the index of a list of the count: nothing, " and " or ", ".
    private static void AppendSeparator(StringBuilder text, int index, int count)
    {
        if (index > 0)
        {
            _ = text.Append(index == count - 1 ? " and " : ", ");
        }
    }

    private static bool IsSameQuery(ModelSource left, ModelSource right) =>
        left.QueryIndex == right.QueryIndex && SqlPath.Comparer.Equals(left.NormalizedPath, right.NormalizedPath);

    // What keeps a model from being generated: a property whose type is not public, in a public model (SQLSRC251), a
    // property named as the model (SQLSRC256), or a name that cannot be used (SQLSRC258).
    private enum ProblemKind
    {
        TypeNotPublic,
        NamedAsTheModel,
        NameUnusable,
    }

    // A problem of a model, with the index of the property it is about, or the reason its name cannot be used.
    private readonly record struct Problem(ProblemKind Kind, int Property, string? Reason);

    // What the project and the other models say of a model's full name.
    private sealed class Names
    {
        // A string's default equality is ordinal.
        private readonly HashSet<string> _fullNames = [];

        // Every namespace a model is in, and each namespace those are in.
        private readonly HashSet<string> _namespaces = [];

        private readonly Dictionary<string, TakenName> _taken = [];

        public Names(EquatableArray<ModelGroup> groups, EquatableArray<TakenName> taken)
        {
            foreach (var group in groups)
            {
                _ = _fullNames.Add(group.FullName);
                var @namespace = group.Requests[0].Namespace;
                for (var period = @namespace.IndexOf('.'); period > 0; period = @namespace.IndexOf('.', period + 1))
                {
                    _ = _namespaces.Add(@namespace.Substring(0, period));
                }

                if (@namespace.Length > 0)
                {
                    _ = _namespaces.Add(@namespace);
                }
            }

            foreach (var name in taken)
            {
                _taken[name.FullName] = name;
            }
        }

        // Why the model's name cannot be used, as SQLSRC258 words it, or null.
        public string? FindUnusable(string fullName, ModelRequest first)
        {
            if (_taken.TryGetValue(fullName, out var taken))
            {
                return taken.Reason switch
                {
                    TakenReason.Type => "a type of this project has that full name",
                    TakenReason.Namespace => "it is a namespace of this project",
                    TakenReason.NamespacePartIsType or _ => "'"
                        + taken.Part
                        + "' is a type of this project, and a part of its namespace",
                };
            }

            if (_namespaces.Contains(fullName))
            {
                return "it is the namespace of another model";
            }

            // Each part of the namespace from its start, the whole namespace last.
            var @namespace = first.Namespace;
            for (
                var period = @namespace.IndexOf('.');
                @namespace.Length > 0;
                period = @namespace.IndexOf('.', period + 1)
            )
            {
                var part = period < 0 ? @namespace : @namespace.Substring(0, period);
                if (_fullNames.Contains(part))
                {
                    return "'" + part + "' is another model, and a part of its namespace";
                }

                if (period < 0)
                {
                    break;
                }
            }

            return IsLowerCaseAsciiLetters(first.Name)
                ? "the compiler warns on a type name of lower-case ASCII letters alone"
                : null;
        }

        // CS8981: a name of ASCII lower-case letters alone may become a keyword.
        private static bool IsLowerCaseAsciiLetters(string name) =>
            name.Length > 0 && name.All(static character => character is >= 'a' and <= 'z');
    }
}
