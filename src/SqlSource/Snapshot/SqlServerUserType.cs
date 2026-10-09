namespace SqlSource.Snapshot;

/// <summary>An alias type or a CLR type of SQL Server.</summary>
internal sealed record SqlServerUserType(string? Schema, string? Name, string? AssemblyQualifiedName);
