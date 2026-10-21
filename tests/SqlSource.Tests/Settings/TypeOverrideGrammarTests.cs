using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Settings;

public sealed class TypeOverrideGrammarTests
{
    [Theory]
    [InlineData("timestamptz=System.DateTimeOffset", "timestamptz", "global::System.DateTimeOffset")]
    [InlineData("int4=int", "int4", "int")]
    [InlineData("numeric=global::System.Double", "numeric", "global::System.Double")]
    [InlineData(
        "int4[]=System.Collections.Generic.List<int?>",
        "int4[]",
        "global::System.Collections.Generic.List<int?>"
    )]
    [InlineData("public.user_status=MyApp.UserStatus", "public.user_status", "global::MyApp.UserStatus")]
    [InlineData(
        "hstore=System.Collections.Generic.Dictionary<string, string?>",
        "hstore",
        "global::System.Collections.Generic.Dictionary<string, string?>"
    )]
    [InlineData(
        "hstore=System.Collections.Generic.Dictionary<string,string?>",
        "hstore",
        "global::System.Collections.Generic.Dictionary<string, string?>"
    )]
    [InlineData("int4=MyApp.Outer.Inner", "int4", "global::MyApp.Outer.Inner")]
    [InlineData("int4=MyApp.Outer<int>.Inner<string>", "int4", "global::MyApp.Outer<int>.Inner<string>")]
    [InlineData("int4=int?[]", "int4", "int?[]")]
    [InlineData("int4=int[,]", "int4", "int[,]")]
    [InlineData("int4=int[][]", "int4", "int[][]")]
    [InlineData(
        "int4=System.Collections.Generic.List<int[ , ]?>",
        "int4",
        "global::System.Collections.Generic.List<int[,]?>"
    )]
    [InlineData("int4=global::App", "int4", "global::App")]
    [InlineData(
        "int4=System.Collections.Generic.List<System.Collections.Generic.List<global::System.Guid?>>",
        "int4",
        "global::System.Collections.Generic.List<global::System.Collections.Generic.List<global::System.Guid?>>"
    )]
    [InlineData("integer=long", "int4", "long")]
    [InlineData("pg_catalog.int8=decimal", "int8", "decimal")]
    [InlineData("Billing.Invoice$State=Billing.State", "Billing.Invoice$State", "global::Billing.State")]
    public void TryReadEntry_ValidEntry_GivesItsKeyAndTheCanonicalText(string text, string key, string type)
    {
        var entry = Read(text, spacesAroundEquals: false);

        entry.ShouldBe(new TypeOverride(key, type, SettingPlace.Property));
    }

    [Theory]
    [InlineData("timestamptz=default")]
    [InlineData("timestamptz=DEFAULT")]
    [InlineData("timestamptz=Default")]
    public void TryReadEntry_Default_HasNoType(string text) =>
        Read(text, spacesAroundEquals: false).ShouldBe(new TypeOverride("timestamptz", null, SettingPlace.Property));

    [Theory]
    [InlineData("numeric = System.Double", "global::System.Double")]
    [InlineData("numeric= System.Double", "global::System.Double")]
    [InlineData("numeric =System.Double", "global::System.Double")]
    [InlineData("numeric\t=\tdefault", null)]
    public void TryReadEntry_SpacesAroundTheEqualsInAMarker_AreAllowed(string text, string? type)
    {
        TypeOverrideGrammar
            .TryReadEntry(
                text.AsSpan(),
                spacesAroundEquals: true,
                SettingPlace.Query,
                DatabaseEngine.PostgreSql,
                out var entry
            )
            .ShouldBeTrue();

        entry.ShouldBe(new TypeOverride("numeric", type, SettingPlace.Query));
    }

    [Fact]
    public void TryReadEntry_QuotedKeyInAMarker_AllowsSpacesAroundEquals()
    {
        TypeOverrideGrammar
            .TryReadEntry(
                "\"my schema\".\"x y\" = int".AsSpan(),
                spacesAroundEquals: true,
                SettingPlace.Query,
                DatabaseEngine.PostgreSql,
                out var entry
            )
            .ShouldBeTrue();

        entry.ShouldBe(new TypeOverride("\"my schema\".\"x y\"", "int", SettingPlace.Query));
    }

