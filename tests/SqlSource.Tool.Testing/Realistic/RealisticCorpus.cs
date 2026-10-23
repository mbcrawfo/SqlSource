using System.Collections.Generic;
using System.IO;
using SqlSource.Snapshot;
using SqlSource.Testing;

namespace SqlSource.Tool.Testing.Realistic;

// The realistic corpus of an engine, the folder Realistic/ of that engine's project, read from the repository: the
// schema and the seed of its database "realistic", the folder of its cases, the file of the classes that claim them,
// and the engine its cases are read for.  A case folder that is wrong fails naming the case and the fault.
internal sealed class RealisticCorpus
{
    private RealisticCorpus(string project, string engine, EngineImage image)
    {
        Project = project;
        Engine = new RealisticEngine(engine, image.Product, image.RecordedMajors);
    }

    public static RealisticCorpus Postgres { get; } =
        new("SqlSource.Tests.Postgres", SidecarValues.Engine.Postgres, EngineImage.Postgres);

    public static RealisticCorpus SqlServer { get; } =
        new("SqlSource.Tests.SqlServer", SidecarValues.Engine.SqlServer, EngineImage.SqlServer);

    // The engine, whose recorded majors a checkedFrom names one of.  It needs no repository, so that a test of a case
    // that reads no file can build on it.
    public RealisticEngine Engine { get; }

    // The folder of the engine's project under tests/, which holds the corpus in Realistic/.
    public string Project { get; }

    // The paths are found from the repository's root when they are read, never when the corpus is made.
    public string Folder => Path.Combine(RepositoryRoot.Folder, "tests", Project, "Realistic");

    public string Schema => Path.Combine(Folder, "schema.sql");

    public string Seed => Path.Combine(Folder, "seed.sql");

    public string Cases => Path.Combine(Folder, "Cases");

    // The classes that claim each case with models as a query of the project.
    public string Queries => Path.Combine(Folder, "Generation", "RealisticQueries.cs");

    // The name of every folder of Cases, in order: each is a case, and a folder that is no case fails when it is
    // read.  Listing them reads no case.json, so that a broken one fails its own case and not every one.
    public IReadOnlyList<string> Names => RealisticCaseReader.NamesIn(Cases);

    // Every case of Cases, read afresh each time, so that a fault is the fault and not a failed type initializer.
    public IReadOnlyList<RealisticCase> All => RealisticCaseReader.AllIn(Cases, Engine);

    // The case of that folder of Cases, read from the repository.
    public RealisticCase ReadNamed(string name) => Read(Path.Combine(Cases, name));

    public RealisticCase Read(string folder) => RealisticCaseReader.Read(folder, Engine);
}
