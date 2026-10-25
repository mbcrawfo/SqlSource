using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.Diagnostics;
using SqlSource.Generation.Converters;
using SqlSource.Generation.Types;
using SqlSource.Settings;

namespace SqlSource.Generation.Methods;

/// <summary>
/// Decides the converter that a column is read through, or a parameter bound through, where a type override chose its
/// type, with <c>SQLSRC279</c> and <c>SQLSRC280</c> for a type whose converters do not serve it, and <c>SQLSRC277</c>
/// and <c>SQLSRC278</c> for a converter of a referenced assembly that it would use.
/// </summary>
/// <remarks>
/// A converter is an edge from its argument's type to its return type.  For a property of type <c>T</c> whose database
/// type the base map reads as <c>B</c>, a column needs the edge <c>B → T</c>, a parameter <c>T → B</c>.  An array
/// <c>X[]</c> over <c>Y[]</c> that no converter touches is converted element by element, by the edges of <c>X</c>
/// and <c>Y</c>.  A type the base map gives, as <c>DateTimeOffset</c>, is never an error, so that a converter between
/// it and another type does not touch the columns the override reads as it.  Anything else is read and bound as the
/// driver reads and binds the type.
/// </remarks>
internal static class ConversionPlanner
{
    private const string Column = "column";

    private const string Parameter = "parameter";

    private const string From = "from";

    private const string To = "to";

    /// <summary>
    /// The conversion of one column or parameter, or null for one read or bound as its type is.  Its problems are
    /// added to <paramref name="problems" />, at <paramref name="at" />.
    /// </summary>
    /// <param name="type">The property's type.</param>
    /// <param name="isRead">Whether it is a column, read, and not a parameter, bound.</param>
    /// <param name="engine">The engine of the query's file, whose base map says which types are foreign.</param>
    /// <param name="converters">The converters of the type's slice, in the order of the set.</param>
    /// <param name="subject">The column or parameter as a message names it: <c>column 'id'</c>.</param>
    /// <param name="at">The query's name.</param>
    /// <param name="problems">The query's problems.</param>
    /// <param name="blocked">
    /// Whether the query gets no method, with a problem added or, for a converter of the project's own source that has
    /// its own error at the method, without one.
    /// </param>
    public static Conversion? Plan(
        PropertyType type,
        bool isRead,
        DatabaseEngine engine,
        EquatableArray<Converter> converters,
        string subject,
        LocationInfo at,
        List<DiagnosticInfo> problems,
        out bool blocked
    )
    {
        blocked = false;
        if (type.Place is null || type.Database is not { } database)
        {
            return null;
        }

        var decision = new Decision(isRead, engine, converters, subject, at, problems);
        var text = type.Text;
        var element = ElementOf(text);
        if (database.Text is not { } databaseText)
        {
            blocked = decision.ReportNoBase(text, element, type.Key);
            return null;
        }

        if (decision.TryEdge(text, databaseText, out var whole, out blocked))
        {
            return whole is null
                ? null
                : new Conversion(whole.Method, databaseText, database.IsValueType, false, null, false);
        }

        // An array that no converter touches is converted by its element's, whichever level chose the element.
        if (element is not null && ElementOf(databaseText) is { } databaseElement && !decision.Touches(text))
        {
            if (decision.TryEdge(element, databaseElement, out var byElement, out blocked))
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
                    element,
                    elementIsValueType
                );
            }

