using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation.Repositories;
using Xunit;

namespace SqlSource.Tests.Generation.Repositories;

public sealed class ConnectionCandidateTests
{
    private static readonly LocationInfo Location = new("/app/Repo.cs", new TextSpan(0, 4), default);

    [Fact]
    public void DisplayName_OwnMember_IsItsName() =>
        Candidate("_connectionString").DisplayName.ShouldBe("_connectionString");

    [Fact]
    public void DisplayName_HeldMember_IsHolderDotName()
    {
        var candidate = Candidate("Open") with
        {
            Tier = CandidateTier.Held,
            Holder = new CandidateHolder("dbf", CandidateMember.Parameter, false, "IDatabaseFactory", Location, true),
        };

        candidate.DisplayName.ShouldBe("dbf.Open");
    }

    // A held member reached through a cast of its holder is named with its interface, so that a member of two
    // interfaces, called directly through one and through a cast to the other, gives two names.  A generic interface
    // keeps its type arguments.
    [Theory]
    [InlineData("global::App.IAuditOpener", "IAuditOpener", "dbf.IAuditOpener.Open")]
    [InlineData(
        "global::App.IFactory<global::Npgsql.NpgsqlConnection>",
        "IFactory<NpgsqlConnection>",
        "dbf.IFactory<NpgsqlConnection>.Open"
    )]
    public void DisplayName_HeldMemberReachedThroughACast_IsHolderDotInterfaceDotName(
        string castText,
        string castName,
        string displayName
    )
    {
        var candidate = Candidate("Open") with
        {
            Tier = CandidateTier.Held,
            Holder = new CandidateHolder("dbf", CandidateMember.Parameter, false, "DatabaseFactory", Location, true),
            CastTypeText = castText,
            CastTypeName = castName,
        };

        candidate.DisplayName.ShouldBe(displayName);
    }

    [Fact]
    public void DisplayName_Global_IsTypeDotName()
    {
        var candidate = Candidate("Reports") with
        {
            Tier = CandidateTier.Global,
            GlobalTypeText = "global::App.Outer.Db",
        };

        candidate.DisplayName.ShouldBe("Db.Reports");
    }

    // A message names a type as a value, without the @ that its text keeps for access.
    [Fact]
    public void DisplayName_GlobalOfAKeywordType_IsWithoutTheEscape()
    {
        var candidate = Candidate("Main") with { Tier = CandidateTier.Global, GlobalTypeText = "global::App.@class" };

        candidate.DisplayName.ShouldBe("class.Main");
    }

    [Fact]
    public void DisplayName_HeldWithoutHolder_IsItsName() =>
        (Candidate("Open") with { Tier = CandidateTier.Held }).DisplayName.ShouldBe("Open");

    // A type of the global namespace has no namespace in its text, whose last name follows the alias's colons.
    [Theory]
    [InlineData("global::Db", "Db.Reports")]
    [InlineData("global::@class", "class.Reports")]
    [InlineData("global::Outer.Db", "Db.Reports")]
    public void DisplayName_GlobalOfTheGlobalNamespace_IsTypeDotName(string typeText, string displayName)
    {
        var candidate = Candidate("Reports") with { Tier = CandidateTier.Global, GlobalTypeText = typeText };

        candidate.DisplayName.ShouldBe(displayName);
    }

    private static ConnectionCandidate Candidate(string name) =>
        new(
            name,
            CandidateMember.Field,
            CandidateType.ConnectionString,
            IsAsync: false,
            TakesCancellationToken: false,
            IsStatic: false,
            IsInherited: false,
            IsMarked: false,
            Database: null,
            DeclaredTypeName: "System.String",
            Location
        );
}
