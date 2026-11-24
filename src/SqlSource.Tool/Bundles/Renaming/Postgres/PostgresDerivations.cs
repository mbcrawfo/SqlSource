using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Statements;
using SqlSource.Tool.Describing.Postgres.Types;
using SqlSource.Tool.Describing.Postgres.Walk;

namespace SqlSource.Tool.Bundles.Renaming.Postgres;

/// <summary>
/// What PostgreSQL's pipeline makes by its own rule, made again from a call's renamed values: the order of the
/// relations that <c>relations</c> and <c>partitions</c> ask, where in the renamed <c>PREPARE</c> an error of
/// <c>prepare</c> is, and the name <c>check</c> binds each parameter by, with the entry's <c>bindName</c>.
/// </summary>
/// <remarks>
/// <para>
/// The describer asks its relations in schema and name order, <see cref="PlanRelations.InOrder" />, which the renamed
/// names change, and the replay finds an answer by the request the pipeline makes.  An error's position counts code
/// points of <see cref="ParameterPlaceholders.PrepareText" />, which move with each renamed name and declared type.
/// The describer reads a position only as after the head, in the query's SQL, or in an item of the type list, whose
/// declared parameter it names (<c>PostgresErrors.Prepare</c>), so a position is mapped into the same part.
/// </para>
/// <para>
/// The renamed SQL and types are made again through <see cref="IRenamingScope.Sql" /> from the originals, as the
/// renaming of their members made them, and a position is refused rather than mapped through a text that is not the
/// renamed one.  A request or an answer that does not read as its record is left alone: the replay then refuses it.
/// </para>
/// <para>
/// A bind name is made by <see cref="PostgresBindNames" /> from the rows of <c>types</c>, and some are a type's own
/// name rather than its element's with <c>[]</c>: an array whose element's name starts with <c>_</c>, or whose own
/// name the server cut.  Renamed token by token, such a name and its element's lose that relation, so the name is
/// made again from the renamed rows, for the parameter's type that <c>prepare</c> answered.
/// </para>
/// </remarks>
internal static class PostgresDerivations
{
    // Where a refusal of a position is.
    private const string PositionPath = "answer.error.position";

    // The members of a prepare answer that hold the error and its position.
    private const string ErrorMember = "error";
    private const string PositionMember = "position";

