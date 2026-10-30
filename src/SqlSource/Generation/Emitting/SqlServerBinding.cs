using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Generation.Methods;
using SqlSource.Generation.Types.Maps;
using SqlSource.Snapshot;

namespace SqlSource.Generation.Emitting;

/// <summary>
/// Writes the binding of a SQL Server parameter as its described type: the <c>SqlParameter</c> the command creates,
/// with the <c>SqlDbType</c> of the type's base name and the size, precision or scale the type has, so that SqlClient
/// infers nothing from the value.  A value longer than a bounded length is bound at <c>max</c> of its variable-length
/// type, which SqlClient would otherwise cut, so the server sees it whole.  A decimal for a type of scale 0 is rounded
/// to a whole number, which SqlClient would otherwise send at the value's own scale.  A CLR type, <c>hierarchyid</c>,
/// <c>geography</c>, <c>geometry</c> or a user's, is a <c>Udt</c> of the <c>UdtTypeName</c> its user type names,
/// which SqlClient needs for every value but a null.  A parameter without a described SQL Server type, or of a type
/// with no binding here, is bound by value.
/// </summary>
internal static class SqlServerBinding
{
    private const string SqlParameterType = "global::Microsoft.Data.SqlClient.SqlParameter";
    private const string SqlDbTypeText = "global::System.Data.SqlDbType.";
    private const string DbNull = "global::System.DBNull.Value";
    private const string VectorNullStart = QueryMethodWriter.VectorType + ".CreateNull(";

    // Each base name's binding, matched ignoring case, as the server matches a type's name.
    private static readonly Dictionary<string, Row> Rows = BuildRows();

    /// <summary>
    /// Appends the lines that bind <paramref name="bound" />, as <see cref="ParameterBinder.Append" /> takes them.
    /// </summary>
    public static void Append(
        StringBuilder builder,
        string indent,
        string command,
        string parameter,
        string parameters,
        MethodParameter bound,
        ConversionNames names
    )
    {
        if (bound.Described is not SqlServerType type || !TryFindRow(type, out var row, out var facet))
        {
            ParameterBinder.AppendByValue(builder, indent, command, parameter, parameters, bound, names);
            return;
        }

        _ = builder
            .Append(indent)
            .Append("var ")
            .Append(parameter)
            .Append(" = (")
            .Append(SqlParameterType)
            .Append(')')
            .Append(command)
            .Append(".CreateParameter();\n")
            .Append(indent)
            .Append(parameter)
            .Append(".ParameterName = ")
            .Append(SymbolDisplay.FormatLiteral(bound.Name, quote: true))
            .Append(";\n");
        if (row.SqlDbType is { } sqlDbType)
        {
            AppendSqlDbType(builder, indent, parameter, sqlDbType);
        }

        if (row.UdtTypeName is { } udtTypeName)
        {
            _ = builder
                .Append(indent)
                .Append(parameter)
                .Append(".UdtTypeName = ")
                .Append(SymbolDisplay.FormatLiteral(udtTypeName, quote: true))
                .Append(";\n");
        }

        AppendFacets(builder, indent, parameter, row.Facet, facet, type.Scale);
        _ = builder.Append(indent).Append(parameter).Append(".Value = ");
        if (row.Facet == Facet.Vector)
        {
            var nullText = VectorNullStart + facet.ToString(CultureInfo.InvariantCulture) + ")";
            ParameterBinder.AppendValueOrNull(builder, parameters, bound, names, nullText);
        }
        else
        {
            ParameterBinder.AppendValueOrNull(builder, parameters, bound, names, DbNull);
        }

        _ = builder.Append(";\n");
        if (row.ValueText is { } valueText && facet > 0)
        {
            AppendOverLong(builder, indent, parameter, valueText, facet, row.Wider);
        }

        if (row.Facet == Facet.PrecisionAndScale && type.Scale == 0)
        {
            AppendRoundedToScale0(builder, indent, parameter);
        }

        _ = builder.Append(indent).Append(command).Append(".Parameters.Add(").Append(parameter).Append(");\n");
    }

    /// <summary>
    /// The member of <c>System.Data.SqlDbType</c> that binds the type of <paramref name="baseName" />, matched ignoring
    /// case, or null for <c>vector</c>, whose value gives its type, and for a name with no binding.
    /// </summary>
    internal static string? SqlDbTypeOf(string baseName) =>
        Rows.TryGetValue(baseName, out var row) ? row.SqlDbType : null;

