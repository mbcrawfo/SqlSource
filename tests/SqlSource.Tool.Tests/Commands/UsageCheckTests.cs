using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using Shouldly;
using Xunit;

namespace SqlSource.Tool.Tests.Commands;

// Most tests give UsageCheck a command built here, apart from the tool's, so that a change to the tool's options
// does not move them; the tests at the end run the same checks over the tool's own commands.
public class UsageCheckTests
{
    private const string Secret = "s3cret";

    private static Command Root()
    {
        var connection = new Option<string>("--connection", "-c") { Arity = ArgumentArity.ExactlyOne };
        var project = new Option<string[]>("--project") { Arity = ArgumentArity.OneOrMore };
        var force = new Option<bool>("--force") { Arity = ArgumentArity.Zero };
        var verbose = new Option<bool>("--verbose") { Arity = ArgumentArity.Zero, Recursive = true };
        var path = new Argument<string?>("path") { Arity = ArgumentArity.ZeroOrOne };
        return new Command("sqlsource")
        {
            verbose,
            new Command("describe") { connection, project, force, path },
        };
    }

    [Theory]
    [InlineData]
    [InlineData("describe")]
    [InlineData("describe", "App.csproj")]
    [InlineData("describe", "--connection", "billing=Host=db")]
    [InlineData("describe", "--connection=billing=Host=db")]
    [InlineData("describe", "--connection:billing=Host=db")]
    [InlineData("describe", "-c", "billing=Host=db", "App.csproj", "--force")]
    [InlineData("--verbose", "describe")]
    [InlineData("describe", "--verbose")]
    [InlineData("describe", "--project", "A.csproj")]
    [InlineData("describe", "--project=A.csproj", "App.slnx", "--project", "B.csproj")]
    public void Check_CommandLineWithNothingWrong_GivesNoLine(params string[] args) =>
        UsageCheck.Check(Root(), args).Messages.ShouldBeEmpty();

    [Fact]
    public void Check_ValueThatStartsWithAHyphen_IsTheValueOfTheOptionBeforeIt() =>
        UsageCheck.Check(Root(), ["describe", "--connection", "-" + Secret]).Messages.ShouldBeEmpty();

    [Fact]
    public void Check_OptionAtTheEndWithoutItsValue_NamesTheOption() =>
        UsageCheck
            .Check(Root(), ["describe", "--connection"])
            .Messages.ShouldBe(["sqlsource: option '--connection' needs a value"]);

    [Fact]
    public void Check_ValueForAFlag_NamesTheOptionAndNotTheValue() =>
        UsageCheck
            .Check(Root(), ["describe", "--force=" + Secret])
            .Messages.ShouldBe(["sqlsource: option '--force' takes no value"]);

    [Fact]
    public void Check_OptionOfACommandGivenBeforeTheCommand_IsUnknownThere() =>
        UsageCheck.Check(Root(), ["--force", "describe"]).Messages.ShouldBe(["sqlsource: unknown option '--force'"]);

    [Fact]
    public void Check_OptionThatIsNotRecursive_IsUnknownUnderACommand()
    {
        var root = new Command("sqlsource") { new Option<bool>("--version"), new Command("describe") };

        UsageCheck.Check(root, ["describe", "--version"]).Messages.ShouldBe(["sqlsource: unknown option '--version'"]);
    }

    [Fact]
    public void Check_SeveralMistakes_GivesALineForEach() =>
        UsageCheck
            .Check(Root(), ["describe", "--force=" + Secret, "one", Secret, "--connection"])
            .Messages.ShouldBe([
                "sqlsource: option '--force' takes no value",
                "sqlsource: unexpected argument at position 4",
                "sqlsource: option '--connection' needs a value",
            ]);

    [Fact]
    public void Check_CommandNameAfterAnArgument_IsAnArgument() =>
        UsageCheck
            .Check(Root(), ["describe", "App.csproj", "describe"])
            .Messages.ShouldBe(["sqlsource: unexpected argument at position 3"]);

