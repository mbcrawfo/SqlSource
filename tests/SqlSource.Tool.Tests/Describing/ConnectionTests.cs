using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using Shouldly;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Describing;

// Where the connection of a database comes from: the command line, its own variable, or the variable with no name.
public sealed class ConnectionTests
{
    // Every selected database can be described, and the plan has no other.
    private static Connections Resolve(
        string[] selected,
        string[] given,
        params (string Name, string Value)[] environment
    ) => Resolve(selected, selected, given, environment);

    private static Connections Resolve(
        string[] selected,
        string[] describable,
        string[] given,
        params (string Name, string Value)[] environment
    ) => Resolve(selected, selected, describable, given, environment);

    // The value at index i of "given" stands at position (2 * i) + 3, as if each followed "describe --connection".
    private static Connections Resolve(
        string[] planned,
        string[] selected,
        string[] describable,
        string[] given,
        params (string Name, string Value)[] environment
    )
    {
        var variables = environment.ToDictionary(variable => variable.Name, variable => variable.Value);
        return Connections.Resolve(
            selected,
            describable,
            planned,
            [.. given.Select((text, index) => ConnectionArguments.Parse(text, (2 * index) + 3))],
            name => variables.GetValueOrDefault(name)
        );
    }

    [Theory]
    [InlineData("billing", "SQLSOURCE_CONNECTION_BILLING")]
    [InlineData("billing-v2", "SQLSOURCE_CONNECTION_BILLING_V2")]
    [InlineData("app.main", "SQLSOURCE_CONNECTION_APP_MAIN")]
    [InlineData("a_b", "SQLSOURCE_CONNECTION_A_B")]
    [InlineData("café", "SQLSOURCE_CONNECTION_CAFÉ")]
    public void For_DatabaseName_IsUpperCaseWithAnythingElseAsAnUnderscore(string database, string variable) =>
        ConnectionVariables.For(database).ShouldBe(variable);

    [Fact]
    public void TryParse_ValueThatHoldsEqualsSigns_IsCutAtTheFirst() =>
        ConnectionArguments
            .Parse("billing=Host=db;Port=5432")
            .ShouldBe(new ConnectionArgument("billing", "Host=db;Port=5432"));

    // Removing a value of white space from a describer's text would turn every space of it into the mark.
    [Theory]
    [InlineData("billing=")]
    [InlineData("billing= ")]
    [InlineData("billing=\t \r\n")]
    [InlineData("billing=\u00a0")]
    public void TryParse_ValueThatIsEmptyOrHoldsOnlyWhiteSpace_IsNoValue(string text)
    {
        ConnectionArgument.TryParse(text, out var argument).ShouldBeFalse();
        argument.ShouldBeNull();
    }

    [Fact]
    public void TryParse_ValueWithWhiteSpaceAroundSomethingElse_IsKeptWhole() =>
        ConnectionArguments.Parse("billing= a ").ShouldBe(new ConnectionArgument("billing", " a "));

    [Fact]
    public void ToString_ConnectionArgument_LeavesTheValueOut() =>
        ConnectionArguments.Parse("billing=" + Secrets.Word).ToString().ShouldNotContain(Secrets.Word);

    [Fact]
    public void ToString_UsageValueOfAConnection_LeavesTheTextOut() =>
        new UsageValue(new Option<string>("--connection"), "billing=" + Secrets.Word, 3)
            .ToString()
            .ShouldBe("UsageValue { Option = --connection, Position = 3 }");

    [Fact]
    public void For_ValueOnTheCommandLine_IsUsedAndSaysWhereItCameFrom() =>
        Resolve(["billing"], ["billing=A"])
            .For("billing")
            .ShouldBe(new DatabaseConnection("billing", "SQLSOURCE_CONNECTION_BILLING", "A", "--connection", null));

    [Fact]
    public void For_NameOnTheCommandLineInAnotherCase_IsThatDatabases() =>
        Resolve(["billing"], ["BILLING=A"]).For("Billing").Value.ShouldBe("A");

    [Fact]
    public void For_VariableOfTheName_IsUsedAndSaysWhereItCameFrom()
    {
        var connection = Resolve(["billing"], [], ("SQLSOURCE_CONNECTION_BILLING", "B")).For("billing");

        connection.Value.ShouldBe("B");
        connection.Source.ShouldBe("SQLSOURCE_CONNECTION_BILLING");
    }

    [Fact]
    public void For_VariableWithNoNameAndOneSelectedDatabase_IsUsed()
    {
        var connection = Resolve(["billing"], [], ("SQLSOURCE_CONNECTION", "C")).For("billing");

        connection.Value.ShouldBe("C");
        connection.Source.ShouldBe("SQLSOURCE_CONNECTION");
    }

    [Fact]
    public void For_EverySource_TheCommandLineWinsThenTheVariableOfTheName()
    {
        (string, string)[] both = [("SQLSOURCE_CONNECTION_BILLING", "B"), ("SQLSOURCE_CONNECTION", "C")];

        Resolve(["billing"], ["billing=A"], both).For("billing").Value.ShouldBe("A");
        Resolve(["billing"], [], both).For("billing").Value.ShouldBe("B");
    }

