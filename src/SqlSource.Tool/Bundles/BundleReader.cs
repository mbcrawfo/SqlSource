using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Bundles;

/// <summary>
/// Reads a bundle: a zip, or a folder that holds a bundle's files, as an unzipped bundle and a fixture's case do.
/// </summary>
/// <remarks>
/// A zip holds every file at its root and must hold <c>bundle.json</c>, <c>query.sql</c> and <c>capture.json</c>.  A
/// folder must hold <c>query.sql</c> and <c>capture.json</c>, and its <c>bundle.json</c> is optional, so a recorded
/// fixture reads.  In both, <c>expected.json</c> and <c>explain-settings.txt</c> are optional.  A <c>bundle.json</c> is
/// read before the files are looked for, so that a bundle of a newer form is named as one.  Every error is
/// <c>SQLSRC288</c> or <c>SQLSRC289</c> at the bundle's path.
/// </remarks>
internal static class BundleReader
{
    private const string Neither = "it is neither a zip nor a folder that holds query.sql and capture.json";

    /// <summary>
    /// The bundle at a full path, or the error that says why it cannot be read.
    /// </summary>
    /// <param name="fullPath">The full path of the zip or of the folder.</param>
    /// <param name="openReadFile">The host's <c>OpenReadFile</c>: the file opened for reading, or null.</param>
    /// <param name="readTextFile">The host's <c>ReadTextFile</c>: the text of a file, or null.</param>
    public static Outcome<Bundle, ToolDiagnostic> Read(
        string fullPath,
        Func<string, Stream?> openReadFile,
        Func<string, string?> readTextFile
    )
    {
        try
        {
            using var file = openReadFile(fullPath);
            return file is null
                ? ReadFiles(fullPath, name => readTextFile(Path.Combine(fullPath, name)), zip: false)
                : ReadZip(fullPath, file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failed(fullPath, exception.Message);
        }
    }

    // A zip that cannot be read throws as it is opened, and one with a damaged file as that file is read.
    private static Outcome<Bundle, ToolDiagnostic> ReadZip(string fullPath, Stream file)
    {
        try
        {
            using var zip = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: true);
            return ReadFiles(fullPath, name => TextOf(zip.GetEntry(name)), zip: true);
        }
        catch (InvalidDataException exception)
        {
            return Failed(fullPath, $"it is not a zip that can be read: {exception.Message}");
        }
    }

    // The bundle from its files, each by its name.  A zip needs its bundle.json and names the file it misses at its
    // root; a folder needs none, and a folder with neither query.sql nor capture.json, or a path with nothing at it, is
    // no bundle at all.
    private static Outcome<Bundle, ToolDiagnostic> ReadFiles(string fullPath, Func<string, string?> textOf, bool zip)
    {
        BundleInfo? info = null;
        if (textOf(BundleFiles.Info) is { } infoText)
        {
            var read = BundleJson.Read(infoText, fullPath);
            if (!read.Succeeded)
            {
                return Outcome<Bundle, ToolDiagnostic>.Failed(read.Failure);
            }

            info = read.Value;
        }

        var query = textOf(BundleFiles.Query);
        var capture = textOf(BundleFiles.Capture);
        if (MissingOf(query, capture, info, zip) is { } missing)
        {
            return Failed(fullPath, missing);
        }

        return Outcome<Bundle, ToolDiagnostic>.Of(
            new Bundle(
                info,
                query!,
                capture!,
                textOf(BundleFiles.Expected),
                SettingsOf(textOf(BundleFiles.ExplainSettings))
            )
        );
    }

    // Why the files read are no bundle, or null when they are one.
    private static string? MissingOf(string? query, string? capture, BundleInfo? info, bool zip)
    {
        string missing;
        if (query is null)
        {
            missing = BundleFiles.Query;
        }
        else if (capture is null)
        {
            missing = BundleFiles.Capture;
        }
        else if (info is null && zip)
        {
            missing = BundleFiles.Info;
        }
        else
        {
            return null;
        }

        if (zip)
        {
            return $"there is no {missing} at the root of the zip";
        }

        return query is null && capture is null ? Neither : $"there is no {missing} in the folder";
    }

    // The text of an entry of the zip, read as UTF-8, or null when there is none.
    private static string? TextOf(ZipArchiveEntry? entry)
    {
        if (entry is null)
        {
            return null;
        }

        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    // The lines of explain-settings.txt, as FixtureCase.ExplainSettings reads them: trimmed, without the blank ones.
    private static List<string> SettingsOf(string? text)
    {
        List<string> settings = [];
        if (text is null)
        {
            return settings;
        }

        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            var setting = line.Trim();
            if (setting.Length > 0)
            {
                settings.Add(setting);
            }
        }

        return settings;
    }

    private static Outcome<Bundle, ToolDiagnostic> Failed(string fullPath, string reason) =>
        Outcome<Bundle, ToolDiagnostic>.Failed(BundleErrors.CannotBeRead(fullPath, reason));
}