    [Fact]
    public void TryReadEntry_EscapedKeyInAMarker_IsItsQuotedForm()
    {
        TypeOverrideGrammar
            .TryReadEntry(
                "public.U&\"st\\0061tus\" = int".AsSpan(),
                spacesAroundEquals: true,
                SettingPlace.Query,
                DatabaseEngine.PostgreSql,
                out var entry
            )
            .ShouldBeTrue();

        entry.ShouldBe(new TypeOverride("public.\"status\"", "int", SettingPlace.Query));
    }

    [Fact]
    public void TryReadEntry_EscapedKeyInAMarker_IsItsWrittenForm()
    {
        TypeOverrideGrammar
            .TryReadEntry(
                "public.U&\"a\\000ab\" = MyApp.Tag".AsSpan(),
                spacesAroundEquals: true,
                SettingPlace.Query,
                DatabaseEngine.PostgreSql,
                out var entry
            )
            .ShouldBeTrue();

        entry.ShouldBe(new TypeOverride("public.U&\"a\\000Ab\"", "global::MyApp.Tag", SettingPlace.Query));
    }

    [Theory]
    [InlineData("numeric = System.Double")]
    [InlineData("numeric= System.Double")]
    [InlineData("numeric =System.Double")]
    public void TryReadEntry_SpacesAroundTheEqualsInAList_AreNotAllowed(string text) =>
        TypeOverrideGrammar
            .TryReadEntry(
                text.AsSpan(),
                spacesAroundEquals: false,
                SettingPlace.Property,
                DatabaseEngine.PostgreSql,
                out _
            )
            .ShouldBeFalse();

    [Theory]
    // A ? at the end of the whole type: a property's nullability is its column's.
    [InlineData("int4=int?")]
    [InlineData("int4=System.Collections.Generic.List<int>?")]
    [InlineData("int4=int[]?")]
    [InlineData("int4=(int, string)")]
    [InlineData("int4=dynamic")]
    [InlineData("int4=void")]
    [InlineData("int4=int*")]
    // Unqualified, and not a keyword: the generator cannot see a file's usings.
    [InlineData("int4=Guid")]
    [InlineData("int4=System.Collections.Generic.List<Guid>")]
    [InlineData("int4=System.Guid extra")]
    [InlineData("int4=System.Guid ")]
    [InlineData("int4=System.Collections.Generic.List<>")]
    [InlineData("int4=System.Collections.Generic.List<int")]
    [InlineData("int4=System.Collections.Generic.List<int,>")]
    [InlineData("int4=System.Collections.Generic.List<,int>")]
    [InlineData("int4=System.Collections.Generic.List<int>>")]
    [InlineData("int4=int[")]
    [InlineData("int4=int]")]
    [InlineData("int4=int[;]")]
    [InlineData("int4=int []")]
    [InlineData("int4=System. Guid")]
    [InlineData("int4=System..Guid")]
    [InlineData("int4=System.Guid.")]
    [InlineData("int4=.System.Guid")]
    [InlineData("int4=global::")]
    [InlineData("int4=global::int")]
    [InlineData("int4=global::global::System.Guid")]
    [InlineData("int4=System.string")]
    [InlineData("int4=System.class")]
    [InlineData("int4=int<string>")]
    [InlineData("int4=int.Max")]
    [InlineData("int4=@System.Guid")]
    [InlineData("int4=1System.Guid")]
    [InlineData("int4=System.Guid=int")]
    [InlineData("timestampz=int")]
    [InlineData("user_status=int")]
    [InlineData("int4=")]
    [InlineData("=int")]
    [InlineData("int4")]
    [InlineData("")]
    [InlineData("int4==int")]
    [InlineData("numeric(18,2)=decimal")]
    [InlineData("int4=default extra")]
    public void TryReadEntry_InvalidEntry_IsNotRead(string text)
    {
        TypeOverrideGrammar
            .TryReadEntry(
                text.AsSpan(),
                spacesAroundEquals: false,
                SettingPlace.Property,
                DatabaseEngine.PostgreSql,
                out var entry
            )
            .ShouldBeFalse();
        entry.ShouldBeNull();
        TypeOverrideGrammar
            .TryReadEntry(text.AsSpan(), spacesAroundEquals: true, SettingPlace.Query, DatabaseEngine.PostgreSql, out _)
            .ShouldBeFalse();
    }

