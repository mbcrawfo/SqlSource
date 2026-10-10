using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

namespace SqlSource.Tool.Projects;

/// <summary>
/// Lists the C# projects of a <c>.sln</c> or <c>.slnx</c> file, with the library the <c>dotnet</c> command reads
/// them with.
/// </summary>
internal static class SolutionReader
{
    /// <summary>
    /// Reads the solution.  A file that is no solution is not an exception: the user wrote it, and the caller
    /// reports it.
    /// </summary>
    /// <param name="solutionPath">The full path of the solution file.</param>
    /// <param name="cancellationToken">Ends the read.</param>
    public static async Task<SolutionProjects> ReadAsync(string solutionPath, CancellationToken cancellationToken)
    {
        if (SolutionSerializers.GetSerializerByMoniker(solutionPath) is not { } serializer)
        {
            return new SolutionProjects([], "it is neither a .sln nor a .slnx file");
        }

        SolutionModel solution;
        try
        {
            solution = await serializer.OpenAsync(solutionPath, cancellationToken);
        }
        // The library gives a .slnx that is no XML as the exception of the XML reader.
        catch (Exception exception)
            when (exception is SolutionException or XmlException or IOException or UnauthorizedAccessException)
        {
            return new SolutionProjects([], exception.Message);
        }

        var directory = Path.GetDirectoryName(solutionPath) ?? "";
        return new SolutionProjects(
            [
                .. solution
                    .SolutionProjects.Select(project => project.FilePath)
                    // A project of another language is not the generator's.
                    .Where(static path => Path.GetExtension(path).Equals(".csproj", StringComparison.OrdinalIgnoreCase))
                    // A .sln writes a path with "\" on every system.  The library gives it with the separator of this
                    // one today, and nothing promises that.
                    .Select(path => Path.GetFullPath(path.Replace('\\', Path.DirectorySeparatorChar), directory))
                    // The library refuses a solution that lists one project twice, so each is here once.
                    .Order(StringComparer.Ordinal),
            ],
            Failure: null
        );
    }
}
