using System;
using Shouldly;
using SqlSource.Settings;
using SqlSource.Settings.TypeOverrides;
using Xunit;
using Overrides = SqlSource.EquatableArray<SqlSource.Settings.TypeOverrides.TypeOverride>?;

namespace SqlSource.Tests.Settings;

// A container that says nothing holds no array, so building one, or merging two, allocates nothing: this runs on the
// parse path on every edit.  Each test builds the container a few times to warm up, so that the JIT and the static
// initialisers are not counted, then measures a loop of builds on the current thread alone.
public sealed class PerEngineAllocationTests
{
    private const int Iterations = 20;

    // Held in fields so that a method group is not made into a delegate on each call.
    private static readonly Func<Overrides, Overrides, Overrides> Merge = TypeOverrideMap.Over;

    private static readonly Func<DatabaseEngine, string?> NoValue = static _ => null;

    private static readonly Func<int, string?> ToNothing = static _ => null;

    [Fact]
    public void Only_DefaultValue_AllocatesNothing() =>
        AllocatedBy(() => PerEngine.Only<Overrides>(DatabaseEngine.SqlServer, null)).ShouldBe(0);

    [Fact]
    public void With_DefaultValueOnTheDefault_AllocatesNothing() =>
        AllocatedBy(() => default(PerEngine<string?>).With(DatabaseEngine.PostgreSql, null)).ShouldBe(0);

    [Fact]
    public void Zip_TwoDefaultContainers_AllocatesNothing() =>
        AllocatedBy(() => default(PerEngine<Overrides>).Zip(default, Merge)).ShouldBe(0);

    [Fact]
    public void Select_EverySlotToDefault_AllocatesNothing() =>
        AllocatedBy(() => default(PerEngine<int>).Select(ToNothing)).ShouldBe(0);

    [Fact]
    public void Of_EverySlotDefault_AllocatesNothing() => AllocatedBy(() => PerEngine.Of(NoValue)).ShouldBe(0);

    // Over of two levels that set no type override builds the merged level, a record, and nothing for the override.
    // The record's own size is measured by building a level with an initializer of the same members, so the
    // assertion holds whatever the record weighs, and a 2-slot array of references on top of it would fail.
    [Fact]
    public void Over_TwoLevelsWithoutATypeOverride_AllocatesNoMoreThanTheRecord()
    {
        var higher = new SettingsLevel { Output = OutputKind.Sql };
        var lower = new SettingsLevel { Database = "db" };

        var merged = AllocatedBy(() => higher.Over(lower));
        var record = AllocatedBy(() => new SettingsLevel { Output = OutputKind.Sql, Database = "db" });

        record.ShouldBeGreaterThan(0);
        merged.ShouldBeLessThanOrEqualTo(record);
    }

    // Generic, so that a container, a struct, is not boxed in the measured loop: a box of the default container would
    // allocate by itself.  The last result is kept alive after the measure, so the runtime cannot drop the build.
    private static long AllocatedBy<T>(Func<T> build)
    {
        var last = default(T);
        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            last = build();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            last = build();
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(last);
        return allocated;
    }
}
