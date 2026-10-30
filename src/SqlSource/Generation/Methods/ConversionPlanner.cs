using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.Diagnostics;
using SqlSource.Generation.Converters;
using SqlSource.Generation.Types;
using SqlSource.Generation.Types.Maps;
using SqlSource.Settings;
using SqlSource.Settings.ClrTypes;

namespace SqlSource.Generation.Methods;

/// <summary>
/// Decides the converter that a column is read through, or a parameter bound through, where a type override, a column
/// type, a marker or a library's row chose its type, with <c>SQLSRC279</c> and <c>SQLSRC280</c> for a type whose
/// converters do not serve it, and <c>SQLSRC277</c> and <c>SQLSRC278</c> for a converter of a referenced assembly that
/// it would use.
/// </summary>
/// <remarks>
/// A converter is an edge from its argument's type to its return type.  For a property of type <c>T</c> whose database
/// type the base map reads as <c>B</c>, a column needs the edge <c>B → T</c>, a parameter <c>T → B</c>.  An array
/// <c>X[]</c> over <c>Y[]</c> that no converter touches is converted element by element, by the edges of <c>X</c>
/// and <c>Y</c>, and so is <c>X?[]</c>.  A type the base map gives, as <c>DateTimeOffset</c>, is never an error, so
/// that a converter between it and another type does not touch the columns the override reads as it.  Anything else
/// is read and bound as the driver reads and binds the type.  Only the converters that serve the query's engine and
/// driver are seen, so a library's edge for SQL Server never converts a PostgreSQL column.  A type that a library's row
/// chose is never <c>SQLSRC279</c> or <c>SQLSRC280</c>: where no edge fits, the driver reads and binds it, as the
/// plugin does on PostgreSQL.
/// <para>
/// A project's own class, a type that is no type of System.Text.Json and none the base map gives, on a type that holds
/// JSON and is read as <c>string</c>, is read and sent through System.Text.Json's serializer when no converter touches
/// it, whole or, for an array of a type that holds JSON, element by element.  The serializer never reports anything: a
/// class with converters of other types keeps the rules above.
/// </para>
/// <para>
/// The JSON opt-in, a place that wrote <c>json</c> before the type, makes a type read as <c>string</c> hold JSON for
/// the serializer.  It is checked before any edge: on a type that neither holds JSON nor is read as <c>string</c>, or
/// with a type the serializer never takes, it is <c>SQLSRC283</c>, which blocks the query.
/// </para>
/// </remarks>
internal static class ConversionPlanner
{
    private const string Column = "column";

    private const string Parameter = "parameter";

    private const string From = "from";

    private const string To = "to";

    // Before the subject of an array's element: "element of column 'ids'".
    private const string ElementSubject = "element of ";

    // The type the base map reads a type that holds JSON as, which the serializer reads and sends.
    private const string JsonText = "string";

    private const string Deserialize = "global::System.Text.Json.JsonSerializer.Deserialize<";

    private const string Serialize = "global::System.Text.Json.JsonSerializer.Serialize<";

