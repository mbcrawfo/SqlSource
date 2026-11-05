using System;
using System.Linq;
using Shouldly;
using SqlSource.Tool.Bundles.Renaming;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming;

public sealed class NameMappingTests
{
    private static readonly byte[] Zeros = new byte[32];

    private static string Fold(string spelling, bool quoted) => quoted ? spelling : spelling.ToUpperInvariant();

    private static NameMapping Mapping(byte seed = 0)
    {
        var bytes = new byte[32];
        bytes[0] = seed;
        return new NameMapping(RenamingKey.Of(bytes), Fold);
    }

    [Fact]
    public void Name_SameKeyAndSpelling_GivesTheSameName() =>
        Mapping().Name("users", false, NameClass.Table).ShouldBe(Mapping().Name("users", false, NameClass.Table));

    [Fact]
    public void Name_OtherKey_GivesAnotherName() =>
        Mapping(1).Name("users", false, NameClass.Table).ShouldNotBe(Mapping(2).Name("users", false, NameClass.Table));

    [Fact]
    public void Name_IsPrefixAndEightLowerHexDigits() =>
        Mapping().Name("users", false, NameClass.Table).ShouldMatch("^t_[0-9a-f]{8}$");

    [Fact]
    public void Name_SameSpellingAnotherClass_KeepsTheFirstClass()
    {
        var mapping = Mapping();

        var first = mapping.Name("users", false, NameClass.Table);
        var second = mapping.Name("users", false, NameClass.Column);

        second.ShouldBe(first);
        mapping.Names.ShouldHaveSingleItem().Class.ShouldBe(NameClass.Table);
    }

    [Fact]
    public void Name_FoldedAlike_IsOneName()
    {
        var mapping = Mapping();

        mapping.Name("Users", false, NameClass.Table).ShouldBe(mapping.Name("users", false, NameClass.Table));
        mapping.Name("Users", true, NameClass.Table).ShouldNotBe(mapping.Name("users", false, NameClass.Table));
    }

    [Fact]
    public void Name_ManySpellings_AreDistinctReplacements()
    {
        var mapping = Mapping();

        var replacements = Enumerable
            .Range(0, 2000)
            .Select(i => mapping.Name($"n{i}", false, NameClass.Column))
            .ToList();

        replacements.Distinct().Count().ShouldBe(2000);
        replacements.ShouldAllBe(r => mapping.IsReplacement(r));
        mapping.IsReplacement("c_nothere").ShouldBeFalse();
    }

    [Fact]
    public void Cut_ALongerLength_KeepsTheLeadingDigits()
    {
        NameMapping.Cut("0123456789abcdef", 8).ShouldBe("01234567");
        NameMapping.Cut("0123456789abcdef", 10).ShouldBe("0123456789");
    }

    [Fact]
    public void Keep_ThenName_StaysVerbatim()
    {
        var mapping = Mapping();

        mapping.Keep("pg_catalog", false);

        mapping.Name("pg_catalog", false, NameClass.Schema).ShouldBe("pg_catalog");
        mapping.TryGet("PG_CATALOG", false, out var replacement).ShouldBeTrue();
        replacement.ShouldBe("pg_catalog");
        mapping.Names.ShouldBeEmpty();
    }

    [Fact]
    public void TryGet_UnknownSpelling_IsFalse() => Mapping().TryGet("x", false, out _).ShouldBeFalse();

    [Fact]
    public void Literal_IsClassLiteral()
    {
        var mapping = Mapping();

        var replacement = mapping.Literal("hello");

        replacement.ShouldMatch("^l_[0-9a-f]{8}$");
        mapping.Literal("hello").ShouldBe(replacement);
        mapping.Names.ShouldHaveSingleItem().ShouldBe(new MappedName(replacement, NameClass.Literal, "hello"));
    }

    [Fact]
    public void NameClasses_EveryMember_HasAPrefixAndAWord()
    {
        var values = Enum.GetValues<NameClass>();

        values.Select(NameClasses.PrefixOf).Distinct().Count().ShouldBe(values.Length);
        values.Select(NameClasses.WordOf).Distinct().Count().ShouldBe(values.Length);
        _ = Should.Throw<InvalidOperationException>(() => NameClasses.PrefixOf((NameClass)99));
        _ = Should.Throw<InvalidOperationException>(() => NameClasses.WordOf((NameClass)99));
    }

    [Fact]
    public void RenamingKey_ToString_HoldsNoByte()
    {
        var bytes = Enumerable.Range(0, 32).Select(i => (byte)(i + 200)).ToArray();

        RenamingKey.Of(bytes).ToString().ShouldNotContain("200");
        RenamingKey.Random().ToString().ShouldBe(RenamingKey.Of(Zeros).ToString());
    }

    [Fact]
    public void RenamingKey_Of_OtherLength_Throws() =>
        Should.Throw<ArgumentException>(() => RenamingKey.Of(new byte[16]));

    [Fact]
    public void RenamingKey_Random_GivesAnotherNameEachTime() =>
        new NameMapping(RenamingKey.Random(), Fold)
            .Name("users", false, NameClass.Table)
            .ShouldNotBe(new NameMapping(RenamingKey.Random(), Fold).Name("users", false, NameClass.Table));
}
