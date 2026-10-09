namespace SqlSource.Snapshot;

/// <summary>A SQL Server type, with the three facets as the server gives them for every type.</summary>
/// <param name="Name">The system type name, verbatim.</param>
/// <param name="MaxLength">The length in bytes, <c>-1</c> for <c>max</c>.</param>
/// <param name="Precision">The precision.</param>
/// <param name="Scale">The scale.</param>
/// <param name="UserType">The alias or CLR type, or null.</param>
internal sealed record SqlServerType(string Name, int MaxLength, int Precision, int Scale, SqlServerUserType? UserType)
    : SidecarType(Name);
