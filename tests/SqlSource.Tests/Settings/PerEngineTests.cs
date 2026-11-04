using System.Collections.Generic;
using Shouldly;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Settings;

public sealed class PerEngineTests
{
    [Fact]
    public void Of_EachEngine_HoldsItsValue()
    {
        var perEngine = PerEngine.Of(engine => engine.ToString());

        foreach (var engine in DatabaseEngines.All)
        {
            perEngine[engine].ShouldBe(engine.ToString());
        }
    }

    [Fact]
    public void Of_AFunction_IsCalledOnceForEachEngineInOrder()
    {
        var called = new List<DatabaseEngine>();

        _ = PerEngine.Of(engine =>
        {
            called.Add(engine);
            return engine.ToString();
        });

        called.ShouldBe(DatabaseEngines.All);
    }

    [Fact]
    public void Default_EverySlot_IsDefault()
    {
        foreach (var engine in DatabaseEngines.All)
        {
            default(PerEngine<string?>)[engine].ShouldBeNull();
        }
    }

    [Fact]
    public void Default_EqualsEverySlotDefault_WithTheSameHash()
    {
        var built = PerEngine.Of<string?>(_ => null);

        built.Equals(default).ShouldBeTrue();
        (built == default).ShouldBeTrue();
        built.GetHashCode().ShouldBe(default(PerEngine<string?>).GetHashCode());
    }

    [Fact]
    public void Only_OneEngine_FillsItsSlotAlone()
    {
        var perEngine = PerEngine.Only<string?>(DatabaseEngine.SqlServer, "a");

        perEngine[DatabaseEngine.SqlServer].ShouldBe("a");
        perEngine[DatabaseEngine.PostgreSql].ShouldBeNull();
    }

    [Fact]
    public void With_ReplacesOneSlot_AndLeavesTheOriginal()
    {
        var original = PerEngine.Of(engine => engine.ToString());

        var replaced = original.With(DatabaseEngine.SqlServer, "other");

        replaced[DatabaseEngine.SqlServer].ShouldBe("other");
        replaced[DatabaseEngine.PostgreSql].ShouldBe(nameof(DatabaseEngine.PostgreSql));
        original[DatabaseEngine.SqlServer].ShouldBe(nameof(DatabaseEngine.SqlServer));
    }

    [Fact]
    public void With_DefaultInEverySlot_EqualsDefault()
    {
        var emptied = PerEngine.Only<string?>(DatabaseEngine.SqlServer, "a").With(DatabaseEngine.SqlServer, null);

        emptied.ShouldBe(default);
        emptied.GetHashCode().ShouldBe(default(PerEngine<string?>).GetHashCode());
    }

    [Fact]
    public void Select_EachSlot_IsMapped()
    {
        var mapped = PerEngine.Of(engine => (int)engine).Select(n => n * 10);

        mapped[DatabaseEngine.PostgreSql].ShouldBe(0);
        mapped[DatabaseEngine.SqlServer].ShouldBe(10);
    }

    [Fact]
    public void Select_EverySlotToDefault_EqualsDefault()
    {
        var mapped = PerEngine.Of(engine => (int)engine).Select(_ => (string?)null);

        mapped.ShouldBe(default);
        mapped.GetHashCode().ShouldBe(default(PerEngine<string?>).GetHashCode());
    }

    [Fact]
    public void Zip_EverySlotToDefault_EqualsDefault()
    {
        var combined = PerEngine.Of(engine => (int)engine).Zip(PerEngine.Of(_ => 10), (_, _) => (string?)null);

        combined.ShouldBe(default);
        combined.GetHashCode().ShouldBe(default(PerEngine<string?>).GetHashCode());
    }

    [Fact]
    public void Zip_TwoContainers_CombineSlotBySlot()
    {
        var combined = PerEngine.Of(engine => (int)engine).Zip(PerEngine.Of(_ => 10), (a, b) => a + b);

        combined[DatabaseEngine.PostgreSql].ShouldBe(10);
        combined[DatabaseEngine.SqlServer].ShouldBe(11);
    }

    [Fact]
    public void Equals_SameValues_IsEqualWithTheSameHash()
    {
        var left = PerEngine.Of(engine => engine.ToString());
        var right = PerEngine.Of(engine => engine.ToString());

        left.Equals(right).ShouldBeTrue();
        (left == right).ShouldBeTrue();
        (left != right).ShouldBeFalse();
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void Equals_OneSlotDiffers_IsNotEqual()
    {
        var left = PerEngine.Of(engine => engine.ToString());
        var right = left.With(DatabaseEngine.SqlServer, "other");

        left.Equals(right).ShouldBeFalse();
        (left == right).ShouldBeFalse();
        (left != right).ShouldBeTrue();
    }

    [Fact]
    public void With_ValueOfNoMember_Throws() =>
        _ = Should.Throw<UnexpectedEnumValueException>(() => default(PerEngine<string?>).With((DatabaseEngine)7, "a"));

    [Fact]
    public void Only_ValueOfNoMember_Throws() =>
        _ = Should.Throw<UnexpectedEnumValueException>(() => PerEngine.Only<string?>((DatabaseEngine)7, "a"));

    [Fact]
    public void Indexer_ValueOfNoMember_Throws()
    {
        _ = Should.Throw<UnexpectedEnumValueException>(() =>
            PerEngine.Only<string?>(DatabaseEngine.SqlServer, "a")[(DatabaseEngine)7]
        );
        _ = Should.Throw<UnexpectedEnumValueException>(() => default(PerEngine<string?>)[(DatabaseEngine)7]);
    }
}
