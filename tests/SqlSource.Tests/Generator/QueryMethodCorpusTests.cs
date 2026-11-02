// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generator;

// The golden corpus of query methods: each case's run, every generated file and every diagnostic, is the text of its
// golden file.  SQLSOURCE_UPDATE_GOLDEN=1 writes the files instead; see "The golden corpus of query methods" in
// tests/README.md.
public sealed class QueryMethodCorpusTests
{
    // Each case, with the trait engine, so that `--filter-trait "engine=postgres"` runs one engine's cases.
    public static IEnumerable<TheoryDataRow<string>> CaseNames =>
        QueryMethodCorpus.Cases.Select(@case =>
            new TheoryDataRow<string>(@case.Name).WithTrait("engine", @case.Engine.Name)
        );

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Run_Case_MatchesItsGoldenFile(string name) =>
        GoldenFiles.Check(name, GoldenFiles.Render(name, QueryMethodCorpus.RunCase(name)));

    // A golden file of a case that was renamed or removed fails here, and the opt-in deletes it.
    [Fact]
    public void GoldenFolder_EveryFile_IsACase()
    {
        var names = QueryMethodCorpus.Cases.Select(@case => @case.Name).ToArray();
        if (GoldenFiles.IsUpdating)
        {
            foreach (var leftover in GoldenFiles.Leftovers(names))
            {
                File.Delete(GoldenFiles.PathOf(leftover));
            }
        }

        GoldenFiles.Leftovers(names).ShouldBeEmpty();
    }
}
