using System;
using Shouldly;
using SqlSource.Tool.Bundles.Renaming;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming;

public sealed class RenamingRunTests
{
    // A catalog's name and a token's are compared as written; another name of the query file as it reads there.
    [Theory]
    [InlineData(nameof(NameClass.Schema), true)]
    [InlineData(nameof(NameClass.Table), true)]
    [InlineData(nameof(NameClass.Column), true)]
    [InlineData(nameof(NameClass.Alias), true)]
    [InlineData(nameof(NameClass.Function), true)]
    [InlineData(nameof(NameClass.Type), true)]
    [InlineData(nameof(NameClass.Index), true)]
    [InlineData(nameof(NameClass.Token), true)]
    [InlineData(nameof(NameClass.Parameter), false)]
    [InlineData(nameof(NameClass.Query), false)]
    [InlineData(nameof(NameClass.Database), false)]
    [InlineData(nameof(NameClass.Literal), false)]
    public void IsQuoted_Class_IsWhetherItIsComparedAsWritten(string cls, bool quoted) =>
        RenamingRun.IsQuoted(Enum.Parse<NameClass>(cls)).ShouldBe(quoted);
}
