using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Describing;

// A connection's value in an entry of a sidecar: found in each member that a describer gives, and in none that the
// plan gives.  A member of an entry that a describer gives and that the check takes for the plan's would hold the
// value unseen, so each has its fact here.
public sealed class EntrySecretCheckTests
{
    private static readonly SidecarParameter Id = new("id", 0, new OtherEngineType("int4"), null, "describe");

    private static readonly SidecarColumn Email = new(
        0,
        "email",
        new OtherEngineType("text"),
        null,
        null,
        null,
        null,
        null
    );

    // An entry of a statement with rows, so that the writer writes every member of it.
    private static readonly SidecarEntry Entry = new(
        "GetUser",
        default,
        "0123abcd",
        "postgres",
        "main",
        "16.4",
        SidecarResultKind.Rows,
        new SidecarTable("public", "users"),
        "not-needed",
        "no-origin",
        new EquatableArray<SidecarParameter>([Id]),
        new EquatableArray<SidecarColumn>([Email])
    );

    private static readonly ConnectionSecret Secret = new(Secrets.Connection);

    private static EquatableArray<SidecarParameter> Parameters(SidecarParameter parameter) => new([parameter]);

    [Fact]
    public void Holds_EntryWithoutTheValue_IsFalse() => EntrySecretCheck.Holds(Entry, Secret).ShouldBeFalse();

    [Fact]
    public void Holds_ValueInTheServersVersion_IsTrue() =>
        EntrySecretCheck.Holds(Entry with { ServerVersion = Secrets.Connection }, Secret).ShouldBeTrue();

    [Fact]
    public void Holds_ValueInTheSchemaOfTheMatchedTable_IsTrue() =>
        EntrySecretCheck
            .Holds(Entry with { MatchesTable = new SidecarTable(Secrets.Connection, "users") }, Secret)
            .ShouldBeTrue();

    [Fact]
    public void Holds_ValueInTheNameOfTheMatchedTable_IsTrue() =>
        EntrySecretCheck
            .Holds(Entry with { MatchesTable = new SidecarTable("public", Secrets.Connection) }, Secret)
            .ShouldBeTrue();

    [Fact]
    public void Holds_ValueInThePlan_IsTrue() =>
        EntrySecretCheck.Holds(Entry with { Plan = Secrets.Connection }, Secret).ShouldBeTrue();

    [Fact]
    public void Holds_ValueInTheTableMatch_IsTrue() =>
        EntrySecretCheck.Holds(Entry with { TableMatch = Secrets.Connection }, Secret).ShouldBeTrue();

    [Fact]
    public void Holds_ValueInTheTypeOfAParameter_IsTrue() =>
        EntrySecretCheck
            .Holds(
                Entry with
                {
                    Parameters = Parameters(Id with { Type = new OtherEngineType(Secrets.Connection) }),
                },
                Secret
            )
            .ShouldBeTrue();

    [Fact]
    public void Holds_ValueInTheTypeSourceOfAParameter_IsTrue() =>
        EntrySecretCheck
            .Holds(Entry with { Parameters = Parameters(Id with { TypeSource = Secrets.Connection }) }, Secret)
            .ShouldBeTrue();

    [Fact]
    public void Holds_ValueInAColumn_IsTrue() =>
        EntrySecretCheck
            .Holds(
                Entry with
                {
                    Columns = new EquatableArray<SidecarColumn>([Email with { Name = Secrets.Connection }]),
                },
                Secret
            )
            .ShouldBeTrue();

    // What the plan gives is in the file whatever the describer does.
    [Theory]
    [InlineData("GetUser")]
    [InlineData("0123abcd")]
    [InlineData("postgres")]
    [InlineData("main")]
    [InlineData("id")]
    public void Holds_ValueThatThePlanGave_IsFalse(string value) =>
        EntrySecretCheck.Holds(Entry, new ConnectionSecret(value)).ShouldBeFalse();

    // The owner's decision: a part is never searched for in an entry, so a password that stands in one is no fault.
    [Fact]
    public void Holds_PasswordInAnEntry_IsFalse() =>
        EntrySecretCheck
            .Holds(
                Entry with
                {
                    ServerVersion = "hunter22",
                },
                new ConnectionSecret("host=db;password=hunter22", ["hunter22"])
            )
            .ShouldBeFalse();

    // A form of the value counts as the whole value.
    [Fact]
    public void Holds_FormInTheServersVersion_IsTrue() =>
        EntrySecretCheck
            .Holds(
                Entry with
                {
                    ServerVersion = "Host=db;Port=5432",
                },
                new ConnectionSecret("host=db", forms: ["Host=db;Port=5432"])
            )
            .ShouldBeTrue();

    // A driver may trim the value, so the trimmed text is as much the value as the value is.
    [Fact]
    public void Holds_TrimmedValueInAMemberOfTheDescriber_IsTrue() =>
        EntrySecretCheck
            .Holds(Entry with { ServerVersion = "Host=db;Password=pw" }, new ConnectionSecret("Host=db;Password=pw \n"))
            .ShouldBeTrue();

    // What the plan gave is in the file whatever the describer does, for a form as for the value.
    [Fact]
    public void Holds_FormThatThePlanGave_IsFalse() =>
        EntrySecretCheck
            .Holds(
                Entry with
                {
                    ServerVersion = "GetUser",
                },
                new ConnectionSecret("Host=db;Password=pw", forms: ["GetUser"])
            )
            .ShouldBeFalse();
}
