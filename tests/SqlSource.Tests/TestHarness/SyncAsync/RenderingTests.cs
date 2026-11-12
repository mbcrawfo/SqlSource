using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using SqlSource.Testing;
using Xunit;

namespace SqlSource.Tests.TestHarness.SyncAsync;

public sealed class RenderingTests
{
    private sealed record Row(int Id, int[] Values);

    private sealed class Own
    {
        public override string ToString() => "own";
    }

    [Fact]
    public void Of_Null_IsNull() => Rendering.Of(null).ShouldBe("null");

    [Fact]
    public void Of_RecordWithArray_RendersElements() =>
        Rendering.Of(new Row(1, [1, 2])).ShouldBe("Row { Id = 1, Values = [1, 2] }");

    [Fact]
    public void Of_DecimalAndDouble_AreInvariant()
    {
        using var scope = new CultureScope(new CultureInfo("de-DE"));

        Rendering.Of(1.5m).ShouldBe("1.5");
        Rendering.Of(2.25d).ShouldBe("2.25");
        Rendering.Of(new List<object> { 3.5f, -4 }).ShouldBe("[3.5, -4]");
    }

    [Fact]
    public void Of_JsonElement_IsRawText()
    {
        using var document = JsonDocument.Parse("""{"a": [1,  2]}""");

        Rendering
            .Of(document.RootElement)
            .ShouldBe( /*lang=json,strict*/
                """{"a": [1,  2]}"""
            );
    }

    [Fact]
    public void Of_ListOfRecords_KeepsOrder() =>
        Rendering
            .Of(new List<Row> { new(2, []), new(1, [5]) })
            .ShouldBe("[Row { Id = 2, Values = [] }, Row { Id = 1, Values = [5] }]");

    [Fact]
    public void Of_DateTime_IncludesKind() =>
        Rendering
            .Of(new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc))
            .ShouldBe("2024-01-02T03:04:05.0000000Z (Utc)");

    [Fact]
    public void Of_TypeWithItsOwnToString_UsesIt() => Rendering.Of(new Own()).ShouldBe("own");

    [Fact]
    public void Of_StringAndBytes_AreQuotedAndHex()
    {
        Rendering.Of("a").ShouldBe("\"a\"");
        Rendering.Of(new byte[] { 1, 255 }).ShouldBe("0x01FF");
    }

    [Fact]
    public void Masked_Guid_IsReplaced() =>
        Rendering
            .Masked("ClientConnectionId:0f8fad5b-d9cb-469f-a165-70867728950e")
            .ShouldBe("ClientConnectionId:<guid>");
}