            blocked = decision.ReportForeign(element, databaseElement, ElementKeyOf(type.Key));
            return null;
        }

        blocked = decision.ReportForeign(text, databaseText, type.Key);
        return null;
    }

    /// <summary>
    /// The element of an array's C# type, <paramref name="text" /> without its last <c>[]</c>, or null.
    /// </summary>
    public static string? ElementOf(string text) =>
        text.EndsWith("[]", StringComparison.Ordinal) ? text.Substring(0, text.Length - 2) : null;

    // The key of an array's element: an entry of the array is keyed by its element's key with [], and the override of
    // the element gives the element's key itself.
    private static string ElementKeyOf(string key) =>
        key.EndsWith("[]", StringComparison.Ordinal) ? key.Substring(0, key.Length - 2) : key;

    // The steps of the decision for one pair of types, the property's or its element's and the base map's.
    private readonly struct Decision(
        bool isRead,
        DatabaseEngine engine,
        EquatableArray<Converter> converters,
        string subject,
        LocationInfo at,
        List<DiagnosticInfo> problems
    )
    {
        // True when the pair decides the conversion: the edge it needs, used or blocked, or the other direction's,
        // SQLSRC280, blocked.  False when neither edge exists.
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
            if (EdgeOf(returns, argument).Count == 0)
            {
                blocked = false;
                return false;
            }

            var (has, needs) = isRead ? (To, From) : (From, To);
            problems.Add(
                DiagnosticInfo.Create(
                    SqlDiagnostics.ConverterDirectionMissing,
                    at,
                    subject,
                    PostgresTypeMap.AsWritten(type),
                    has,
                    PostgresTypeMap.AsWritten(database),
                    needs,
                    isRead ? Column : Parameter,
                    PostgresTypeMap.AsWritten(argument),
                    PostgresTypeMap.AsWritten(returns)
                )
            );
            blocked = true;
            return true;
        }

        // SQLSRC279 for a foreign type with converters, or an array of a foreign element with converters, whose
        // database type the base map gives no C# type, which blocks the query; false, and nothing, for any other.
        public bool ReportNoBase(string type, string? element, string key)
        {
            var touched = type;
            if (!IsForeignWithConverters(type))
            {
                if (element is null || !IsForeignWithConverters(element))
                {
                    return false;
                }

                touched = element;
            }

            problems.Add(
                DiagnosticInfo.Create(
                    SqlDiagnostics.ConverterTypeMismatch,
                    at,
                    subject,
                    PostgresTypeMap.AsWritten(type),
                    "which has converters, but the database type '" + key + "' has no C# type to convert from",
                    "Remove the type override, or the converters of '" + PostgresTypeMap.AsWritten(touched) + "'"
                )
            );
            return true;
        }

        // Whether any converter takes or returns the type.
        public bool Touches(string type) =>
            converters.Any(converter => IsText(converter.ArgumentText, type) || IsText(converter.ReturnText, type));

        public bool IsForeignWithConverters(string type) => IsForeign(type) && Touches(type);

        // SQLSRC279 for a foreign type whose converters are of other types than the database's, which blocks the
        // query; false, and nothing, for any other.
        public bool ReportForeign(string type, string database, string key)
        {
            if (!IsForeignWithConverters(type))
            {
                return false;
            }

            var others = converters
                .Where(converter => IsText(converter.ArgumentText, type) || IsText(converter.ReturnText, type))
                .Select(converter =>
                    IsText(converter.ArgumentText, type) ? converter.ReturnText : converter.ArgumentText
                )
                .Select(PostgresTypeMap.AsWritten)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static other => other, StringComparer.Ordinal)
                .ToList();
            var written = PostgresTypeMap.AsWritten(type);
            var databaseWritten = PostgresTypeMap.AsWritten(database);
            problems.Add(
                DiagnosticInfo.Create(
                    SqlDiagnostics.ConverterTypeMismatch,
                    at,
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
                    problems.Add(DiagnosticInfo.Create(SqlDiagnostics.NotAConverter, at, converter.DisplayName, fault));
                }
            }

            if (sound.Count > 1 && sound.TrueForAll(static converter => converter.Origin == ConverterOrigin.MappedType))
            {
                problems.Add(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.DuplicateConverter,
                        at,
                        sound[0].DisplayName,
                        sound[1].DisplayName,
                        PostgresTypeMap.AsWritten(sound[0].ArgumentText),
                        PostgresTypeMap.AsWritten(sound[0].ReturnText)
                    )
                );
            }

            return blocked ? null : sound[0];
        }

        // The converters from the one type to the other.
        private List<Converter> EdgeOf(string from, string to) =>
            [
                .. converters.Where(converter =>
                    IsText(converter.ArgumentText, from) && IsText(converter.ReturnText, to)
                ),
            ];

        private bool IsForeign(string type) =>
            engine == DatabaseEngine.SqlServer ? !SqlServerTypeMap.Gives(type) : !PostgresTypeMap.Gives(type);

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
