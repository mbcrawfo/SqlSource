using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Reporting;
using Xunit;

namespace SqlSource.Tool.Tests;

// Where the connection of a database comes from: the command line, its own variable, or the variable with no name.
public class ConnectionTests
{
    private const string Secret = "s3cret";

    private static ConnectionArgument Parse(string text)
    {
        ConnectionArgument.TryParse(text, out var argument).ShouldBeTrue();
        return argument.ShouldNotBeNull();
    }

    private static Connections Resolve(
        string[] selected,
        string[] given,
        params (string Name, string Value)[] environment
    )
    {
        var variables = environment.ToDictionary(variable => variable.Name, variable => variable.Value);
        return Connections.Resolve(selected, [.. given.Select(Parse)], name => variables.GetValueOrDefault(name));
    }

    private static Command ToolRoot() =>
        Cli.BuildCommands(Hosts.Create("/work", new FakeProcessRunner(), "/tmp"), new Reporter(TextWriter.Null));

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
        Parse("billing=Host=db;Port=5432").ShouldBe(new ConnectionArgument("billing", "Host=db;Port=5432"));

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
        Parse("billing= a ").ShouldBe(new ConnectionArgument("billing", " a "));

    [Fact]
    public void ToString_ConnectionArgument_LeavesTheValueOut() =>
        Parse("billing=" + Secret).ToString().ShouldNotContain(Secret);

    [Fact]
    public void ToString_UsageValueOfAConnection_LeavesTheTextOut() =>
        new UsageValue(new Option<string>("--connection"), "billing=" + Secret, 3)
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

    [Fact]
    public void UnusedNames_NameThatIsNoSelectedDatabase_IsNoErrorAndIsKept()
    {
        var connections = Resolve(["billing"], ["biling=A", "billing=B", "reports=C"]);

        connections.For("billing").Value.ShouldBe("B");
        connections.UnusedNames.ShouldBe(["biling", "reports"]);
        connections.Databases.ShouldBe(["billing"]);
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

    // The one on the command line too: the spec gives neither a connection.
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
        DescribeCommand.CheckUsage(UsageCheck.Check(ToolRoot(), args)).ShouldBeEmpty();

    [Theory]
    [InlineData(3, "describe", "--connection", Secret)]
    [InlineData(3, "describe", "--connection", "=" + Secret)]
    [InlineData(3, "describe", "--connection", "not a name=" + Secret)]
    [InlineData(3, "describe", "--connection", "billing=")]
    [InlineData(3, "describe", "--connection", "billing= ")]
    [InlineData(3, "describe", "--connection", "billing=\t")]
    [InlineData(2, "describe", "--connection=" + Secret)]
    [InlineData(2, "describe", "--connection:" + Secret)]
    public void CheckUsage_ConnectionThatIsNoNameAndValue_IsReportedByItsPositionAndNotItsText(
        int position,
        params string[] args
    ) =>
        DescribeCommand
            .CheckUsage(UsageCheck.Check(ToolRoot(), args))
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
        DescribeCommand.CheckUsage(UsageCheck.Check(ToolRoot(), args)).ShouldBeEmpty();

    [Theory]
    [InlineData(4, "describe", "--connection", "billing=Host=db;User", "Id=sa;Password=" + Secret)]
    [InlineData(2, "describe", "Id=sa;Password=" + Secret, "--connection=billing=Host=db")]
    [InlineData(4, "describe", "--connection", "billing=Host=db", "Password=" + Secret)]
    [InlineData(4, "describe", "--connection", "billing=Host=db", "Id=sa;" + Secret)]
    [InlineData(4, "describe", "--connection", "billing=Host=db", "Users=1;2.sql")]
    [InlineData(2, "describe", "Id=sa;Password=" + Secret + ".sql", "--connection=billing=Host=db")]
    public void CheckUsage_PathWithEqualsOrSemicolonBesideAConnection_IsReportedByItsPositionAndNotItsText(
        int position,
        params string[] args
    ) =>
        DescribeCommand
            .CheckUsage(UsageCheck.Check(ToolRoot(), args))
            .ShouldBe([
                $"sqlsource: the argument at position {position} looks like a part of a connection string; put the "
                    + "value of '--connection' in quotes",
            ]);

    [Theory]
    [InlineData("describe", "--connection", "billing=Host=db", "App.csproj")]
    [InlineData("describe", "--connection", "billing=Host=db", "a b")]
    public void CheckUsage_PathWithNothingOfAConnectionStringBesideAConnection_GivesNoLine(params string[] args) =>
        DescribeCommand.CheckUsage(UsageCheck.Check(ToolRoot(), args)).ShouldBeEmpty();

    [Fact]
    public void CheckUsage_SecondConnectionForOneNameIgnoringCase_IsReportedByItsPosition() =>
        DescribeCommand
            .CheckUsage(
                UsageCheck.Check(
                    ToolRoot(),
                    ["describe", "--connection", "billing=A", "--connection=BILLING=" + Secret]
                )
            )
            .ShouldBe(["sqlsource: option '--connection' at position 4 names a database that an earlier one names"]);
}
