using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Tests;

// The maps of FakeDescriber between the seam's types and its plain records.  They run on the describer's side of
// the exchange, as a real describer's do: Plain gives what crosses, and Seam gives it back.
internal static class FakeCalls
{
    public static FakeServer Plain(ServerInfo server) =>
        new(
            server.Version,
            server.Driver,
            server.DriverVersion,
            [.. server.Settings.Select(static setting => new FakeSetting(setting.Name, setting.Value))]
        );

    public static FakeDescribeRequest Plain(DescribeRequest request) =>
        new(
            request.Name,
            request.Sql,
            [
                .. request.Parameters.Select(static parameter => new FakeListedParameter(
                    parameter.Name,
                    parameter.Type,
                    parameter.Nullable,
                    parameter.IsDeclared
                )),
            ]
        );

    public static FakeDescription Plain(QueryDescription description) =>
        new(
            description.ResultKind.ToString(),
            [
                .. description.Parameters.Select(static parameter => new FakeParameter(
                    parameter.Name,
                    parameter.Type is { } type ? PlainType(type) : null,
                    parameter.TypeSource
                )),
            ],
            description.Columns?.Select(PlainColumn).ToList(),
            description.MatchesTable is { } table ? new FakeTable(table.Schema, table.Table) : null,
            description.Plan,
            description.TableMatch
        );

    public static FakeFailure Plain(DescribeFailure failure) =>
        new(
            failure.Descriptor.Id,
            failure.Descriptor.Title.ToString(CultureInfo.InvariantCulture),
            failure.Descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture),
            failure.Descriptor.Category,
            failure.Descriptor.Description.ToString(CultureInfo.InvariantCulture),
            failure.Descriptor.HelpLinkUri,
            [.. failure.Arguments],
            failure.Step?.ToString(),
            [.. failure.ServerLines],
            failure.Help
        );

    public static ServerInfo Seam(FakeServer server) =>
        new(
            server.Version,
            server.Driver,
            server.DriverVersion,
            new EquatableArray<ServerSetting>([
                .. server.Settings.Select(static setting => new ServerSetting(setting.Name, setting.Value)),
            ])
        );

    public static QueryDescription Seam(FakeDescription description) =>
        new(
            Enum.Parse<SidecarResultKind>(description.ResultKind),
            new EquatableArray<DescribedParameter>([
                .. description.Parameters.Select(static parameter => new DescribedParameter(
                    parameter.Name,
                    parameter.Type is { } type ? SeamType(type) : null,
                    parameter.TypeSource
                )),
            ]),
            SeamColumns(description.Columns),
            description.MatchesTable is { } table ? new SidecarTable(table.Schema, table.Table) : null,
            description.Plan,
            description.TableMatch
        );

    // A failure of the describer of the tests is an enabled error, so neither is carried.
    public static DescribeFailure Seam(FakeFailure failure) =>
        new(
            new DiagnosticDescriptor(
                failure.Id,
                failure.Title,
                failure.MessageFormat,
                failure.Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: failure.Description,
                helpLinkUri: failure.HelpLink
            ),
            new EquatableArray<string>([.. failure.Arguments]),
            failure.Step is { } step ? Enum.Parse<DescribeStep>(step) : null,
            new EquatableArray<string>([.. failure.ServerLines]),
            failure.Help
        );

    private static FakeColumn PlainColumn(SidecarColumn column) =>
        new(
            column.Ordinal,
            column.Name,
            PlainType(column.Type),
            column.Nullable,
            column.NullableSource,
            column.Origin is { } origin ? new FakeOrigin(origin.Schema, origin.Table, origin.Column) : null,
            column.Identity,
            column.Computed
        );

    private static EquatableArray<SidecarColumn>? SeamColumns(IReadOnlyList<FakeColumn>? columns)
    {
        if (columns is null)
        {
            return null;
        }

        return new EquatableArray<SidecarColumn>([
            .. columns.Select(static column => new SidecarColumn(
                column.Ordinal,
                column.Name,
                SeamType(column.Type),
                column.Nullable,
                column.NullableSource,
                column.Origin is { } origin ? new SidecarOrigin(origin.Schema, origin.Table, origin.Column) : null,
                column.Identity,
                column.Computed
            )),
        ]);
    }

    private static FakeType PlainType(SidecarType type) =>
        type switch
        {
            PostgresType postgres => PlainPostgres(postgres),
            OtherEngineType other => new FakeType(
                other.Name,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null
            ),
            _ => throw new NotSupportedException(
                $"The describer of the tests does not carry a {type.GetType().Name} through the exchange."
            ),
        };

    private static FakeType PlainPostgres(PostgresType type) =>
        new(
            type.Name,
            type.Kind,
            type.Schema,
            type.InternalName,
            type.Length,
            type.Precision,
            type.Scale,
            type.Element is { } element ? PlainPostgres(element) : null,
            type.Base is { } baseType ? PlainPostgres(baseType) : null,
            type.Labels?.ToList(),
            type.Subtype is { } subtype ? PlainPostgres(subtype) : null
        );

    private static SidecarType SeamType(FakeType type) =>
        type.Kind is null ? new OtherEngineType(type.Name) : SeamPostgres(type);

    private static PostgresType SeamPostgres(FakeType type) =>
        new(
            type.Name,
            type.Kind ?? throw new InvalidOperationException($"The type '{type.Name}' has no kind."),
            type.Schema ?? throw new InvalidOperationException($"The type '{type.Name}' has no schema."),
            type.InternalName ?? throw new InvalidOperationException($"The type '{type.Name}' has no internal name."),
            type.Length,
            type.Precision,
            type.Scale,
            type.Element is { } element ? SeamPostgres(element) : null,
            type.Base is { } baseType ? SeamPostgres(baseType) : null,
            SeamLabels(type.Labels),
            type.Subtype is { } subtype ? SeamPostgres(subtype) : null
        );

    private static EquatableArray<string>? SeamLabels(IReadOnlyList<string>? labels)
    {
        if (labels is null)
        {
            return null;
        }

        return new EquatableArray<string>([.. labels]);
    }
}
