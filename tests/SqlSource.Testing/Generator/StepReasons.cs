using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Methods;
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

    // The files of the models that went.  A model is emitted from the node of its file, in the slot of its query and
    // role, so what goes is the model that went, and a model that moves to another file's node goes from the old one.
    // A slot of several models loses its last place, so what goes there is the last model it held, which may be
    // another.
    public static string[] ModelsRemoved(GeneratorDriverRunResult result) =>
        [
            .. Outputs(result, TrackingNames.ModelOutput)
                .Where(output => output.Reason == IncrementalStepRunReason.Removed)
                .Select(output => HintNameOf(output.Value)),
        ];

    // Why each type's methods ran at one of their steps, MethodsInput, TypeMethods or MethodOutput, by the name of the
    // file of the type's methods, as App.UserQueries-Methods.g.cs.
    public static Dictionary<string, IncrementalStepRunReason> MethodReasons(
        GeneratorDriverRunResult result,
        string step
    ) => Outputs(result, step).ToDictionary(output => MethodsFileOf(output.Value), output => output.Reason);

    // Why each type's file ran at TypeOutput, by the file's name, as App.UserQueries.g.cs.
    public static Dictionary<string, IncrementalStepRunReason> TypeReasons(GeneratorDriverRunResult result) =>
        Reasons<TypeOutput>(result, TrackingNames.TypeOutput, output => output.HintName);

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

    // Why the input of each file's claim is what it is, by the file's path.  A file's claim is the step after this one,
    // and it is read only when the claim is new or has changed.  So that a file was not read is shown by its claim,
    // which is neither Modified nor New (Claims), or by its input here and ClaimedPaths, neither of which is.
    public static Dictionary<string, IncrementalStepRunReason> ParseInputs(GeneratorDriverRunResult result) =>
        Reasons<FileParseInput>(result, TrackingNames.FileParseInput, file => file.File.Path);

    // Why the claim of each file ran, by the file's path.  A file is read when its claim is Modified or New, and only
    // then: ParsedFile alone cannot show it, since it reports a file that was read again and came out equal as Cached.
    public static Dictionary<string, IncrementalStepRunReason> Claims(GeneratorDriverRunResult result) =>
        Reasons<FileClaim>(result, TrackingNames.FileClaim, claim => claim.Input.File.Path);

    // A step that is not among the tracked steps fails the test: it has lost its name.  The exceptions are the steps
    // that give nothing, and so are not tracked, in a run without what they read: SettingsFile where there is no
    // settings file, FileSettings where no file has metadata, FileLevel where no file has a settings file above it or
    // metadata, SidecarInput and SidecarFile where there is no sidecar, and TypeNeeds where no query needs an entry.
    // Every run of CachingScene has the project's settings file but a first run of a test of the MSBuild path,
    // FirstRunOnMSBuild, so only such a run gives nothing at the first and the third.
    // CachingParseTests.Run_MethodBodyEdited_TakesEveryStepFromThePreviousRun reads the first and the third in a run
    // with the project's settings file, as CachingSettingsFileTests reads the first and
    // CachingOptionsTests.Run_SettingsFileOutputChanged_EmitsTheTypesOfItsFolderAndParsesNoFile the third in runs with
    // settings files of folders,
    // CachingOptionsTests.Run_MetadataOfOneFileGainsAParameter_ParsesNoFileAndEmitsOnlyItsType the second in a run
    // where a file has metadata, and CachingSidecarTests.Run_MethodBodyEdited_ReadsNoSidecar, with the other tests of
    // sidecars, the other three.
    public static IncrementalStepRunReason[] AllReasons(GeneratorDriverRunResult result, string step)
    {
        var steps = result.Results.Single().TrackedSteps;
        return
            step
                is TrackingNames.SettingsFile
                    or TrackingNames.FileSettings
                    or TrackingNames.FileLevel
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

    // A method output is its hint name and its source, as a model output is.
    private static string MethodsFileOf(object value) =>
        value switch
        {
            MethodsInput input => HintName.ForMethods(input.Type.HintName),
            TypeMethods methods => methods.HintName,
            _ => HintNameOf(value),
        };
}
