using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Shouldly;

namespace SqlSource.DatabaseTests.TestHarness.EndToEnd;

// The JSON values that the JSON tests of every engine write and compare.
internal static class JsonElements
{
    // The element that the JSON parses to, which outlives the document it came from.
    public static JsonElement ElementOf([StringSyntax(StringSyntaxAttribute.Json)] string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    // Fails unless the element read is not null and equals the element written by value, whatever the order of the
    // properties' text.
    public static void ShouldBeSame(JsonElement? read, JsonElement? written, string name) =>
        JsonElement.DeepEquals(read.ShouldNotBeNull(name), written!.Value).ShouldBeTrue(name);
}
