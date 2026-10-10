using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SqlSource.Tool.Processes;

namespace SqlSource.Tool.Tests;

// What FakeProcessRunner answers for one project, in MSBuild's place.  As it is made, it is a restored project
// with one target framework that uses SqlSource and whose manifest holds nothing but the project.
internal sealed class FakeProject
{
    // The value of SqlSourceImported.  With several frameworks it is what an evaluation with a framework gives:
    // one without gives none, as NuGet imports the package's props under a condition on the framework.
    public string Imported { get; set; } = "true";

    // The value of SqlSourceImported for a framework that differs from Imported.
    public Dictionary<string, string> ImportedByFramework { get; } = [];

    public string TargetFramework { get; set; } = "net10.0";

    public string TargetFrameworks { get; set; } = "";

    public string ProjectAssetsFile { get; set; } = "";

    public List<string> PackageReferences { get; } = [];

    // What the evaluation prints in place of its JSON.
    public string? EvaluationOutput { get; set; }

    public int EvaluationExitCode { get; set; }

    public int TargetExitCode { get; set; }

    // What a run that fails prints.
    public string Output { get; set; } = "";

    public string Error { get; set; } = "";

    // Whether the target writes the file it was asked for.
    public bool WritesManifest { get; set; } = true;

    // The text of the manifest; when null, the version, the project and the framework.
    public string? Manifest { get; set; }

    public ProcessResult Evaluate(ProcessRequest request)
    {
        if (EvaluationExitCode != 0)
        {
            return new ProcessResult(EvaluationExitCode, Output, Error);
        }

        if (EvaluationOutput is not null)
        {
            return new ProcessResult(0, EvaluationOutput, Error);
        }

        var framework = FakeProcessRunner.PropertyOf(request, "TargetFramework");
        var several = TargetFrameworks.Length > 0;
        var imported =
            several && framework is null ? "" : ImportedByFramework.GetValueOrDefault(framework ?? "", Imported);
        var json = JsonSerializer.Serialize(
            new
            {
                Properties = new
                {
                    SqlSourceImported = imported,
                    TargetFramework = several ? framework ?? "" : TargetFramework,
                    TargetFrameworks,
                    ProjectAssetsFile,
                },
                Items = new
                {
                    PackageReference = PackageReferences.Select(name => new { Identity = name, Version = "1.0.0" }),
                },
            }
        );
        return new ProcessResult(0, json, "");
    }

    public ProcessResult WriteManifest(ProcessRequest request)
    {
        if (TargetExitCode != 0)
        {
            return new ProcessResult(TargetExitCode, Output, Error);
        }

        if (WritesManifest)
        {
            var file = FakeProcessRunner.PropertyOf(request, "SqlSourceManifestFile")!;
            var framework = FakeProcessRunner.PropertyOf(request, "TargetFramework") ?? TargetFramework;
            File.WriteAllText(
                file,
                Manifest ?? $"SqlSourceManifest=1\nProject={request.Arguments[1]}\nTargetFramework={framework}\n"
            );
        }

        return new ProcessResult(0, Output, Error);
    }
}
