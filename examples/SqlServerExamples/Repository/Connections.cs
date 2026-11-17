using SqlSource;

namespace SqlServerExamples.Repository;

// The connection string of the application's database, marked once here, so that every repository of the project
// with no connection of its own, such as OrderRepository, uses it.  It has no Database, so it serves every logical
// database.  The application sets it once at startup, from its configuration, before a repository method runs; until
// then a method throws InvalidOperationException.  UserRepository keeps its own connection string, which wins.  See
// site/reference/repository-connections.md, "A static member of the project".
public static class Connections
{
    [SqlSourceConnection]
    public static string? ConnectionString { get; set; }
}
