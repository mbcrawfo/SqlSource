namespace SqlSource.Snapshot;

/// <summary>
/// Writes a sidecar as the text of its file, in the layout and the key order of the sidecar format design.  What it
/// gives is a contract with every committed file: a change here is a change to the format design, to the schema and
/// to the examples.
/// </summary>
/// <remarks>
/// A key the format marks "always" is written with <c>null</c> when it has no value.  A key with a condition is
/// written when the condition holds, whatever the model holds: an entry without rows has no <c>columns</c>, and a
/// parameter without a type no <c>typeSource</c>.  A facet is written when it has a value.  The writer checks
/// nothing and never throws: a model the schema would refuse is the caller's mistake.
/// </remarks>
internal static class SidecarWriter
{
    public static string Write(Sidecar sidecar)
    {
        var json = new SidecarJsonBuilder();
        json.BeginObject(null);
        json.WriteString(SidecarKeys.Warning, SidecarFormat.Warning);
        json.WriteString(SidecarKeys.SchemaUrl, SidecarFormat.SchemaId);
        json.WriteNumber(SidecarKeys.FormatVersion, sidecar.FormatVersion);
        json.WriteString(SidecarKeys.ToolVersion, sidecar.ToolVersion);
        json.BeginObject(SidecarKeys.Queries);
        foreach (var entry in sidecar.Queries)
        {
            WriteEntry(json, entry);
        }

        json.EndObject();
        json.EndObject();
        return json.ToText();
    }

    private static void WriteEntry(SidecarJsonBuilder json, SidecarEntry entry)
    {
        var rows = entry.ResultKind == SidecarResultKind.Rows;
        json.BeginObject(entry.Name);
        json.WriteString(SidecarKeys.Hash, entry.Hash);
        json.WriteString(SidecarKeys.Engine, entry.Engine);
        json.WriteString(SidecarKeys.Database, entry.Database);
        json.WriteString(SidecarKeys.ServerVersion, entry.ServerVersion);
        json.WriteString(SidecarKeys.ResultKind, rows ? SidecarValues.ResultKind.Rows : SidecarValues.ResultKind.None);
        if (rows)
        {
            WriteTable(json, entry.MatchesTable);
            json.WriteString(SidecarKeys.Plan, entry.Plan);
            json.WriteString(SidecarKeys.TableMatch, entry.TableMatch);
        }

        json.BeginArray(SidecarKeys.Parameters);
        foreach (var parameter in entry.Parameters)
        {
            WriteParameter(json, parameter);
        }

        json.EndArray();
        if (rows)
        {
            WriteColumns(json, entry.Columns);
        }

        json.EndObject();
    }

    private static void WriteTable(SidecarJsonBuilder json, SidecarTable? table)
    {
        if (table is null)
        {
            json.WriteNull(SidecarKeys.MatchesTable);
            return;
        }

        json.BeginObject(SidecarKeys.MatchesTable);
        json.WriteString(SidecarKeys.Schema, table.Schema);
        json.WriteString(SidecarKeys.Table, table.Table);
        json.EndObject();
    }

    private static void WriteParameter(SidecarJsonBuilder json, SidecarParameter parameter)
    {
        json.BeginObject(null);
        json.WriteString(SidecarKeys.Name, parameter.Name);
        json.WriteNumber(SidecarKeys.Ordinal, parameter.Ordinal);
        WriteType(json, SidecarKeys.Type, parameter.Type);
        json.WriteBoolean(SidecarKeys.Nullable, parameter.Nullable);
        if (parameter.Type is not null)
        {
            json.WriteString(SidecarKeys.TypeSource, parameter.TypeSource);
        }

        json.EndObject();
    }

    private static void WriteColumns(SidecarJsonBuilder json, EquatableArray<SidecarColumn>? columns)
    {
        if (columns is not { } list)
        {
            json.WriteNull(SidecarKeys.Columns);
            return;
        }

        json.BeginArray(SidecarKeys.Columns);
        foreach (var column in list)
        {
            WriteColumn(json, column);
        }

        json.EndArray();
    }

