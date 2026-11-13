using System;
using System.Collections.Generic;
using System.Data.Common;
using Npgsql;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios;

// Queries/Repository.sql, by each of its four repositories: GetNote and GetNoteBoth with a note and without, and
// TouchNote on a copy of the seed.  A repository's method takes its connection from its own source and no
// transaction, so the connection a scenario gives is used by the borrowed one alone, open and closed, and the others'
// scenarios leave it closed.  ListNotes is a stream, which has no synchronous method, and the Sync copies have no
// asynchronous one.
internal static class RepositoryScenarios
{
    private static readonly Setup TouchesClosed = new(
        ConnectionStart.Closed,
        Writes: true,
        Tables: ["SELECT id, account_id, body, ref FROM notes ORDER BY id"]
    );

    private static readonly (Type Host, Func<DbConnection, DbDataSource, object> Instance)[] OwnSources =
    [
        (typeof(DataSourceRepository), static (_, source) => new DataSourceRepository((NpgsqlDataSource)source)),
        (
            typeof(ConnectionStringRepository),
            static (_, source) => new ConnectionStringRepository(source.ConnectionString)
        ),
        (typeof(FactoryRepository), static (_, source) => new FactoryRepository((NpgsqlDataSource)source)),
    ];

    public static IEnumerable<Scenario> All
    {
        get
        {
            foreach (var (host, instance) in OwnSources)
            {
                foreach (var scenario in ScenariosOf(host, instance, Closed, TouchesClosed, ""))
                {
                    yield return scenario;
                }
            }

            static object Borrowed(DbConnection connection, DbDataSource _) => new BorrowedRepository(connection);
            foreach (
                var scenario in ScenariosOf(
                    typeof(BorrowedRepository),
                    Borrowed,
                    Open,
                    TouchesClosed with
                    {
                        Connection = ConnectionStart.Open,
                    },
                    ", open"
                )
            )
            {
                yield return scenario;
            }

            foreach (
                var scenario in ScenariosOf(typeof(BorrowedRepository), Borrowed, Closed, TouchesClosed, ", closed")
            )
            {
                yield return scenario;
            }
        }
    }

    private static IEnumerable<Scenario> ScenariosOf(
        Type host,
        Func<DbConnection, DbDataSource, object> instance,
        Setup reads,
        Setup touches,
        string suffix
    ) =>
        [
            Of(host, "GetNote", ScenarioTag.Ordinary, "a note" + suffix, reads, new GetNoteArgs(2)) with
            {
                Instance = instance,
            },
            Of(host, "GetNote", ScenarioTag.NoRow, "no note" + suffix, reads, new GetNoteArgs(99)) with
            {
                Instance = instance,
            },
            Of(host, "GetNoteBoth", ScenarioTag.Ordinary, "a note" + suffix, reads, new GetNoteBothArgs(3)) with
            {
                Instance = instance,
            },
            Of(host, "GetNoteBoth", ScenarioTag.NoRow, "no note" + suffix, reads, new GetNoteBothArgs(99)) with
            {
                Instance = instance,
            },
            Of(host, "TouchNote", ScenarioTag.Ordinary, "a note" + suffix, touches, new TouchNoteArgs(1)) with
            {
                Instance = instance,
            },
            Of(host, "TouchNote", ScenarioTag.Other, "no note" + suffix, touches, new TouchNoteArgs(99)) with
            {
                Instance = instance,
            },
        ];
}
