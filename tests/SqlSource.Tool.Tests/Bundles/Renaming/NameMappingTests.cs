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
        return new NameMapping(RenamingKey.Of(bytes), Fold, Fold);
    }

    // A mapping that tells apart spellings of one name that differ by case: SQL Server's way, where the spelling is as
    // written and the name ignores case.
    private static NameMapping CaseKeeping(byte seed = 0, int? firstLength = null)
    {
        var bytes = new byte[32];
        bytes[0] = seed;
        return new NameMapping(
            RenamingKey.Of(bytes),
            static (spelling, _) => spelling.ToUpperInvariant(),
            static (spelling, _) => spelling,
            firstLength ?? 8
        );
    }

    // Every class's replacement, a literal's too, is shaped as one: the plan renamer reads a child of one by it.
    [Fact]
    public void IsShapedAsReplacement_OfEveryClassAndALiteral_IsTrue()
    {
        var mapping = Mapping();
        foreach (var cls in Enum.GetValues<NameClass>().Where(static c => c != NameClass.Literal))
        {
            NameMapping.IsShapedAsReplacement(mapping.Name("x" + cls, quoted: false, cls)).ShouldBeTrue();
        }

        NameMapping.IsShapedAsReplacement(mapping.Literal("v")).ShouldBeTrue();
    }

    [Theory]
    [InlineData("t_1234abcd", true)]
    [InlineData("t_1234abcdef", true)]
    [InlineData("t_1234abc", false)]
    [InlineData("T_1234abcd", false)]
    [InlineData("t_1234ABCD", false)]
    [InlineData("tt1234abcd", false)]
    [InlineData("t_1234abcg", false)]
    public void IsShapedAsReplacement_Text_IsALetterUnderscoreAndEightHexDigits(string text, bool shaped) =>
        NameMapping.IsShapedAsReplacement(text).ShouldBe(shaped);

    [Fact]
    public void Name_SameKeyAndSpelling_GivesTheSameName() =>
        Mapping().Name("users", false, NameClass.Table).ShouldBe(Mapping().Name("users", false, NameClass.Table));

    [Fact]
    public void Name_OtherKey_GivesAnotherName() =>
        Mapping(1).Name("users", false, NameClass.Table).ShouldNotBe(Mapping(2).Name("users", false, NameClass.Table));

    [Fact]
    public void Name_Table_IsPrefixAndEightLowerHexDigits() =>
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

    // Seventeen spellings of one class at one digit: sixteen digits cannot hold them, so at least one collides and is
    // lengthened by two digits, and every spelling still has a replacement of its own, the same each time it is asked.
    [Fact]
    public void Name_ReplacementThatCollides_IsLengthened()
    {
        var mapping = new NameMapping(RenamingKey.Of(Zeros), Fold, Fold, firstLength: 1);
        var spellings = Enumerable.Range(0, 17).Select(i => $"n{i}").ToList();

        var replacements = spellings.Select(spelling => mapping.Name(spelling, false, NameClass.Column)).ToList();

        replacements.Distinct().Count().ShouldBe(spellings.Count);
        replacements.ShouldAllBe(r => mapping.IsReplacement(r));
        replacements.ShouldAllBe(r => r.Length == "c_".Length + 1 || r.Length == "c_".Length + 3);
        replacements.ShouldContain(r => r.Length == "c_".Length + 3);
        spellings.Select(spelling => mapping.Name(spelling, false, NameClass.Column)).ShouldBe(replacements);
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
        mapping.IsMapped("PG_CATALOG", false).ShouldBeTrue();
        mapping.Name("PG_CATALOG", false, NameClass.Schema).ShouldBe("pg_catalog");
        mapping.Names.ShouldBeEmpty();
    }

    // Only a spelling kept from an engine's row is kept for the token check; one kept otherwise maps to itself alone.
    [Fact]
    public void IsKeptFromEngineRow_OnlyASpellingKeptFromARow_IsTrue()
    {
        var mapping = Mapping();

        mapping.Keep("users", false);
        mapping.KeepFromEngineRow("regdictionary", false);

        mapping.IsKeptFromEngineRow("users", false).ShouldBeFalse();
        mapping.IsMapped("users", false).ShouldBeTrue();
        mapping.Name("users", false, NameClass.Table).ShouldBe("users");
        mapping.IsKeptFromEngineRow("REGDICTIONARY", false).ShouldBeTrue();
        mapping.Name("regdictionary", false, NameClass.Type).ShouldBe("regdictionary");
        mapping.Names.ShouldBeEmpty();
    }

    [Fact]
    public void KeepFromEngineRow_AfterName_KeepsTheReplacementAndIsNotKept()
    {
        var mapping = Mapping();
        var replacement = mapping.Name("users", false, NameClass.Table);

        mapping.KeepFromEngineRow("users", false);

        mapping.Name("users", false, NameClass.Table).ShouldBe(replacement);
        mapping.IsKeptFromEngineRow("users", false).ShouldBeFalse();
    }

    [Fact]
    public void Keep_AfterName_KeepsTheReplacement()
    {
        var mapping = Mapping();
        var replacement = mapping.Name("users", false, NameClass.Table);

        mapping.Keep("users", false);

        mapping.Name("users", false, NameClass.Table).ShouldBe(replacement);
        mapping.IsMapped("users", false).ShouldBeTrue();
    }

    [Fact]
    public void IsMapped_UnknownSpelling_IsFalse() => Mapping().IsMapped("x", false).ShouldBeFalse();

    [Fact]
    public void Name_SpellingsTheEngineTellsApart_DifferByCaseAlone()
    {
        var mapping = CaseKeeping();
        var lower = mapping.Name("email", false, NameClass.Column);
        var upper = mapping.Name("EMAIL", false, NameClass.Column);

        lower.ShouldMatch("^c_[0-9a-f]{8}$");
        upper.ShouldNotBe(lower);
        string.Equals(upper, lower, StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        upper.ShouldBe("C" + lower[1..]);
        mapping.Name("EMAIL", false, NameClass.Column).ShouldBe(upper);
        mapping.Name("email", false, NameClass.Column).ShouldBe(lower);
        mapping.IsReplacement(upper).ShouldBeTrue();
    }

    [Fact]
    public void Name_SpellingsTheEngineTellsApart_AreEachListedWithTheirOriginal()
    {
        var mapping = CaseKeeping();
        var lower = mapping.Name("email", false, NameClass.Column);
        var upper = mapping.Name("EMAIL", false, NameClass.Column);

        mapping.Names.ShouldBe([
            new MappedName(lower, NameClass.Column, "email"),
            new MappedName(upper, NameClass.Column, "EMAIL"),
        ]);
    }

    [Fact]
    public void Name_SpellingsTheEngineTakesForOne_AreOneName()
    {
        var mapping = Mapping();

        mapping.Name("Users", false, NameClass.Table).ShouldBe(mapping.Name("users", false, NameClass.Table));
        _ = mapping.Names.ShouldHaveSingleItem();
    }

    // firstLength 1: a replacement holds two letters at most, so four spellings at most differ by case alone.
    [Fact]
    public void Name_MoreSpellingsThanTheCaseCanTellApart_EachHasItsOwnName()
    {
        var mapping = CaseKeeping(firstLength: 1);
        string[] spellings = ["abc", "abC", "aBc", "aBC", "Abc", "AbC", "ABc", "ABC"];

        var replacements = spellings.Select(spelling => mapping.Name(spelling, false, NameClass.Column)).ToList();

        replacements.Distinct(StringComparer.Ordinal).Count().ShouldBe(spellings.Length);
        replacements.ShouldAllBe(r => mapping.IsReplacement(r));
        var alike = replacements.Count(r => string.Equals(r, replacements[0], StringComparison.OrdinalIgnoreCase));
        alike.ShouldBeInRange(2, 4);
        spellings.Select(spelling => mapping.Name(spelling, false, NameClass.Column)).ShouldBe(replacements);
    }

    [Fact]
    public void FellBack_SpellingsTheCaseTellsApart_IsFalse()
    {
        var mapping = CaseKeeping();

        _ = mapping.Name("email", false, NameClass.Column);
        _ = mapping.Name("EMAIL", false, NameClass.Column);
        _ = mapping.Name("users", false, NameClass.Table);

        mapping.FellBack.ShouldBeFalse();
    }

    // firstLength 1, as above: the fifth spelling of one name at the latest has a replacement of its own.
    [Fact]
    public void FellBack_MoreSpellingsThanTheCaseCanTellApart_IsTrue()
    {
        var mapping = CaseKeeping(firstLength: 1);

        foreach (var spelling in (string[])["abc", "abC", "aBc", "aBC", "Abc"])
        {
            _ = mapping.Name(spelling, false, NameClass.Column);
        }

        mapping.FellBack.ShouldBeTrue();
    }

    [Fact]
    public void Keep_SpellingsTheEngineTellsApart_KeepEachAsWritten()
    {
        var mapping = CaseKeeping();

        mapping.Keep("sys", false);

        mapping.Name("SYS", false, NameClass.Schema).ShouldBe("SYS");
        mapping.Name("sys", false, NameClass.Schema).ShouldBe("sys");
        mapping.Names.ShouldBeEmpty();
    }

    [Fact]
    public void Keep_SpellingsTheEngineTakesForOne_MapToTheFirstKept()
    {
        var mapping = Mapping();

        mapping.Keep("pg_catalog", false);

        mapping.Name("PG_CATALOG", false, NameClass.Schema).ShouldBe("pg_catalog");
    }

    [Fact]
    public void KeepFromEngineRow_AnotherCase_IsKeptFromTheRow()
    {
        var mapping = CaseKeeping();

        mapping.KeepFromEngineRow("name", false);

        mapping.IsKeptFromEngineRow("NAME", false).ShouldBeTrue();
    }

    [Fact]
    public void IsMapped_AnotherCaseOfAMappedName_IsTrue()
    {
        var mapping = CaseKeeping();
        _ = mapping.Name("email", false, NameClass.Column);

        mapping.IsMapped("EMAIL", false).ShouldBeTrue();
        mapping.IsMapped("x", false).ShouldBeFalse();
    }

    [Fact]
    public void Literal_Text_IsMappedAsClassLiteral()
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
    public void RenamingKey_OfAnotherLength_Throws() =>
        Should.Throw<ArgumentException>(() => RenamingKey.Of(new byte[16]));

    [Fact]
    public void RenamingKey_Random_GivesAnotherNameEachTime() =>
        new NameMapping(RenamingKey.Random(), Fold, Fold)
            .Name("users", false, NameClass.Table)
            .ShouldNotBe(new NameMapping(RenamingKey.Random(), Fold, Fold).Name("users", false, NameClass.Table));
}
