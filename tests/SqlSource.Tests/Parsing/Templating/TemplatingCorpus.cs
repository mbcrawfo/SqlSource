using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation;
using SqlSource.Generation.Chains;
using SqlSource.Generation.Paths;
using SqlSource.Parsing;
using SqlSource.Repository;
using SqlSource.SettingsFiles;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Generator.Corpus;
using Xunit;

namespace SqlSource.Tests.Parsing.Templating;

// One case of the templater's corpus: a .sql file's name and text, with the texts of the settings files above it,
// nearest first, as far as its chain reads.  Its name is the path of its file under the corpus folder, without .json.
internal sealed record TemplatingCase(string Name, string FileName, ImmutableArray<string> Settings, string Input);

// The corpus that the sqlfluff templater of src/sqlfluff-templater-sqlsource/ is tested against: the templating plan of
// each case, rendered as JSON, compared with the committed file and written there only when
// SQLSOURCE_UPDATE_TEMPLATER_CORPUS is 1.  See "The templater's corpus" in tests/README.md.
internal static class TemplatingCorpus
{
    public const string UpdateVariable = "SQLSOURCE_UPDATE_TEMPLATER_CORPUS";

    // The file of the names a dialect and its options are set by, at the root of the corpus.  It is no case.
    public const string DialectNames = "dialect-names";

    private const string Repository = "repo";
    private const string Lexer = "lexer";
    private const string Fidelity = "cases";

    // The file name of a case that has none of its own: an unnamed file is one query, named by its file.
    private const string LexerFileName = "Query.sql";

    // A corpus file is written at LF and without a byte order mark, whatever the machine.
    private static readonly UTF8Encoding Encoding = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly Lazy<ImmutableDictionary<string, TemplatingCase>> AllCases = new(
        () => RepositoryCases().Concat(LexerCases()).Concat(FidelityCases()).ToImmutableDictionary(@case => @case.Name),
        LazyThreadSafetyMode.ExecutionAndPublication
    );

    // The run writes the corpus files of its cases, and deletes those of no case, in place of comparing them.
    public static bool IsUpdating { get; } = Environment.GetEnvironmentVariable(UpdateVariable) == "1";

    public static string Folder { get; } =
        RepositoryRoot.Path("src", "sqlfluff-templater-sqlsource", "tests", "corpus");

    public static ImmutableDictionary<string, TemplatingCase> Cases => AllCases.Value;

    // The source of a case, the first segment of its name: repo, lexer or cases.
    public static string SourceOf(string name) => name[..name.IndexOf('/', StringComparison.Ordinal)];

    public static string PathOf(string name) => Path.Combine([Folder, .. name.Split('/')]) + ".json";

    // Fails when the corpus file of the case is missing or holds other text, unless updating, which writes it.
    public static void Check(string name, string actual)
    {
        var path = PathOf(name);
        var committed = File.Exists(path)
            ? File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal)
            : null;
        if (committed == actual)
        {
            return;
        }

