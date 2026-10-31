using System.Data.Common;
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
    // Testcontainers' reaper from it, and the image is not disposed, which would delete it.
    protected override async Task PrepareImageAsync(CancellationToken token)
    {
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
}
