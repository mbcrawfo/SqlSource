using System;
using System.IO;

namespace SqlSource.Tool;

/// <summary>
/// A full path with every symbolic link on the way replaced by what it leads to, so that a path through a link and the
/// real path of one file compare alike.  The working directory that the system gives is the real path, and a path
/// typed through a link to it, as a shell's <c>$PWD</c> gives, is not.
/// </summary>
internal static class RealPath
{
    // As the limit of links that Linux follows in one path, so that a loop of links ends.
    private const int MaxLinks = 40;

    private static readonly char[] Separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    /// <summary>
    /// The path with each link among its folders, and the file itself, replaced by its target, whose own links are
    /// replaced too.  Its part from the first name that does not exist is kept as text.  A loop of links, or a part
    /// the tool may not read, gives the path back as it is.
    /// </summary>
    /// <param name="fullPath">A full path, as <c>Path.GetFullPath</c> gives it.</param>
    public static string Of(string fullPath)
    {
        var links = 0;
        var path = fullPath;
        var current = Path.GetPathRoot(path);
        if (string.IsNullOrEmpty(current))
        {
            return fullPath;
        }

        var names = path[current.Length..].Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        var index = 0;
        while (index < names.Length)
        {
            var next = Path.Join(current, names[index]);
            FileSystemInfo? target;
            try
            {
                if (!Path.Exists(next) && new FileInfo(next).LinkTarget is null)
                {
                    return Path.Join([next, .. names[(index + 1)..]]);
                }

                target = File.ResolveLinkTarget(next, returnFinalTarget: false);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return fullPath;
            }

            if (target is null)
            {
                current = next;
                index++;
                continue;
            }

            if (++links > MaxLinks)
            {
                return fullPath;
            }

            // The target's own folders may hold links, so the walk starts again from its root.
            path = Path.Join([Path.GetFullPath(target.FullName), .. names[(index + 1)..]]);
            current = Path.GetPathRoot(path)!;
            names = path[current.Length..].Split(Separators, StringSplitOptions.RemoveEmptyEntries);
            index = 0;
        }

        return current;
    }
}
