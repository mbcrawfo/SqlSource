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

    // A value whose own ToString drops what it holds, as a NodaTime value's drops its sub-second digits.
    private sealed class Lossy(int value)
    {
        public int Value => value;

        public override string ToString() => "lossy";
    }

    private sealed record Holder(Lossy Top, int Count);

    // An engine's Render, which is never given null.
    private static string? Library(object value) =>
        value switch
        {
            null => throw new ArgumentNullException(nameof(value)),
            Lossy lossy => "lossy " + lossy.Value.ToString(CultureInfo.InvariantCulture),
            int number => "int " + number.ToString(CultureInfo.InvariantCulture),
            _ => null,
        };

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
    public void Of_Memory_RendersItsItems()
    {
        Rendering.Of(new ReadOnlyMemory<float>([1.5f, -2.25f])).ShouldBe("[1.5, -2.25]");
        Rendering.Of(new Memory<int>([3])).ShouldBe("[3]");
    }

    [Fact]
    public void Of_TypeWithItsOwnToString_UsesIt() => Rendering.Of(new Own()).ShouldBe("own");

    // The library is asked before anything else, the framework's types too, in every place a value can hold another.
    [Fact]
    public void Of_ValueTheLibraryRenders_UsesItFirstAtEveryDepth()
    {
        Rendering.Of(new Lossy(1), Library).ShouldBe("lossy 1");
        Rendering.Of(new Holder(new Lossy(2), 3), Library).ShouldBe("Holder { Top = lossy 2, Count = int 3 }");
        Rendering.Of(new List<Lossy> { new(4) }, Library).ShouldBe("[lossy 4]");
        Rendering.Of((new Lossy(5), 6), Library).ShouldBe("(lossy 5, int 6)");
        Rendering.Of(new ReadOnlyMemory<int>([7]), Library).ShouldBe("[int 7]");
        Rendering.Of(null, Library).ShouldBe("null");
    }

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