    [Theory]
    [InlineData("-")]
    [InlineData("--")]
    [InlineData("-" + Secret)]
    public void Check_AnyTokenThatStartsWithAHyphen_IsAnOption(string token) =>
        UsageCheck.Check(Root(), ["describe", token]).Messages.ShouldBe([$"sqlsource: unknown option '{token}'"]);

    [Theory]
    [InlineData("--connection=", "--connection")]
    [InlineData("--connection:", "--connection")]
    [InlineData("-c=", "-c")]
    public void Check_SeparatorWithNothingAfterIt_NeedsAValue(string token, string name) =>
        // What --connection="$UNSET" gives.  System.CommandLine reads it as no value and takes the next token.
        UsageCheck
            .Check(Root(), ["describe", token, "--connection", Secret])
            .Messages.ShouldBe([$"sqlsource: option '{name}' needs a value"]);

    [Fact]
    public void Check_OptionOfTheCommandWhereAValueShouldStand_IsNotTakenForTheValue() =>
        // System.CommandLine does not take a token that names an option as a value, and neither may this.
        UsageCheck
            .Check(Root(), ["describe", "--connection", "--force=" + Secret, "--connection=x"])
            .Messages.ShouldBe([
                "sqlsource: option '--connection' needs a value",
                "sqlsource: option '--force' takes no value",
            ]);

    // System.CommandLine rejects a second value for an option that takes one, with a message that repeats it.
    [Theory]
    [InlineData("--connection", "describe", "--connection", "a=b", "--connection=" + Secret)]
    [InlineData("-c", "describe", "-c", "a=b", "App.csproj", "-c:" + Secret)]
    public void Check_OptionThatTakesOneValueGivenTwice_IsNamedAsItWasTypedAndItsValueIsNot(
        string name,
        params string[] args
    ) => UsageCheck.Check(Root(), args).Messages.ShouldBe([$"sqlsource: option '{name}' is given more than once"]);

    // An option that may be given several times takes one value each time, and the token after that is the path.
    [Fact]
    public void Check_OptionThatMayBeRepeated_TakesOneValueEachTime()
    {
        var usage = UsageCheck.Check(Root(), ["describe", "--project", "A.csproj", "App.slnx", "--project", Secret]);

        usage.Messages.ShouldBeEmpty();
        usage.Arguments.ShouldBe(["App.slnx"]);
    }

    [Theory]
    [InlineData("describe", "--project")]
    [InlineData("describe", "--project=")]
    [InlineData("describe", "--project", "--force")]
    public void Check_RepeatedOptionWithoutAValue_NeedsOne(params string[] args) =>
        UsageCheck.Check(Root(), args).Messages.ShouldBe(["sqlsource: option '--project' needs a value"]);

    [Fact]
    public void Check_CommandLineWithNothingWrong_GivesTheCommandAndItsArguments()
    {
        var usage = UsageCheck.Check(Root(), ["--verbose", "describe", "-c", Secret, "App.csproj", "--force"]);

        usage.Command.Name.ShouldBe("describe");
        usage.Arguments.ShouldBe(["App.csproj"]);
    }

    [Fact]
    public void IsReadTheSameBy_ParseThatBindsAnotherArgument_IsFalse()
    {
        var root = Root();
        var usage = UsageCheck.Check(root, ["describe", "App.csproj"]);

        usage.IsReadTheSameBy(root.Parse(["describe", "App.csproj"], Cli.Parser)).ShouldBeTrue();
        usage.IsReadTheSameBy(root.Parse(["describe", Secret], Cli.Parser)).ShouldBeFalse();
        usage.IsReadTheSameBy(root.Parse(["describe"], Cli.Parser)).ShouldBeFalse();
        usage.IsReadTheSameBy(root.Parse([], Cli.Parser)).ShouldBeFalse();
    }

