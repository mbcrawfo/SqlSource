using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Snapshot;
using Xunit;
using static SqlSource.Tests.Snapshot.TestSidecars;

namespace SqlSource.Tests.Snapshot;

public class SidecarRoundTripTests
{
    private const int Seed = 20261009;

    private const int Count = 200;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Write_AnySidecar_ReadsBackEqualAndWritesTheSameTextAgain(bool lenient)
    {
        foreach (var sidecar in Samples(lenient))
        {
            var text = SidecarWriter.Write(sidecar);

            var result = SidecarReader.Read(text);

            result.Error.ShouldBeNull(text);
            var read = result.Sidecar.ShouldNotBeNull();
            WithoutSpans(read).ShouldBe(sidecar, text);
            SidecarWriter.Write(read).ShouldBe(text);
        }
    }

    // The samples are worth what they cover: each of the two engines and each kind.
    [Fact]
    public void Samples_AsTheToolBuildsThem_HoldBothEnginesAndEveryKind()
    {
        var samples = Samples(lenient: false);
        var types = samples.SelectMany(Types).ToList();

        samples
            .SelectMany(sidecar => sidecar.Queries)
            .Select(entry => entry.Engine)
            .Distinct()
            .Order()
            .ShouldBe([SidecarValues.Engine.SqlServer, SidecarValues.Engine.Postgres]);
        types.OfType<SqlServerType>().ShouldNotBeEmpty();
        types
            .OfType<PostgresType>()
            .Select(type => type.Kind)
            .Distinct()
            .Order()
            .ShouldBe(["array", "base", "composite", "domain", "enum", "multirange", "range"]);
        types.OfType<OtherEngineType>().ShouldBeEmpty();
    }

    [Fact]
    public void Samples_AsAReaderMayFindThem_HoldWhatTheToolNeverWrites()
    {
        var samples = Samples(lenient: true);
        var entries = samples.SelectMany(sidecar => sidecar.Queries).ToList();
        var types = samples.SelectMany(Types).ToList();

        types.OfType<OtherEngineType>().ShouldNotBeEmpty();
        types.OfType<PostgresType>().ShouldContain(type => type.Kind == SidecarFaker.UnknownKind);
        entries.ShouldContain(entry => entry.Database == null);
        entries.ShouldContain(entry => entry.Plan == "future-value");
        entries.ShouldContain(entry => entry.Columns != null && entry.Columns.Value.Count == 0);
    }

    internal static List<Sidecar> Samples(bool lenient)
    {
        var faker = new SidecarFaker(Seed, lenient);
        return [.. Enumerable.Range(0, Count).Select(_ => faker.Next())];
    }

    // Every type of a sidecar, the ones inside another included.
    private static IEnumerable<SidecarType> Types(Sidecar sidecar)
    {
        foreach (var entry in sidecar.Queries)
        {
            var types = entry
                .Parameters.Select(parameter => parameter.Type)
                .Concat((entry.Columns ?? Of<SidecarColumn>()).Select(column => (SidecarType?)column.Type));
            foreach (var type in types.SelectMany(Flatten))
            {
                yield return type;
            }
        }
    }

    private static IEnumerable<SidecarType> Flatten(SidecarType? type)
    {
        if (type is null)
        {
            yield break;
        }

        yield return type;
        if (type is PostgresType postgres)
        {
            foreach (var inner in new[] { postgres.Element, postgres.Base, postgres.Subtype }.SelectMany(Flatten))
            {
                yield return inner;
            }
        }
    }
}