        if (IsUpdating)
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual, Encoding);
            return;
        }

        var problem = committed is null
            ? $"There is no corpus file at {path}."
            : $"The corpus file {path} differs at {GoldenFiles.FirstDifference(committed, actual)}.";
        throw new ShouldAssertException(
            problem
                + $"  Run the test with {UpdateVariable}=1 to write it and read the diff of the file.  If the plan "
                + "changed, and not only the text of the case or of its settings files, change the templater to match."
        );
    }

    // The names of the corpus files that no case has, `/`-separated, in ordinal order; none when there is no folder.
    public static IReadOnlyList<string> Leftovers()
    {
        if (!Directory.Exists(Folder))
        {
            return [];
        }

        return
        [
            .. Directory
                .EnumerateFiles(Folder, "*.json", SearchOption.AllDirectories)
                .Select(file => Path.ChangeExtension(Path.GetRelativePath(Folder, file), null).Replace('\\', '/'))
                .Where(name => !Cases.ContainsKey(name) && name != DialectNames)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];
    }

    // The case as its corpus file holds it: what the settings chain gives, then the plan, with every offset counted in
    // code points.  A lexer row or a fidelity case with an error that does not stop its plan fails here, since the spec
    // promises nothing for one, and so does a fidelity case that does not name its dialect first.
    public static string Render(TemplatingCase @case)
    {
        var start = StartDialect(@case);
        var plan = SqlFileParser.PlanTemplating(@case.Input, @case.FileName, start);
        if (SourceOf(@case.Name) is Lexer or Fidelity)
        {
            plan.HasOtherErrors.ShouldBeFalse($"{@case.Name} has an error that does not stop its plan.");
        }

        if (SourceOf(@case.Name) is Fidelity)
        {
            @case.Input.ShouldStartWith("-- dialect:", customMessage: $"{@case.Name} names its dialect first.");
        }

        var codePoints = CodePointOffsets(@case.Input);

        var json = new StringBuilder().Append("{\n  \"fileName\": ");
        AppendString(json, @case.FileName);
        _ = json.Append(",\n  \"settings\": ");
        AppendLines(json, @case.Settings, "  ", AppendString);
        _ = json.Append(",\n  \"input\": ");
        AppendString(json, @case.Input);
        _ = json.Append(",\n  \"expected\": {\n    \"startDialect\": ");
        AppendString(json, SqlDialectName.Canonical(start.Dialect));
        _ = json.Append(",\n    \"startOptions\": ");
        AppendOptions(json, start.Options);
        _ = json.Append(",\n    \"dialect\": ");
        AppendString(json, SqlDialectName.Canonical(plan.Dialect.Dialect));
        _ = json.Append(",\n    \"options\": ");
        AppendOptions(json, plan.Dialect.Options);
        _ = json.Append(",\n    \"lexemes\": ");
        AppendLines(
            json,
            plan.Lexemes,
            "    ",
            (line, lexeme) =>
            {
                _ = line.Append('[');
                AppendString(line, lexeme.Kind.ToString());
                _ = line.Append(", ")
                    .Append(codePoints[lexeme.Span.Start])
                    .Append(", ")
                    .Append(codePoints[lexeme.Span.End])
                    .Append(']');
            }
        );
        _ = json.Append(",\n    \"spans\": ");
        AppendLines(
            json,
            plan.Spans,
            "    ",
            (line, span) =>
            {
                var kind = span.Kind.ToString();
                _ = line.Append("{ \"kind\": ");
                AppendString(line, char.ToLowerInvariant(kind[0]) + kind[1..]);
                _ = line.Append(", \"start\": ")
                    .Append(codePoints[span.Span.Start])
                    .Append(", \"end\": ")
                    .Append(codePoints[span.Span.End])
                    .Append(", \"name\": ");
                AppendString(line, span.Name);
                _ = line.Append(", \"default\": ");
                AppendString(line, span.Default);
                _ = line.Append(" }");
            }
        );
        _ = json.Append(",\n    \"error\": ");
        if (plan.Error is { } error)
        {
            _ = json.Append("{ \"code\": ");
            AppendString(json, SqlDiagnostics.ForParseError(error.Kind).Id);
            _ = json.Append(", \"start\": ")
                .Append(codePoints[error.Span.Start])
                .Append(", \"end\": ")
                .Append(codePoints[error.Span.End])
                .Append(" }");
        }
        else
        {
            _ = json.Append("null");
        }

        return json.Append(",\n    \"hasOtherErrors\": ")
            .Append(plan.HasOtherErrors ? "true" : "false")
            .Append("\n  }\n}\n")
            .ToString();
    }

    // SqlDialectName.Names and SqlDialectName.Options in their order, each name with its dialect's member of SqlDialect
    // or its option's value, so that the templater reads the names that the parser reads.
    public static string RenderDialectNames()
    {
        var json = new StringBuilder().Append("{\n  \"names\": ");
        AppendLines(
            json,
            SqlDialectName.Names,
            "  ",
            (line, entry) =>
            {
                _ = line.Append('[');
                AppendString(line, entry.Name);
                _ = line.Append(", ");
                AppendString(line, entry.Dialect.ToString());
                _ = line.Append(']');
            }
        );
        _ = json.Append(",\n  \"options\": ");
        AppendLines(
            json,
            SqlDialectName.Options,
            "  ",
            (line, entry) =>
            {
                _ = line.Append('[');
                AppendString(line, entry.Name);
                _ = line.Append(", ").Append(((int)entry.Option).ToString(CultureInfo.InvariantCulture)).Append(']');
            }
        );
        return json.Append("\n}\n").ToString();
    }

    // The dialect the chain of the case's settings files gives its .sql file, as the generator resolves it: the files
    // are placed one folder apart, the nearest in the file's own folder.
    private static SqlDialectChoice StartDialect(TemplatingCase @case)
    {
        var count = @case.Settings.Length;
        var listed = @case.Settings.Select(
            (text, nearness) =>
                ListedSettingsFile.Read(
                    new InMemoryAdditionalText(FolderAt(count - 1 - nearness) + SettingsFile.FileName, text),
                    SettingsFileSide.Build,
                    CancellationToken.None
                )!
        );
        var path = FolderAt(Math.Max(count - 1, 0)) + @case.FileName;
        var normalized = SqlPath.Normalize(path)!;
        var chain = SettingsFileSet.Of(listed).ChainOf(normalized);
        return FileParseInput.Resolve(new InMemoryAdditionalText(path, @case.Input), normalized, chain).Dialect;

        static string FolderAt(int depth) => "/corpus/" + string.Concat(Enumerable.Repeat("d/", depth));
    }

    // Every .sql file of the repository whose plan is exact or stopped by an error.  The spec promises nothing for a
    // file with another error, so it is left out.
    private static IEnumerable<TemplatingCase> RepositoryCases()
    {
        var root = RepositoryRoot.Path();
        foreach (var file in SqlFilesBelow(root, root).OrderBy(file => file, StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var @case = new TemplatingCase(
                $"{Repository}/{relative[..^".sql".Length]}",
                Path.GetFileName(file),
                SettingsAbove(root, Path.GetDirectoryName(file)!),
                AsStored(file)
            );
            if (!SqlFileParser.PlanTemplating(@case.Input, @case.FileName, StartDialect(@case)).HasOtherErrors)
            {
                yield return @case;
            }
        }
    }

    // The .sql files below the folder, but for build output, the folders of dot-names (.git, and .claude, whose
    // worktrees are whole checkouts), the templater's own project and the fidelity cases, which are cases of their own.
    private static IEnumerable<string> SqlFilesBelow(string root, string folder)
    {
        foreach (var file in Directory.EnumerateFiles(folder, "*.sql"))
        {
            if (file.EndsWith(".sql", StringComparison.Ordinal))
            {
                yield return file;
            }
        }

        foreach (var child in Directory.EnumerateDirectories(folder))
        {
            var name = Path.GetFileName(child);
            var relative = Path.GetRelativePath(root, child).Replace('\\', '/');
            if (
                name is "bin" or "obj" or "node_modules"
                || name.StartsWith('.')
                || relative is "src/sqlfluff-templater-sqlsource" or "tests/SqlSource.Tests/Parsing/Templating/Cases"
            )
            {
                continue;
            }

            foreach (var file in SqlFilesBelow(root, child))
            {
                yield return file;
            }
        }
    }

    // The texts of the settings files of the folder and the folders above it, up to the root, nearest first, through
    // the first whose root is true.  Only a file named sqlsource.json exactly is one, as the generator reads them.
    private static ImmutableArray<string> SettingsAbove(string root, string folder)
    {
        var texts = ImmutableArray.CreateBuilder<string>();
        var current = folder;
        while (true)
        {
            var path = Path.Combine(current, SettingsFile.FileName);
            if (
                Directory
                    .EnumerateFiles(current, SettingsFile.FileName)
                    .Any(found => Path.GetFileName(found) == SettingsFile.FileName)
            )
            {
                var text = AsStored(path);
                texts.Add(text);
                if (SettingsFileReader.Read(text, default).Root == true)
                {
                    break;
                }
            }

            if (string.Equals(current, root, StringComparison.Ordinal))
            {
                break;
            }

            current = Path.GetDirectoryName(current)!;
        }

        return texts.ToImmutable();
    }

    // A file of the repository as git stores it, at LF, so that a checkout at CRLF gives the same corpus.
    private static string AsStored(string path) =>
        File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);

    // Each row of Lex_Construct_IsReadByTheRulesOfTheDialect, once for each dialect it names, numbered from 1 in the
    // order of the rows and given its dialect by a settings file.  A row that is not found leaves its files without a
    // case, which Corpus_HasNoOrphanFiles reports.
    private static IEnumerable<TemplatingCase> LexerCases()
    {
        var rows = typeof(SqlLexerTests)
            .GetMethod(nameof(SqlLexerTests.Lex_Construct_IsReadByTheRulesOfTheDialect))!
            .GetCustomAttributes<InlineDataAttribute>()
            .ToList();
        for (var row = 0; row < rows.Count; row++)
        {
            var data = rows[row].Data;
            foreach (var dialect in ((string)data[0]!).Split(','))
            {
                var value = DialectValue(ChoiceOf(dialect));
                yield return new TemplatingCase(
                    $"{Lexer}/{value.Replace(',', '+')}/{(row + 1).ToString(CultureInfo.InvariantCulture)}",
                    LexerFileName,
                    [$"{{ \"dialect\": \"{value}\" }}"],
                    (string)data[1]!
                );
            }
        }
    }

    // A dialect as the rows of the lexer's tests name it: the member of SqlDialect, then each option's member after a
    // `+`.
    private static SqlDialectChoice ChoiceOf(string dialect)
    {
        var parts = dialect.Split('+');
        var options = parts
            .Skip(1)
            .Aggregate(SqlDialectOptions.None, (all, option) => all | Enum.Parse<SqlDialectOptions>(option));
        return new SqlDialectChoice(Enum.Parse<SqlDialect>(parts[0]), options);
    }

    // The value of a settings file's dialect that names the choice: its canonical name, then its options.
    private static string DialectValue(SqlDialectChoice choice) =>
        string.Join(",", [SqlDialectName.Canonical(choice.Dialect), .. OptionNames(choice.Options)]);

    // The name of each option that is on, the first that SqlDialectName.Options gives it, in the order of the list.
    private static IEnumerable<string> OptionNames(SqlDialectOptions options)
    {
        var named = SqlDialectOptions.None;
        foreach (var (name, option) in SqlDialectName.Options)
        {
            if ((options & option) != 0 && (named & option) == 0)
            {
                named |= option;
                yield return name;
            }
        }
    }

    // The .sql files of Cases/, each read as it is, with its line endings, and without settings: each names its
    // dialect with a marker, which Render checks.
    private static IEnumerable<TemplatingCase> FidelityCases()
    {
        var folder = RepositoryRoot.Path("tests", "SqlSource.Tests", "Parsing", "Templating", "Cases");
        foreach (var file in Directory.EnumerateFiles(folder, "*.sql").OrderBy(file => file, StringComparer.Ordinal))
        {
            yield return new TemplatingCase(
                $"{Fidelity}/{Path.GetFileNameWithoutExtension(file)}",
                Path.GetFileName(file),
                [],
                File.ReadAllText(file)
            );
        }
    }

    // For each UTF-16 offset of the text, and the end, the number of code points before it.
    private static int[] CodePointOffsets(string text)
    {
        var offsets = new int[text.Length + 1];
        var count = 0;
        for (var i = 0; i < text.Length; i++)
        {
            offsets[i] = count;
            if (!(char.IsLowSurrogate(text[i]) && i > 0 && char.IsHighSurrogate(text[i - 1])))
            {
                count++;
            }
        }

        offsets[text.Length] = count;
        return offsets;
    }

    private static void AppendOptions(StringBuilder json, SqlDialectOptions options)
    {
        _ = json.Append('[');
        var first = true;
        foreach (var name in OptionNames(options))
        {
            _ = json.Append(first ? "" : ", ");
            AppendString(json, name);
            first = false;
        }

        _ = json.Append(']');
    }

    // An array with each element on a line of its own, indented one level past the indent of the array's key, or `[]`.
    private static void AppendLines<T>(
        StringBuilder json,
        IEnumerable<T> items,
        string indent,
        Action<StringBuilder, T> append
    )
    {
        var any = false;
        foreach (var item in items)
        {
            _ = json.Append(any ? ",\n" : "[\n").Append(indent).Append("  ");
            append(json, item);
            any = true;
        }

        _ = any ? json.Append('\n').Append(indent).Append(']') : json.Append("[]");
    }

    // A JSON string, or null.  Control characters and a surrogate without its pair are escaped; every other character
    // is written as it is, so that a diff of the file reads as the text.
    private static void AppendString(StringBuilder json, string? value)
    {
        if (value is null)
        {
            _ = json.Append("null");
            return;
        }

        _ = json.Append('"');
        var i = 0;
        while (i < value.Length)
        {
            var c = value[i++];
            switch (c)
            {
                case '"':
                    _ = json.Append("\\\"");
                    break;
                case '\\':
                    _ = json.Append("\\\\");
                    break;
                case '\n':
                    _ = json.Append("\\n");
                    break;
                case '\r':
                    _ = json.Append("\\r");
                    break;
                case '\t':
                    _ = json.Append("\\t");
                    break;
                default:
                    if (char.IsHighSurrogate(c) && i < value.Length && char.IsLowSurrogate(value[i]))
                    {
                        _ = json.Append(c).Append(value[i++]);
                    }
                    else if (char.IsControl(c) || char.IsSurrogate(c))
                    {
                        _ = json.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        _ = json.Append(c);
                    }

                    break;
            }
        }

        _ = json.Append('"');
    }
}
