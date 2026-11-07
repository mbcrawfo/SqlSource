using System.IO;
using System.Text.Json.Nodes;
using Shouldly;

namespace SqlSource.DatabaseTests.TestHarness.EndToEnd;

// The check of the variant copies of a query, which every end-to-end project makes of its committed entries.
internal static class VariantCopies
{
    // Fails unless the committed entry of the copy, in the file's entries of the folder, has the hash of the entry of
    // its original.
    public static void ShouldShareHash(string queriesFolder, string file, string original, string copy)
    {
        var queries = JsonNode
            .Parse(File.ReadAllText(Path.Combine(queriesFolder, file + ".json")))
            .ShouldNotBeNull()["queries"]
            .ShouldNotBeNull();
        var hash = queries[original].ShouldNotBeNull()["hash"].ShouldNotBeNull().GetValue<string>();

        queries[copy].ShouldNotBeNull()["hash"].ShouldNotBeNull().GetValue<string>().ShouldBe(hash);
    }
}
