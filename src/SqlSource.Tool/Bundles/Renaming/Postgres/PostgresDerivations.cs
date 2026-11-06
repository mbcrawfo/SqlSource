using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Statements;
using SqlSource.Tool.Describing.Postgres.Walk;

namespace SqlSource.Tool.Bundles.Renaming.Postgres;

/// <summary>
/// What PostgreSQL's pipeline makes by its own rule, made again from a call's renamed values: the order of the
/// relations that <c>relations</c> and <c>partitions</c> ask, and where in the renamed <c>PREPARE</c> an error of
/// <c>prepare</c> is.
/// </summary>
/// <remarks>
/// <para>
/// The describer asks its relations in schema and name order, <see cref="PlanRelations.InOrder" />, which the renamed
/// names change, and the replay finds an answer by the request the pipeline makes.  An error's position counts code
/// points of <see cref="ParameterPlaceholders.PrepareText" />, which move with each renamed name and declared type.
/// The describer reads a position only as after the head, in the query's SQL, or in an item of the type list, whose
/// declared parameter it names (<c>PostgresErrors.Prepare</c>), so a position is mapped into the same part: the spike
/// of phase 10's finding 5.
/// </para>
/// <para>
/// The renamed SQL and types are made again through <see cref="IRenamingScope.Sql" /> from the originals, as the
/// renaming of their members made them, and a position is refused rather than mapped through a text that is not the
/// renamed one.  A request or an answer that does not read as its record is left alone: the replay then refuses it.
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
    /// <c>relations</c> and <c>partitions</c> in their renamed order, and the position of a <c>prepare</c> error in
    /// the renamed text.  A position the mapping cannot place is refused at <c>answer.error.position</c>, with no
    /// token.  Every other call is left alone.
    /// </summary>
    public static void Derive(RenamingCall call, IRenamingScope scope)
    {
        switch (call.Method)
        {
            case PostgresCallNames.Relations:
                if (Read<PostgresRelationsRequest>(call.Renamed[CaptureRenamer.Request]) is { } relations)
                {
                    call.Renamed[CaptureRenamer.Request] = Write(
                        relations with
                        {
                            Relations = PlanRelations.InOrder(relations.Relations),
                        }
                    );
                }

                break;
            case PostgresCallNames.Partitions:
                if (Read<PostgresPartitionsRequest>(call.Renamed[CaptureRenamer.Request]) is { } partitions)
                {
                    call.Renamed[CaptureRenamer.Request] = Write(
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
            default:
                break;
        }
    }

    private static void DerivePosition(RenamingCall call, IRenamingScope scope)
    {
        if (
            Read<PostgresPrepareAnswer>(call.Original[CaptureRenamer.Answer]) is not { Error.Position: { } position }
            || Read<PostgresPrepareRequest>(call.Original[CaptureRenamer.Request]) is not { } original
            || Read<PostgresPrepareRequest>(call.Renamed[CaptureRenamer.Request]) is not { } renamed
            || call.Renamed[CaptureRenamer.Answer]?[ErrorMember] is not JsonObject error
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
