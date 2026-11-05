using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace SqlSource.Tool.Bundles;

/// <summary>
/// Writes a bundle as a zip that <see cref="BundleReader" /> reads.
/// </summary>
/// <remarks>
/// Each file is at the zip's root, in a fixed order: <c>bundle.json</c>, <c>query.sql</c>, <c>capture.json</c>, then
/// <c>expected.json</c> and <c>explain-settings.txt</c> when the bundle has them.  Each is UTF-8 with no byte order
/// mark, and has one time, the earliest a zip holds, so two writes of one bundle are the same bytes.
/// </remarks>
internal static class BundleWriter
{
    // The earliest time of a zip's entry; the zip keeps it with no offset.
    private static readonly DateTimeOffset EntryTime = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Writes the bundle into the stream as a zip, and leaves the stream open.</summary>
    /// <exception cref="ArgumentException">
    /// The bundle has no <see cref="Bundle.Info" />, which a zip must hold.
    /// </exception>
    public static void Write(Bundle bundle, Stream into)
    {
        if (bundle.Info is null)
        {
            throw new ArgumentException("A zip of a bundle holds its bundle.json.", nameof(bundle));
        }

        using var zip = new ZipArchive(into, ZipArchiveMode.Create, leaveOpen: true);
        Add(zip, BundleFiles.Info, BundleJson.Write(bundle.Info));
        Add(zip, BundleFiles.Query, bundle.Query);
        Add(zip, BundleFiles.Capture, bundle.Capture);
        if (bundle.Expected is not null)
        {
            Add(zip, BundleFiles.Expected, bundle.Expected);
        }

        if (bundle.ExplainSettings.Count > 0)
        {
            Add(zip, BundleFiles.ExplainSettings, string.Join('\n', bundle.ExplainSettings) + "\n");
        }
    }

    private static void Add(ZipArchive zip, string name, string text)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        entry.LastWriteTime = EntryTime;
        using var stream = entry.Open();
        var bytes = Utf8.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }
}
