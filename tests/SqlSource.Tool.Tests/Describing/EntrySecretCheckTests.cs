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

    private static EquatableArray<SidecarParameter> Parameters(SidecarParameter parameter) => new([parameter]);

    [Fact]
    public void Holds_EntryWithoutTheValue_IsFalse() =>
        EntrySecretCheck.Holds(Entry, new ConnectionSecret(Secrets.Connection)).ShouldBeFalse();

    [Fact]
    public void Holds_ValueInTheServersVersion_IsTrue() =>
        EntrySecretCheck
            .Holds(Entry with { ServerVersion = Secrets.Connection }, new ConnectionSecret(Secrets.Connection))
            .ShouldBeTrue();

    [Fact]
    public void Holds_ValueInTheSchemaOfTheMatchedTable_IsTrue() =>
        EntrySecretCheck
            .Holds(
                Entry with
                {
                    MatchesTable = new SidecarTable(Secrets.Connection, "users"),
                },
                new ConnectionSecret(Secrets.Connection)
            )
            .ShouldBeTrue();

    [Fact]
    public void Holds_ValueInTheNameOfTheMatchedTable_IsTrue() =>
        EntrySecretCheck
            .Holds(
                Entry with
                {
                    MatchesTable = new SidecarTable("public", Secrets.Connection),
                },
                new ConnectionSecret(Secrets.Connection)
            )
            .ShouldBeTrue();

    [Fact]
    public void Holds_ValueInThePlan_IsTrue() =>
        EntrySecretCheck
            .Holds(Entry with { Plan = Secrets.Connection }, new ConnectionSecret(Secrets.Connection))
            .ShouldBeTrue();

    [Fact]
    public void Holds_ValueInTheTableMatch_IsTrue() =>
        EntrySecretCheck
            .Holds(Entry with { TableMatch = Secrets.Connection }, new ConnectionSecret(Secrets.Connection))
            .ShouldBeTrue();

    [Fact]
    public void Holds_ValueInTheTypeOfAParameter_IsTrue() =>
        EntrySecretCheck
            .Holds(
                Entry with
                {
                    Parameters = Parameters(Id with { Type = new OtherEngineType(Secrets.Connection) }),
                },
                new ConnectionSecret(Secrets.Connection)
            )
            .ShouldBeTrue();

    [Fact]
    public void Holds_ValueInTheTypeSourceOfAParameter_IsTrue() =>
        EntrySecretCheck
            .Holds(
                Entry with
                {
                    Parameters = Parameters(Id with { TypeSource = Secrets.Connection }),
                },
                new ConnectionSecret(Secrets.Connection)
            )
            .ShouldBeTrue();

    [Fact]
    public void Holds_ValueInAColumn_IsTrue() =>
        EntrySecretCheck
            .Holds(
                Entry with
                {
                    Columns = new EquatableArray<SidecarColumn>([Email with { Name = Secrets.Connection }]),
                },
                new ConnectionSecret(Secrets.Connection)
            )
            .ShouldBeTrue();

    private static SidecarEntry WithColumn(string name) =>
        Entry with
        {
            Columns = new EquatableArray<SidecarColumn>([Email with { Name = name }]),
        };

    // A password equal to a name of the database: the describer gave it, so it is a fault like any other.
    [Fact]
    public void Holds_PasswordPartAsAColumnsWholeName_IsTrue() =>
        EntrySecretCheck
            .Holds(WithColumn("admin"), new ConnectionSecret("Host=db;Password=admin", ["admin"]))
            .ShouldBeTrue();

    [Fact]
    public void Holds_PasswordPartInsideAColumnsName_IsFalse() =>
        EntrySecretCheck
            .Holds(WithColumn("is_admin_x"), new ConnectionSecret("Host=db;Password=admin", ["admin"]))
            .ShouldBeFalse();

    // The engine holds the word whatever the describer gave.
    [Fact]
    public void Holds_PasswordPartThatTheEngineHolds_IsFalse() =>
        EntrySecretCheck
            .Holds(WithColumn("postgres"), new ConnectionSecret("Host=db;Password=postgres", ["postgres"]))
            .ShouldBeFalse();

    // What the plan gives is in the file whatever the describer does.
    [Theory]
    [InlineData("GetUser")]
    [InlineData("0123abcd")]
    [InlineData("postgres")]
    [InlineData("main")]
    [InlineData("id")]
    public void Holds_ValueThatThePlanGave_IsFalse(string value) =>
        EntrySecretCheck.Holds(Entry, new ConnectionSecret(value)).ShouldBeFalse();
}
