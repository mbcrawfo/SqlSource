using System.Linq;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Types;

namespace SqlSource.Testing.Generator;

// What a named step of the pipeline gave in a run, for a test that reads a value no output shows.  The driver tracks
// the steps because GeneratorHarness turns tracking on.
//
// Like the other files of this folder, it stays within Roslyn 4.8.0, since tests/SqlSource.Tests.RoslynFloor
// references this library.
internal static class StepOutputs
{
    // Every value the step gave, in the order the driver ran it.  None for a step the driver did not track, which it
    // does not when nothing the step gave reaches an output.
    public static T[] Of<T>(GeneratorRun run, string step) =>
        run.Result.Results.Single().TrackedSteps.TryGetValue(step, out var executions)
            ? [.. executions.SelectMany(execution => execution.Outputs).Select(output => (T)output.Value)]
            : [];

    // The one value the step gave, for a step that runs once for the compilation, or once for the one file a run has.
    public static T Single<T>(GeneratorRun run, string step) => Of<T>(run, step).ShouldHaveSingleItem();

    // The types of the columns of the run's one query, or of its query of the name, as the types of its file gave them.
    public static PropertyType[] ColumnsOf(GeneratorRun run, string? queryName = null)
    {
        var queries = Of<FileTypes>(run, TrackingNames.FileTypes).SelectMany(file => file.Queries);
        var query = queryName is null ? queries.ShouldHaveSingleItem() : queries.Single(each => each.Name == queryName);
        return [.. query.Columns.ShouldNotBeNull()];
    }
}
