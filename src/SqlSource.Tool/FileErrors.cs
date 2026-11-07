using System;
using System.IO;

namespace SqlSource.Tool;

/// <summary>
/// What the tool treats as an error of a path it was given.
/// </summary>
internal static class FileErrors
{
    /// <summary>
    /// Whether opening a path threw for a reason of the path: an empty path, a directory, a folder that is not there,
    /// or a file the system will not open.
    /// </summary>
    public static bool IsPathError(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;
}
