using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Describing;

// Step 3 of the run: a describer that breaks its contract, by throwing, by returning nothing, or by putting the
// connection's value in a description.
public sealed class DatabaseRunsFaultTests : IDisposable
{
    private readonly DatabaseRunsScene _scene = new();

    public void Dispose() => _scene.Dispose();

    // The guard that asks for the parts knows only the whole value.
    [Fact]
    public async Task Run_DescriberThatThrowsWhenAskedForTheSecrets_IsOneFaultWithoutTheValue()
    {
        _scene.Describer.SecretsException = new InvalidOperationException($"cannot read {Secrets.ConnectionWithWord}");

        var fault = await Should.ThrowAsync<DescriberFaultException>(() => _scene.RunAsync());

        fault.InnerException.ShouldBeNull();
        fault.Diagnostic.Descriptor.Id.ShouldBe("SQLSRC200");
        fault.Diagnostic.Arguments.ToArray().ShouldBe(["System.InvalidOperationException", "cannot read ***"]);
        _scene.Error.ToString().ShouldBeEmpty();
        _scene.Describer.Opened.ShouldBeEmpty();
    }

    // The driver's rewrite of the value is asked for under the same guard.
    [Fact]
    public async Task Run_DescriberThatThrowsWhenAskedForTheRewrite_IsOneFaultWithoutTheValue()
    {
        _scene.Describer.RewriteException = new InvalidOperationException($"cannot write {Secrets.ConnectionWithWord}");

        var fault = await Should.ThrowAsync<DescriberFaultException>(() => _scene.RunAsync());

        fault.InnerException.ShouldBeNull();
        fault.Diagnostic.Descriptor.Id.ShouldBe("SQLSRC200");
        fault.Diagnostic.Arguments.ToArray().ShouldBe(["System.InvalidOperationException", "cannot write ***"]);
        _scene.Error.ToString().ShouldBeEmpty();
        _scene.Describer.Opened.ShouldBeEmpty();
    }

