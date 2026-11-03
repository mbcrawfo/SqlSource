using System.Linq;
using SqlSource.Tool.Planning;
using Xunit;

namespace SqlSource.Tool.Testing;

// The plan of a project that a test builds, for a test of what comes after the plan.
internal static class Plans
{
    // A type that claims every .sql file in the folder of its own source file.
    public const string Type =
        "[SqlSource.SqlSourceGenerate(Source = \".\")]\ninternal static partial class Queries;\n";

    // A type with the attribute and these arguments, in a file of its own.  Unless they set Source, it has
    // Source = "." too, and claims every .sql file in the folder of its source file.
    public static string TypeNamed(string name, string arguments = "") =>
        $"[SqlSource.SqlSourceGenerate({WithFolder(arguments)})]\ninternal static partial class {name};\n";

    private static string WithFolder(string arguments)
    {
        if (arguments.Contains("Source =", System.StringComparison.Ordinal))
        {
            return arguments;
        }

        return arguments.Length == 0 ? "Source = \".\"" : "Source = \".\", " + arguments;
    }

    // A project whose settings file says the dialect is postgres, with that type at its root: a .sql file added at the
    // root is claimed.
    public static TestProject Postgres(TempFolder folder, string name = "App", string? directory = null)
    {
        var project = new TestProject(folder, name, directory);
        project.Settings["dialect"] = "postgres";
        _ = project.AddSource("Queries.cs", Type);
        return project;
    }

    public static RunPlan Of(params TestProject[] projects) => Of(RunFilters.None, projects);

    // The plan, whatever is wrong with it: a test of a query with a problem wants the plan all the same.
    public static RunPlan Of(RunFilters filters, params TestProject[] projects) => Result(filters, projects).Plan;

    public static RunPlanResult Result(params TestProject[] projects) => Result(RunFilters.None, projects);

    // The plan with what is wrong with it, for a test of the planner itself.
    public static RunPlanResult Result(RunFilters filters, params TestProject[] projects) =>
        RunPlanner.Plan(
            [.. projects.Select(project => project.Manifest())],
            filters,
            TestContext.Current.CancellationToken
        );

    // The first query of a file that holds this SQL, in a postgres project of its own.
    public static PlannedQuery Query(TempFolder folder, string sql)
    {
        var project = Postgres(folder);
        _ = project.AddSql("Q.sql", sql);
        return Of(project).Files[0].Queries[0];
    }
}
