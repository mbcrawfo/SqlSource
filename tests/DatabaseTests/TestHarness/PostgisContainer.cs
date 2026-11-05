using System;
using System.Data.Common;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using SqlSource.Testing;

namespace SqlSource.DatabaseTests.TestHarness;

// A PostgreSQL with PostGIS, for the NetTopologySuite tests alone, beside the project's other container: the image of
// Postgis/Dockerfile built on the postgres image the subclass names, with PostGIS at a pinned package version, so that
// it runs the same PostgreSQL as the project's other container.  The image is named by both versions and kept after
// the run, so that a later run on the same versions reuses it and Docker's cache rebuilds only what changed.
public abstract class PostgisContainer(DbProviderFactory factory, string postgresImage)
    : PostgresContainer(factory, ImageOf(postgresImage))
{
    // The package of PostGIS that the image installs, from the base image's pgdg source or pgdg's archive.
    // tools/describe-end-to-end.sh reads this line by its shape, with sed, to build the same image.
    public const string Package = "3.6.4+dfsg-2.pgdg13+1";

    // The image built on the postgres image: sqlsource-postgis:18.6-3.6.4 on postgres:18.6.
    public static string ImageOf(string postgresImage) =>
        $"sqlsource-postgis:{EngineImage.TagOf(postgresImage)}-{Package.Split('+')[0]}";

    // Builds the image through Docker, which reuses its cached layers.  It is never removed: WithCleanUp(false) keeps
    // Testcontainers' reaper from it, and the image is not disposed, which would delete it.  Testcontainers writes the
    // build's context to a tar in the temp folder named after the image, so two test projects that build one image at
    // once write and delete the same file; a lock file held for the build makes the second wait, and then reuse the
    // first's layers.
    protected override async Task PrepareImageAsync(CancellationToken token)
    {
        await using var buildLock = await LockBuildsAsync(
            Path.Combine(Path.GetTempPath(), "sqlsource-postgis-build.lock"),
            BuildPatience
        );
        var image = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(RepositoryRoot.Path("tests", "DatabaseTests", "TestHarness", "Postgis"))
            .WithDockerfile("Dockerfile")
            .WithName(ImageOf(postgresImage))
            .WithBuildArgument("PG", EngineImage.TagOf(postgresImage))
            .WithBuildArgument("POSTGIS", Package)
            .WithDeleteIfExists(false)
            .WithCleanUp(false)
            .Build();
        await image.CreateAsync(token);
    }

    // How long a build waits for another test process's build of the image to let go of the lock.  A build on a cold
    // cache pulls the postgres image and PostGIS's packages, a few minutes on CI's runner, so fifteen minutes, as long
    // as a CI job that runs the tests may take, outlasts any build that is still going and ends one that hangs.
    private static readonly TimeSpan BuildPatience = TimeSpan.FromMinutes(15);

    // FileShare.None locks the file against every other process, and every other handle of this one, until it is
    // disposed.  The file is left in place, since deleting it would let a waiting build lock a new file beside it.
    internal static async Task<FileStream> LockBuildsAsync(string path, TimeSpan patience)
    {
        FileStream? held = null;
        await Eventually.TrueAsync(
            () =>
            {
                held = TryLock(path);
                return Task.FromResult(held is not null);
            },
            $"Another build of the PostGIS image held {path} for longer than {patience}.",
            patience
        );
        return held ?? throw new InvalidOperationException("The wait for the lock ended without it.");
    }

    // The lock, or null when another handle holds it.
    private static FileStream? TryLock(string path)
    {
        try
        {
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException exception) when (IsHeldByAnother(exception))
        {
            return null;
        }
    }

    // Only a lock that another handle holds is waited for; any other error fails the build at once.  On Unix .NET gives
    // that sharing violation as a plain IOException whose HResult is EWOULDBLOCK's errno, 35 on macOS and 11 on Linux;
    // on Windows it is ERROR_SHARING_VIOLATION as an HRESULT.
    private static bool IsHeldByAnother(IOException exception)
    {
        if (OperatingSystem.IsWindows())
        {
            return exception.HResult == unchecked((int)0x80070020);
        }

        return exception.HResult == (OperatingSystem.IsMacOS() ? 35 : 11);
    }
}
