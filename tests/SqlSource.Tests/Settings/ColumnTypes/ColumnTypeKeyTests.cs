using System;
using Shouldly;
using SqlSource.Settings.ColumnTypes;
using Xunit;

namespace SqlSource.Tests.Settings.ColumnTypes;

public sealed class ColumnTypeKeyTests
{
    // A plain part is kept as written, and a quoted one, the escaped form among them, in the quoted form of its text.
    [Theory]
    [InlineData("users.id", "users.id")]
    [InlineData("public.users.id", "public.users.id")]
    [InlineData("Users.Id", "Users.Id")]
    [InlineData("\"my schema\".\"Users\".id", "\"my schema\".\"Users\".id")]
    [InlineData("U&\"us\\0065rs\".id", "\"users\".id")]
    [InlineData("\"a.b\".\"c\"\"d\"", "\"a.b\".\"c\"\"d\"")]
    [InlineData("billing.invoice$lines.line_no", "billing.invoice$lines.line_no")]
    public void TryNormalize_ValidKey(string key, string normalized)
    {
        ColumnTypeKey.TryNormalize(key.AsSpan(), out var written).ShouldBeTrue();

        written.ShouldBe(normalized);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("a.b.c.d")]
    [InlineData("users.id[]")]
    [InlineData("[dbo].[users].[id]")]
    [InlineData("")]
    [InlineData("users.")]
    [InlineData(".id")]
    [InlineData("users..id")]
    [InlineData("users.\"\"")]
    [InlineData("users.\"id")]
    [InlineData("users.id ")]
    [InlineData("users id")]
    [InlineData("users.U&\"\\zzzz\"")]
    public void TryNormalize_NotAKey(string key) => ColumnTypeKey.TryNormalize(key.AsSpan(), out _).ShouldBeFalse();
}
