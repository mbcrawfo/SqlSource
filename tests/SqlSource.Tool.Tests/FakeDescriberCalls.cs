using System.Collections.Generic;

namespace SqlSource.Tool.Tests;

// What FakeDescriber passes through the exchange, one file as a real describer's are: plain records that a capture
// can hold.  The two calls are "open" and "describe".  FakeCalls maps between these and the seam's types, and
// Describing/Capture/CallRecordTests holds each to the rule.

// The request of "open".  It holds nothing: nothing of a connection may reach a capture.  Its one member is static,
// so it is written as {}.
internal sealed record FakeOpenRequest
{
    public static FakeOpenRequest Instance { get; } = new();
}

// The server of a session that was opened, or why it was not.
internal sealed record FakeOpenAnswer(FakeServer? Server, FakeFailure? Error);

internal sealed record FakeServer(
    string Version,
    string? Driver,
    string? DriverVersion,
    IReadOnlyList<FakeSetting> Settings
);

internal sealed record FakeSetting(string Name, string Value);

// Everything that decides the answer: the query's name, the SQL as sent and the parameter list.
internal sealed record FakeDescribeRequest(string Name, string Sql, IReadOnlyList<FakeListedParameter> Parameters);

internal sealed record FakeListedParameter(string Name, string? Type, bool? Nullable, bool IsDeclared);

// The description of a query, or why it could not be described.
internal sealed record FakeDescribeAnswer(FakeDescription? Description, FakeFailure? Error);

internal sealed record FakeDescription(
    string ResultKind,
    IReadOnlyList<FakeParameter> Parameters,
    IReadOnlyList<FakeColumn>? Columns,
    FakeTable? MatchesTable,
    string? Plan,
    string? TableMatch
);

internal sealed record FakeParameter(string Name, FakeType? Type, string? TypeSource);

internal sealed record FakeColumn(
    int Ordinal,
    string Name,
    FakeType Type,
    bool? Nullable,
    string? NullableSource,
    FakeOrigin? Origin,
    bool? Identity,
    bool? Computed
);

internal sealed record FakeOrigin(string? Schema, string? Table, string? Column);

internal sealed record FakeTable(string? Schema, string? Table);

// A type of PostgreSQL, or with no kind a type of an engine the sidecar's model does not know, of which only the
// name is kept.
internal sealed record FakeType(
    string Name,
    string? Kind,
    string? Schema,
    string? InternalName,
    int? Length,
    int? Precision,
    int? Scale,
    FakeType? Element,
    FakeType? Base,
    IReadOnlyList<string>? Labels,
    FakeType? Subtype
);

// A failure, with the parts of its descriptor: a descriptor is no plain data.
internal sealed record FakeFailure(
    string Id,
    string Title,
    string MessageFormat,
    string Category,
    string Description,
    string HelpLink,
    IReadOnlyList<string> Arguments,
    string? Step,
    IReadOnlyList<string> ServerLines,
    string? Help
);
