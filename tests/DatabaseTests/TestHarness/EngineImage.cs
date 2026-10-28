using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace SqlSource.DatabaseTests.TestHarness;

// What the tests of an engine's containers share: the image they run on, which an environment variable can change, the
// majors that the tests pin answers for, the gates that skip a test on a server whose version it does not suit, and
// the start of a container.  The default image of each is in DefaultImages.
internal sealed class EngineImage
{
    private EngineImage(
        string product,
        string variable,
        string defaultImage,
        IReadOnlyList<int> recordedMajors,
        bool retriesCrashAtStart
    )
    {
        Product = product;
        Variable = variable;
        Default = defaultImage;
        RecordedMajors = recordedMajors;
        RetriesCrashAtStart = retriesCrashAtStart;
    }

    public static EngineImage Postgres { get; } =
        new("PostgreSQL", "SQLSOURCE_TESTS_POSTGRES_IMAGE", DefaultImages.Postgres, [14, 18], false);

    public static EngineImage SqlServer { get; } =
        new("SQL Server", "SQLSOURCE_TESTS_SQLSERVER_IMAGE", DefaultImages.SqlServer, [14, 17], true);

    // The name of the engine in a message.
    public string Product { get; }

    // Names another image: CI runs the tests again on the oldest supported server, and the nightly run on each major
    // between it and the default.
    public string Variable { get; }

    public string Default { get; }

    // The majors that CI runs and that the tests pin answers for: a realistic case's committed entry, the noise budget
    // and, for PostgreSQL, the forms of a plan.
    public IReadOnlyList<int> RecordedMajors { get; }

    // Whether a container of the engine is started again after sqlservr's crash at its start (TD-0072): SQL Server's.
    public bool RetriesCrashAtStart { get; }

    // The image the tests run on, read each time since a test of the variable sets it.
    public string Image => Environment.GetEnvironmentVariable(Variable) is { Length: > 0 } image ? image : Default;

    // The tag of an image, which for a pinned image names the server's version or update: "postgres:18.6" is "18.6".
    public static string TagOf(string image) => image[(image.LastIndexOf(':') + 1)..];

    // Skips the test on a server before that major, which lacks what the reason names.
    public static void SkipBelow(int server, int major, string reason) => Assert.SkipWhen(server < major, reason);

    // Skips the test on a server whose major is not recorded, where what it pins is not known.
    public void SkipUnlessRecordedMajor(int server, string what) =>
        Assert.SkipUnless(
            RecordedMajors.Contains(server),
            string.Create(
                CultureInfo.InvariantCulture,
                $"{what} are pinned for {Product} {string.Join(" and ", RecordedMajors)} alone."
            )
        );

    // Builds the container and starts it, and returns it started.  Xunit does not dispose a fixture whose start threw,
    // so a container that fails to start is disposed before the exception leaves.  SQL Server's goes through
    // SqlServerStart, which also starts it again when sqlservr crashed (TD-0072).
    public async Task<TContainer> StartAsync<TContainer>(Func<TContainer> build, CancellationToken token)
        where TContainer : IContainer
    {
        if (RetriesCrashAtStart)
        {
            return await SqlServerStart.StartAsync(build, token);
        }

        return (await StartOnceAsync(() => new Testcontainer<TContainer>(build()), token)).Container;
    }

    // Builds a container and starts it, once, and disposes it before the exception leaves when the start throws.
    internal static async Task<TStart> StartOnceAsync<TStart>(Func<TStart> build, CancellationToken token)
        where TStart : IStartingContainer
    {
        var container = build();
        try
        {
            await container.StartAsync(token);
            return container;
        }
        catch
        {
            await container.DisposeAsync();
            throw;
        }
    }
}
