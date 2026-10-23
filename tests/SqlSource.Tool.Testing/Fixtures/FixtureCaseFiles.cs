using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Capture;

namespace SqlSource.Tool.Testing.Fixtures;

// Writes the files of a case of a describer's fixtures, laid out as FixtureCases reads them: a folder of the engine's
// under a root, holding a folder for each case.
internal static class FixtureCaseFiles
{
    // A case of the engine under the root, with its query.sql.
    public static FixtureCase Create(string root, string engine, string name, string query)
    {
        var folder = Directory.CreateDirectory(Path.Combine(root, engine, name)).FullName;
        File.WriteAllText(Path.Combine(folder, FixtureCase.QueryFile), query);
        return new FixtureCase(engine, name, folder);
    }

    public static Task WriteCaptureAsync(
        FixtureCase fixture,
        DescribeCapture capture,
        CancellationToken cancellationToken
    ) =>
        File.WriteAllTextAsync(
            Path.Combine(fixture.Folder, FixtureCase.CaptureFile),
            CaptureJson.Write(capture),
            cancellationToken
        );

    // The two files a recording writes: what was asked and answered, and the sidecar described from it.  A capture
    // that holds the connection's value or a password of it fails the test, and nothing is written: a recording is
    // committed.
    public static async Task WriteRecordingAsync(
        FixtureCase fixture,
        DescribeCapture capture,
        string expected,
        ConnectionSecret secret,
        CancellationToken cancellationToken
    )
    {
        var written = CaptureJson.Write(capture, secret);
        if (!written.Succeeded)
        {
            throw new InvalidOperationException(
                $"The capture of the case {fixture} is not written.  {written.Failure}"
            );
        }

        await File.WriteAllTextAsync(
            Path.Combine(fixture.Folder, FixtureCase.CaptureFile),
            written.Value,
            cancellationToken
        );
        await File.WriteAllTextAsync(
            Path.Combine(fixture.Folder, FixtureCase.ExpectedFile),
            expected,
            cancellationToken
        );
    }
}