    /// <summary>
    /// The conversion of one column or parameter, or null for one read or bound as its type is.  Its problems are
    /// added to the problems of <paramref name="context" />, at its place.
    /// </summary>
    /// <param name="type">The property's type.</param>
    /// <param name="isRead">Whether it is a column, read, and not a parameter, bound.</param>
    /// <param name="subject">The column or parameter as a message names it: <c>column 'id'</c>.</param>
    /// <param name="context">What the query's conversions read, and report to.</param>
    /// <param name="blocked">
    /// Whether the query gets no method, with a problem added or, for a converter of the project's own source that has
    /// its own error at the method, without one.
    /// </param>
    public static Conversion? Plan(
        PropertyType type,
        bool isRead,
        string subject,
        ConversionContext context,
        out bool blocked
    )
    {
        blocked = false;
        if (type.Place is null || type.Database is not { } database)
        {
            return null;
        }

        var decision = new Decision(isRead, subject, context, type.Library is not null);
        // Matched as a converter's types are written, a keyword's type by its keyword and System.Nullable<T> as T?,
        // which the element of a conversion by element keeps, so that a nullable element is seen as one.
        var text = ClrTypeName.KeywordTextOf(type.Text);
        var element = EdgeElementOf(text);
        if (type.IsJson && OptInProblemOf(text, database, type.Key, decision) is { } reason)
        {
            context.Problems.Add(
                DiagnosticInfo.Create(
                    SqlDiagnostics.JsonOptInNotApplicable,
                    context.At,
                    subject,
                    ClrTypeName.AsWritten(text),
                    type.Place,
                    reason
                )
            );
            blocked = true;
            return null;
        }

        if (database.Text is not { } databaseText)
        {
            // A foreign type with converters, else a foreign element with them, which the message names.
            if (decision.IsForeignWithConverters(text))
            {
                decision.ReportNoBase(text, type.Key);
                blocked = true;
            }
            else if (element is not null && decision.IsForeignWithConverters(element))
            {
                decision.OfElement().ReportNoBase(element, ElementKeyOf(type.Key));
                blocked = true;
            }

            return null;
        }

        if (decision.TryEdge(text, databaseText, out var whole, out blocked))
        {
            return whole is null
                ? null
                : new Conversion(
                    whole.Method,
                    databaseText,
                    database.IsValueType,
                    false,
                    null,
                    false,
                    whole.WarningIds,
                    whole.Library?.Library,
                    isRead && JsonTypes.IsNode(whole.ReturnText)
                );
        }

        // An array that no converter touches is converted by its element's, whichever level chose the element.
        // Its messages name the element, whose converter is the one to add.
        if (element is not null && ElementOf(databaseText) is { } databaseElement && !decision.Touches(text))
        {
            var ofElement = decision.OfElement();
            if (ofElement.TryEdge(element, databaseElement, out var byElement, out blocked))
            {
                if (byElement is null)
                {
                    return null;
                }

                var elementIsValueType = isRead ? byElement.ReturnIsValueType : byElement.ArgumentIsValueType;
                return new Conversion(
                    byElement.Method,
                    databaseText,
                    database.ElementIsValueType,
                    true,
                    ElementOf(text),
                    elementIsValueType,
                    byElement.WarningIds,
                    byElement.Library?.Library,
                    isRead && JsonTypes.IsNode(byElement.ReturnText)
                );
            }

            if (TakesSerializer(database.HoldsJson, databaseElement, element, ofElement))
            {
                return new Conversion(
                    SerializerOf(element, isRead),
                    databaseText,
                    database.ElementIsValueType,
                    true,
                    ElementOf(text),
                    type.ElementIsValueType,
                    MayReturnNull: isRead && !type.ElementIsValueType,
                    IsSerializer: true
                );
            }

            blocked = ofElement.ReportForeign(element, databaseElement, ElementKeyOf(type.Key));
            return null;
        }

        var holdsJson = database.HoldsJson || (type.IsJson && databaseText.Equals(JsonText, StringComparison.Ordinal));
        if (TakesSerializer(holdsJson, databaseText, text, decision))
        {
            return new Conversion(
                SerializerOf(text, isRead),
                databaseText,
                database.IsValueType,
                false,
                null,
                false,
                MayReturnNull: isRead && !type.IsValueType,
                IsSerializer: true
            );
        }

        blocked = decision.ReportForeign(text, databaseText, type.Key);
        return null;
    }

    /// <summary>
    /// The element of an array's C# type, <paramref name="text" /> without its last <c>[]</c>, or null.
    /// </summary>
    public static string? ElementOf(string text) =>
        text.EndsWith("[]", StringComparison.Ordinal) ? text.Substring(0, text.Length - 2) : null;

    /// <summary>
    /// The element of an array's C# type whose edges convert it element by element, or null: the element without the
    /// <c>?</c> it is written nullable with, <c>global::App.UserId</c> of <c>global::App.UserId?[]</c>, since a
    /// converter never sees a null.
    /// </summary>
    public static string? EdgeElementOf(string text)
    {
        var element = ElementOf(text);
        return element is not null && element.EndsWith("?", StringComparison.Ordinal)
            ? element.Substring(0, element.Length - 1)
            : element;
    }

