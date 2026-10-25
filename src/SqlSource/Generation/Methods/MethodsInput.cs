using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using SqlSource.Generation.Converters;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Models;
using SqlSource.Generation.Paths;
using SqlSource.Settings;

namespace SqlSource.Generation.Methods;

/// <summary>
/// Everything <see cref="MethodPlanner" /> plans the methods of one type from: the type's own slice of what the
/// project gives, so that it compares equal until something of this type changes.
/// </summary>
/// <param name="Type">The type with its parsed files.</param>
/// <param name="Property">What the project's properties say.</param>
/// <param name="Files">
/// The models of each of the type's files: <c>Files[i]</c> is that of <c>Type.Files[i]</c>, or one with no queries when
/// it has none.
/// </param>
/// <param name="Assignments">
/// The models that the type's queries got, ordered by <see cref="ModelAssignment.Order" />.
/// </param>
/// <param name="MemberNames">
/// The names the type already has, sorted ordinally, when its methods go on itself, and empty otherwise.
/// </param>
/// <param name="Drivers">Which engines' methods the project's drivers allow.</param>
/// <param name="Converters">
/// The converters that touch the type of a property of the type's files' models that an override chose, or its
/// element's for an array, each once, in the order of <see cref="ConverterSet.Comparer" />, and each without its
/// location, so that an edit that only moves a converter plans no method again.
/// </param>
internal sealed record MethodsInput(
    TypeQueries Type,
    SettingsLevel Property,
    EquatableArray<FileModels> Files,
    EquatableArray<ModelAssignment> Assignments,
    EquatableArray<string> MemberNames,
    MethodDrivers Drivers,
    EquatableArray<Converter> Converters
)
{
    /// <summary>
    /// Takes from what the project gives what the type needs: a search for each of its files, for its assignments and
    /// for its names, so that the cost does not grow with the other types.
    /// </summary>
    /// <param name="type">The type with its parsed files.</param>
    /// <param name="property">What the project's properties say.</param>
    /// <param name="allFileModels">The models of every claimed file, sorted by path.</param>
    /// <param name="assignments">Every assignment, ordered by <see cref="ModelAssignment.Order" />.</param>
    /// <param name="memberNames">The names of every type whose methods go on itself, sorted by key ordinally.</param>
    /// <param name="drivers">Which engines' methods the project's drivers allow.</param>
    /// <param name="converters">Every converter of the project.</param>
    public static MethodsInput Gather(
        TypeQueries type,
        SettingsLevel property,
        EquatableArray<FileModels> allFileModels,
        EquatableArray<ModelAssignment> assignments,
        EquatableArray<TypeMemberNames> memberNames,
        MethodDrivers drivers,
        ConverterSet converters
    )
    {
        var files = ImmutableArray.CreateBuilder<FileModels>(type.Files.Count);
        foreach (var path in type.Files.Select(static file => file.NormalizedPath))
        {
            var index = SqlPath.IndexOf(allFileModels, path, static models => models.NormalizedPath);
            files.Add(index < 0 ? new FileModels(path, string.Empty, default, default) : allFileModels[index]);
        }

        var key = HintName.NameOf(type.Type.Type);
        var names = SortedSearch.IndexOf(memberNames, key, static names => names.TypeKey, StringComparer.Ordinal);
        var slice = new EquatableArray<FileModels>(files.MoveToImmutable());
        return new MethodsInput(
            type,
            property,
            slice,
            SliceOf(assignments, key),
            names < 0 ? EquatableArray<string>.Empty : memberNames[names].Names,
            drivers,
            ConvertersOf(slice, converters)
        );
    }

    // The converters of the overridden properties' types: a pass over the properties, and a search of the set only for
    // a type that an override chose, so that a type without one costs no search.
    private static EquatableArray<Converter> ConvertersOf(EquatableArray<FileModels> files, ConverterSet converters)
    {
        if (converters.Converters.Count == 0)
        {
            return EquatableArray<Converter>.Empty;
        }

        HashSet<Converter>? found = null;
        foreach (var query in files.SelectMany(static file => file.Queries))
        {
            Add(query.Input, converters, ref found);
            Add(query.Output, converters, ref found);
        }

        return found is null
            ? EquatableArray<Converter>.Empty
            : new EquatableArray<Converter>(
                found.OrderBy(static converter => converter, ConverterSet.Comparer).ToImmutableArray()
            );
    }

    private static void Add(
        EquatableArray<ModelProperty>? properties,
        ConverterSet converters,
        ref HashSet<Converter>? found
    )
    {
        if (properties is not { } each)
        {
            return;
        }

        var texts = each.Where(static property => property.Type.Place is not null)
            .Select(static property => property.Type.Text);
        foreach (var text in texts)
        {
            AddEach(converters.Touching(text), ref found);
            if (ConversionPlanner.ElementOf(text) is { } element)
            {
                AddEach(converters.Touching(element), ref found);
            }
        }
    }

    private static void AddEach(EquatableArray<Converter> touching, ref HashSet<Converter>? found)
    {
        foreach (var converter in touching)
        {
            _ = (found ??= []).Add(converter with { Location = null });
        }
    }

    // The run of the type's assignments.
    private static EquatableArray<ModelAssignment> SliceOf(EquatableArray<ModelAssignment> sorted, string key)
    {
        var first = ModelAssignment.FirstOf(sorted, key);
        if (first < 0)
        {
            return EquatableArray<ModelAssignment>.Empty;
        }

        var end = first;
        while (end < sorted.Count && string.Equals(sorted[end].ClaimingType, key, StringComparison.Ordinal))
        {
            end++;
        }

        var slice = ImmutableArray.CreateBuilder<ModelAssignment>(end - first);
        for (var index = first; index < end; index++)
        {
            slice.Add(sorted[index]);
        }

        return new EquatableArray<ModelAssignment>(slice.MoveToImmutable());
    }
}
