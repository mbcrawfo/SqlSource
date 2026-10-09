namespace SqlSource.Snapshot;

/// <summary>The keys of the format.  The reader, the writer and the comparer all name a key through here.</summary>
internal static class SidecarKeys
{
    public const string Warning = "_WARNING";
    public const string SchemaUrl = "$schema";
    public const string FormatVersion = "formatVersion";
    public const string ToolVersion = "toolVersion";
    public const string Queries = "queries";

    public const string Hash = "hash";
    public const string Engine = "engine";
    public const string Database = "database";
    public const string ServerVersion = "serverVersion";
    public const string ResultKind = "resultKind";
    public const string MatchesTable = "matchesTable";
    public const string Plan = "plan";
    public const string TableMatch = "tableMatch";
    public const string Parameters = "parameters";
    public const string Columns = "columns";

    public const string Name = "name";
    public const string Ordinal = "ordinal";
    public const string Type = "type";
    public const string Nullable = "nullable";
    public const string TypeSource = "typeSource";
    public const string NullableSource = "nullableSource";
    public const string Origin = "origin";
    public const string Identity = "identity";
    public const string Computed = "computed";

    public const string Schema = "schema";
    public const string Table = "table";
    public const string Column = "column";

    public const string Kind = "kind";
    public const string InternalName = "internalName";
    public const string Length = "length";
    public const string Precision = "precision";
    public const string Scale = "scale";
    public const string Element = "element";
    public const string Base = "base";
    public const string Labels = "labels";
    public const string Subtype = "subtype";

    public const string MaxLength = "maxLength";
    public const string UserType = "userType";
    public const string AssemblyQualifiedName = "assemblyQualifiedName";
}
