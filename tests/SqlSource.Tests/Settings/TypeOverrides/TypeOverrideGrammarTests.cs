using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Settings;
using SqlSource.Settings.TypeOverrides;
using Xunit;

namespace SqlSource.Tests.Settings.TypeOverrides;

public sealed class TypeOverrideGrammarTests
{
    private static readonly SettingPlace Place = SettingPlace.OfSettingsFile("/app/sqlsource.json", 1);

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
    [InlineData("int4=UserId", "int4", "UserId")]
    [InlineData(
        "int4=System.Collections.Generic.List<UserId>",
        "int4",
        "global::System.Collections.Generic.List<UserId>"
    )]
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
        var entry = Read(text);

        entry.ShouldBe(new TypeOverride(key, type, SettingPlace.Query));
    }

    [Theory]
    [InlineData("timestamptz=default")]
    [InlineData("timestamptz=DEFAULT")]
    [InlineData("timestamptz=Default")]
    public void TryReadEntry_Default_HasNoType(string text) =>
        Read(text).ShouldBe(new TypeOverride("timestamptz", null, SettingPlace.Query));

    [Theory]
    [InlineData("numeric = System.Double", "global::System.Double")]
    [InlineData("numeric= System.Double", "global::System.Double")]
    [InlineData("numeric =System.Double", "global::System.Double")]
    [InlineData("numeric\t=\tdefault", null)]
    public void TryReadEntry_SpacesAroundTheEquals_AreAllowed(string text, string? type)
    {
        TypeOverrideGrammar
            .TryReadEntry(text.AsSpan(), SettingPlace.Query, DatabaseEngine.PostgreSql, out var entry)
            .ShouldBeTrue();

        entry.ShouldBe(new TypeOverride("numeric", type, SettingPlace.Query));
    }

    [Fact]
    public void TryReadEntry_QuotedKeyWithSpaces_AllowsSpacesAroundEquals()
    {
        TypeOverrideGrammar
            .TryReadEntry(
                "\"my schema\".\"x y\" = int".AsSpan(),
                SettingPlace.Query,
                DatabaseEngine.PostgreSql,
                out var entry
            )
            .ShouldBeTrue();

        entry.ShouldBe(new TypeOverride("\"my schema\".\"x y\"", "int", SettingPlace.Query));
    }

    [Fact]
    public void TryReadEntry_EscapedKey_IsItsQuotedForm()
    {
        TypeOverrideGrammar
            .TryReadEntry(
                "public.U&\"st\\0061tus\" = int".AsSpan(),
                SettingPlace.Query,
                DatabaseEngine.PostgreSql,
                out var entry
            )
            .ShouldBeTrue();

        entry.ShouldBe(new TypeOverride("public.\"status\"", "int", SettingPlace.Query));
    }

    [Fact]
    public void TryReadEntry_EscapedKey_IsItsWrittenForm()
    {
        TypeOverrideGrammar
            .TryReadEntry(
                "public.U&\"a\\000ab\" = MyApp.Tag".AsSpan(),
                SettingPlace.Query,
                DatabaseEngine.PostgreSql,
                out var entry
            )
            .ShouldBeTrue();

        entry.ShouldBe(new TypeOverride("public.U&\"a\\000Ab\"", "global::MyApp.Tag", SettingPlace.Query));
    }

    [Theory]
    // A ? at the end of the whole type: a property's nullability is its column's.
    [InlineData("int4=int?")]
    [InlineData("int4=System.Collections.Generic.List<int>?")]
    [InlineData("int4=int[]?")]
    [InlineData("int4=(int, string)")]
    [InlineData("int4=dynamic")]
    [InlineData("int4=dynamic[]")]
    [InlineData("int4=System.Collections.Generic.List<dynamic>")]
    [InlineData("int4=void")]
    [InlineData("int4=int*")]
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
            .TryReadEntry(text.AsSpan(), SettingPlace.Query, DatabaseEngine.PostgreSql, out var entry)
            .ShouldBeFalse();
        entry.ShouldBeNull();
    }

    // Nothing a user writes may make the generator throw, so a type nested past a limit is not read, and the
    // stack is never at risk.
    [Fact]
    public void TryReadEntry_TypeNestedVeryDeeply_IsNotReadAndDoesNotThrow()
    {
        const int Depth = 100_000;
        var text = "int4=" + string.Concat(Enumerable.Repeat("A.B<", Depth)) + "int" + new string('>', Depth);

        TypeOverrideGrammar
            .TryReadEntry(text.AsSpan(), SettingPlace.Query, DatabaseEngine.PostgreSql, out _)
            .ShouldBeFalse();
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

    [Fact]
    public void ReadMembers_Members_AreEntriesInTheOrderOfTheMapAtThePlace()
    {
        var (invalid, repeated) = (new List<int>(), new List<(int Index, int First)>());

        var lists = TypeOverrideGrammar.ReadMembers(
            [new("timestamptz", "default"), new("int4", "long"), new("public.\"Email\"", "MyApp.Email")],
            Place,
            invalid,
            repeated
        );

        lists.PostgreSql.ShouldBe([
            new TypeOverride("int4", "long", Place),
            new TypeOverride("public.\"Email\"", "global::MyApp.Email", Place),
            new TypeOverride("timestamptz", null, Place),
        ]);
        lists.SqlServer.ShouldBe([new TypeOverride("public.\"Email\"", "global::MyApp.Email", Place)]);
        invalid.ShouldBeEmpty();
        repeated.ShouldBeEmpty();
    }

    // The map's order ignores case, and keys that differ by case alone are two entries.
    [Fact]
    public void ReadMembers_Keys_AreSortedIgnoringCaseAndThoseThatDifferByCaseAreTwo()
    {
        var (invalid, repeated) = (new List<int>(), new List<(int Index, int First)>());

        TypeOverrideGrammar
            .ReadMembers(
                [
                    new("text", "string"),
                    new("public.Zeta", "App.Z"),
                    new("Public.alpha", "App.A"),
                    new("timestamptz", "default"),
                    new("public.Status", "App.S"),
                    new("public.status", "App.T"),
                    new("bool", "bool"),
                ],
                Place,
                invalid,
                repeated
            )
            .PostgreSql.Select(static entry => entry.Key)
            .ShouldBe(["bool", "Public.alpha", "public.Status", "public.status", "public.Zeta", "text", "timestamptz"]);
        invalid.ShouldBeEmpty();
        repeated.ShouldBeEmpty();
    }

    // White space inside double quotes belongs to the key, as the quoted part of a name may hold any character.
    [Fact]
    public void ReadMembers_QuotedKeyWithSpaces_IsOneEntry()
    {
        var (invalid, repeated) = (new List<int>(), new List<(int Index, int First)>());

        TypeOverrideGrammar
            .ReadMembers(
                [new("\"my schema\".\"order status\"", "MyApp.A"), new("int4", "long")],
                Place,
                invalid,
                repeated
            )
            .PostgreSql.ShouldBe([
                new TypeOverride("int4", "long", Place),
                new TypeOverride("\"my schema\".\"order status\"", "global::MyApp.A", Place),
            ]);
        invalid.ShouldBeEmpty();
        repeated.ShouldBeEmpty();
    }

    [Fact]
    public void ReadMembers_KeyThatOneEngineReads_IsInItsListAlone()
    {
        var (invalid, repeated) = (new List<int>(), new List<(int Index, int First)>());

        var lists = TypeOverrideGrammar.ReadMembers([new("nvarchar", "string")], Place, invalid, repeated);

        lists.PostgreSql.ShouldBeEmpty();
        lists.SqlServer.ShouldBe([new TypeOverride("nvarchar", "string", Place)]);
        invalid.ShouldBeEmpty();
        repeated.ShouldBeEmpty();
    }

    // A member is a key and a type, each trimmed, and the type default ignoring case.
    [Theory]
    [InlineData(" int4 ", " long ", "long")]
    [InlineData("\tint4", "DEFAULT ", null)]
    public void ReadMembers_WhiteSpaceAroundTheKeyAndTheType_IsTrimmed(string key, string type, string? expected)
    {
        var (invalid, repeated) = (new List<int>(), new List<(int Index, int First)>());

        TypeOverrideGrammar
            .ReadMembers([new(key, type)], Place, invalid, repeated)
            .PostgreSql.ShouldBe([new TypeOverride("int4", expected, Place)]);
        invalid.ShouldBeEmpty();
        repeated.ShouldBeEmpty();
    }

    // A member neither engine reads, or whose type is not valid or empty, is named by its index, and the others apply.
    [Theory]
    [InlineData("int4", "int?")]
    [InlineData("[dbo].[Email]", "X")]
    [InlineData("int4", "")]
    [InlineData("int4", " \t ")]
    [InlineData("int4", "int4=long")]
    [InlineData("", "long")]
    [InlineData("int4=long", "long")]
    public void ReadMembers_MemberThatIsNotValid_IsItsIndexAndTheOthersApply(string key, string type)
    {
        var (invalid, repeated) = (new List<int>(), new List<(int Index, int First)>());

        TypeOverrideGrammar
            .ReadMembers([new("int8", "long"), new(key, type)], Place, invalid, repeated)
            .PostgreSql.ShouldBe([new TypeOverride("int8", "long", Place)]);
        invalid.ShouldBe([1]);
        repeated.ShouldBeEmpty();
    }

    // One key is the map's identity: an alias and its name, a quoted and a plain spelling of one text, and one text
    // twice.  The first member applies.
    [Theory]
    [InlineData("integer", "int4", "int4")]
    [InlineData("int4", "int4", "int4")]
    [InlineData("public.status", "public.\"status\"", "public.status")]
    [InlineData("s.U&\"st\\0061tus\"", "s.\"status\"", "s.\"status\"")]
    [InlineData("s.U&\"a\\000Ab\"", "s.U&\"a\\000ab\"", "s.U&\"a\\000Ab\"")]
    public void ReadMembers_TwoMembersOfOneKey_TheSecondIsRepeatedAndTheFirstApplies(
        string first,
        string second,
        string key
    )
    {
        var (invalid, repeated) = (new List<int>(), new List<(int Index, int First)>());

        TypeOverrideGrammar
            .ReadMembers([new(first, "long"), new(second, "int")], Place, invalid, repeated)
            .PostgreSql.ShouldBe([new TypeOverride(key, "long", Place)]);
        invalid.ShouldBeEmpty();
        repeated.ShouldBe([(1, 0)]);
    }

    // int4 is PostgreSQL's alone and int is read by both, as int4 and int: the second member's key is repeated in
    // PostgreSQL's list, so it goes in SQL Server's neither.
    [Fact]
    public void ReadMembers_KeyRepeatedInPostgreSqlsList_IsInNeither()
    {
        var (invalid, repeated) = (new List<int>(), new List<(int Index, int First)>());

        var lists = TypeOverrideGrammar.ReadMembers(
            [new("int4", "long"), new("int", "MyApp.X")],
            Place,
            invalid,
            repeated
        );

        lists.PostgreSql.ShouldBe([new TypeOverride("int4", "long", Place)]);
        lists.SqlServer.ShouldBeEmpty();
        repeated.ShouldBe([(1, 0)]);
    }

    // rowversion is SQL Server's timestamp alone, and timestamp is read by both.
    [Fact]
    public void ReadMembers_KeyRepeatedInSqlServersList_IsInNeither()
    {
        var (invalid, repeated) = (new List<int>(), new List<(int Index, int First)>());

        var lists = TypeOverrideGrammar.ReadMembers(
            [new("rowversion", "byte[]"), new("timestamp", "MyApp.X")],
            Place,
            invalid,
            repeated
        );

        lists.PostgreSql.ShouldBeEmpty();
        lists.SqlServer.ShouldBe([new TypeOverride("timestamp", "byte[]", Place)]);
        repeated.ShouldBe([(1, 0)]);
    }

    // integer is SQL Server's int, which the first member put, and PostgreSQL's int4, which the second put.
    [Fact]
    public void ReadMembers_KeyRepeatedInEachListByTwoMembers_NamesTheEarlier()
    {
        var (invalid, repeated) = (new List<int>(), new List<(int Index, int First)>());

        _ = TypeOverrideGrammar.ReadMembers(
            [new("sys.int", "long"), new("int4", "long"), new("integer", "MyApp.X")],
            Place,
            invalid,
            repeated
        );

        repeated.ShouldBe([(2, 0)]);
    }

    [Fact]
    public void ReadMembers_NoMember_IsEmpty()
    {
        var (invalid, repeated) = (new List<int>(), new List<(int Index, int First)>());

        var lists = TypeOverrideGrammar.ReadMembers([], Place, invalid, repeated);

        lists.PostgreSql.ShouldBeEmpty();
        lists.SqlServer.ShouldBeEmpty();
        invalid.ShouldBeEmpty();
        repeated.ShouldBeEmpty();
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
    [InlineData(
        "U&\"dbo\".[Email]=X",
        ".  A key is not written in brackets: write U&\"dbo\".Email, or U&\"dbo\".\"Email\" to match its case exactly"
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

    // An unqualified quoted key must be a name in lower case, so an unqualified key has the plain form alone, which
    // matches in any case.
    [Theory]
    [InlineData("[INT]=long", ".  A key is not written in brackets: write INT")]
    [InlineData("[Email]=X", ".  A key is not written in brackets: write Email")]
    [InlineData("[int][]=long", ".  A key is not written in brackets: write int[]")]
    public void BracketHelp_UnqualifiedKeyInBrackets_ShowsThePlainFormAlone(string entry, string help) =>
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
        TypeOverrideGrammar.TryReadEntry(text.AsSpan(), SettingPlace.Query, engine, out var entry)
            ? entry.ShouldNotBeNull().Key
            : null;

    private static TypeOverride Read(string text)
    {
        TypeOverrideGrammar
            .TryReadEntry(text.AsSpan(), SettingPlace.Query, DatabaseEngine.PostgreSql, out var entry)
            .ShouldBeTrue();
        return entry.ShouldNotBeNull();
    }
}