    [Fact]
    public void For_VariableThatIsEmpty_IsNotSet()
    {
        var connections = Resolve(["billing"], [], ("SQLSOURCE_CONNECTION_BILLING", ""), ("SQLSOURCE_CONNECTION", ""));

        connections.For("billing").Value.ShouldBeNull();
        connections.UnnamedIsSetAndNotUsed.ShouldBeFalse();
    }

    // Redacting a space would turn every space of a describer's text into the mark.
    [Theory]
    [InlineData(" ")]
    [InlineData("\t \r\n")]
    public void For_VariableThatHoldsOnlyWhiteSpace_IsNotSet(string blank)
    {
        var connections = Resolve(
            ["billing"],
            [],
            ("SQLSOURCE_CONNECTION_BILLING", blank),
            ("SQLSOURCE_CONNECTION", blank)
        );

        connections.For("billing").Value.ShouldBeNull();
        connections.UnnamedIsSetAndNotUsed.ShouldBeFalse();
    }

    [Fact]
    public void For_VariableWithNoNameAndTwoSelectedDatabases_IsNotUsed()
    {
        var connections = Resolve(["billing", "reports"], ["billing=A"], ("SQLSOURCE_CONNECTION", "C"));

        connections.For("billing").Value.ShouldBe("A");
        connections.For("reports").Value.ShouldBeNull();
        connections.UnnamedIsSetAndNotUsed.ShouldBeTrue();
    }

    // A database that only a file which cannot be described gives the run is none the variable could be meant for.
    [Fact]
    public void For_VariableWithNoNameAndOneOfTwoSelectedDatabasesThatCanBeDescribed_IsUsedForThatOne()
    {
        var connections = Resolve(["ansi", "Billing"], ["billing"], [], ("SQLSOURCE_CONNECTION", "C"));

        connections.For("billing").Source.ShouldBe("SQLSOURCE_CONNECTION");
        connections.For("billing").Value.ShouldBe("C");
        connections.For("ansi").Value.ShouldBeNull();
        connections.UnnamedIsSetAndNotUsed.ShouldBeFalse();
        connections.Databases.ShouldBe(["ansi", "Billing"]);
    }

    [Fact]
    public void Describable_TwoOfThreeSelectedDatabases_AreTheOnesTheVariableWithNoNameIsNotUsedFor()
    {
        var connections = Resolve(
            ["ansi", "billing", "reports"],
            ["billing", "reports"],
            [],
            ("SQLSOURCE_CONNECTION", "C")
        );

        connections.For("billing").Value.ShouldBeNull();
        connections.For("reports").Value.ShouldBeNull();
        connections.UnnamedIsSetAndNotUsed.ShouldBeTrue();
        connections.Describable.ShouldBe(["billing", "reports"]);
    }

    [Fact]
    public void UnusedNames_NameThatIsNoSelectedDatabase_IsNoErrorAndIsKept()
    {
        var connections = Resolve(
            ["billing", "reports"],
            ["billing"],
            ["billing"],
            ["biling=A", "billing=B", "reportz=C", "reports=D"]
        );

        connections.For("billing").Value.ShouldBe("B");
        connections.UnusedNames.ShouldBe(["biling", "reportz", "reports"]);
        connections.UnusedPositions.ShouldBeEmpty();
        connections.Databases.ShouldBe(["billing"]);
    }

    // A name near a database of the plan, selected or not, is kept; ignoring case, as the databases are.
    [Fact]
    public void UnusedNames_NameCloseToAnUnselectedDatabaseOfThePlanIgnoringCase_IsKept()
    {
        var connections = Resolve(["billing", "reports"], ["billing"], ["billing"], ["BILING=A"]);

        connections.UnusedNames.ShouldBe(["BILING"]);
        connections.UnusedPositions.ShouldBeEmpty();
    }

    // The name of a secret typed in the wrong place is kept by the position of its value, in the order given.
    [Fact]
    public void UnusedNames_NameFarFromEveryDatabase_IsKeptByItsPosition()
    {
        var connections = Resolve(
            ["billing", "reports"],
            ["billing"],
            ["billing"],
            ["Host=A", "biling=B", "Server=C", "billing=D"]
        );

        connections.UnusedNames.ShouldBe(["biling"]);
        connections.UnusedPositions.ShouldBe([3, 7]);
        connections.For("billing").Value.ShouldBe("D");
    }

    [Fact]
    public void For_TwoNamesWithOneVariableBothFromTheEnvironment_NeitherHasAConnection()
    {
        var connections = Resolve(["app-v2", "app_v2"], [], ("SQLSOURCE_CONNECTION_APP_V2", "B"));

        connections
            .For("app-v2")
            .ShouldBe(new DatabaseConnection("app-v2", "SQLSOURCE_CONNECTION_APP_V2", null, null, "app_v2"));
        connections
            .For("app_v2")
            .ShouldBe(new DatabaseConnection("app_v2", "SQLSOURCE_CONNECTION_APP_V2", null, null, "app-v2"));
    }

