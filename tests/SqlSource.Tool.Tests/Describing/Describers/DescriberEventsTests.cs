using System.Linq;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Describers;

// The members both describers' events share: a relation, an origin, the head of a column and the tail of a table
// match, each with the names and order both engines' logs are read by.
public sealed class DescriberEventsTests
{
    private static readonly SidecarColumn Id = new(
        0,
        "id",
        new OtherEngineType("integer"),
        false,
        "catalog",
        new SidecarOrigin("public", "users", "id"),
        null,
        null
    );

    [Fact]
    public void TableMembers_SchemaAndTable_AreTheTwoMembersInOrder()
    {
        var members = DescriberEvents.TableMembers("public", "a.b");

        members.Select(member => member.Name).ShouldBe(["schema", "table"]);
        members[0].Value.ShouldBe((LogValue)"public");
        members[1].Value.ShouldBe((LogValue)"a.b");
    }

    [Fact]
    public void TableOf_SchemaAndTable_IsAMapOfThemApart() =>
        DescriberEvents.TableOf("public", "a.b").ShouldBe(LogValue.MapOf(new("schema", "public"), new("table", "a.b")));

    [Fact]
    public void TableOf_NoSchema_HasANullSchema() =>
        DescriberEvents.TableOf(null, "t").ShouldBe(LogValue.MapOf(new("schema", LogValue.Null), new("table", "t")));

    [Fact]
    public void OriginOf_AnOrigin_IsItsSchemaTableAndColumn() =>
        DescriberEvents
            .OriginOf(new SidecarOrigin("public", "users", "id"))
            .ShouldBe(LogValue.MapOf(new("schema", "public"), new("table", "users"), new("column", "id")));

    [Fact]
    public void OriginOf_NoOrigin_IsNull() => DescriberEvents.OriginOf(null).ShouldBe(LogValue.Null);

    [Fact]
    public void ColumnHead_AColumn_HasQueryOrdinalNameTypeAndOrigin()
    {
        var head = DescriberEvents.ColumnHead("ListUsers", Id);

        head.Select(member => member.Name).ShouldBe(["query", "ordinal", "name", "type", "origin"]);
        head[0].Value.ShouldBe((LogValue)"ListUsers");
        head[1].Value.ShouldBe((LogValue)0L);
        head[2].Value.ShouldBe((LogValue)"id");
        head[3].Value.ShouldBe((LogValue)"integer");
        head[4].Value.ShouldBe(DescriberEvents.OriginOf(Id.Origin));
    }

    [Fact]
    public void TableMatchTail_ATable_HasTheVerdictAndTheTable()
    {
        var tail = DescriberEvents.TableMatchTail(new SidecarTable("public", "users"), "matched");

        tail.Select(member => member.Name).ShouldBe(["tableMatch", "table"]);
        tail[0].Value.ShouldBe((LogValue)"matched");
        tail[1].Value.ShouldBe(DescriberEvents.TableOf("public", "users"));
    }

    [Fact]
    public void TableMatchTail_NoTable_HasANullTable()
    {
        var tail = DescriberEvents.TableMatchTail(null, "no-origin");

        tail[0].Value.ShouldBe((LogValue)"no-origin");
        tail[1].Value.ShouldBe(LogValue.Null);
    }
}