    // The binding of the type and the number its facets give.  A CLR type is bound by its user type, before its name
    // is looked for, since a CLR type of a user may share a name with a system type that the server tells apart by its
    // schema: dbo.Money is no money.  False for a type with no binding, or facets no SqlParameter can hold.
    private static bool TryFindRow(SqlServerType type, out Row row, out int facet)
    {
        facet = 0;
        if (type.UserType is { Name: { Length: > 0 } name, AssemblyQualifiedName.Length: > 0 } userType)
        {
            row = new Row("Udt", Facet.None, UdtTypeName: UdtTypeNameOf(userType.Schema, name));
            return true;
        }

        return Rows.TryGetValue(SqlServerTypeMap.BaseName(type.Name), out row)
            && TryReadFacets(type, row.Facet, out facet);
    }

    // The UdtTypeName of a CLR type: its bare name in sys, where SqlServerNames.ClrSystemTypes are, and schema.name
    // otherwise, each part as SqlServerNames.UdtPart writes it.
    private static string UdtTypeNameOf(string? schema, string name) =>
        schema is null || SystemSchemas.IsSqlServer(schema)
            ? SqlServerNames.UdtPart(name)
            : SqlServerNames.UdtPart(schema) + "." + SqlServerNames.UdtPart(name);

    // The number the binding takes from the type: the Size of a length, -1 for max; the precision, of a decimal, whose
    // scale is the type's; or the dimension of a vector.  False for one no SqlParameter can hold, which only a sidecar
    // edited by hand can give, and which is then bound by value.
    private static bool TryReadFacets(SqlServerType type, Facet kind, out int facet)
    {
        facet = 0;
        switch (kind)
        {
            case Facet.Bytes:
                facet = type.MaxLength;
                return facet is -1 or > 0;
            case Facet.Characters:
                facet = type.MaxLength == -1 ? -1 : type.MaxLength / 2;
                return facet is -1 or > 0;
            case Facet.PrecisionAndScale:
                facet = type.Precision;
                return type.Precision is >= 0 and <= byte.MaxValue && type.Scale is >= 0 and <= byte.MaxValue;
            case Facet.Vector:
                return TryReadDimension(type.Name, out facet);
            case Facet.None:
            case Facet.Scale7:
            case Facet.Max:
            default:
                return true;
        }
    }