    private static void WriteColumn(SidecarJsonBuilder json, SidecarColumn column)
    {
        json.BeginObject(null);
        json.WriteNumber(SidecarKeys.Ordinal, column.Ordinal);
        json.WriteString(SidecarKeys.Name, column.Name);
        WriteType(json, SidecarKeys.Type, column.Type);
        json.WriteBoolean(SidecarKeys.Nullable, column.Nullable);
        json.WriteString(SidecarKeys.NullableSource, column.NullableSource);
        if (column.Origin is { } origin)
        {
            json.BeginObject(SidecarKeys.Origin);
            json.WriteString(SidecarKeys.Schema, origin.Schema);
            json.WriteString(SidecarKeys.Table, origin.Table);
            json.WriteString(SidecarKeys.Column, origin.Column);
            json.EndObject();
        }
        else
        {
            json.WriteNull(SidecarKeys.Origin);
        }

        json.WriteBoolean(SidecarKeys.Identity, column.Identity);
        json.WriteBoolean(SidecarKeys.Computed, column.Computed);
        json.EndObject();
    }

    private static void WriteType(SidecarJsonBuilder json, string key, SidecarType? type)
    {
        switch (type)
        {
            case null:
                json.WriteNull(key);
                break;
            case PostgresType postgres:
                WritePostgres(json, key, postgres);
                break;
            case SqlServerType sqlServer:
                WriteSqlServer(json, key, sqlServer);
                break;
            default:
                json.BeginObject(key);
                json.WriteString(SidecarKeys.Name, type.Name);
                json.EndObject();
                break;
        }
    }

    private static void WritePostgres(SidecarJsonBuilder json, string key, PostgresType type)
    {
        json.BeginObject(key);
        json.WriteString(SidecarKeys.Name, type.Name);
        json.WriteString(SidecarKeys.Kind, type.Kind);
        json.WriteString(SidecarKeys.Schema, type.Schema);
        json.WriteString(SidecarKeys.InternalName, type.InternalName);
        WriteFacet(json, SidecarKeys.Length, type.Length);
        WriteFacet(json, SidecarKeys.Precision, type.Precision);
        WriteFacet(json, SidecarKeys.Scale, type.Scale);
        switch (type.Kind)
        {
            case SidecarValues.Kind.Array:
                WriteType(json, SidecarKeys.Element, type.Element);
                break;
            case SidecarValues.Kind.Domain:
                WriteType(json, SidecarKeys.Base, type.Base);
                break;
            case SidecarValues.Kind.Enum:
                WriteLabels(json, type.Labels);
                break;
            case SidecarValues.Kind.Range or SidecarValues.Kind.Multirange:
                WriteType(json, SidecarKeys.Subtype, type.Subtype);
                break;
            default:
                break;
        }

        json.EndObject();
    }

    private static void WriteFacet(SidecarJsonBuilder json, string key, int? value)
    {
        if (value is not null)
        {
            json.WriteNumber(key, value);
        }
    }

    private static void WriteLabels(SidecarJsonBuilder json, EquatableArray<string>? labels)
    {
        if (labels is not { } list)
        {
            json.WriteNull(SidecarKeys.Labels);
            return;
        }

        json.BeginArray(SidecarKeys.Labels);
        foreach (var label in list)
        {
            json.WriteString(null, label);
        }

        json.EndArray();
    }

    private static void WriteSqlServer(SidecarJsonBuilder json, string key, SqlServerType type)
    {
        json.BeginObject(key);
        json.WriteString(SidecarKeys.Name, type.Name);
        json.WriteNumber(SidecarKeys.MaxLength, type.MaxLength);
        json.WriteNumber(SidecarKeys.Precision, type.Precision);
        json.WriteNumber(SidecarKeys.Scale, type.Scale);
        if (type.UserType is { } userType)
        {
            json.BeginObject(SidecarKeys.UserType);
            json.WriteString(SidecarKeys.Schema, userType.Schema);
            json.WriteString(SidecarKeys.Name, userType.Name);
            if (userType.AssemblyQualifiedName is not null)
            {
                json.WriteString(SidecarKeys.AssemblyQualifiedName, userType.AssemblyQualifiedName);
            }

            json.EndObject();
        }

        json.EndObject();
    }
}
