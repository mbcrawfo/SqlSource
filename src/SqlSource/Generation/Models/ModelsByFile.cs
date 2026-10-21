using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using SqlSource.Generation.Paths;

namespace SqlSource.Generation.Models;

/// <summary>
/// The models arranged by the file whose query gave them, so that each file's models are emitted from that file's
/// own step, and a model added or removed moves no model of another file or slot.
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

    /// <summary>
    /// Arranges the models of <paramref name="set" /> by their places.
    /// </summary>
    /// <param name="set">The models, with <c>Places[i]</c> the place of <c>Models[i]</c>.</param>
    /// <param name="repeatedPaths">
    /// The paths the project lists more than once, as <see cref="SqlPath.FindRepeated" /> gives them.
    /// </param>
    /// <remarks>
    /// This runs whenever <c>Models</c> changes and builds every file's slots, so it sorts the indexes of the models
    /// rather than pairs, and builds each array through a builder used for every file, or from its one model.
    /// </remarks>
    public static ModelsByFile Arrange(ModelSet set, EquatableArray<string> repeatedPaths)
    {
        if (set.Models.Count == 0)
        {
            return None;
        }

        // The indexes of the models of the files, which come first, up to count.
        var ordered = new int[set.Models.Count];
        var count = 0;
        ImmutableArray<Model>.Builder? ofRepeatedPaths = null;
        for (var index = 0; index < set.Models.Count; index++)
        {
            if (SqlPath.Contains(repeatedPaths, set.Places[index].NormalizedPath))
            {
                (ofRepeatedPaths ??= ImmutableArray.CreateBuilder<Model>()).Add(set.Models[index]);
            }
            else
            {
                ordered[count++] = index;
            }
        }

        Array.Sort(ordered, 0, count, new PlaceOrder(set.Places));

        // A file has at least one model, so there are no more files than models, and most often as many.
        var files = ImmutableArray.CreateBuilder<FileModelSlots>(count);
        var slots = ImmutableArray.CreateBuilder<EquatableArray<Model>>();
        var models = ImmutableArray.CreateBuilder<Model>();
        for (var start = 0; start < count; )
        {
            var path = set.Places[ordered[start]].NormalizedPath;
            var end = start + 1;
            while (end < count && SqlPath.Comparer.Equals(set.Places[ordered[end]].NormalizedPath, path))
            {
                end++;
            }

            files.Add(new FileModelSlots(path, SlotsOfFile(set, ordered, start, end, slots, models)));
            start = end;
        }

        return new ModelsByFile(
            new EquatableArray<FileModelSlots>(
                files.Count == files.Capacity ? files.MoveToImmutable() : files.ToImmutable()
            ),
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

    // The slots of the models from start up to end of ordered, which are the models of one file, in the order of
    // their places.  That order puts the models of a slot together, so each slot takes the run of models that has its
    // number.  The two builders are empty, and are left empty.
    private static EquatableArray<EquatableArray<Model>> SlotsOfFile(
        ModelSet set,
        int[] ordered,
        int start,
        int end,
        ImmutableArray<EquatableArray<Model>>.Builder slots,
        ImmutableArray<Model>.Builder models
    )
    {
        // The last of the file's models is of its last query.
        var slotCount = (set.Places[ordered[end - 1]].QueryIndex + 1) * 2;
        var position = start;
        for (var slot = 0; slot < slotCount; slot++)
        {
            var runEnd = position;
            while (runEnd < end && SlotOf(set.Places[ordered[runEnd]]) == slot)
            {
                runEnd++;
            }

            slots.Add(ModelsOfRun(set, ordered, position, runEnd, models));
            position = runEnd;
        }

        var ofFile = slots.ToImmutable();
        slots.Clear();
        return new EquatableArray<EquatableArray<Model>>(ofFile);
    }

    // The models from start up to end of ordered, which share a slot.  A slot most often has one model or none.
    private static EquatableArray<Model> ModelsOfRun(
        ModelSet set,
        int[] ordered,
        int start,
        int end,
        ImmutableArray<Model>.Builder models
    )
    {
        switch (end - start)
        {
            case 0:
                return EquatableArray<Model>.Empty;
            case 1:
                return new EquatableArray<Model>(ImmutableArray.Create(set.Models[ordered[start]]));
            default:
                for (var position = start; position < end; position++)
                {
                    models.Add(set.Models[ordered[position]]);
                }

                var ofSlot = models.ToImmutable();
                models.Clear();
                return new EquatableArray<Model>(ofSlot);
        }
    }

    // Orders the indexes of models by their places: by path, in the order of SqlPath.Comparer, then by the query's
    // index, the role and the claiming type, ordinally, and last by the index in the set, which is unique.
    private sealed class PlaceOrder(EquatableArray<ModelPlace> places) : IComparer<int>
    {
        public int Compare(int left, int right)
        {
            var (leftPlace, rightPlace) = (places[left], places[right]);
            var byPath = SqlPath.Comparer.Compare(leftPlace.NormalizedPath, rightPlace.NormalizedPath);
            if (byPath != 0)
            {
                return byPath;
            }

            var byQuery = leftPlace.QueryIndex.CompareTo(rightPlace.QueryIndex);
            if (byQuery != 0)
            {
                return byQuery;
            }

            // As numbers, since the CompareTo of an enum boxes both.
            var byRole = ((int)leftPlace.Role).CompareTo((int)rightPlace.Role);
            if (byRole != 0)
            {
                return byRole;
            }

            var byType = string.CompareOrdinal(leftPlace.ClaimingType, rightPlace.ClaimingType);
            return byType != 0 ? byType : left.CompareTo(right);
        }
    }
}
