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
            Holder = new CandidateHolder("dbf", CandidateMember.Parameter, false, "IDatabaseFactory", Location),
        };

        candidate.DisplayName.ShouldBe("dbf.Open");
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

    [Fact]
    public void DisplayName_HeldWithoutHolder_IsItsName() =>
        (Candidate("Open") with { Tier = CandidateTier.Held }).DisplayName.ShouldBe("Open");

    [Fact]
    public void DisplayName_GlobalWithoutDot_IsTextDotName()
    {
        var candidate = Candidate("Reports") with { Tier = CandidateTier.Global, GlobalTypeText = "Db" };

        candidate.DisplayName.ShouldBe("Db.Reports");
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
