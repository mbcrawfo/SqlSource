using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Shouldly;

namespace SqlSource.Testing.Generator;

// The golden files of the query-method corpus: a run rendered to text, compared with the file of its case under
// Folder, and written there only when SQLSOURCE_UPDATE_GOLDEN is 1.  See "The golden corpus of query methods" in
// tests/README.md.
//
// Like the other files of this folder, it stays within Roslyn 4.14.0, since tests/SqlSource.Tests.RoslynFloor
// references this library.
internal static class GoldenFiles
{
    public const string UpdateVariable = "SQLSOURCE_UPDATE_GOLDEN";

    private const string None = "(none)";

    // A golden file is written at LF and without a byte order mark, whatever the machine.
    private static readonly UTF8Encoding Encoding = new(encoderShouldEmitUTF8Identifier: false);

    // The run writes the golden files of its cases, and deletes those of no case, in place of comparing them.
    public static bool IsUpdating { get; } = Environment.GetEnvironmentVariable(UpdateVariable) == "1";

    public static string Folder { get; } = RepositoryRoot.Path("tests", "SqlSource.Tests", "Generator", "Golden");

    // The path of the golden file of a case, whose name is `<engine>/<scenario>`.
    public static string PathOf(string caseName) => Path.Combine([Folder, .. caseName.Split('/')]) + ".txt";

    public static string Render(string caseName, GeneratorRun run) =>
        Render(caseName, run.Sources, run.Diagnostics, run.CompilationErrors, run.GeneratedCodeWarnings);

    // The header, the three lists of diagnostics and each generated file by its hint name, in ordinal order.  A path
    // in a diagnostic is written with `/`, so that a run on Windows writes the same text.
    internal static string Render(
        string caseName,
        IReadOnlyDictionary<string, string> sources,
        IReadOnlyList<string> diagnostics,
        IReadOnlyList<string> errors,
        IReadOnlyList<string> warnings
    )
    {
        var text = new StringBuilder()
            .Append("// The golden output of the query-method corpus case ")
            .Append(caseName)
            .Append(".  Written by ")
            .Append(UpdateVariable)
            .Append("=1: see \"The golden corpus of query methods\" in tests/README.md.\n");
        AppendList(text, "Generator diagnostics", diagnostics);
        AppendList(text, "Compilation errors", errors);
        AppendList(text, "Warnings in generated code", warnings);
        foreach (var hintName in sources.Keys.OrderBy(name => name, StringComparer.Ordinal))
        {
            var source = sources[hintName];
            _ = text.Append("\n==== File: ").Append(hintName).Append('\n').Append(source);
            if (!source.EndsWith('\n'))
            {
                _ = text.Append("\n\\ No newline at end of file\n");
            }
        }

        return text.ToString();
    }

    public static void Check(string caseName, string actual) => Check(PathOf(caseName), actual, IsUpdating);

    // Fails when the golden file at the path is missing or holds other text, unless updating, which writes it.
    internal static void Check(string path, string actual, bool updating)
    {
        var golden = File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal) : null;
        if (golden == actual)
        {
            return;
        }

        if (updating)
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual, Encoding);
            return;
        }

        var problem = golden is null
            ? $"There is no golden file at {path}."
            : $"The output differs from the golden file {path} at {FirstDifference(golden, actual)}.";
        throw new ShouldAssertException(
            problem
                + $"  Run the test with {UpdateVariable}=1 to write it, read the diff of the file, and say in the PR "
                + "why it changed."
        );
    }

    public static IReadOnlyList<string> Leftovers(IEnumerable<string> caseNames) => Leftovers(Folder, caseNames);

    // The case names of the golden files below the folder that none of the cases has, `/`-separated, in ordinal order;
    // none when there is no folder.
    internal static IReadOnlyList<string> Leftovers(string folder, IEnumerable<string> caseNames)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        var cases = new HashSet<string>(caseNames, StringComparer.Ordinal);
        return
        [
            .. Directory
                .EnumerateFiles(folder, "*.txt", SearchOption.AllDirectories)
                .Select(file => Path.ChangeExtension(Path.GetRelativePath(folder, file), null).Replace('\\', '/'))
                .Where(name => !cases.Contains(name))
                .OrderBy(name => name, StringComparer.Ordinal),
        ];
    }

    private static void AppendList(StringBuilder text, string title, IReadOnlyList<string> lines)
    {
        _ = text.Append("\n==== ").Append(title).Append('\n');
        if (lines.Count == 0)
        {
            _ = text.Append(None).Append('\n');
            return;
        }

        foreach (var line in lines)
        {
            _ = text.Append(line.Replace('\\', '/')).Append('\n');
        }
    }

    // "line 2: the golden file has `b`, the output `B`", counted from one; a text that has ended has "(end of text)".
    private static string FirstDifference(string golden, string actual)
    {
        var goldenLines = golden.Split('\n');
        var actualLines = actual.Split('\n');
        var line = 0;
        while (
            line < goldenLines.Length
            && line < actualLines.Length
            && string.Equals(goldenLines[line], actualLines[line], StringComparison.Ordinal)
        )
        {
            line++;
        }

        var expected = LineOf(goldenLines, line);
        return $"line {line + 1}: the golden file has {expected}, the output {LineOf(actualLines, line)}";
    }

    private static string LineOf(string[] lines, int index) =>
        index < lines.Length ? "`" + lines[index] + "`" : "(end of text)";
}
