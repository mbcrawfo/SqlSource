using System.Diagnostics;
using System.Threading.Tasks;
using SqlSource.Repository;
using SqlSource.Testing;
using SqlSource.Tool.Testing;

namespace SqlSource.DatabaseTests.Postgres;

// Runs tools/build-postgis-image.sh, or a copy of it, for PostgisImageScriptTests and PostgisImageCheckTests.
internal static class PostgisImageScript
{
    public static Task<ProgramResult> RunAsync(params string[] arguments) =>
        RunCopyAsync(
            RepositoryRoot.Path("tools", "build-postgis-image.sh"),
            RepositoryRoot.WorkingFolder.Path,
            arguments
        );

    // Runs the script at the path given, from the folder given, as a test runs a copy of the repository's.
    public static Task<ProgramResult> RunCopyAsync(string script, string folder, params string[] arguments) =>
        Programs.RunAsync(
            new ProcessStartInfo("bash", [script, .. arguments]) { WorkingDirectory = folder },
            Eventually.Patience
        );
}
