using System;
using System.IO;

namespace SqlSource.Tests.Snapshot;

// Sample sidecars, as files beside the tests.  SidecarSchemaTests validates them against
// schemas/sidecar-v1.schema.json.
internal static class SidecarExamples
{
    public const string Users = "Users.sql.json";

    public const string Orders = "Orders.sql.json";

    public static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Snapshot", "Examples", name));
}
