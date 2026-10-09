using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using Shouldly;
using Xunit;

namespace SqlSource.Tool.Tests;

// The command line of this sub-phase has no option that takes a value.  These tests give UsageCheck a command that
// has one, as later sub-phases will, so that the rule for a value is held before the first secret arrives.
public class UsageCheckTests
{
    private const string Secret = "s3cret";

    private static Command Root()
    {
        var connection = new Option<string>("--connection", "-c") { Arity = ArgumentArity.ExactlyOne };
        var force = new Option<bool>("--force") { Arity = ArgumentArity.Zero };
        var verbose = new Option<bool>("--verbose") { Arity = ArgumentArity.Zero, Recursive = true };
        var path = new Argument<string?>("path") { Arity = ArgumentArity.ZeroOrOne };
        return new Command("sqlsource")
        {
            verbose,
            new Command("describe") { connection, force, path },
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
}