    // The list of the parts is the describer's too, and it is read under the same guard.
    [Fact]
    public async Task Run_DescriberWhoseSecretsThrowWhenTheyAreRead_IsOneFaultWithoutTheValue()
    {
        _scene.Describer.Secrets = new ThrowsWhenRead($"cannot list {Secrets.ConnectionWithWord}");

        var fault = await Should.ThrowAsync<DescriberFaultException>(() => _scene.RunAsync());

        fault.InnerException.ShouldBeNull();
        fault.Diagnostic.Descriptor.Id.ShouldBe("SQLSRC200");
        fault.Diagnostic.Arguments.ToArray().ShouldBe(["System.InvalidOperationException", "cannot list ***"]);
        _scene.Error.ToString().ShouldBeEmpty();
        _scene.Describer.Opened.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1")]
    public async Task Run_DescriberThatThrows_IsAFaultWithoutTheConnection(string? debug)
    {
        if (debug is not null)
        {
            _scene.Environment[Cli.DebugVariable] = debug;
        }

        _scene.Describer.Exceptions["GetUser"] = new ArgumentException(
            $"Keyword not supported in '{Secrets.ConnectionWithWord}'"
        );

        var fault = await Should.ThrowAsync<DescriberFaultException>(() => _scene.RunAsync());

        fault.InnerException.ShouldBeNull();
        fault.Message.ShouldNotContain(Secrets.Word);
        fault.Diagnostic.Descriptor.Id.ShouldBe("SQLSRC200");
        fault.Diagnostic.Arguments.ToArray().ShouldBe(["System.ArgumentException", "Keyword not supported in '***'"]);
        (fault.Diagnostic.Lines.Count > 0).ShouldBe(debug is not null);
        fault.Diagnostic.Lines.ShouldAllBe(line => line.Label == "trace" && !line.Text.Contains(Secrets.Word));
        // The session is closed all the same.
        _scene.Describer.Closed.ShouldBe(1);
    }

    // The settings of the server are a describer's text: the event that names them is built inside the guard, so that
    // a setting without a name is the describer's fault and not a bare exception.
    [Fact]
    public async Task Run_ServerWithASettingThatHasNoName_IsOneSqlsrc200AndClosesTheSession()
    {
        _scene.Describer.Server = _scene.Describer.Server with
        {
            Settings = new EquatableArray<ServerSetting>([new ServerSetting(null!, "x")]),
        };

        var fault = await Should.ThrowAsync<DescriberFaultException>(() => _scene.RunAsync());

        fault.Diagnostic.Descriptor.Id.ShouldBe("SQLSRC200");
        fault.Diagnostic.Arguments[0].ShouldBe("System.NullReferenceException");
        _scene.Describer.Closed.ShouldBe(1);
    }

    // A value that spans lines is in the stack trace across the lines it is cut into.
    [Fact]
    public async Task Run_DescriberThatThrowsWithAConnectionOfSeveralLines_IsAFaultWithoutIt()
    {
        const string Spread = "Host=db;\r\nPassword=s3cret";
        _scene.Environment[Cli.DebugVariable] = "1";
        _scene.Describer.Exceptions["GetUser"] = new ArgumentException(
            $"Bad '{Spread}'",
            new IOException($"inner {Spread}")
        );

        var fault = await Should.ThrowAsync<DescriberFaultException>(() =>
            _scene.RunAsync(given: ["postgres=" + Spread, "billing=" + Spread])
        );

        fault.Diagnostic.Arguments.ToArray().ShouldBe(["System.ArgumentException", "Bad '***'"]);
        fault.Diagnostic.Lines.Count.ShouldBeGreaterThan(0);
        fault.Diagnostic.Lines.ShouldAllBe(line => !line.Text.Contains(Secrets.Word) && !line.Text.Contains("Host=db"));
        fault.Diagnostic.Lines.ShouldContain(line => line.Text.Contains("inner ***"));
    }

    [Fact]
    public async Task Run_DescriberThatThrowsOnOpen_IsAFaultWithoutTheConnection()
    {
        _scene.Describer.OpenException = new InvalidOperationException($"cannot parse {Secrets.ConnectionWithWord}");

        var fault = await Should.ThrowAsync<DescriberFaultException>(() => _scene.RunAsync());

        fault.Diagnostic.Arguments.ToArray().ShouldBe(["System.InvalidOperationException", "cannot parse ***"]);
    }

    // The describer of the tests throws from a task.  One that throws before it has a task to return, a method that
    // is not async for one, is the same fault, and its text is held to the same.
    [Fact]
    public async Task Run_DescriberThatThrowsOnOpenBeforeItReturnsATask_IsAFaultWithoutTheConnection()
    {
        var fault = await Should.ThrowAsync<DescriberFaultException>(() =>
            _scene.RunAsync(describer: new ThrowsBeforeItReturns())
        );

        fault.InnerException.ShouldBeNull();
        fault.Diagnostic.Arguments.ToArray().ShouldBe(["System.InvalidOperationException", "cannot parse ***"]);
    }

    [Fact]
    public async Task Run_DescriberThatReturnsNothing_IsAFault()
    {
        _scene.Describer.OpensNothing = true;

        var fault = await Should.ThrowAsync<DescriberFaultException>(() => _scene.RunAsync());

        fault
            .Diagnostic.Arguments.ToArray()
            .ShouldBe(["System.InvalidOperationException", "A describer returned nothing."]);
    }

    private async Task ShouldBeTheFaultOfNothingAsync()
    {
        var fault = await Should.ThrowAsync<DescriberFaultException>(() => _scene.RunAsync());

        fault
            .Diagnostic.Arguments.ToArray()
            .ShouldBe(["System.InvalidOperationException", "A describer returned nothing."]);
    }

    // The list of the parts is a return like any other.
    [Fact]
    public async Task Run_DescriberThatGivesNoSecrets_IsAFault()
    {
        _scene.Describer.Secrets = null!;

        await ShouldBeTheFaultOfNothingAsync();
    }

    [Fact]
    public async Task Run_DescriberThatFailsWithNoFailure_IsAFault()
    {
        _scene.Describer.OpenFailsWithNothing = true;

        await ShouldBeTheFaultOfNothingAsync();
    }

    // An outcome that says it succeeded and holds no value is nothing too: the run reads the value, not the claim.
    [Fact]
    public async Task Run_DescriberThatOpensWithNoSession_IsAFault()
    {
        _scene.Describer.OpensWithNoSession = true;

        await ShouldBeTheFaultOfNothingAsync();
    }

    [Fact]
    public async Task Run_SessionThatReturnsNothingForAQuery_IsAFault()
    {
        _scene.Describer.DescribeResult = static () => null;

        await ShouldBeTheFaultOfNothingAsync();
    }

    [Fact]
    public async Task Run_SessionThatDescribesWithNoDescription_IsAFault()
    {
        _scene.Describer.DescribeResult = static () => Outcome<QueryDescription, DescribeFailure>.Of(null!);

        await ShouldBeTheFaultOfNothingAsync();
    }

    [Fact]
    public async Task Run_SessionThatFailsAQueryWithNoFailure_IsAFault()
    {
        _scene.Describer.DescribeResult = static () => Outcome<QueryDescription, DescribeFailure>.Failed(null!);

        await ShouldBeTheFaultOfNothingAsync();
    }

    [Fact]
    public async Task Run_SessionThatThrowsWhenItIsClosed_IsAFaultWithoutTheConnection()
    {
        _scene.Describer.CloseException = new IOException($"lost {Secrets.ConnectionWithWord}");

        var fault = await Should.ThrowAsync<DescriberFaultException>(() => _scene.RunAsync());

        fault.Diagnostic.Arguments.ToArray().ShouldBe(["System.IO.IOException", "lost ***"]);
    }

    [Fact]
    public async Task Run_DescriptionWithAParameterTheQueryLacks_IsAFault()
    {
        _scene.Describer.Descriptions["GetUser"] = FakeDescriber.NoRows with
        {
            Parameters = new EquatableArray<DescribedParameter>([new DescribedParameter("nope", null, null)]),
        };

        var fault = await Should.ThrowAsync<DescriberFaultException>(() => _scene.RunAsync());

        fault
            .Diagnostic.Arguments[1]
            .ShouldBe("The describer gave the parameter 'nope', which the query 'GetUser' does not have.");
    }

    // A description that holds the value would be written into a file that is committed.
    private async Task<DescriberFaultException> RunExpectingTheValueInTheDescriptionAsync(string secret)
    {
        var fault = await Should.ThrowAsync<DescriberFaultException>(() =>
            _scene.RunAsync(given: ["postgres=" + secret, "billing=" + secret])
        );

        fault.InnerException.ShouldBeNull();
        fault.Message.ShouldNotContain(Secrets.Word);
        fault.Diagnostic.Descriptor.Id.ShouldBe("SQLSRC200");
        fault
            .Diagnostic.Arguments.ToArray()
            .ShouldBe([
                "System.InvalidOperationException",
                "The describer put the connection's value in its description of the query 'GetUser'.",
            ]);
        fault.Diagnostic.Arguments.ShouldAllBe(argument => !argument.Contains(Secrets.Word));
        _scene.Files[0].Queries[0].State.ShouldNotBe(QueryState.Described);
        _scene.Files[0].Queries[0].Entry.ShouldBeNull();
        _scene.Describer.Closed.ShouldBe(1);
        return fault;
    }

    [Fact]
    public async Task Run_DescriptionWithTheValueInTheServersVersion_IsAFault()
    {
        _scene.Describer.Server = FakeDescriber.ServerWith($"16.4 at {Secrets.ConnectionWithWord}");

        _ = await RunExpectingTheValueInTheDescriptionAsync(Secrets.ConnectionWithWord);
    }

    [Fact]
    public async Task Run_DescriptionWithTheValueInAColumnsName_IsAFault()
    {
        _scene.Describer.Descriptions["GetUser"] = FakeDescriber.NoRows with
        {
            ResultKind = SidecarResultKind.Rows,
            Columns = new EquatableArray<SidecarColumn>([
                new SidecarColumn(
                    0,
                    $"id of {Secrets.ConnectionWithWord}",
                    new OtherEngineType("int4"),
                    null,
                    null,
                    null,
                    null,
                    null
                ),
            ]),
        };

        _ = await RunExpectingTheValueInTheDescriptionAsync(Secrets.ConnectionWithWord);
    }

    // A description is searched for the whole value alone: a password that equals a name of the catalog is no fault.
    [Fact]
    public async Task Run_DescriptionWithAPasswordTheDescriberGaveAsAColumnsName_IsDescribed()
    {
        _scene.Describer.Secrets = ["admin"];
        _scene.Describer.Descriptions["GetUser"] = FakeDescriber.NoRows with
        {
            ResultKind = SidecarResultKind.Rows,
            Columns = new EquatableArray<SidecarColumn>([
                new SidecarColumn(0, "admin", new OtherEngineType("int4"), null, null, null, null, null),
            ]),
        };

        _ = await _scene.RunAsync(given: ["postgres=Host=db;Password=admin", "billing=Host=db;Password=admin"]);

        _scene.Error.ToString().ShouldBeEmpty();
        _scene.Files[0].Queries[0].State.ShouldBe(QueryState.Described);
        _scene.Files[0].Queries[0].Entry.ShouldNotBeNull().Columns.ShouldNotBeNull()[0].Name.ShouldBe("admin");
    }

    // The writer escapes a quote and a backslash, so the value is in the file as it escapes it.
    [Fact]
    public async Task Run_DescriptionWithAValueOfAQuoteAndABackslashInTheServersVersion_IsAFault()
    {
        const string Awkward = "Pwd=\"s3cret\\\"\r\n";
        _scene.Describer.Server = FakeDescriber.ServerWith($"16.4 {Awkward}");

        _ = await RunExpectingTheValueInTheDescriptionAsync(Awkward);
    }

    [Fact]
    public async Task Run_DescriptionWithoutTheValue_IsDescribedAsBefore()
    {
        _scene.Describer.Server = FakeDescriber.ServerWith();
        _scene.Describer.Descriptions["GetUser"] = FakeDescriber.NoRows with
        {
            ResultKind = SidecarResultKind.Rows,
            Columns = new EquatableArray<SidecarColumn>([
                new SidecarColumn(0, "id", new OtherEngineType("int4"), null, null, null, null, null),
            ]),
        };

        var files = await _scene.RunAsync();

        DatabaseRunsScene.States(files).ShouldBe([QueryState.Described, QueryState.Described]);
        _scene.Error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_DescriberThatThrowsAndTheSessionThrowsWhenItIsClosed_IsAFaultOfTheFirstException()
    {
        _scene.Describer.Exceptions["GetUser"] = new ArgumentException($"broken {Secrets.ConnectionWithWord}");
        _scene.Describer.CloseException = new IOException($"lost {Secrets.ConnectionWithWord}");

        var fault = await Should.ThrowAsync<DescriberFaultException>(() => _scene.RunAsync());

        fault.Diagnostic.Arguments.ToArray().ShouldBe(["System.ArgumentException", "broken ***"]);
        _scene.Describer.Closed.ShouldBe(1);
    }

    // A describer whose OpenAsync is not async.
    private sealed class ThrowsBeforeItReturns : IQueryDescriber
    {
        public SqlDialect Dialect => SqlDialect.PostgreSql;

        public IReadOnlyList<string> SecretsOf(string connection, Func<string, string?> getEnvironmentVariable) => [];

        public string? RewriteOf(string connection) => null;

        public Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
            OpenRequest request,
            IRunLog log,
            CancellationToken cancellationToken
        ) => throw new InvalidOperationException($"cannot parse {request.Connection}");
    }

    // A list of a describer's parts that throws whenever it is read.
    private sealed class ThrowsWhenRead(string message) : IReadOnlyList<string>
    {
        public int Count => throw new InvalidOperationException(message);

        public string this[int index] => throw new InvalidOperationException(message);

        public IEnumerator<string> GetEnumerator() => throw new InvalidOperationException(message);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