    [Fact]
    public void ReadsConnectionsAs_ParseThatBindsOtherValues_IsFalse()
    {
        var root = Cli.BuildCommands();
        var usage = UsageCheck.Check(root, ["describe", "--connection", "a=" + Secret, "--connection", "b=2"]);

        DescribeCommand
            .ReadsConnectionsAs(usage, root.Parse(["describe", "--connection", "a=" + Secret, "--connection", "b=2"]))
            .ShouldBeTrue();
        DescribeCommand
            .ReadsConnectionsAs(usage, root.Parse(["describe", "--connection", "a=x", "--connection", "b=2"]))
            .ShouldBeFalse();
        DescribeCommand
            .ReadsConnectionsAs(usage, root.Parse(["describe", "--connection", "b=2", "--connection", "a=" + Secret]))
            .ShouldBeFalse();
        DescribeCommand
            .ReadsConnectionsAs(usage, root.Parse(["describe", "--connection", "a=" + Secret]))
            .ShouldBeFalse();
        DescribeCommand
            .ReadsConnectionsAs(
                usage,
                root.Parse(["describe", "--connection", "a=" + Secret, "--connection", "b=2", "--connection", "c=3"])
            )
            .ShouldBeFalse();
        DescribeCommand
            .ReadsConnectionsAs(UsageCheck.Check(root, ["--version"]), root.Parse(["--version"]))
            .ShouldBeTrue();
    }

    // The two readers of a command line must agree, or a token that this one took for a value is the path of the
    // other, and a path is printed.  Every command line of up to four of these tokens that both accept is read by
    // both as one command with the same arguments.
    [Fact]
    public void Check_EveryAcceptedCommandLine_IsReadTheSameBySystemCommandLine()
    {
        string[] vocabulary =
        [
            "describe",
            "--connection",
            "--connection=",
            "--connection=V",
            "--connection:V",
            "-c",
            "--project",
            "--project=",
            "--project=P",
            "--force",
            "--force=V",
            "--verbose",
            "--log",
            "--log=L",
            "--",
            "V",
            "W",
        ];
        var root = Root();
        root.Add(new Option<string>("--log") { Arity = ArgumentArity.ExactlyOne, Recursive = true });
        var disagreements = new List<string>();

        foreach (var args in Sequences(vocabulary, 4))
        {
            var usage = UsageCheck.Check(root, args);
            var parsed = root.Parse(args, Cli.Parser);
            if (usage.Messages.Count == 0 && parsed.Errors.Count == 0 && !usage.IsReadTheSameBy(parsed))
            {
                disagreements.Add(string.Join(' ', args));
            }
        }

        disagreements.Count.ShouldBe(0, string.Join(" | ", disagreements.Take(10)));
    }

    // Every sequence of the vocabulary's tokens, from none to the maximum length.
    private static IEnumerable<string[]> Sequences(string[] vocabulary, int maximumLength)
    {
        List<string[]> ofOneLength =
        [
            [],
        ];
        for (var length = 0; length <= maximumLength; length++)
        {
            foreach (var sequence in ofOneLength)
            {
                yield return sequence;
            }

            ofOneLength =
            [
                .. ofOneLength.SelectMany(_ => vocabulary, (sequence, token) => (string[])[.. sequence, token]),
            ];
        }
    }

    [Fact]
    public void Check_UnknownOptionWithALineBreak_IsNamedOnOneLine() =>
        // A line that starts in the first column and looks like an error is read as one by a build.
        UsageCheck
            .Check(Root(), ["describe", "--oops\nsrc/App.cs(1,1): error CS0000: forged"])
            .Messages.ShouldBe(["sqlsource: unknown option '--oops src/App.cs(1,1)'"]);

    [Fact]
    public void Check_ValuesOfOptionsAndArguments_AreGivenWithTheirPositions()
    {
        var usage = UsageCheck.Check(
            Root(),
            ["describe", "--project", "A.csproj", "App.slnx", "--project=B.csproj", "-c:x"]
        );

        usage.Messages.ShouldBeEmpty();
        usage
            .Values.Select(value => (value.Option?.Name, value.Text, value.Position))
            .ShouldBe([
                ("--project", "A.csproj", 3),
                (null, "App.slnx", 4),
                ("--project", "B.csproj", 5),
                ("--connection", "x", 6),
            ]);
    }

