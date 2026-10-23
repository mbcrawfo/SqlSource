using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres.Columns;
using SqlSource.Tool.Describing.Postgres.Types;
using SqlSource.Tool.Describing.Postgres.Walk;

namespace SqlSource.Tool.Describing.Postgres;

// What a describe makes of the answers, and reads no state of the session.
internal sealed partial class PostgresSession
{
    private static ServerInfo ServerOf(PostgresOpenAnswer answer) =>
        new(
            answer.ServerVersion
                ?? throw new InvalidOperationException("The answer to 'open' holds no version and no error."),
            DriverName,
            answer.DriverVersion,
            new EquatableArray<ServerSetting>([
                .. (answer.Settings ?? []).Select(static setting => new ServerSetting(setting.Name, setting.Value)),
            ])
        );

    // The types the server resolved for the parameters of the query's list, which are the first of its types, and
    // how many arguments an EXECUTE of the statement takes: a type for each parameter the server resolved, the
    // query's own and any "$n" the server read where the tool's lexer did not.  The describe of "columns" takes any
    // number, and EXPLAIN EXECUTE refuses any but this one.  The SQL holds no "$n" of its own, so the server resolves
    // no more than the list; a text that the tool's lexer and the server read differently could still make it list
    // more, and the extra are left out of the parameters.
    private static (IReadOnlyList<long> Oids, int Arguments) ParameterOids(
        DescribeRequest request,
        PostgresPrepareAnswer prepared
    )
    {
        var count = request.Parameters.Count;
        var resolved =
            prepared.ParameterTypes
            ?? throw new InvalidOperationException("The answer to 'prepare' holds no types and no error.");
        return resolved.Count < count
            ? throw new InvalidOperationException(
                $"The server resolved {resolved.Count} types for the {count} parameters of '{request.Name}'."
            )
            : ([.. resolved.Take(count)], resolved.Count);
    }

    // The rows of "types" once a row was found for every type that was asked for: the one form in which the session
    // hands them on, so that nothing reads them before CheckTypes.
    private sealed record CheckedTypes(IReadOnlyDictionary<long, PostgresTypeRow> Rows);

    // The rows, or the first type that was asked for and has none, in the order of the request: the parameters, the
    // columns, the attributes of their origins.  A type that another session dropped since "tables" has none, and
    // PostgresTypes.Build and the classes of a column would throw for it.  A type that the closure reaches by itself,
    // the base of a domain or the element of an array, comes from the query of the row that reaches it, so it is
    // never missing alone.
    private static Outcome<CheckedTypes, string> CheckTypes(
        DescribeRequest request,
        IReadOnlyList<long> parameterOids,
        IReadOnlyList<PostgresColumnRow> columns,
        ColumnOrigin?[] origins,
        IReadOnlyDictionary<long, PostgresTypeRow> rows
    )
    {
        for (var index = 0; index < parameterOids.Count; index++)
        {
            if (!rows.ContainsKey(parameterOids[index]))
            {
                var written = PostgresErrors.Written(request.Parameters[index]);
                return Outcome<CheckedTypes, string>.Failed($"the type of the parameter '{written}' no longer exists");
            }
        }

        var column = columns.FirstOrDefault(column => !rows.ContainsKey(column.TypeOid));
        if (column is not null)
        {
            return Outcome<CheckedTypes, string>.Failed($"the type of the column '{column.Name}' no longer exists");
        }

        for (var index = 0; index < origins.Length; index++)
        {
            if (origins[index] is { } origin && !rows.ContainsKey(origin.Attribute.TypeOid))
            {
                return Outcome<CheckedTypes, string>.Failed(
                    $"the type of the column '{columns[index].Name}' no longer exists in its table"
                );
            }
        }

        return Outcome<CheckedTypes, string>.Of(new CheckedTypes(rows));
    }

    // Each parameter of the query's list gets the type the server resolved for it.
    private static Outcome<IReadOnlyList<DescribedParameter>, DescribeFailure> TypeParameters(
        DescribeRequest request,
        IReadOnlyList<long> oids,
        CheckedTypes types,
        TypeSpellings spellings
    )
    {
        var rows = types.Rows;
        var parameters = new List<DescribedParameter>(oids.Count);
        for (var index = 0; index < oids.Count; index++)
        {
            var parameter = request.Parameters[index];
            if (PostgresTypes.IsPseudo(oids[index], rows))
            {
                // The server accepts a pseudo-type, declared or inferred, and no value has one.
                return Outcome<IReadOnlyList<DescribedParameter>, DescribeFailure>.Failed(
                    PostgresErrors.PseudoType(request, parameter, rows[oids[index]].FormatType)
                );
            }

            // The protocol gives a parameter an OID and no modifier, so its own type has none: the base of a domain
            // has the domain's.  The name it binds by is on its own type alone, and null when no name can say the
            // type, which "check" refuses before it asks.
            parameters.Add(
                new DescribedParameter(
                    parameter.Name,
                    PostgresTypes.Build(oids[index], rows, TypeModifiers.NoModifier, spellings) with
                    {
                        BindName = PostgresBindNames.Of(oids[index], rows),
                    },
                    parameter.Type is null ? SidecarValues.TypeSource.Inferred : SidecarValues.TypeSource.Declared
                )
            );
        }

        return Outcome<IReadOnlyList<DescribedParameter>, DescribeFailure>.Of(parameters);
    }

    // A parameter as generated code will bind it: by the name its type binds by, as the sidecar will record it.
    private static PostgresCheckParameter CheckParameterOf(DescribedParameter parameter) =>
        new(
            parameter.Name,
            parameter.Type?.BindName
                ?? throw new InvalidOperationException(
                    $"The parameter '{parameter.Name}' was given no name to bind by."
                )
        );

    // The columns as they are recorded: the walk's when there was one, and else as they were built.
    private static IReadOnlyList<SidecarColumn> RecordedColumns(
        IReadOnlyList<SidecarColumn> built,
        WalkOutcome? walked
    ) => walked is null ? built : [.. walked.Columns.Select(static column => column.Column)];

    // A statement without rows has its parameters and nothing else: no columns, no table and no provenance.  One
    // with rows says what became of its plan, "not-needed" when it was not gated and every column has an origin, and
    // which table its result is, or the first check of the match that failed.
    private static Outcome<QueryDescription, DescribeFailure> Described(
        IReadOnlyList<DescribedParameter> parameters,
        IReadOnlyList<SidecarColumn> columns,
        string plan,
        (SidecarTable? Table, string Result)? match
    )
    {
        var typed = new EquatableArray<DescribedParameter>([.. parameters]);
        return Outcome<QueryDescription, DescribeFailure>.Of(
            match is { } found
                ? new QueryDescription(
                    SidecarResultKind.Rows,
                    typed,
                    new EquatableArray<SidecarColumn>([.. columns]),
                    found.Table,
                    plan,
                    found.Result
                )
                : new QueryDescription(SidecarResultKind.None, typed, null, null, null, null)
        );
    }

    private static Outcome<QueryDescription, DescribeFailure> Failed(DescribeFailure failure) =>
        Outcome<QueryDescription, DescribeFailure>.Failed(failure);
}