    // Why the JSON opt-in does not apply to the type on the database type, as SQLSRC283's {3} words it, or null when it
    // does: on a type that holds JSON or is read as string, with a type the serializer takes.
    private static string? OptInProblemOf(string type, DatabaseType database, string key, Decision decision)
    {
        if (!database.HoldsJson && !JsonText.Equals(database.Text, StringComparison.Ordinal))
        {
            return database.Text is { } read
                ? "the database type '" + key + "' is read as '" + ClrTypeName.AsWritten(read) + "', not as a string"
                : "the database type '" + key + "' is not read as a string";
        }

        var bare = WithoutQuestionMark(type);
        if (JsonTypes.IsJsonType(bare))
        {
            return "'"
                + ClrTypeName.AsWritten(bare)
                + "' is a type of System.Text.Json, which is read without the serializer";
        }

        return decision.IsSerializable(bare)
            ? null
            : "'" + ClrTypeName.AsWritten(bare) + "' is a type that the driver reads, which the serializer never takes";
    }

    // Whether the serializer reads and sends the type, the property's or its element's: a class that no converter
    // touches, on a type that holds JSON, which the base map reads as string.  The type is matched without the ? of a
    // nullable value type.
    private static bool TakesSerializer(bool holdsJson, string database, string type, Decision decision)
    {
        var bare = WithoutQuestionMark(type);
        return holdsJson
            && database.Equals(JsonText, StringComparison.Ordinal)
            && !decision.Touches(bare)
            && decision.IsSerializable(bare);
    }

    // The serializer's call for the type, without the ? of a nullable value type, which the method never passes a
    // null: JsonSerializer.Deserialize<T> to read, JsonSerializer.Serialize<T> to send, with the default options.
    private static string SerializerOf(string type, bool isRead) =>
        (isRead ? Deserialize : Serialize) + WithoutQuestionMark(type) + ">";

    private static string WithoutQuestionMark(string type) =>
        type.EndsWith("?", StringComparison.Ordinal) ? type.Substring(0, type.Length - 1) : type;

    // The key of an array's element: an entry of the array is keyed by its element's key with [], and the override of
    // the element gives the element's key itself.
    private static string ElementKeyOf(string key) =>
        key.EndsWith("[]", StringComparison.Ordinal) ? key.Substring(0, key.Length - 2) : key;

    // The steps of the decision for one pair of types, the property's or its element's and the base map's.  One that is
    // exempt, of a type that a library's row chose, reports neither SQLSRC279 nor SQLSRC280.
    private readonly struct Decision(bool isRead, string subject, ConversionContext context, bool isExempt)
    {
        // The decision for the element of the array that the subject is.
        public Decision OfElement() => new(isRead, ElementSubject + subject, context, isExempt);

        // True when the pair decides the conversion: the edge it needs, used or blocked, or the other direction's,
        // SQLSRC280, blocked.  False when neither edge exists, or when only the other direction does and the decision
        // is exempt.
        public bool TryEdge(string type, string database, out Converter? used, out bool blocked)
        {
            var (argument, returns) = isRead ? (database, type) : (type, database);
            var edge = EdgeOf(argument, returns);
            if (edge.Count > 0)
            {
                used = Use(edge, out blocked);
                return true;
            }

            used = null;
            if (isExempt || EdgeOf(returns, argument).Count == 0)
            {
                blocked = false;
                return false;
            }

            var (has, needs) = isRead ? (To, From) : (From, To);
            context.Problems.Add(
                DiagnosticInfo.Create(
                    SqlDiagnostics.ConverterDirectionMissing,
                    context.At,
                    subject,
                    ClrTypeName.AsWritten(type),
                    has,
                    ClrTypeName.AsWritten(database),
                    needs,
                    isRead ? Column : Parameter,
                    ClrTypeName.AsWritten(argument),
                    ClrTypeName.AsWritten(returns)
                )
            );
            blocked = true;
            return true;
        }

        // SQLSRC279 for a foreign type with converters, the property's or its element's, whose database type the base
        // map gives no C# type.  Its help names no place, since a type override, a column type or a marker may have
        // chosen the type.
        public void ReportNoBase(string type, string key) =>
            context.Problems.Add(
                DiagnosticInfo.Create(
                    SqlDiagnostics.ConverterTypeMismatch,
                    context.At,
                    subject,
                    ClrTypeName.AsWritten(type),
                    "which has converters, but the database type '" + key + "' has no C# type to convert from",
                    "Choose a C# type without converters for it, or remove the converters of '"
                        + ClrTypeName.AsWritten(type)
                        + "'"
                )
            );

        // Whether any converter that serves the target takes or returns the type.
        public bool Touches(string type)
        {
            var target = context.Target;
            return context.Converters.Any(converter =>
                converter.Serves(target) && (IsText(converter.ArgumentText, type) || IsText(converter.ReturnText, type))
            );
        }

        // Whether the type is foreign with converters, so that a missing base type or edge is SQLSRC279: never for an
        // exempt decision.
        public bool IsForeignWithConverters(string type) => !isExempt && IsForeign(type) && Touches(type);

        // SQLSRC279 for a foreign type whose converters are of other types than the database's, which blocks the
        // query; false, and nothing, for any other.
        public bool ReportForeign(string type, string database, string key)
        {
            if (!IsForeignWithConverters(type))
            {
                return false;
            }

            var target = context.Target;
            var others = context
                .Converters.Where(converter =>
                    converter.Serves(target)
                    && (IsText(converter.ArgumentText, type) || IsText(converter.ReturnText, type))
                )
                .Select(converter =>
                    IsText(converter.ArgumentText, type) ? converter.ReturnText : converter.ArgumentText
                )
                .Select(ClrTypeName.AsWritten)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static other => other, StringComparer.Ordinal)
                .ToList();
            var written = ClrTypeName.AsWritten(type);
            var databaseWritten = ClrTypeName.AsWritten(database);
            context.Problems.Add(
                DiagnosticInfo.Create(
                    SqlDiagnostics.ConverterTypeMismatch,
                    context.At,
                    subject,
                    written,
                    "whose converters convert from and to "
                        + ListOf(others)
                        + ", and not from and to '"
                        + databaseWritten
                        + "', the type the database type '"
                        + key
                        + "' is read as",
                    "Add a converter between '" + written + "' and '" + databaseWritten + "'"
                )
            );
            return true;
        }