    // The same as the test above it, for the commands of the tool itself, whose "describe" takes several paths and
    // has rules of its own.
    [Fact]
    public void Check_EveryAcceptedCommandLineOfTheTool_IsReadTheSameBySystemCommandLine()
    {
        string[] vocabulary =
        [
            "describe",
            "--project",
            "--project=P",
            "--database",
            "--database=D",
            "--database=",
            "--connection",
            "--connection=N=V",
            "--connection:N=V",
            "--connection=",
            "N=V",
            "--force",
            "--force=V",
            "--check",
            "--verbose",
            "--log",
            "--log=L",
            "--help",
            "--",
            "V",
            "W",
            "Q.sql",
            "R.SQL",
        ];
        var root = Cli.BuildCommands();
        var disagreements = new List<string>();

        foreach (var args in Sequences(vocabulary, 4))
        {
            var usage = UsageCheck.Check(root, args);
            var parsed = root.Parse(args, Cli.Parser);
            if (usage.Messages.Count > 0 || DescribeCommand.CheckUsage(usage).Count > 0 || parsed.Errors.Count > 0)
            {
                continue;
            }

            // The command checks the values that UsageCheck read and runs with the ones System.CommandLine read,
            // so the two must give each option the same values too.
            if (
                !usage.IsReadTheSameBy(parsed)
                || !HasTheSameValues(usage, parsed, "--project", "--database", "--connection")
            )
            {
                disagreements.Add(string.Join(' ', args));
            }
        }

        disagreements.Count.ShouldBe(0, string.Join(" | ", disagreements.Take(10)));
    }

    [Theory]
    [InlineData("describe", "App.csproj", "Q.sql", "R.sql")]
    [InlineData("describe", "Q.sql", "App.csproj", "R.SQL")]
    [InlineData("describe", "Q.sql")]
    [InlineData("describe", "--database", "billing", "--database=app-v2.main")]
    public void CheckUsage_PathsAndDatabasesOfDescribe_GiveNoLine(params string[] args) =>
        DescribeCommand.CheckUsage(UsageCheck.Check(Cli.BuildCommands(), args)).ShouldBeEmpty();

    [Fact]
    public void CheckUsage_SecondPathThatIsNoSqlFile_IsReportedByItsPosition() =>
        DescribeCommand
            .CheckUsage(
                UsageCheck.Check(Cli.BuildCommands(), ["describe", "App.csproj", "Q.sql", Secret, "--project", "P"])
            )
            .ShouldBe(["sqlsource: unexpected argument at position 4"]);

    [Theory]
    [InlineData(3, "describe", "--database", "not a name")]
    [InlineData(2, "describe", "--database=" + Secret + "!")]
    [InlineData(3, "describe", "--database", "")]
    public void CheckUsage_DatabaseThatIsNoName_IsReportedByItsPositionAndNotItsText(
        int position,
        params string[] args
    ) =>
        DescribeCommand
            .CheckUsage(UsageCheck.Check(Cli.BuildCommands(), args))
            .ShouldBe([$"sqlsource: the value of option '--database' at position {position} is not a database name"]);

    [Fact]
    public void CheckUsage_AnotherCommand_GivesNoLine() =>
        DescribeCommand.CheckUsage(UsageCheck.Check(Cli.BuildCommands(), ["--version"])).ShouldBeEmpty();

    private static bool HasTheSameValues(Usage usage, ParseResult parsed, params string[] options) =>
        usage.Command.Name != DescribeCommand.Name
        || options.All(name =>
            usage
                .Values.Where(value => value.Option?.Name == name)
                .Select(value => value.Text)
                .SequenceEqual(parsed.GetValue<string[]>(name) ?? [])
        );
}