    // Nothing a user writes may make the generator throw, so a type nested past a limit is not read, and the
    // stack is never at risk.
    [Fact]
    public void TryReadEntry_TypeNestedVeryDeeply_IsNotReadAndDoesNotThrow()
    {
        const int Depth = 100_000;
        var text = "int4=" + string.Concat(Enumerable.Repeat("A.B<", Depth)) + "int" + new string('>', Depth);

        TypeOverrideGrammar
            .TryReadEntry(
                text.AsSpan(),
                spacesAroundEquals: false,
                SettingPlace.Property,
                DatabaseEngine.PostgreSql,
                out _
            )
            .ShouldBeFalse();
    }

    [Fact]
    public void ReadList_EntriesOverSeveralLines_AreReadAndTheLastOfAKeyWinsThroughItsAlias()
    {
        var invalid = new List<string>();

        var list = TypeOverrideGrammar
            .ReadList("int4=int\n  int8=long  integer=short", SettingPlace.Metadata, invalid)
            .PostgreSql;

        list.ShouldBe([
            new TypeOverride("int4", "short", SettingPlace.Metadata),
            new TypeOverride("int8", "long", SettingPlace.Metadata),
        ]);
        invalid.ShouldBeEmpty();
    }

    [Fact]
    public void ReadList_InvalidEntry_IsRecordedAsWrittenAndTheOthersApply()
    {
        var invalid = new List<string>();

        var list = TypeOverrideGrammar.ReadList("int4=int timestampz=int", SettingPlace.Property, invalid).PostgreSql;

        list.ShouldBe([new TypeOverride("int4", "int", SettingPlace.Property)]);
        invalid.ShouldBe(["timestampz=int"]);
    }

    // The targets of the package turn a line break into a space, so an entry split over lines inside <> arrives
    // with a space there, and is still one entry.
    [Theory]
    [InlineData("hstore=System.Collections.Generic.Dictionary<string, string?>")]
    [InlineData("hstore=System.Collections.Generic.Dictionary< string , string? >")]
    public void ReadList_SpaceInsideAngleBrackets_KeepsOneEntry(string text)
    {
        var invalid = new List<string>();

        var list = TypeOverrideGrammar.ReadList(text, SettingPlace.Property, invalid).PostgreSql;

        list.ShouldBe([
            new TypeOverride(
                "hstore",
                "global::System.Collections.Generic.Dictionary<string, string?>",
                SettingPlace.Property
            ),
        ]);
        invalid.ShouldBeEmpty();
    }

    [Fact]
    public void ReadList_SpaceInsideSquareBrackets_KeepsOneEntry()
    {
        var invalid = new List<string>();

        TypeOverrideGrammar
            .ReadList("int4=int[ , ] int8=long", SettingPlace.Property, invalid)
            .PostgreSql.ShouldBe([
                new TypeOverride("int4", "int[,]", SettingPlace.Property),
                new TypeOverride("int8", "long", SettingPlace.Property),
            ]);
        invalid.ShouldBeEmpty();
    }

    // Spaces around the equals separate a list's entries, and each part is an entry that is not valid.
    [Fact]
    public void ReadList_SpacesAroundTheEquals_AreEntriesThatAreNotValid()
    {
        var invalid = new List<string>();

        TypeOverrideGrammar
            .ReadList("numeric = System.Double", SettingPlace.Property, invalid)
            .PostgreSql.ShouldBeEmpty();
        invalid.ShouldBe(["numeric", "=", "System.Double"]);
    }

