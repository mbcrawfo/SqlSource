using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tests.SqlServer.Describer;

// SqlClient's own events in the run's log, while a session lives.  The tests of the collection run one after another,
// so the SqlClient activity a listener sees is the test's own.  The log here does not redact, so a string that named
// the host, the login, the database or the password would be found as it is.
[Collection(UsesSqlServer.Name)]
public sealed class DriverLogTests(SqlServerContainer sqlServer)
{
    private static readonly string[] BeginExecuteMembers =
    [
        "category",
        "level",
        "eventId",
        "objectId",
        "commandText",
        "exception",
    ];

    private static readonly string[] EndExecuteMembers =
    [
        "category",
        "level",
        "eventId",
        "objectId",
        "compositestate",
        "sqlExceptionNumber",
        "exception",
    ];

    [Fact]
    public async Task DescribeAsync_WithLog_WritesDriverEvents()
    {
        var log = new ConcurrentLog();
        var exchange = new EnabledExchange();

        await using (var session = await Sessions.OpenSessionAsync(sqlServer.ConnectionString, exchange, log))
        {
            var token = TestContext.Current.CancellationToken;
            _ = await session.DescribeAsync(Request("SELECT id, name FROM dbo.users WHERE id = @id"), token);
            _ = await session.DescribeAsync(Request("SELECT nope FROM dbo.sqlsrc_missing"), token);
        }

        exchange.Enabled.ShouldNotBeEmpty();
        exchange.Enabled.ShouldAllBe(enabled => enabled);
        var events = log.Events.Where(static e => e.Name == SqlClientRunListener.EventName).ToList();
        var begins = events.Where(static e => Member(e, "category") == "BeginExecute").ToList();
        var ends = events.Where(static e => Member(e, "category") == "EndExecute").ToList();
        begins.ShouldNotBeEmpty();
        ends.Count.ShouldBe(begins.Count);
        events.Count.ShouldBe(begins.Count + ends.Count);
        begins.ShouldAllBe(e => e.Members.Select(static m => m.Name).SequenceEqual(BeginExecuteMembers));
        ends.ShouldAllBe(e => e.Members.Select(static m => m.Name).SequenceEqual(EndExecuteMembers));
        // A member that SqlClient's payload does not name is written as null, so each listed one must have a value.
        begins.ShouldAllBe(e => Number(e, "objectId") != null && Member(e, "commandText") != null);
        ends.ShouldAllBe(e =>
            Number(e, "objectId") != null
            && Number(e, "compositestate") != null
            && Number(e, "sqlExceptionNumber") != null
        );
        begins.Select(static e => Member(e, "commandText")).ShouldContain(SqlServerLive.DescribeUndeclaredParameters);
        ends.Select(static e => Number(e, "sqlExceptionNumber")).ShouldContain(208L);
        ShouldNameNothingOf(log, new SqlConnectionStringBuilder { ConnectionString = sqlServer.ConnectionString });
    }

    [Fact]
    public async Task OpenAsync_RefusedConnection_WritesNoSecret()
    {
        var connection = new SqlConnectionStringBuilder(
            "Data Source=127.0.0.1,1;Connect Timeout=3;Encrypt=False;User ID=sqlsrc_login;Password=" + Secrets.Marker
        );

        await ShouldNotOpenAsync(connection);
    }

    [Fact]
    public async Task OpenAsync_RejectedLogin_WritesNoSecret()
    {
        var connection = new SqlConnectionStringBuilder(sqlServer.ConnectionString) { Password = Secrets.Marker };

        await ShouldNotOpenAsync(connection);
    }

    [Fact]
    public async Task DescribeAsync_WithoutLog_EnablesNoListener()
    {
        var exchange = new EnabledExchange();

        SqlClientRunListener.For(NullRunLog.Instance).ShouldBeNull();
        await using (var session = await Sessions.OpenSessionAsync(sqlServer.ConnectionString, exchange))
        {
            _ = await session.DescribeAsync(
                Request("SELECT id, name FROM dbo.users WHERE id = @id"),
                TestContext.Current.CancellationToken
            );
        }

        exchange.Enabled.ShouldNotBeEmpty();
        exchange.Enabled.ShouldAllBe(enabled => !enabled);
    }

