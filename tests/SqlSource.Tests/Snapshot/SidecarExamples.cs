using System;
using System.IO;

namespace SqlSource.Tests.Snapshot;

// Sample sidecars, as the files of Examples/ beside the tests, read from the output folder by that path.  Each stays
// valid against schemas/sidecar-v1.schema.json, which SidecarSchemaTests checks.
internal static class SidecarExamples
{
    public const string Users = "Users.sql.json";

    public const string Orders = "Orders.sql.json";

    public static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Snapshot", "Examples", name));
}