    /// <summary>
    /// Derives what the pipeline makes of one call, writing into <see cref="RenamingCall.Renamed" />: the relations of
    /// <c>relations</c> and <c>partitions</c> in their renamed order, the position of a <c>prepare</c> error in the
    /// renamed text, and the name <c>check</c> binds each parameter by, from the renamed rows of every <c>types</c>
    /// call.  A position the mapping cannot place is refused at <c>answer.error.position</c>, with no token.  Every
    /// other call is left alone.
    /// </summary>
    public static void Derive(RenamingCall call, IReadOnlyList<RenamingCall> calls, IRenamingScope scope)
    {
        switch (call.Method)
        {
            case PostgresCallNames.Relations:
                if (Read<PostgresRelationsRequest>(call.Renamed[CaptureJson.RequestKey]) is { } relations)
                {
                    call.Renamed[CaptureJson.RequestKey] = Write(
                        relations with
                        {
                            Relations = PlanRelations.InOrder(relations.Relations),
                        }
                    );
                }

                break;
            case PostgresCallNames.Partitions:
                if (Read<PostgresPartitionsRequest>(call.Renamed[CaptureJson.RequestKey]) is { } partitions)
                {
                    call.Renamed[CaptureJson.RequestKey] = Write(
                        partitions with
                        {
                            Relations = PlanRelations.InOrder(partitions.Relations),
                        }
                    );
                }

                break;
            case PostgresCallNames.Prepare:
                DerivePosition(call, scope);
                break;
            case PostgresCallNames.Check:
                DeriveBindNames(call, calls);
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Gives each parameter of the renamed entry whose type has a <c>bindName</c> the name that the derived request of
    /// the last <c>check</c> binds the parameter at its place by, as the pipeline records the name it bound.  An entry
    /// without parameters, or a capture without <c>check</c>, is left alone.
    /// </summary>
    public static void DeriveEntry(JsonObject entry, IReadOnlyList<RenamingCall> calls)
    {
        if (
            calls.LastOrDefault(static call => call.Method == PostgresCallNames.Check) is not { } check
            || Read<PostgresCheckRequest>(check.Renamed[CaptureJson.RequestKey]) is not { } asked
            || entry[SidecarKeys.Parameters] is not JsonArray parameters
        )
        {
            return;
        }

        for (var index = 0; index < parameters.Count && index < asked.Parameters.Count; index++)
        {
            if (parameters[index]?[SidecarKeys.Type] is JsonObject type && type.ContainsKey(SidecarKeys.BindName))
            {
                type[SidecarKeys.BindName] = asked.Parameters[index].DataTypeName;
            }
        }
    }

    // Each parameter's name to bind by, made again from the renamed rows for the OID that the last prepare before the
    // check answered at the parameter's place, as the pipeline makes it.  A parameter whose original name is not the
    // one the original rows give, or whose rows are not all answered, keeps its name renamed token by token.
    private static void DeriveBindNames(RenamingCall call, IReadOnlyList<RenamingCall> calls)
    {
        if (
            Read<PostgresCheckRequest>(call.Original[CaptureJson.RequestKey]) is not { } original
            || Read<PostgresCheckRequest>(call.Renamed[CaptureJson.RequestKey]) is not { } renamed
            || renamed.Parameters.Count != original.Parameters.Count
            || ParameterTypesBefore(call, calls) is not { } oids
        )
        {
            return;
        }

        var originalRows = RowsOf(calls, static each => each.Original);
        var renamedRows = RowsOf(calls, static each => each.Renamed);
        var parameters = new List<PostgresCheckParameter>(renamed.Parameters.Count);
        for (var index = 0; index < renamed.Parameters.Count; index++)
        {
            var parameter = renamed.Parameters[index];
            if (
                index < oids.Count
                && string.Equals(
                    BindNameOf(oids[index], originalRows),
                    original.Parameters[index].DataTypeName,
                    StringComparison.Ordinal
                )
                && BindNameOf(oids[index], renamedRows) is { } derived
            )
            {
                parameter = parameter with { DataTypeName = derived };
            }

            parameters.Add(parameter);
        }

        call.Renamed[CaptureJson.RequestKey] = Write(renamed with { Parameters = parameters });
    }

    // The OIDs of the parameters that the last prepare before the call answered, or null when there is none.
    private static IReadOnlyList<long>? ParameterTypesBefore(RenamingCall call, IReadOnlyList<RenamingCall> calls) =>
        calls.LastOrDefault(each => each.Index < call.Index && each.Method == PostgresCallNames.Prepare) is { } prepare
            ? Read<PostgresPrepareAnswer>(prepare.Original[CaptureJson.AnswerKey])?.ParameterTypes
            : null;

    // The rows that every types call answered, by OID, from the original or the renamed answers.
    private static Dictionary<long, PostgresTypeRow> RowsOf(
        IReadOnlyList<RenamingCall> calls,
        Func<RenamingCall, JsonObject> side
    )
    {
        var rows = new Dictionary<long, PostgresTypeRow>();
        foreach (var call in calls.Where(static each => each.Method == PostgresCallNames.Types))
        {
            foreach (var row in Read<PostgresTypesAnswer>(side(call)[CaptureJson.AnswerKey])?.Rows ?? [])
            {
                rows[row.Oid] = row;
            }
        }

        return rows;
    }

    // The bind name of a type, or null when it has none or the rows lack a type it reaches.
    private static string? BindNameOf(long oid, IReadOnlyDictionary<long, PostgresTypeRow> rows)
    {
        try
        {
            return PostgresBindNames.Of(oid, rows);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static void DerivePosition(RenamingCall call, IRenamingScope scope)
    {
        if (
            Read<PostgresPrepareAnswer>(call.Original[CaptureJson.AnswerKey]) is not { Error.Position: { } position }
            || Read<PostgresPrepareRequest>(call.Original[CaptureJson.RequestKey]) is not { } original
            || Read<PostgresPrepareRequest>(call.Renamed[CaptureJson.RequestKey]) is not { } renamed
            || call.Renamed[CaptureJson.AnswerKey]?[ErrorMember] is not JsonObject error
        )
        {
            return;
        }

        if (Mapped(position, original, renamed, scope) is { } mapped)
        {
            error[PositionMember] = mapped;
        }
        else
        {
            scope.Refuse(new RenamingRefusal(RefusalStep.Token, CaptureRenamer.PlaceOf(call, PositionPath), null));
        }
    }

    // The position in the renamed PREPARE, or null when the mapping cannot place it.
    private static int? Mapped(
        int position,
        PostgresPrepareRequest original,
        PostgresPrepareRequest renamed,
        IRenamingScope scope
    )
    {
        var head = ParameterPlaceholders.HeadLength(original.Statement, original.Types);
        if (position > head)
        {
            return MappedIn(original.Sql, renamed.Sql, NameClass.Alias, position - head, scope) is { } inSql
                ? ParameterPlaceholders.HeadLength(renamed.Statement, renamed.Types) + inSql
                : null;
        }

        // Before the list, in "PREPARE <statement> (", which the renaming does not change.
        var opening = ParameterPlaceholders.Characters(ParameterPlaceholders.Opening(original.Statement));
        if (original.Types.Count == 0 || position <= opening)
        {
            return position >= 1 ? position : null;
        }

        if (renamed.Types.Count != original.Types.Count)
        {
            return null;
        }

        // A position in the separator after its item, or after the last item, is at the item's last code point, so
        // that it falls in the same renamed item.
        var item = ParameterPlaceholders.TypeAt(original.Statement, original.Types, position);
        var type = original.Types[item];
        var offset = Math.Min(position - StartOf(original, item), Math.Max(ParameterPlaceholders.Characters(type), 1));
        return MappedIn(type, renamed.Types[item], NameClass.Type, offset, scope) is { } inItem
            ? StartOf(renamed, item) + inItem
            : null;
    }

    // A position of an original text in its renamed one, made again through the scope, or null when the scope does not
    // make the renamed text or the position is not in the original.
    private static int? MappedIn(
        string original,
        string renamed,
        NameClass newWords,
        int position,
        IRenamingScope scope
    )
    {
        var text = scope.Sql(original, newWords);
        return
            string.Equals(text.Text, renamed, StringComparison.Ordinal)
            && position >= 1
            && position <= text.Offsets.Length
            ? text.MapPosition(position)
            : null;
    }

    // How many code points of the PREPARE stand before an item of the type list.
    private static int StartOf(PostgresPrepareRequest request, int item) =>
        ParameterPlaceholders.Characters(ParameterPlaceholders.Opening(request.Statement))
        + request
            .Types.Take(item)
            .Sum(static type => ParameterPlaceholders.Characters(type) + ParameterPlaceholders.Separator.Length);

    // A request or an answer as its record, or null when it does not read as one.
    private static T? Read<T>(JsonNode? node)
        where T : class
    {
        T? read;
        try
        {
            read = node.Deserialize<T>(CaptureJson.Options);
        }
        catch (JsonException)
        {
            return null;
        }

        return read is not null && AnswerNulls.Find(read) is null ? read : null;
    }

    private static JsonNode? Write<T>(T value) => JsonSerializer.SerializeToNode(value, CaptureJson.Options);
}
