using System;
using System.IO;

namespace SqlSource.Tests.Snapshot;

// The examples of the sidecar format design, sections 4.1 and 4.2, as files beside the tests.  They are copies:
// a pull request that changes an example changes it in the document and here.
internal static class SidecarExamples
{
    public const string Users = "Users.sql.json";

    public const string Orders = "Orders.sql.json";

    public static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Snapshot", "Examples", name));
}