    private static DescribeRequest Request(string sql) => DescribeRequests.Of(SqlDialectRules.SqlServer, sql);

    // No session, no event that names the connection, and no listener left: SqlClient's source is not enabled, and a
    // command of another connection afterwards reaches the log of the failed session in no event.
    private async Task ShouldNotOpenAsync(SqlConnectionStringBuilder connection)
    {
        var log = new ConcurrentLog();

        var opened = await Sessions.OpenAsync(connection.ConnectionString, log: log);

        opened.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC224");
        ShouldNameNothingOf(log, connection);
        SqlClientSource().IsEnabled().ShouldBeFalse();
        _ = await sqlServer.ScalarAsync("SELECT 1");
        log.Events.ShouldNotContain(static e => e.Name == SqlClientRunListener.EventName);
    }

    // Every string of every driver event, member names included, holds neither the host, the login as a word, the
    // database nor the password.  The describer's own events may name the database, as a column's origin does.
    private static void ShouldNameNothingOf(ConcurrentLog log, SqlConnectionStringBuilder connection)
    {
        var texts = log
            .Events.Where(static e => e.Name == SqlClientRunListener.EventName)
            .SelectMany(static e => TextsOf(e))
            .ToList();
        var host = connection.DataSource.Split(',')[0];
        string[] words = [host, connection.InitialCatalog, connection.Password];
        foreach (var word in words.Where(static word => word.Length > 0))
        {
            texts.ShouldAllBe(text => !text.Contains(word, StringComparison.Ordinal), word);
        }

        var login = new Regex(
            @"\b" + Regex.Escape(connection.UserID) + @"\b",
            RegexOptions.None,
            TimeSpan.FromSeconds(1)
        );
        texts.ShouldAllBe(text => !login.IsMatch(text), connection.UserID);
    }

    private static IEnumerable<string> TextsOf((string Name, LogMember[] Members) logged) =>
        [logged.Name, .. logged.Members.SelectMany(static m => (IEnumerable<string>)[m.Name, .. TextsOf(m.Value)])];

    private static IEnumerable<string> TextsOf(LogValue value) =>
        value switch
        {
            LogValue.Text text => [text.Value],
            LogValue.Items items => items.Values.SelectMany(TextsOf),
            LogValue.Members map => map.Values.SelectMany(static m =>
                (IEnumerable<string>)[m.Name, .. TextsOf(m.Value)]
            ),
            _ => [],
        };

    private static string? Member((string Name, LogMember[] Members) logged, string name) =>
        (logged.Members.Single(m => m.Name == name).Value as LogValue.Text)?.Value;

    private static long? Number((string Name, LogMember[] Members) logged, string name) =>
        (logged.Members.Single(m => m.Name == name).Value as LogValue.Number)?.Value;

    private static EventSource SqlClientSource() =>
        EventSource.GetSources().Single(static source => source.Name == SqlClientRunListener.SourceName);

    // A log that keeps what it is given from any thread, as SqlClient writes from the thread that runs its command.
    private sealed class ConcurrentLog : IRunLog
    {
        private readonly Lock _gate = new();
        private readonly List<(string Name, LogMember[] Members)> _events = [];

        public bool Writes => true;

        public IReadOnlyList<(string Name, LogMember[] Members)> Events
        {
            get
            {
                lock (_gate)
                {
                    return [.. _events];
                }
            }
        }

        public void Write(string name, params LogMember[] members)
        {
            lock (_gate)
            {
                _events.Add((name, members));
            }
        }
    }

    // Asks the server as the live exchange does, and keeps whether SqlClient's source was enabled at each call.
    private sealed class EnabledExchange : IDescribeExchange
    {
        public List<bool> Enabled { get; } = [];

        public Task<TAnswer> AskAsync<TRequest, TAnswer>(
            string method,
            TRequest request,
            Func<TRequest, CancellationToken, Task<TAnswer>> live,
            CancellationToken cancellationToken
        )
        {
            Enabled.Add(SqlClientSource().IsEnabled());
            return live(request, cancellationToken);
        }
    }
}
