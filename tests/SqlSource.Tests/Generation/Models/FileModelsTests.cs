using System.Globalization;
using System.Linq;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation.Models;
using SqlSource.Settings.Libraries;
using SqlSource.Testing.Snapshot;
using Xunit;

namespace SqlSource.Tests.Generation.Models;

public sealed class FileModelsTests
{
    private static readonly FileModels File = new(
        "app/Repo/Users.sql",
        "Users.sql",
        TestSidecars.Of(Models("First", 0), Models("Second", 2), Models("Third", 5)),
        TestSidecars.Of<DiagnosticInfo>()
    );

    [Theory]
    [InlineData(0, "First")]
    [InlineData(2, "Second")]
    [InlineData(5, "Third")]
    public void Find_IndexAndName_GivesTheModels(int index, string name) =>
        File.Find(index, name).ShouldNotBeNull().Name.ShouldBe(name);

    // At index 0 the models are at the query's own index; at 2 they are found by the search.
    [Theory]
    [InlineData(0, "Second")]
    [InlineData(2, "Third")]
    public void Find_IndexWithAnotherName_IsNull(int index, string name) => File.Find(index, name).ShouldBeNull();

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Find_MissingIndex_IsNull(int index) => File.Find(index, "Second").ShouldBeNull();

    // An earlier query without models puts the query's models before its own index, so the search runs: it must go
    // right from its first middle, query 2's models, to query 5's, and left from its first middle, query 5's, to
    // query 4's.
    [Theory]
    [InlineData(5, "E", "A:0", "B:1", "C:2", "D:3", "E:5", "F:9")]
    [InlineData(4, "B", "A:0", "B:4", "C:5", "D:6", "E:7", "F:8")]
    public void Find_ModelsBeforeTheQuerysOwnIndex_AreFoundByTheSearch(int index, string name, params string[] models)
    {
        var file = new FileModels(
            "app/Repo/Users.sql",
            "Users.sql",
            TestSidecars.Of([
                .. models.Select(static model =>
                    Models(model[..1], int.Parse(model[2..], CultureInfo.InvariantCulture))
                ),
            ]),
            TestSidecars.Of<DiagnosticInfo>()
        );

        file.Find(index, name).ShouldNotBeNull().Name.ShouldBe(name);
    }

    private static QueryModels Models(string name, int index) =>
        new(
            name,
            index,
            TestModels.NameLocation("Users.sql"),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            EquatableArray<LibrarySetting>.Empty
        );
}
