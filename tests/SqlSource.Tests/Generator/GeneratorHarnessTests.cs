// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Chains;
using SqlSource.Settings;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generator;

// The output, the dialect, the generator parameters and the settings of a run are the project's settings file, at
// GeneratorHarness.ProjectSettingsPath, which the harness writes among the run's additional files.
public sealed class GeneratorHarnessTests
{
    private const string Kept = "-- name: Q\nSELECT 1 -- kept\n";

    // Under ansi, Models would be SQLSRC209 for a dialect without a database.  Under postgres the query needs an entry
    // in its sidecar, which the run has not.
    [Fact]
    public void Run_OutputAndDialect_AreTheProjectsSettingsFile()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Models,
            [new SourceFile(GeneratorHarness.SourcePath, GeneratorHarness.SampleSource(null))],
            [GeneratorHarness.UsersFile],
            dialect: "postgres"
        );

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC241 " + GeneratorHarness.UsersPath + "(");
        var file = StepOutputs.Single<ListedSettingsFile>(run, TrackingNames.SettingsFile);
        file.Path.ShouldBe(GeneratorHarness.ProjectSettingsPath);
        file.File.Errors.ShouldBeEmpty();
    }

    // At sql the query needs no entry, so the run is clean.
    [Fact]
    public void Run_SettingsEntry_WinsOverTheOutput()
    {
        var run = GeneratorHarness.Run(
            OutputKind.CodeGen,
            [new SourceFile(GeneratorHarness.SourcePath, GeneratorHarness.SampleSource(null))],
            [GeneratorHarness.UsersFile],
            dialect: "postgres",
            settings: /*lang=json,strict*/
            """{"output": "Sql"}"""
        );

        run.ShouldBeClean();
        run.Sources["App.Sample.g.cs"].ShouldContain("public const string GetUser = ");
    }

    // The harness writes the words, split at white space, as the array of the project's settings file.
    [Fact]
    public void Run_GeneratorParameters_AreTheArrayOfTheirWords()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [new SourceFile(GeneratorHarness.SourcePath, GeneratorHarness.SampleSource(null))],
            [new SqlFile("/app/Repo/Q.sql", Kept)],
            generatorParameters: " sort-output  keep-comments "
        );

        run.ShouldBeClean();
        run.Sources["App.Sample.g.cs"].ShouldContain("public const string Q = \"SELECT 1 -- kept\";");
        var file = StepOutputs.Single<ListedSettingsFile>(run, TrackingNames.SettingsFile);
        file.Path.ShouldBe(GeneratorHarness.ProjectSettingsPath);
        file.File.Errors.ShouldBeEmpty();
        file.File.Level.Parameters.ShouldBe(GeneratorParameters.SortOutput | GeneratorParameters.KeepComments);
    }

    // A value is escaped as JSON escapes it, here a quote and a line break, and the words are an array.  The dialect
    // is not valid, which a test of the text alone leaves to the generator.
    [Fact]
    public void Run_ProjectsFile_IsTheJsonAUserWrites()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Models,
            [new SourceFile(GeneratorHarness.SourcePath, GeneratorHarness.SampleSource(null))],
            [],
            generatorParameters: " keep-comments  sort-output ",
            dialect: "a\"b\nc"
        );

        run.ProjectSettings.ShouldBe(
            "{\n  \"output\": \"Models\",\n  \"dialect\": \"a\\\"b\\nc\",\n  \"generatorParameters\": [\n"
                + "    \"keep-comments\",\n    \"sort-output\"\n  ]\n}"
        );
    }

    // The spelling is the tests' own, so a change to the generator's spelling fails a test.  A test's data cannot be of
    // the generator's internal type, so the member is found by its spelling.
    [Theory]
    [InlineData("Sql")]
    [InlineData("Models")]
    [InlineData("CodeGen")]
    public void Run_Output_IsSpelledAsAUserWritesIt(string spelled)
    {
        var run = GeneratorHarness.Run(Enum.Parse<OutputKind>(spelled), GeneratorHarness.SampleSource(null));

        run.ProjectSettings.ShouldBe("{\n  \"output\": \"" + spelled + "\"\n}");
    }

    [Fact]
    public void Run_NothingGiven_WritesNoSettingsFile()
    {
        var run = GeneratorHarness.Run(null, GeneratorHarness.SampleSource(null), GeneratorHarness.UsersFile);

        StepOutputs.Of<ListedSettingsFile>(run, TrackingNames.SettingsFile).ShouldBeEmpty();
        run.ProjectSettings.ShouldBeNull();
    }

    // The two files would be of one folder, and the generator would read only one of them.
    [Fact]
    public void Run_OwnFileAtTheProjectsPath_IsRefused()
    {
        var exception = Should.Throw<InvalidOperationException>(() =>
            GeneratorHarness.Run(
                OutputKind.Sql,
                GeneratorHarness.SampleSource(null),
                GeneratorHarness.UsersFile,
                GeneratorHarness.SettingsFile(
                    GeneratorHarness.ProjectSettingsPath, /*lang=json,strict*/
                    """{"dialect": "postgres"}"""
                )
            )
        );

        exception.Message.ShouldBe(
            "The run writes the project's settings file /sqlsource.json, and a file of the test is at that path.  Put "
                + "its keys in the run's settings instead."
        );
    }

    // A run that writes no file leaves the path to the test's own.
    [Fact]
    public void Run_OwnFileAtTheProjectsPathWithNothingGiven_IsTheProjectsFile()
    {
        var run = GeneratorHarness.Run(
            null,
            GeneratorHarness.SampleSource(null),
            new SqlFile("/app/Repo/Q.sql", Kept),
            GeneratorHarness.SettingsFile(
                GeneratorHarness.ProjectSettingsPath,
                /*lang=json,strict*/
                """{"output": "Sql", "generatorParameters": ["keep-comments"]}"""
            )
        );

        run.ShouldBeClean();
        run.Sources["App.Sample.g.cs"].ShouldContain("public const string Q = \"SELECT 1 -- kept\";");
    }
}