    // A bracket that does not close takes the rest of the value into its entry, so the entries after it are lost
    // with it, and those before it apply.
    [Theory]
    [InlineData("int4=System.Collections.Generic.List<int int8=long text=string")]
    [InlineData("int4=int[ int8=long text=string")]
    public void ReadList_BracketThatDoesNotClose_SwallowsTheRestOfTheValue(string rest)
    {
        var invalid = new List<string>();

        TypeOverrideGrammar
            .ReadList("bool=bool " + rest, SettingPlace.Property, invalid)
            .PostgreSql.ShouldBe([new TypeOverride("bool", "bool", SettingPlace.Property)]);
        invalid.ShouldBe([rest]);
    }

    [Fact]
    public void ReadList_EntriesInAnyOrder_AreSortedByKeyIgnoringCase()
    {
        var invalid = new List<string>();

        TypeOverrideGrammar
            .ReadList(
                "text=string public.Zeta=App.Z Public.alpha=App.A timestamptz=default bool=bool",
                SettingPlace.Property,
                invalid
            )
            .PostgreSql.Select(static entry => entry.Key)
            .ShouldBe(["bool", "Public.alpha", "public.Zeta", "text", "timestamptz"]);
    }

    // White space inside double quotes belongs to the key, as the quoted part of a name may hold any character.
    [Fact]
    public void ReadList_QuotedKeyWithSpaces_IsOneEntry()
    {
        var invalid = new List<string>();

        TypeOverrideGrammar
            .ReadList("\"my schema\".\"order status\"=MyApp.A int4=long", SettingPlace.Property, invalid)
            .PostgreSql.ShouldBe([
                new TypeOverride("int4", "long", SettingPlace.Property),
                new TypeOverride("\"my schema\".\"order status\"", "global::MyApp.A", SettingPlace.Property),
            ]);
        invalid.ShouldBeEmpty();
    }

    // Inside double quotes a bracket does not open an entry's bracket, and an equals is not the entry's.
    [Fact]
    public void ReadList_BracketOrEqualsInsideQuotes_IsPartOfTheKey()
    {
        var invalid = new List<string>();

        TypeOverrideGrammar
            .ReadList("s.\"a[b\"=int s.\"a=b\"=long int8=long", SettingPlace.Property, invalid)
            .PostgreSql.ShouldBe([
                new TypeOverride("int8", "long", SettingPlace.Property),
                new TypeOverride("s.\"a=b\"", "long", SettingPlace.Property),
                new TypeOverride("s.\"a[b\"", "int", SettingPlace.Property),
            ]);
        invalid.ShouldBeEmpty();
    }

    // A quote that does not close takes the rest of the value into its entry, as a bracket does.
    [Fact]
    public void ReadList_UnclosedQuote_MakesTheRestOneInvalidEntry()
    {
        var invalid = new List<string>();

        TypeOverrideGrammar
            .ReadList("int4=long \"my schema.x=int int8=long", SettingPlace.Property, invalid)
            .PostgreSql.ShouldBe([new TypeOverride("int4", "long", SettingPlace.Property)]);
        invalid.ShouldBe(["\"my schema.x=int int8=long"]);
    }

    // Quotes belong to a key, so a stray one in a type is an entry that is not valid by itself.
    [Fact]
    public void ReadList_QuoteInTheType_IsInvalidAloneAndTheRestApply()
    {
        var invalid = new List<string>();

        TypeOverrideGrammar
            .ReadList("a=string\"x int4=long int8=long", SettingPlace.Property, invalid)
            .PostgreSql.ShouldBe([
                new TypeOverride("int4", "long", SettingPlace.Property),
                new TypeOverride("int8", "long", SettingPlace.Property),
            ]);
        invalid.ShouldBe(["a=string\"x"]);
    }

