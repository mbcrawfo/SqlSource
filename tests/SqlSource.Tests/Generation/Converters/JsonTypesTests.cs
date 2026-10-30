using Shouldly;
using SqlSource.Generation.Converters;
using Xunit;

namespace SqlSource.Tests.Generation.Converters;

public sealed class JsonTypesTests
{
    [Theory]
    [InlineData(JsonTypes.Element, true)]
    [InlineData(JsonTypes.Document, true)]
    [InlineData(JsonTypes.Node, true)]
    [InlineData(JsonTypes.Object, true)]
    [InlineData(JsonTypes.Array, true)]
    [InlineData("global::System.Text.Json.Nodes.JsonValue", false)]
    [InlineData("string", false)]
    [InlineData("System.Text.Json.JsonElement", false)]
    [InlineData("global::System.Text.Json.JsonElement?", false)]
    public void IsJsonType_OfTheText_IsWhetherItIsOneOfTheFive(string text, bool expected) =>
        JsonTypes.IsJsonType(text).ShouldBe(expected);

    [Theory]
    [InlineData(JsonTypes.Node, true)]
    [InlineData(JsonTypes.Object, true)]
    [InlineData(JsonTypes.Array, true)]
    [InlineData(JsonTypes.Element, false)]
    [InlineData(JsonTypes.Document, false)]
    [InlineData("global::System.Text.Json.Nodes.JsonValue", false)]
    [InlineData("global::System.Text.Json.Nodes.JsonNode?", false)]
    public void IsNode_OfTheText_IsWhetherItIsANode(string text, bool expected) =>
        JsonTypes.IsNode(text).ShouldBe(expected);
}