    // The n of vector(n), or of vector(n, type).
    private static bool TryReadDimension(string name, out int dimension)
    {
        dimension = 0;
        var open = name.IndexOf('(');
        if (open < 0)
        {
            return false;
        }

        var end = open + 1;
        while (end < name.Length && name[end] != ',' && name[end] != ')')
        {
            end++;
        }

        return end < name.Length
            && int.TryParse(
                name.Substring(open + 1, end - open - 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out dimension
            )
            && dimension > 0;
    }

    private static void AppendSqlDbType(StringBuilder builder, string indent, string parameter, string sqlDbType) =>
        _ = builder
            .Append(indent)
            .Append(parameter)
            .Append(".SqlDbType = ")
            .Append(SqlDbTypeText)
            .Append(sqlDbType)
            .Append(";\n");

    private static void AppendFacets(
        StringBuilder builder,
        string indent,
        string parameter,
        Facet kind,
        int facet,
        int scale
    )
    {
        switch (kind)
        {
            case Facet.Bytes:
            case Facet.Characters:
                AppendSet(builder, indent, parameter, "Size", facet);
                break;
            case Facet.Max:
                AppendSet(builder, indent, parameter, "Size", -1);
                break;
            case Facet.PrecisionAndScale:
                // Both, since SqlClient takes the one not set from the value; a Scale of 0 counts as not set, and the
                // value is rounded to scale 0 after it is set.
                AppendSet(builder, indent, parameter, "Precision", facet);
                AppendSet(builder, indent, parameter, "Scale", scale);
                break;
            case Facet.Scale7:
                // Scale 0 counts as not set, and one from 1 to 6 makes SqlClient truncate the value where the server
                // would round it, so every such parameter is declared at 7 and the server rounds on assignment.
                AppendSet(builder, indent, parameter, "Scale", 7);
                break;
            case Facet.None:
            case Facet.Vector:
            default:
                break;
        }
    }

    private static void AppendSet(StringBuilder builder, string indent, string parameter, string member, int value) =>
        _ = builder
            .Append(indent)
            .Append(parameter)
            .Append('.')
            .Append(member)
            .Append(" = ")
            .Append(value.ToString(CultureInfo.InvariantCulture))
            .Append(";\n");

    // A value longer than the length is bound at max of the variable-length type, since SqlClient cuts a value to the
    // Size with no error: the value sent is tested, so a converted parameter is tested as converted.
    private static void AppendOverLong(
        StringBuilder builder,
        string indent,
        string parameter,
        string valueText,
        int size,
        string? wider
    )
    {
        var inner = indent + GeneratedText.Indent;
        _ = builder
            .Append(indent)
            .Append("if (")
            .Append(parameter)
            .Append(".Value is ")
            .Append(valueText)
            .Append(" { Length: > ")
            .Append(size.ToString(CultureInfo.InvariantCulture))
            .Append(" })\n")
            .Append(indent)
            .Append("{\n");
        if (wider is not null)
        {
            AppendSqlDbType(builder, inner, parameter, wider);
        }

        AppendSet(builder, inner, parameter, "Size", -1);
        _ = builder.Append(indent).Append("}\n");
    }

    // SqlClient takes a Scale of 0 as not set and sends a decimal at its own scale: 12345.00 for a numeric(5,0) as a
    // decimal(5,2), which it refuses as out of range, and 1.5 for a decimal(12,0) as a decimal(12,1), which a WHERE
    // compares unrounded.  So the value is rounded half away from zero, as the server rounds it on an assignment, and
    // its scale is 0.  The value sent is tested, so a converted parameter is rounded as converted.  The type pattern
    // declares no variable, whose name could clash with a parameter of the method or a local of a converted value.
    private static void AppendRoundedToScale0(StringBuilder builder, string indent, string parameter) =>
        _ = builder
            .Append(indent)
            .Append("if (")
            .Append(parameter)
            .Append(".Value is decimal)\n")
            .Append(indent)
            .Append("{\n")
            .Append(indent)
            .Append(GeneratedText.Indent)
            .Append(parameter)
            .Append(".Value = decimal.Round((decimal)")
            .Append(parameter)
            .Append(".Value, global::System.MidpointRounding.AwayFromZero);\n")
            .Append(indent)
            .Append("}\n");

    private static Dictionary<string, Row> BuildRows()
    {
        var rows = new Dictionary<string, Row>(StringComparer.OrdinalIgnoreCase);

        void Add(string sqlDbType, params string[] names)
        {
            foreach (var name in names)
            {
                rows.Add(name, new Row(sqlDbType, Facet.None));
            }
        }

        rows.Add("char", new Row("Char", Facet.Bytes, "string", "VarChar"));
        rows.Add("varchar", new Row("VarChar", Facet.Bytes, "string"));
        rows.Add("nchar", new Row("NChar", Facet.Characters, "string", "NVarChar"));
        rows.Add("nvarchar", new Row("NVarChar", Facet.Characters, "string"));
        rows.Add("sysname", new Row("NVarChar", Facet.Characters, "string"));
        rows.Add("binary", new Row("Binary", Facet.Bytes, "byte[]", "VarBinary"));
        rows.Add("varbinary", new Row("VarBinary", Facet.Bytes, "byte[]"));
        rows.Add("decimal", new Row("Decimal", Facet.PrecisionAndScale));
        rows.Add("numeric", new Row("Decimal", Facet.PrecisionAndScale));
        rows.Add("datetime2", new Row("DateTime2", Facet.Scale7));
        rows.Add("time", new Row("Time", Facet.Scale7));
        rows.Add("datetimeoffset", new Row("DateTimeOffset", Facet.Scale7));
        rows.Add("json", new Row("NVarChar", Facet.Max));
        rows.Add("vector", new Row(null, Facet.Vector));
        foreach (var name in SqlServerNames.ClrSystemTypes)
        {
            rows.Add(name, new Row("Udt", Facet.None, UdtTypeName: name));
        }

        Add("Timestamp", "timestamp", "rowversion");
        Add("BigInt", "bigint");
        Add("Int", "int");
        Add("SmallInt", "smallint");
        Add("TinyInt", "tinyint");
        Add("Bit", "bit");
        Add("Float", "float");
        Add("Real", "real");
        Add("Money", "money");
        Add("SmallMoney", "smallmoney");
        Add("Date", "date");
        Add("DateTime", "datetime");
        Add("SmallDateTime", "smalldatetime");
        Add("UniqueIdentifier", "uniqueidentifier");
        Add("Text", "text");
        Add("NText", "ntext");
        Add("Xml", "xml");
        Add("Image", "image");
        Add("Variant", "sql_variant");
        return rows;
    }

    // What a type's binding sets besides its SqlDbType.
    private enum Facet
    {
        // Nothing.
        None,

        // Size, the length in bytes, -1 for max.
        Bytes,

        // Size, the length in characters, half the bytes, -1 for max.
        Characters,

        // Precision and Scale, the type's, and for scale 0 the value rounded to a whole number.
        PrecisionAndScale,

        // Scale 7, whatever the type's.
        Scale7,

        // Size -1.
        Max,

        // Nothing: the value gives the type and its dimension, and a null is the null vector of the type's dimension.
        Vector,
    }

    // A base name's binding: the member of SqlDbType, null for vector; what else it sets; for a type with a length,
    // the C# type of a value that can be longer than it; the variable-length type that an over-long value of a
    // fixed-length type is bound as; and the UdtTypeName of a CLR type.
    private sealed record Row(
        string? SqlDbType,
        Facet Facet,
        string? ValueText = null,
        string? Wider = null,
        string? UdtTypeName = null
    );
}
