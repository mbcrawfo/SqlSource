using System.Linq;
using SqlSource.Tool.Planning;
using Xunit;

namespace SqlSource.Tool.Tests;

// The plan of a project that a test builds, for a test of what comes after the plan.
internal static class Plans
{
    // A type that claims every .sql file in the folder of its own source file.
    public const string Type = "[SqlSource.SqlSourceGenerate]\ninternal static partial class Queries;\n";

    // A project whose dialect is postgres, with that type at its root: a .sql file added at the root is claimed.
    public static TestProject Postgres(TempFolder folder, string name = "App", string? directory = null)
    {
        var project = new TestProject(folder, name, directory);
        project.Properties["SqlSourceDialect"] = "postgres";
        _ = project.AddSource("Queries.cs", Type);
        return project;
    }

    public static RunPlan Of(params TestProject[] projects) => Of(RunFilters.None, projects);

    // The plan, whatever is wrong with it: a test of a query with a problem wants the plan all the same.
    public static RunPlan Of(RunFilters filters, params TestProject[] projects) =>
        RunPlanner
            .Plan([.. projects.Select(project => project.Manifest())], filters, TestContext.Current.CancellationToken)
            .Plan;

    // The first query of a file that holds this SQL, in a postgres project of its own.
    public static PlannedQuery Query(TempFolder folder, string sql)
    {
        var project = Postgres(folder);
        _ = project.AddSql("Q.sql", sql);
        return Of(project).Files[0].Queries[0];
    }
}
