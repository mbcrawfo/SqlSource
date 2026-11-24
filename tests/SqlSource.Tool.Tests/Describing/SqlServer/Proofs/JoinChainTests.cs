using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Tool.Describing.SqlServer.Proofs;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.SqlServer.Proofs;

// The FROM read as a chain of joins: each source's name and whether a join can make it NULL, or null for a FROM it
// cannot follow.  "?" marks an optional source and "!" one that only its own LEFT JOIN makes optional.
public sealed class JoinChainTests
{
    [Theory]
    [InlineData("FROM t", "t")]
    [InlineData("FROM dbo.t", "t")]
    [InlineData("FROM dbo.t AS a", "a")]
    [InlineData("FROM dbo.t a WITH (NOLOCK)", "a")]
    [InlineData("FROM (SELECT 1 AS x) AS q", "q")]
    [InlineData("FROM (SELECT 1) AS q (x)", "q")]
    [InlineData("FROM a JOIN b ON b.k = a.k", "a b")]
    [InlineData("FROM a INNER JOIN b ON b.k = a.k", "a b")]
    [InlineData("FROM a CROSS JOIN b", "a b")]
    [InlineData("FROM a LEFT JOIN b ON b.k = a.k", "a b!")]
    [InlineData("FROM a LEFT OUTER JOIN b ON b.k = a.k", "a b!")]
    [InlineData("FROM a RIGHT JOIN b ON b.k = a.k", "a? b")]
    [InlineData("FROM a FULL JOIN b ON b.k = a.k", "a? b?")]
    [InlineData("FROM a FULL OUTER JOIN b ON b.k = a.k", "a? b?")]
    [InlineData("FROM a LEFT JOIN b ON b.k = a.k JOIN c ON c.k = a.k", "a b! c")]
    [InlineData("FROM a LEFT JOIN b ON b.k = a.k RIGHT JOIN c ON c.k = a.k", "a? b? c")]
    [InlineData("FROM a JOIN b ON LEFT(b.k, 1) = RIGHT(a.k, 1) LEFT JOIN c ON c.k = a.k", "a b c!")]
    public void Read_Chain_GivesEachSource(string from, string sources) => Sources(from).ShouldBe(sources);

    [Theory]
    [InlineData("FROM a, b")]
    [InlineData("FROM a JOIN b ON b.k = a.k, c")]
    [InlineData("FROM a CROSS APPLY f(a.k) AS b")]
    [InlineData("FROM a OUTER APPLY (SELECT 1 AS x) AS b")]
    [InlineData("FROM a JOIN b ON b.k = a.k CROSS APPLY f(a.k) AS c")]
    [InlineData("FROM a LEFT JOIN b JOIN c ON c.k = b.k ON b.k = a.k")]
    [InlineData("FROM a JOIN b LEFT JOIN c ON c.k = b.k ON b.k = a.k")]
    [InlineData("FROM a LEFT HASH JOIN b ON b.k = a.k")]
    [InlineData("FROM a LEFT OUTER MERGE JOIN b ON b.k = a.k")]
    [InlineData("FROM a INNER LOOP JOIN b ON b.k = a.k")]
    [InlineData("FROM a JOIN b ON b.k = a.k LEFT HASH JOIN c ON c.k = a.k")]
    [InlineData("FROM a JOIN b")]
    [InlineData("FROM a JOIN b ON")]
    [InlineData("FROM (a JOIN b ON b.k = a.k)")]
    [InlineData("FROM (a LEFT JOIN b ON b.k = a.k) JOIN c ON c.k = a.k")]
    [InlineData("FROM f(1) AS a")]
    [InlineData("FROM dbo.f(1) AS a")]
    [InlineData("FROM a PIVOT (MAX(v) FOR k IN ([1])) AS p")]
    [InlineData("FROM a UNPIVOT (v FOR k IN (x, y)) AS p")]
    [InlineData("FROM a JOIN b ON b.k = a.k PIVOT (MAX(v) FOR k IN ([1])) AS p")]
    [InlineData("FROM a TABLESAMPLE (10 PERCENT)")]
    [InlineData("FROM a FOR SYSTEM_TIME AS OF '2020-01-01'")]
    [InlineData("FROM a.b.c.d.e")]
    public void Read_NoChain_IsNull(string from)
    {
        var tokens = TsqlTokens.Read($"SELECT 1 AS n {from}");
        var range = SelectStatement.Read(tokens).ShouldNotBeNull().Body.From.ShouldNotBeNull();

        JoinChain.Read(tokens, range).ShouldBeNull();
    }

