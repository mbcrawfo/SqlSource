using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation.Models;
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

    private static QueryModels Models(string name, int index) =>
        new(name, index, TestModels.NameLocation("Users.sql"), null, null, null, null, null, null, null, null);
}
