using System.Collections.Generic;
using System.IO;
using SqlSource.Snapshot;
using SqlSource.Tool.Testing.Realistic;

namespace SqlSource.Tests.SqlServer.Realistic;

// The cases of Realistic/Cases, read from the repository through RealisticCaseReader of tests/SqlSource.Tool.Testing.
// A case folder that is wrong fails naming the case and the fault.
internal static class RealisticCases
{
    // SQL Server, whose recorded majors a checkedFrom names one of.
    public static RealisticEngine Engine { get; } =
        new(SidecarValues.Engine.SqlServer, "SQL Server", SqlServerContainer.RecordedMajors);

    // The name of every folder of Cases, in order: each is a case, and a folder that is no case fails when it is
    // read.  Listing them reads no case.json, so that a broken one fails its own case and not every one.
    public static IReadOnlyList<string> Names => RealisticCaseReader.NamesIn(RepositoryPaths.RealisticCases);

    // Every case of Cases, read afresh each time, so that a fault is the fault and not a failed type initializer.
    public static IReadOnlyList<RealisticCase> All => RealisticCaseReader.AllIn(RepositoryPaths.RealisticCases, Engine);

    // The case of that folder of Cases, read from the repository.
    public static RealisticCase ReadNamed(string name) => Read(Path.Combine(RepositoryPaths.RealisticCases, name));

    public static RealisticCase Read(string folder) => RealisticCaseReader.Read(folder, Engine);
}