    // The one on the command line too: neither has a connection.
    [Fact]
    public void For_TwoNamesWithOneVariableAndOneOnTheCommandLine_NeitherHasAConnection()
    {
        var connections = Resolve(["app-v2", "app_v2"], ["app-v2=A"], ("SQLSOURCE_CONNECTION_APP_V2", "B"));

        connections.For("app-v2").Value.ShouldBeNull();
        connections.For("app-v2").SharesVariableWith.ShouldBe("app_v2");
        connections.For("app_v2").SharesVariableWith.ShouldBe("app-v2");
    }

    [Fact]
    public void For_TwoNamesWithOneVariableBothOnTheCommandLine_EachHasItsOwn()
    {
        var connections = Resolve(["app-v2", "app_v2"], ["app-v2=A", "app_v2=B"]);

        connections.For("app-v2").Value.ShouldBe("A");
        connections.For("app_v2").Value.ShouldBe("B");
        connections.For("app_v2").SharesVariableWith.ShouldBeNull();
    }

    [Theory]
    [InlineData("describe", "--connection", "billing=Host=db")]
    [InlineData("describe", "--connection=billing=Host=db", "--connection:reports=Host=db:5432")]
    public void CheckUsage_ConnectionsWithANameAndAValue_GiveNoLine(params string[] args) =>
        DescribeCommand.CheckUsage(UsageCheck.Check(Cli.BuildCommands(), args)).ShouldBeEmpty();

    [Theory]
    [InlineData(3, "describe", "--connection", Secrets.Word)]
    [InlineData(3, "describe", "--connection", "=" + Secrets.Word)]
    [InlineData(3, "describe", "--connection", "not a name=" + Secrets.Word)]
    [InlineData(3, "describe", "--connection", "billing=")]
    [InlineData(3, "describe", "--connection", "billing= ")]
    [InlineData(3, "describe", "--connection", "billing=\t")]
    [InlineData(2, "describe", "--connection=" + Secrets.Word)]
    [InlineData(2, "describe", "--connection:" + Secrets.Word)]
    public void CheckUsage_ConnectionThatIsNoNameAndValue_IsReportedByItsPositionAndNotItsText(
        int position,
        params string[] args
    ) =>
        DescribeCommand
            .CheckUsage(UsageCheck.Check(Cli.BuildCommands(), args))
            .ShouldBe([
                $"sqlsource: the value of option '--connection' at position {position} is not "
                    + "<name>=<connection string>",
            ]);

    // A path may hold "=" or ";", as the fixture OddPaths does, until a connection is on the line.  Then a .sql path
    // too is refused, since it may be the end of a value that the shell split.
    [Theory]
    [InlineData("describe", "dir=a;b")]
    [InlineData("describe", "dir=a;b", "x=y;z.sql")]
    [InlineData("describe", "--database", "billing", "x=y.sql")]
    public void CheckUsage_PathWithEqualsOrSemicolonAndNoConnection_GivesNoLine(params string[] args) =>
        DescribeCommand.CheckUsage(UsageCheck.Check(Cli.BuildCommands(), args)).ShouldBeEmpty();

    [Theory]
    [InlineData(4, "describe", "--connection", "billing=Host=db;User", "Id=sa;Password=" + Secrets.Word)]
    [InlineData(2, "describe", "Id=sa;Password=" + Secrets.Word, "--connection=billing=Host=db")]
    [InlineData(4, "describe", "--connection", "billing=Host=db", "Password=" + Secrets.Word)]
    [InlineData(4, "describe", "--connection", "billing=Host=db", "Id=sa;" + Secrets.Word)]
    [InlineData(4, "describe", "--connection", "billing=Host=db", "Users=1;2.sql")]
    [InlineData(2, "describe", "Id=sa;Password=" + Secrets.Word + ".sql", "--connection=billing=Host=db")]
    public void CheckUsage_PathWithEqualsOrSemicolonBesideAConnection_IsReportedByItsPositionAndNotItsText(
        int position,
        params string[] args
    ) =>
        DescribeCommand
            .CheckUsage(UsageCheck.Check(Cli.BuildCommands(), args))
            .ShouldBe([
                $"sqlsource: the argument at position {position} looks like a part of a connection string; put the "
                    + "value of '--connection' in quotes",
            ]);

    [Theory]
    [InlineData("describe", "--connection", "billing=Host=db", "App.csproj")]
    [InlineData("describe", "--connection", "billing=Host=db", "a b")]
    public void CheckUsage_PathWithNothingOfAConnectionStringBesideAConnection_GivesNoLine(params string[] args) =>
        DescribeCommand.CheckUsage(UsageCheck.Check(Cli.BuildCommands(), args)).ShouldBeEmpty();

    [Fact]
    public void CheckUsage_SecondConnectionForOneNameIgnoringCase_IsReportedByItsPosition() =>
        DescribeCommand
            .CheckUsage(
                UsageCheck.Check(
                    Cli.BuildCommands(),
                    ["describe", "--connection", "billing=A", "--connection=BILLING=" + Secrets.Word]
                )
            )
            .ShouldBe(["sqlsource: option '--connection' at position 4 names a database that an earlier one names"]);
}
