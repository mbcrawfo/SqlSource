namespace SqlSource.Snapshot;

/// <summary>The kinds of a PostgreSQL type that the format lists.</summary>
internal enum PostgresTypeKind
{
    Base,
    Array,
    Domain,
    Enum,
    Range,
    Multirange,
    Composite,
}
