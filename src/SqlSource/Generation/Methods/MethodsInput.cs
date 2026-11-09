using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using SqlSource.Generation.Converters;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Models;
using SqlSource.Generation.Paths;
using SqlSource.Generation.Repositories;
using SqlSource.Generation.Types.Drivers;
using SqlSource.Settings;
using SqlSource.Settings.ClrTypes;
using SqlSource.Settings.Libraries;

namespace SqlSource.Generation.Methods;

/// <summary>
/// Everything <see cref="MethodPlanner" /> plans the methods of one type from: the type's own slice of what the
/// project gives, so that it compares equal until something of this type changes.
/// </summary>
/// <param name="Type">The type with its parsed files.</param>
/// <param name="Files">
/// The models of each of the type's files: <c>Files[i]</c> is that of <c>Type.Files[i]</c>, or one with no queries when
/// it has none.
/// </param>
/// <param name="Assignments">
/// The models that the type's queries got, ordered by <see cref="ModelAssignment.Order" />.
/// </param>
/// <param name="Drivers">Which engines' methods the project's drivers allow.</param>
/// <param name="Converters">
/// The converters that touch the type of a property of the type's files' models that a type override, a column type, a
/// marker or a library's row chose, or its element's for an array, each type as
/// <see cref="ClrTypeName.KeywordTextOf" /> writes it, without the <c>?</c> of an element written nullable
/// (<see cref="ConversionPlanner.EdgeElementOf" />); each converter once, whatever engines and drivers it serves, in
/// the order of <see cref="ConverterSet.Comparer" />, and without its location, so that an edit that only moves a
/// converter plans no method again.
/// </param>
/// <param name="LibrariesBelowTheFloor">
/// The libraries that the project references older than their floor, each with the engine whose queries use the old
/// package, from <see cref="BelowTheFloorOf" />: a query of that engine that uses one gets no method.  Without the
/// versions, so that a version that stays on one side of the floor plans no method again.
/// </param>
/// <param name="Repository">
/// The type's candidates for a repository's connection and the settings of its interface, found by its key, or null
/// when the shapes have none of it.
/// </param>
internal sealed record MethodsInput(
    TypeQueries Type,
    EquatableArray<FileModels> Files,
    EquatableArray<ModelAssignment> Assignments,
    MethodDrivers Drivers,
    EquatableArray<Converter> Converters,
    EquatableArray<LibraryUse> LibrariesBelowTheFloor,
    RepositoryShape? Repository
)
{
    // Every library and engine, in the order of SqlLibrary and then of DatabaseEngine: a new member of either is in it.
    // JSON is in it too, and is never below its floor.
    private static readonly LibraryUse[] AllUses =
    [
        .. from library in (SqlLibrary[])Enum.GetValues(typeof(SqlLibrary))
        from engine in DatabaseEngines.All
        select new LibraryUse(library, engine),
    ];

    /// <summary>
    /// The libraries that <paramref name="probe" /> says the project references older than their floor, each with
    /// the engine whose queries use the old package, in the order of <see cref="SqlLibrary" /> and then of
    /// <see cref="DatabaseEngine" />.
    /// </summary>
    public static EquatableArray<LibraryUse> BelowTheFloorOf(LibraryProbe probe)
    {
        var below = AllUses.Where(use => probe.IsBelowTheFloor(use.Library, use.Engine)).ToImmutableArray();
        return below.IsEmpty ? EquatableArray<LibraryUse>.Empty : new EquatableArray<LibraryUse>(below);
    }

    /// <summary>
    /// Whether <paramref name="library" /> is one of <see cref="LibrariesBelowTheFloor" /> for
    /// <paramref name="engine" />'s queries.
    /// </summary>
    public bool IsBelowTheFloor(SqlLibrary library, DatabaseEngine engine)
    {
        for (var index = 0; index < LibrariesBelowTheFloor.Count; index++)
        {
            if (LibrariesBelowTheFloor[index] == new LibraryUse(library, engine))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Takes from what the project gives what the type needs: a search for each of its files and for its assignments,
    /// so that the cost does not grow with the other types.  The names the type already has are its own, in
    /// <see cref="TargetType.TakenNames" />.
    /// </summary>
    /// <param name="type">The type with its parsed files.</param>
    /// <param name="allFileModels">The models of every claimed file, sorted by path.</param>
    /// <param name="assignments">Every assignment, ordered by <see cref="ModelAssignment.Order" />.</param>
    /// <param name="drivers">Which engines' methods the project's drivers allow.</param>
    /// <param name="converters">Every converter of the project.</param>
    /// <param name="librariesBelowTheFloor">The libraries that the project references older than their floor.</param>
    /// <param name="repositories">Every type's shape, sorted ordinally by its key.</param>
    public static MethodsInput Gather(
        TypeQueries type,
        EquatableArray<FileModels> allFileModels,
        EquatableArray<ModelAssignment> assignments,
        MethodDrivers drivers,
        ConverterSet converters,
        EquatableArray<LibraryUse> librariesBelowTheFloor,
        EquatableArray<RepositoryShape> repositories
    )
    {
        var files = ImmutableArray.CreateBuilder<FileModels>(type.Files.Count);
        foreach (var path in type.Files.Select(static file => file.NormalizedPath))
        {
            var index = SqlPath.IndexOf(allFileModels, path, static models => models.NormalizedPath);
            files.Add(index < 0 ? new FileModels(path, string.Empty, default, default) : allFileModels[index]);
        }

        var key = HintName.NameOf(type.Type.Type);
        var slice = new EquatableArray<FileModels>(files.MoveToImmutable());
        return new MethodsInput(
            type,
            slice,
            SliceOf(assignments, key),
            drivers,
            ConvertersOf(slice, converters),
            librariesBelowTheFloor,
            ShapeOf(repositories, key)
        );
    }

    // The first shape of the key, by a search of the sorted shapes.  Two types whose hint names are ambiguous share a
    // key, and get no file, so either shape will do.
    private static RepositoryShape? ShapeOf(EquatableArray<RepositoryShape> shapes, string key)
    {
        var low = 0;
        var high = shapes.Count;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (string.CompareOrdinal(shapes[middle].TypeKey, key) < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low < shapes.Count && string.Equals(shapes[low].TypeKey, key, StringComparison.Ordinal)
            ? shapes[low]
            : null;
    }

    // The converters of the chosen properties' types: a pass over the properties, and a search of the set only for a
    // type that a type override, a column type, a marker or a library's row chose, so that a type without one costs no
    // search.
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
            .Select(static property => ClrTypeName.KeywordTextOf(property.Type.Text));
        foreach (var text in texts)
        {
            AddEach(converters.Touching(text), ref found);
            if (ConversionPlanner.EdgeElementOf(text) is { } element)
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