        // The converter of an edge, or null when the query is blocked: by two converters of it, or one with a fault.
        // One of the project's own source has its error at the method already; one of a referenced assembly has none,
        // so it is reported here.
        private Converter? Use(List<Converter> edge, out bool blocked)
        {
            var sound = edge.Where(static converter => converter.Fault is null).ToList();
            blocked = sound.Count != 1 || sound.Count != edge.Count;
            foreach (var converter in edge.Where(static converter => converter.Origin == ConverterOrigin.MappedType))
            {
                if (converter.Fault is { } fault)
                {
                    context.Problems.Add(
                        DiagnosticInfo.Create(SqlDiagnostics.NotAConverter, context.At, converter.DisplayName, fault)
                    );
                }
            }

            if (sound.Count > 1 && sound.TrueForAll(static converter => converter.Origin == ConverterOrigin.MappedType))
            {
                context.Problems.Add(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.DuplicateConverter,
                        context.At,
                        sound[0].DisplayName,
                        sound[1].DisplayName,
                        ClrTypeName.AsWritten(sound[0].ArgumentText),
                        ClrTypeName.AsWritten(sound[0].ReturnText)
                    )
                );
            }

            return blocked ? null : sound[0];
        }

        // The converters that serve the target from the one type to the other.
        private List<Converter> EdgeOf(string from, string to)
        {
            var target = context.Target;
            return
            [
                .. context.Converters.Where(converter =>
                    converter.Serves(target) && IsText(converter.ArgumentText, from) && IsText(converter.ReturnText, to)
                ),
            ];
        }

        // Whether the type is a class for the serializer: no type of System.Text.Json, and none the base map gives.
        public bool IsSerializable(string type) => !JsonTypes.IsJsonType(type) && IsForeign(type);

        private bool IsForeign(string type) =>
            context.Engine == DatabaseEngine.SqlServer ? !SqlServerTypeMap.Gives(type) : !PostgresTypeMap.Gives(type);

        // 'int', 'int' and 'short', or 'a', 'b' and 'c'.
        private static string ListOf(List<string> types)
        {
            var quoted = types.Select(static type => "'" + type + "'").ToList();
            return quoted.Count == 1
                ? quoted[0]
                : string.Join(", ", quoted.Take(quoted.Count - 1)) + " and " + quoted[quoted.Count - 1];
        }

        private static bool IsText(string left, string right) => left.Equals(right, StringComparison.Ordinal);
    }
}