    [Theory]
    [InlineData("FROM a LEFT JOIN b ON b.k = a.k", "LEFT")]
    [InlineData("FROM a LEFT OUTER JOIN b ON b.k = a.k", "LEFT OUTER")]
    public void Read_LeftJoin_NamesTheWordsOfItsOuter(string from, string words)
    {
        var tokens = TsqlTokens.Read($"SELECT 1 AS n {from}");
        var range = SelectStatement.Read(tokens).ShouldNotBeNull().Body.From.ShouldNotBeNull();

        var outer = JoinChain.Read(tokens, range).ShouldNotBeNull()[1].Outer;

        string.Join(' ', outer.Select(index => tokens[index].Text)).ShouldBe(words);
    }

    [Fact]
    public void Read_FullJoin_GivesEachSourceItsReferenceAndJoinStart()
    {
        var (chain, sources) = Read("FROM issued AS iss FULL JOIN received AS rec ON rec.invoice_id = iss.id");

        sources.Select(source => Text(chain, source.Reference)).ShouldBe(["issued AS iss", "received AS rec"]);
        sources[0].JoinStart.ShouldBe(-1);
        chain.Tokens[sources[1].JoinStart].Text.ShouldBe("FULL");
        sources.Select(static source => source.IsFull).ShouldBe([false, true]);
        sources.Select(static source => source.IsDerived).ShouldBe([false, false]);
    }

    [Fact]
    public void Read_DerivedTableWithColumns_ReferenceHoldsTheColumnList()
    {
        var (chain, sources) = Read("FROM a LEFT JOIN (SELECT 1) AS q (x) ON q.x = a.k");

        Text(chain, sources[1].Reference).ShouldBe("(SELECT 1) AS q (x)");
        Text(chain, new TokenRange(sources[1].JoinStart, sources[1].Reference.Start)).ShouldBe("LEFT JOIN");
        sources.Select(static source => source.IsDerived).ShouldBe([false, true]);
        sources.Select(static source => source.IsFull).ShouldBe([false, false]);
    }

    [Fact]
    public void Read_TableHint_ReferenceHoldsTheHint()
    {
        var (chain, sources) = Read("FROM dbo.t a WITH (NOLOCK) JOIN b ON b.k = a.k");

        Text(chain, sources[0].Reference).ShouldBe("dbo.t a WITH (NOLOCK)");
        Text(chain, sources[1].Reference).ShouldBe("b");
        chain.Tokens[sources[1].JoinStart].Text.ShouldBe("JOIN");
    }

    [Fact]
    public void Read_RightJoin_MarksTheSourceItAdds()
    {
        var (_, sources) = Read("FROM a FULL JOIN b ON 1 = 1 RIGHT OUTER JOIN c ON 1 = 1 LEFT JOIN d ON 1 = 1");

        sources.Select(static source => source.IsRight).ShouldBe([false, false, true, false]);
        sources.Select(static source => source.IsFull).ShouldBe([false, true, false, false]);
    }

    private static (Chain Chain, IReadOnlyList<JoinSource> Sources) Read(string from)
    {
        var sql = $"SELECT 1 AS n {from}";
        var tokens = TsqlTokens.Read(sql);
        var range = SelectStatement.Read(tokens).ShouldNotBeNull().Body.From.ShouldNotBeNull();
        return (new Chain(sql, tokens), JoinChain.Read(tokens, range).ShouldNotBeNull());
    }

    private static string Text(Chain chain, TokenRange range) =>
        chain.Sql[chain.Tokens[range.Start].Start..chain.Tokens[range.End - 1].End];

    private static string Mark(JoinSource source)
    {
        if (source.IsOptionalBesidesItsLeftJoin)
        {
            return "?";
        }

        return source.IsOptional ? "!" : "";
    }

    private sealed record Chain(string Sql, IReadOnlyList<TsqlToken> Tokens);

    private static string Sources(string from)
    {
        var tokens = TsqlTokens.Read($"SELECT 1 AS n {from}");
        var range = SelectStatement.Read(tokens).ShouldNotBeNull().Body.From.ShouldNotBeNull();
        return string.Join(
            ' ',
            JoinChain.Read(tokens, range).ShouldNotBeNull().Select(static source => source.Name + Mark(source))
        );
    }
}
