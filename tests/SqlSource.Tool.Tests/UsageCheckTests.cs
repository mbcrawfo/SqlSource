using System.CommandLine;
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
        UsageCheck.Check(Root(), args).ShouldBeEmpty();

    [Fact]
    public void Check_ValueThatStartsWithAHyphen_IsTheValueOfTheOptionBeforeIt() =>
        UsageCheck.Check(Root(), ["describe", "--connection", "-" + Secret]).ShouldBeEmpty();

    [Fact]
    public void Check_OptionAtTheEndWithoutItsValue_NamesTheOption() =>
        UsageCheck
            .Check(Root(), ["describe", "--connection"])
            .ShouldBe(["sqlsource: option '--connection' needs a value"]);

    [Fact]
    public void Check_ValueForAFlag_NamesTheOptionAndNotTheValue() =>
        UsageCheck
            .Check(Root(), ["describe", "--force=" + Secret])
            .ShouldBe(["sqlsource: option '--force' takes no value"]);

    [Fact]
    public void Check_OptionOfACommandGivenBeforeTheCommand_IsUnknownThere() =>
        UsageCheck.Check(Root(), ["--force", "describe"]).ShouldBe(["sqlsource: unknown option '--force'"]);

    [Fact]
    public void Check_OptionThatIsNotRecursive_IsUnknownUnderACommand()
    {
        var root = new Command("sqlsource") { new Option<bool>("--version"), new Command("describe") };

        UsageCheck.Check(root, ["describe", "--version"]).ShouldBe(["sqlsource: unknown option '--version'"]);
    }

    [Fact]
    public void Check_SeveralMistakes_GivesALineForEach() =>
        UsageCheck
            .Check(Root(), ["describe", "--force=" + Secret, "one", Secret, "--connection"])
            .ShouldBe([
                "sqlsource: option '--force' takes no value",
                "sqlsource: unexpected argument at position 4",
                "sqlsource: option '--connection' needs a value",
            ]);

    [Fact]
    public void Check_CommandNameAfterAnArgument_IsAnArgument() =>
        UsageCheck
            .Check(Root(), ["describe", "App.csproj", "describe"])
            .ShouldBe(["sqlsource: unexpected argument at position 3"]);

    [Theory]
    [InlineData("-")]
    [InlineData("--")]
    [InlineData("-" + Secret)]
    public void Check_AnyTokenThatStartsWithAHyphen_IsAnOption(string token) =>
        UsageCheck.Check(Root(), ["describe", token]).ShouldBe([$"sqlsource: unknown option '{token}'"]);
}
