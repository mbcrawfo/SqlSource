using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.Tool.Describing.Postgres;

// The calls of a describe: each is asked through the exchange by DescriberSession.AskAsync and written to the log where
// it happens, with how long it took by the run's clock.  Under a replay the event is written all the same.
internal sealed partial class PostgresSession
{
    private Task<PostgresPrepareAnswer> PrepareAsync(
        string query,
        PostgresPrepareRequest request,
        CancellationToken cancellationToken
    ) =>
        AskAsync(
            PostgresCallNames.Prepare,
            request,
            _live.PrepareAsync,
            (asked, answer, durationMs) => PostgresEvents.Prepare(_log, query, asked, answer, durationMs),
            cancellationToken
        );

    private Task<PostgresColumnsAnswer> ColumnsAsync(
        string query,
        PostgresColumnsRequest request,
        CancellationToken cancellationToken
    ) =>
        AskAsync(
            PostgresCallNames.Columns,
            request,
            _live.ColumnsAsync,
            (asked, answer, durationMs) => PostgresEvents.Columns(_log, query, asked, answer, durationMs),
            cancellationToken
        );

    private Task<PostgresTablesAnswer> TablesAsync(
        string query,
        PostgresTablesRequest request,
        CancellationToken cancellationToken
    ) =>
        AskAsync(
            PostgresCallNames.Tables,
            request,
            _live.TablesAsync,
            (asked, answer, durationMs) => PostgresEvents.Tables(_log, query, asked, answer, durationMs),
            cancellationToken
        );

    private Task<PostgresTypesAnswer> TypesAsync(
        string query,
        PostgresTypesRequest request,
        CancellationToken cancellationToken
    ) =>
        AskAsync(
            PostgresCallNames.Types,
            request,
            _live.TypesAsync,
            (asked, answer, durationMs) => PostgresEvents.Types(_log, query, asked, answer, durationMs),
            cancellationToken
        );

    private Task<PostgresSpellingsAnswer> SpellingsAsync(
        string query,
        PostgresSpellingsRequest request,
        CancellationToken cancellationToken
    ) =>
        AskAsync(
            PostgresCallNames.Spellings,
            request,
            _live.SpellingsAsync,
            (asked, answer, durationMs) => PostgresEvents.Spellings(_log, query, asked, answer, durationMs),
            cancellationToken
        );

    private Task<PostgresExplainAnswer> ExplainAsync(
        string query,
        PostgresExplainRequest request,
        CancellationToken cancellationToken
    ) =>
        AskAsync(
            PostgresCallNames.Explain,
            request,
            _live.ExplainAsync,
            (asked, answer, durationMs) => PostgresEvents.Explain(_log, query, asked, answer, durationMs),
            cancellationToken
        );

    private Task<PostgresPartitionsAnswer> PartitionsAsync(
        string query,
        PostgresPartitionsRequest request,
        CancellationToken cancellationToken
    ) =>
        AskAsync(
            PostgresCallNames.Partitions,
            request,
            _live.PartitionsAsync,
            (asked, answer, durationMs) => PostgresEvents.Partitions(_log, query, asked, answer, durationMs),
            cancellationToken
        );

    private Task<PostgresTablesAnswer> RelationsAsync(
        string query,
        PostgresRelationsRequest request,
        CancellationToken cancellationToken
    ) =>
        AskAsync(
            PostgresCallNames.Relations,
            request,
            _live.RelationsAsync,
            (asked, answer, durationMs) => PostgresEvents.Relations(_log, query, asked, answer, durationMs),
            cancellationToken
        );

    private Task<PostgresOverloadsAnswer> OverloadsAsync(
        string query,
        PostgresOverloadsRequest request,
        CancellationToken cancellationToken
    ) =>
        AskAsync(
            PostgresCallNames.Overloads,
            request,
            _live.OverloadsAsync,
            (asked, answer, durationMs) => PostgresEvents.Overloads(_log, query, asked, answer, durationMs),
            cancellationToken
        );

    // The error of the call is logged and never reported.
    private Task<PostgresDeallocateAnswer> DeallocateAsync(
        string query,
        PostgresDeallocateRequest request,
        CancellationToken cancellationToken
    ) =>
        AskAsync(
            PostgresCallNames.Deallocate,
            request,
            _live.DeallocateAsync,
            (asked, answer, durationMs) => PostgresEvents.Deallocate(_log, query, asked, answer, durationMs),
            cancellationToken
        );
}
