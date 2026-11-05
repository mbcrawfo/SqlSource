using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Bundles;
using Xunit;

namespace SqlSource.Tool.Tests.Fixtures;

// The bundles of Fixtures/Bundles/, each replayed as replay runs it and compared with what its folder expects: its
// expected.json for an entry, or the id and step of its bundle.json for a refusal.
public sealed class BundleCasesTests
{
    private static readonly string[] Engines = ["postgres", "mssql"];

    private const string Path = "/work/report";

    public static IEnumerable<TheoryDataRow<string, string>> Cases =>
        Engines.SelectMany(static engine =>
            BundleCases.Of(engine).Select(static found => new TheoryDataRow<string, string>(found.Engine, found.Name))
        );

    [Theory]
    [InlineData("postgres")]
    [InlineData("mssql")]
    public void Of_EachEngine_HasACase(string engine) => BundleCases.Of(engine).ShouldNotBeEmpty();

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Replay_EveryBundleCase_GivesItsExpectedAnswer(string engine, string name)
    {
        var bundle = BundleReplays.Read(BundleCases.Of(engine).Single(found => found.Name == name).Folder);
        var info = bundle.Info.ShouldNotBeNull();
        var expected = BundleReplay.ExpectedOf(bundle, Path);
        expected.Failure.ShouldBeNull();

        var replayed = await BundleReplays.ReplayAsync(bundle, Path);

        replayed.ShouldBe(expected.Value.ShouldNotBeNull());
        info.Engine.ShouldBe(engine);
    }
}
