using Shouldly;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.Realistic.Generation;

// The SQL of each case with models, as an in-process run at Models writes it, is the constant the build compiled for
// its claim in RealisticQueries.cs, so that the run can take the place of that claim.  It reads the compiled constant
// and the committed files, and needs no server.  It goes with RealisticQueries.cs in the next commit.
public sealed class RealisticSqlParityTests
{
    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public void SqlOf_CaseWithModels_IsTheConstantOfItsModelsClaim(string name)
    {
        var found = RealisticCases.Corpus.ReadNamed(name);
        RealisticModels.SkipUnlessHasModels(found);
        var entry = RealisticModels.EntryOf(found);

        RealisticGeneration
            .SqlOf(RealisticCases.Corpus, found, entry)
            .ShouldBe(RealisticCases.Models.SqlOf(RealisticCases.Corpus, found, entry));
    }
}