    [Fact]
    public void ReadList_KeysThatDifferByCase_AreTwoEntries()
    {
        var invalid = new List<string>();

        TypeOverrideGrammar
            .ReadList("public.Status=MyApp.A public.status=MyApp.B", SettingPlace.Property, invalid)
            .PostgreSql.ShouldBe([
                new TypeOverride("public.Status", "global::MyApp.A", SettingPlace.Property),
                new TypeOverride("public.status", "global::MyApp.B", SettingPlace.Property),
            ]);
        invalid.ShouldBeEmpty();
    }

    [Fact]
    public void ReadList_QuotedAndPlainSpellingOfOneText_TheLastWins()
    {
        var invalid = new List<string>();

        TypeOverrideGrammar
            .ReadList("public.status=MyApp.A public.\"status\"=MyApp.B", SettingPlace.Property, invalid)
            .PostgreSql.ShouldBe([new TypeOverride("public.\"status\"", "global::MyApp.B", SettingPlace.Property)]);
        invalid.ShouldBeEmpty();
    }

    // The escaped form keeps "" for a quote, so the quotes of a key still pair, and an equals or a space inside it
    // is part of the key.
    [Fact]
    public void ReadList_EscapedKey_IsOneEntryAndTheRestApply()
    {
        var invalid = new List<string>();

        TypeOverrideGrammar
            .ReadList("s.U&\"a=b\\0022 c\"=int int4=long s.U&\"\\00\"=int", SettingPlace.Property, invalid)
            .PostgreSql.ShouldBe([
                new TypeOverride("int4", "long", SettingPlace.Property),
                new TypeOverride("s.\"a=b\"\" c\"", "int", SettingPlace.Property),
            ]);
        invalid.ShouldBe(["s.U&\"\\00\"=int"]);
    }

    [Fact]
    public void ReadList_TwoSpellingsOfOneEscapedText_TheLastWins()
    {
        var invalid = new List<string>();

        TypeOverrideGrammar
            .ReadList("s.U&\"st\\0061tus\"=int s.\"status\"=long", SettingPlace.Property, invalid)
            .PostgreSql.ShouldBe([new TypeOverride("s.\"status\"", "long", SettingPlace.Property)]);
        invalid.ShouldBeEmpty();
    }

