using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Paths;
using SqlSource.Parsing;
using SqlSource.Settings;
using SqlSource.Snapshot;

namespace SqlSource.Generation.Models;

/// <summary>
/// What one type that claims queries asks for of their models: for each model, its name, namespace, accessibility,
/// shape and the order of its properties, as the settings in effect for the type decide them.
/// </summary>
internal static class TypeModels
{
    /// <summary>
    /// The requests of the type, for each query of a file it can use whose output is <see cref="OutputKind.Models" />
    /// or <see cref="OutputKind.CodeGen" /> and whose models are in <paramref name="files" />.  A type with a
    /// diagnostic of its own asks for none, as it gets no file of its own.
    /// </summary>
    /// <param name="type">The type with its parsed files.</param>
    /// <param name="files">The models of every claimed file, sorted by path.</param>
    public static EquatableArray<ModelRequest> Of(TypeQueries type, EquatableArray<FileModels> files)
    {
        var target = type.Type.Type;
        if (type.Type.Diagnostics.Count > 0 || type.Files.Count == 0)
        {
            return EquatableArray<ModelRequest>.Empty;
        }

        var usable = TypeEmitter.FindUsableFiles(target, type.Files, type.FileSettings, null);
        var requests = ImmutableArray.CreateBuilder<ModelRequest>();
        (string Key, string Name)? claimingType = null;
        for (var index = 0; index < type.Files.Count; index++)
        {
            var parsed = type.Files[index];
            var modelsAt = SqlPath.IndexOf(files, parsed.NormalizedPath, static file => file.NormalizedPath);
            if (!usable[index] || modelsAt < 0)
            {
                continue;
            }

            var models = files[modelsAt];
            foreach (var query in models.Queries)
            {
                // The models were read from the same parse, so the index finds the query; a mismatch is skipped
                // rather than thrown on.
                if (
                    query.Index >= parsed.Queries.Count
                    || !string.Equals(parsed.Queries[query.Index].Name, query.Name, StringComparison.Ordinal)
                )
                {
                    continue;
                }

                var settings = QuerySettings.Resolve(
                    parsed.Queries[query.Index].Markers,
                    target.Settings,
                    type.FileSettings[index]
                );
                if (settings.Output is not (OutputKind.Models or OutputKind.CodeGen))
                {
                    continue;
                }

                claimingType ??= ClaimingTypeOf(target);
                var claiming = claimingType.Value;
                if (query.Input is { } input)
                {
                    requests.Add(Request(target, settings, parsed, query, claiming, ModelRole.Input, input));
                }

                if (query.Output is { } output)
                {
                    requests.Add(Request(target, settings, parsed, query, claiming, ModelRole.Output, output));
                }
            }
        }

        return new EquatableArray<ModelRequest>(requests.ToImmutable());
    }

    private static ModelRequest Request(
        TargetType target,
        QuerySettings settings,
        ParsedSqlFile file,
        QueryModels query,
        (string Key, string Name) claimingType,
        ModelRole role,
        EquatableArray<ModelProperty> properties
    )
    {
        var isInput = role == ModelRole.Input;
        var marker = isInput ? query.InputMarker : query.OutputMarker;
        string name;
        string @namespace;
        string namespaceText;
        SidecarTable? table = null;
        var period = marker?.Name.LastIndexOf('.') ?? -1;
        if (marker is not null && period >= 0)
        {
            // A full name, which ignores the namespace setting.  Its parts are names that are not keywords, so the
            // value is the text.
            name = marker.Name.Substring(period + 1);
            @namespace = namespaceText = marker.Name.Substring(0, period);
        }
        else
        {
            // A row of the table a query matches is named after the table, unless the query names its model or
            // no-table-models is in effect.  A table that gives no name leaves the query's.
            string? tableName = null;
            if (
                !isInput
                && marker is null
                && (settings.Parameters & GeneratorParameters.NoTableModels) == 0
                && query.Table is { } matched
                && PropertyNames.FromTable(matched, SqlDescribableDialects.EngineOf(file.Dialect)) is { } fromTable
            )
            {
                tableName = fromTable;
                table = matched;
            }

            name =
                marker?.Name
                ?? ((tableName ?? query.Name) + (isInput ? settings.InputModelSuffix : settings.OutputModelSuffix));

            // The setting's names are not keywords, so its value is its text.  The type's namespace keeps the @ of a
            // keyword in code.
            if (settings.ModelNamespace is { } setting)
            {
                @namespace = namespaceText = setting;
            }
            else
            {
                @namespace = target.NamespaceValue;
                namespaceText = target.Namespace;
            }
        }

        var parameter = isInput ? GeneratorParameters.SortInput : GeneratorParameters.SortOutput;
        var isSorted = (settings.Parameters & parameter) != 0;
        return new ModelRequest(
            @namespace,
            namespaceText,
            name,
            role,
            isInput ? settings.InputModelType : settings.OutputModelType,
            isSorted,
            target.IsPublic,
            table,
            isSorted ? Sort(properties) : properties,
            new ModelSource(
                file.NormalizedPath,
                file.FileName,
                query.Index,
                query.Name,
                claimingType.Key,
                claimingType.Name,
                query.NameLocation,
                marker?.Location,
                query.Summary
            )
        );
    }

    // The names are distinct ignoring case, so an ordinal sort has no ties.
    private static EquatableArray<ModelProperty> Sort(EquatableArray<ModelProperty> properties) =>
        new(properties.OrderBy(static property => property.Name, StringComparer.Ordinal).ToImmutableArray());

    // The claiming type as a sort key and as a message names it.  The key is HintName.NameOf, the namespace and each
    // type's name, as values, with a generic type's arity after a hyphen, "App.Outer.Repository-1", so that Repository
    // and Repository<T> are told apart.  The name has the type parameters instead, "App.Outer.Repository<T>".
    internal static (string Key, string Name) ClaimingTypeOf(TargetType target)
    {
        var name = new StringBuilder(target.NamespaceValue);
        foreach (var declaration in target.Types)
        {
            if (name.Length > 0)
            {
                _ = name.Append('.');
            }

            _ = name.Append(declaration.ValueName).Append(declaration.TypeParameters);
        }

        return (HintName.NameOf(target), name.ToString());
    }
}
