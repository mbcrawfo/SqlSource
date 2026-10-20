using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Sidecars;
using SqlSource.Generation.Types;
using Xunit;

namespace SqlSource.Testing.Generator;

// What a run of the generator did at each step of the pipeline, for the Caching*Tests of
// tests/SqlSource.Tests/Generator/.  The generator runs again on every edit in the IDE, and each step reports, for each
// of its outputs, whether it was taken from the previous run.  The steps are read by their names in TrackingNames.
//
// Like the other files of this folder, it stays within Roslyn 4.8.0, since tests/SqlSource.Tests.RoslynFloor
// references this library.
internal static class StepReasons
{
    // The models the run has, by the name of their file, and why each ran.  A model that went is no part of them: see
    // ModelsRemoved.
    public static Dictionary<string, IncrementalStepRunReason> ModelReasons(GeneratorDriverRunResult result) =>
        Outputs(result, TrackingNames.ModelOutput)
            .Where(output => output.Reason != IncrementalStepRunReason.Removed)
            .ToDictionary(output => HintNameOf(output.Value), output => output.Reason);

    // A model output is its hint name and its source.
    public static string HintNameOf(object modelOutput)
    {
        var (hintName, _) = ((string HintName, string Source))modelOutput;
        return hintName;
    }

    // The files of the models that went.  Nodes are matched by their place, so what goes is the last place the list
    // had, and not the model that was removed.
    public static string[] ModelsRemoved(GeneratorDriverRunResult result) =>
        [
            .. Outputs(result, TrackingNames.ModelOutput)
                .Where(output => output.Reason == IncrementalStepRunReason.Removed)
                .Select(output => HintNameOf(output.Value)),
        ];

    // The types of the file of the path.
    public static FileTypes TypesOf(GeneratorDriverRunResult result, string path) =>
        Outputs(result, TrackingNames.FileTypes)
            .Select(output => (FileTypes)output.Value)
            .Single(types => types.NormalizedPath == path);

    // Why the check of each file ran, by the file's path.
    public static Dictionary<string, IncrementalStepRunReason> Checks(GeneratorDriverRunResult result) =>
        Reasons<SidecarCheckResult>(result, TrackingNames.SidecarCheck, check => check.NormalizedPath);

    // What one type needs, named by the path of its one file.
    public static string NeedsKey(EquatableArray<FileNeeds> needs) => needs.ShouldHaveSingleItem().NormalizedPath;

    public static GeneratorDriverRunResult Run(GeneratorDriver driver, Compilation compilation) =>
        driver.RunGenerators(compilation, TestContext.Current.CancellationToken).GetRunResult();

    // Every output of a step, with its value and why it ran.
    public static (object Value, IncrementalStepRunReason Reason)[] Outputs(
        GeneratorDriverRunResult result,
        string step
    ) => [.. result.Results.Single().TrackedSteps[step].SelectMany(run => run.Outputs)];

    // Why each output of a step ran, keyed by something that names the output.
    public static Dictionary<string, IncrementalStepRunReason> Reasons<T>(
        GeneratorDriverRunResult result,
        string step,
        Func<T, string> key
    ) =>
        result
            .Results.Single()
            .TrackedSteps[step]
            .SelectMany(run => run.Outputs)
            .ToDictionary(output => key((T)output.Value), output => output.Reason);

    // Why the input of each file's read is what it is, by the file's path.  The read is the step before the one named
    // ParsedFile, which reports a file that was read again and gave an equal value as Cached.  So that a file was not
    // read is shown by its input here, and by ClaimedPaths, the other input of every read: neither is Modified or New.
    public static Dictionary<string, IncrementalStepRunReason> ParseInputs(GeneratorDriverRunResult result) =>
        Reasons<FileParseInput>(result, TrackingNames.FileParseInput, file => file.File.Path);

    // A step that is not among the tracked steps fails the test: it has lost its name.  The exceptions are the steps
    // that give nothing, and so are not tracked, in a run without what they read: FileSettings where no file has
    // metadata, SidecarInput and SidecarFile where there is no sidecar, and TypeNeeds where no query needs an entry.
    // Run_MetadataOfOneFileGainsAParameter_ParsesNoFileAndEmitsOnlyItsType reads the first in a run where a file has
    // metadata, and Run_MethodBodyEdited_ReadsNoSidecar, with the other tests of sidecars, the other three.
    public static IncrementalStepRunReason[] AllReasons(GeneratorDriverRunResult result, string step)
    {
        var steps = result.Results.Single().TrackedSteps;
        return
            step
                is TrackingNames.FileSettings
                    or TrackingNames.SidecarInput
                    or TrackingNames.SidecarFile
                    or TrackingNames.TypeNeeds
            && !steps.ContainsKey(step)
            ? []
            : [.. steps[step].SelectMany(run => run.Outputs).Select(output => output.Reason)];
    }

    // The steps that add source and report diagnostics.
    public static IncrementalStepRunReason[] OutputReasons(GeneratorDriverRunResult result) =>
        [
            .. result
                .Results.Single()
                .TrackedOutputSteps.SelectMany(steps => steps.Value)
                .SelectMany(run => run.Outputs)
                .Select(output => output.Reason),
        ];
}