    [Fact]
    public void ReadList_TwoEscapedSpellingsOfOneText_TheLastWins()
    {
        var invalid = new List<string>();

        TypeOverrideGrammar
            .ReadList("s.U&\"a\\000Ab\"=int s.U&\"a\\000ab\"=long", SettingPlace.Property, invalid)
            .PostgreSql.ShouldBe([new TypeOverride("s.U&\"a\\000Ab\"", "long", SettingPlace.Property)]);
        invalid.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \n\t ")]
    public void ReadList_NoEntry_IsEmpty(string text)
    {
        var invalid = new List<string>();

        TypeOverrideGrammar.ReadList(text, SettingPlace.Property, invalid).PostgreSql.ShouldBeEmpty();
        invalid.ShouldBeEmpty();
    }

    // Each engine reads the key by its own rules, and a key one engine does not read is no entry of it.  A null key
    // is none.
    [Theory]
    [InlineData("int=long", "int4", "int")]
    [InlineData("int4=long", "int4", null)]
    [InlineData("bit=bool", "bit", "bit")]
    [InlineData("timestamp=byte[]", "timestamp", "timestamp")]
    [InlineData("rowversion=byte[]", null, "timestamp")]
    [InlineData("nvarchar=string", null, "nvarchar")]
    [InlineData("pg_catalog.text=string", "text", null)]
    [InlineData("sys.text=string", "sys.text", "text")]
    [InlineData("_int4=int[]", "int4[]", null)]
    [InlineData("dbo.Email=MyApp.Email", "dbo.Email", "dbo.Email")]
    public void TryReadEntry_KeyOfEachEngine_IsReadByItsRules(string text, string? postgreSql, string? sqlServer)
    {
        KeyOf(text, DatabaseEngine.PostgreSql).ShouldBe(postgreSql);
        KeyOf(text, DatabaseEngine.SqlServer).ShouldBe(sqlServer);
    }

    // An entry goes in the list of each engine that reads its key, and the last of a key in a list wins.
    [Fact]
    public void ReadList_EntryOfEachEngine_GoesInTheListOfEachThatReadsIt()
    {
        var invalid = new List<string>();

        var lists = TypeOverrideGrammar.ReadList(
            "int=MyApp.X int4=MyApp.Y nvarchar=string",
            SettingPlace.Property,
            invalid
        );

        lists.PostgreSql.ShouldBe([new TypeOverride("int4", "global::MyApp.Y", SettingPlace.Property)]);
        lists.SqlServer.ShouldBe([
            new TypeOverride("int", "global::MyApp.X", SettingPlace.Property),
            new TypeOverride("nvarchar", "string", SettingPlace.Property),
        ]);
        invalid.ShouldBeEmpty();
    }

    [Fact]
    public void ReadList_KeyThatNeitherEngineReads_IsInvalid()
    {
        var invalid = new List<string>();

        var lists = TypeOverrideGrammar.ReadList("[dbo].[Email]=MyApp.Email", SettingPlace.Property, invalid);

        lists.PostgreSql.ShouldBeEmpty();
        lists.SqlServer.ShouldBeEmpty();
        invalid.ShouldBe(["[dbo].[Email]=MyApp.Email"]);
    }

    // A key written in brackets, as T-SQL writes a name, gets the forms that are read: plain, which matches ignoring
    // case, and quoted, which matches it exactly.  A part that cannot be plain has the quoted form alone.
    [Theory]
    [InlineData(
        "[dbo].[Email]=X",
        ".  A key is not written in brackets: write dbo.Email, or \"dbo\".\"Email\" to match its case exactly"
    )]
    [InlineData(
        "dbo.[Email]=X",
        ".  A key is not written in brackets: write dbo.Email, or dbo.\"Email\" to match its case exactly"
    )]
    [InlineData(
        "\"dbo\".[Email]=X",
        ".  A key is not written in brackets: write \"dbo\".Email, or \"dbo\".\"Email\" to match its case exactly"
    )]
    [InlineData("[my schema].[Email]=X", ".  A key is not written in brackets: write \"my schema\".\"Email\"")]
    [InlineData("[a]]b].c=X", ".  A key is not written in brackets: write \"a]b\".c")]
    [InlineData("[a\"b]=X", ".  A key is not written in brackets: write \"a\"\"b\"")]
    [InlineData(
        "[dbo].[Email] = X",
        ".  A key is not written in brackets: write dbo.Email, or \"dbo\".\"Email\" to match its case exactly"
    )]
    public void BracketHelp_KeyInBrackets_ShowsTheFormsThatAreRead(string entry, string help) =>
        TypeOverrideGrammar.BracketHelp(entry.AsSpan()).ShouldBe(help);

    [Theory]
    [InlineData("dbo.Email=X")]
    [InlineData("int4[]=Guid")]
    [InlineData("s.\"a[b\"=Guid")]
    [InlineData("int4=int[]")]
    [InlineData("[dbo=X")]
    [InlineData("[]=X")]
    [InlineData("[a]b=X")]
    [InlineData("")]
    public void BracketHelp_KeyWithNoPartInBrackets_IsEmpty(string entry) =>
        TypeOverrideGrammar.BracketHelp(entry.AsSpan()).ShouldBeEmpty();

    private static string? KeyOf(string text, DatabaseEngine engine) =>
        TypeOverrideGrammar.TryReadEntry(
            text.AsSpan(),
            spacesAroundEquals: false,
            SettingPlace.Property,
            engine,
            out var entry
        )
            ? entry.ShouldNotBeNull().Key
            : null;

    private static TypeOverride Read(string text, bool spacesAroundEquals)
    {
        TypeOverrideGrammar
            .TryReadEntry(
                text.AsSpan(),
                spacesAroundEquals,
                SettingPlace.Property,
                DatabaseEngine.PostgreSql,
                out var entry
            )
            .ShouldBeTrue();
        return entry.ShouldNotBeNull();
    }
}
