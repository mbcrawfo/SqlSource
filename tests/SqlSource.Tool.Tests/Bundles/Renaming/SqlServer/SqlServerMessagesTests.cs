using System;
using System.Collections.Generic;
using Shouldly;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Bundles.Renaming.SqlServer;
using SqlSource.Tool.Describing.SqlServer;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming.SqlServer;

// A server's message becomes the parameters it names, renamed, as SqlServerErrors.NamedIn reads them.
public sealed class SqlServerMessagesTests
{
    private static string Rename(string message) => SqlServerMessages.Rename(message, new Scope());

    [Fact]
    public void Rename_ACopyOfAMappedParameter_IsWrittenAsTheParameter() =>
        Rename("The undeclared parameter '@label_1' is used more than once in the batch being analyzed.")
            .ShouldBe("@<label>");

    [Fact]
    public void Rename_ACopyWithALongerSeparator_IsWrittenAsTheParameter() => Rename("@Label__12").ShouldBe("@<label>");

    // x__1 is a copy of x_ when the query reuses both x and x_, which no spelling tells.
    [Fact]
    public void Rename_ACopyOfAParameterThatEndsInASeparator_IsWrittenAsThatParameter() =>
        Rename("@x__1 and @x_1").ShouldBe("@<x_> @<x>");

    [Fact]
    public void Rename_AGlobalVariable_IsLeftOut() => Rename("@other and @@ROWCOUNT").ShouldBe("@<other>");

    [Fact]
    public void Rename_SeveralNames_AreJoinedByOneSpace() =>
        Rename("'@other' and '@label' differ, and @select_2.").ShouldBe("@<other> @<label> @<select>");

    // A name is a copy only when the capture records it, whatever its spelling.
    [Fact]
    public void Rename_AMappedNameThatLooksLikeACopy_IsWrittenWhole() => Rename("@label_9").ShouldBe("@<label_9>");

    [Fact]
    public void Rename_ANameNotMapped_IsWrittenAsItsOwn() => Rename("@nobody_1 and @").ShouldBe("@<nobody_1>");

    [Fact]
    public void Rename_AConnectionErrorsText_IsEmpty()
    {
        Rename(ConnectionErrors.LoginRefused).ShouldBe("");
        Rename("Invalid object name 'dbo.users'.").ShouldBe("");
    }

    [Fact]
    public void WordsOf_ARenamedMessage_IsEachPrefixAndName() =>
        SqlServerMessages.WordsOf("@p_1 @p_2").ShouldBe(["@", "p_1", "@", "p_2"]);

    [Fact]
    public void WordsOf_AnEmptyMessage_IsNothing() => SqlServerMessages.WordsOf("").ShouldBeEmpty();

    [Fact]
    public void WordsOf_APieceWithNoPrefix_IsThePieceWhole() =>
        SqlServerMessages.WordsOf("users @p_1").ShouldBe(["users", "@", "p_1"]);

    // Records the copies of label, of x and x_, and of select, a keyword, finds a copy ignoring case as SQL Server
    // compares parameters, and writes a parameter as <name>.
    private sealed class Scope : IRenamingScope
    {
        private static readonly Dictionary<string, string> Copies = new(StringComparer.OrdinalIgnoreCase)
        {
            ["label_1"] = "label",
            ["label__12"] = "label",
            ["x_1"] = "x",
            ["x__1"] = "x_",
            ["select_2"] = "select",
        };

        public string Name(string spelling, bool quoted, NameClass cls) => throw new NotSupportedException();

        public void Keep(string spelling, bool quoted) => throw new NotSupportedException();

        public bool IsMapped(string spelling, bool quoted) => throw new NotSupportedException();

        public string Literal(string content) => throw new NotSupportedException();

        public RenamedText Sql(string sql, NameClass newWords) => throw new NotSupportedException();

        public string Parameter(string name) => "<" + name + ">";

        public string ParameterOf(string name) => Copies.GetValueOrDefault(name) ?? name;

        public void Refuse(RenamingRefusal refusal) => throw new NotSupportedException();
    }
}
