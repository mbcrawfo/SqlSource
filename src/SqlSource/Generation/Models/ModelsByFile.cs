using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using SqlSource.Generation.Paths;

namespace SqlSource.Generation.Models;

/// <summary>
/// The models arranged by the file whose query gave them, so that each file's models are emitted from that file's
/// own step and a model added or removed rewrites only itself.
/// </summary>
/// <param name="Files">
/// The files that have a model, in the order of <see cref="SqlPath.Comparer" />, each with its models in slots.
/// </param>
/// <param name="OfRepeatedPaths">
/// The models whose file is a path that the project lists more than once, in the order of the set.  Such a file is
/// parsed once, from the first listing, so these are emitted apart from the files.
/// </param>
internal sealed record ModelsByFile(EquatableArray<FileModelSlots> Files, EquatableArray<Model> OfRepeatedPaths)
{
    private static readonly ModelsByFile None = new(EquatableArray<FileModelSlots>.Empty, EquatableArray<Model>.Empty);

    // The place of a model before the place of another: by path, in the order of SqlPath.Comparer, then by the
    // query's index, the role and the claiming type, ordinally, and last by the index in the set, which is unique.
    private static readonly Comparison<(ModelPlace Place, int Index)> PlaceOrder = static (left, right) =>
    {
        var byPath = SqlPath.Comparer.Compare(left.Place.NormalizedPath, right.Place.NormalizedPath);
        if (byPath != 0)
        {
            return byPath;
        }

        var byQuery = left.Place.QueryIndex.CompareTo(right.Place.QueryIndex);
        if (byQuery != 0)
        {
            return byQuery;
        }

        var byRole = left.Place.Role.CompareTo(right.Place.Role);
        if (byRole != 0)
        {
            return byRole;
        }

        var byType = string.CompareOrdinal(left.Place.ClaimingType, right.Place.ClaimingType);
        return byType != 0 ? byType : left.Index.CompareTo(right.Index);
    };

    /// <summary>
    /// Arranges the models of <paramref name="set" /> by their places.
    /// </summary>
    /// <param name="set">The models, with <c>Places[i]</c> the place of <c>Models[i]</c>.</param>
    /// <param name="repeatedPaths">
    /// The paths the project lists more than once, as <see cref="SqlPath.FindRepeated" /> gives them.
    /// </param>
    public static ModelsByFile Arrange(ModelSet set, EquatableArray<string> repeatedPaths)
    {
        if (set.Models.Count == 0)
        {
            return None;
        }

        var ordered = new List<(ModelPlace Place, int Index)>(set.Models.Count);
        ImmutableArray<Model>.Builder? ofRepeatedPaths = null;
        for (var index = 0; index < set.Models.Count; index++)
        {
            var place = set.Places[index];
            if (SqlPath.Contains(repeatedPaths, place.NormalizedPath))
            {
                (ofRepeatedPaths ??= ImmutableArray.CreateBuilder<Model>()).Add(set.Models[index]);
            }
            else
            {
                ordered.Add((place, index));
            }
        }

        ordered.Sort(PlaceOrder);

        var files = ImmutableArray.CreateBuilder<FileModelSlots>();
        for (var start = 0; start < ordered.Count; )
        {
            var path = ordered[start].Place.NormalizedPath;
            var end = start + 1;
            while (end < ordered.Count && SqlPath.Comparer.Equals(ordered[end].Place.NormalizedPath, path))
            {
                end++;
            }

            files.Add(new FileModelSlots(path, SlotsOfFile(set, ordered, start, end)));
            start = end;
        }

        return new ModelsByFile(
            new EquatableArray<FileModelSlots>(files.ToImmutable()),
            ofRepeatedPaths is null
                ? EquatableArray<Model>.Empty
                : new EquatableArray<Model>(ofRepeatedPaths.ToImmutable())
        );
    }

    /// <summary>
    /// The slots of the file at <paramref name="normalizedPath" />, or none when it has no model.
    /// </summary>
    public static EquatableArray<EquatableArray<Model>> SlotsOf(string normalizedPath, ModelsByFile byFile)
    {
        var index = SqlPath.IndexOf(byFile.Files, normalizedPath, static file => file.NormalizedPath);
        return index < 0 ? EquatableArray<EquatableArray<Model>>.Empty : byFile.Files[index].Slots;
    }

    // The slot of a place: two for each query, the role in the lower bit.  Input is 0 and Output is 1, which
    // ModelsByFileTests holds.
    private static int SlotOf(ModelPlace place) => (place.QueryIndex * 2) + (int)place.Role;

    // The slots of the models from start up to end, which are the models of one file, in the order of their places.
    // That order puts the models of a slot together, so each slot takes the run of models that has its number.
    private static EquatableArray<EquatableArray<Model>> SlotsOfFile(
        ModelSet set,
        List<(ModelPlace Place, int Index)> ordered,
        int start,
        int end
    )
    {
        // The last of the file's models is of its last query.
        var slots = ImmutableArray.CreateBuilder<EquatableArray<Model>>((ordered[end - 1].Place.QueryIndex + 1) * 2);
        var position = start;
        for (var slot = 0; slot < slots.Capacity; slot++)
        {
            var runEnd = position;
            while (runEnd < end && SlotOf(ordered[runEnd].Place) == slot)
            {
                runEnd++;
            }

            if (runEnd == position)
            {
                slots.Add(EquatableArray<Model>.Empty);
                continue;
            }

            var models = ImmutableArray.CreateBuilder<Model>(runEnd - position);
            for (; position < runEnd; position++)
            {
                models.Add(set.Models[ordered[position].Index]);
            }

            slots.Add(new EquatableArray<Model>(models.MoveToImmutable()));
        }

        return new EquatableArray<EquatableArray<Model>>(slots.MoveToImmutable());
    }
}
