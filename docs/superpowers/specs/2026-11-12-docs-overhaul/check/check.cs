#:property RestorePackagesWithLockFile=false
#:property PublishAot=false
#:property RootNamespace=DocsOverhaul

// The checker of the docs overhaul epic.  check.sh beside it builds and runs it from the root of the repository.  The
// epic's phase 0 brief, docs/superpowers/specs/2026-11-12-docs-overhaul-phase-00-guard-rails-design.md, specifies its
// commands, the inventory and the ledger.  Exit codes: 0 when the command passes, 1 when it finds something, 2 when it
// cannot run.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace DocsOverhaul;

internal static partial class Check
{
    // The folder of the epic, from the root of the repository, and its files.
    internal const string Epic = "docs/superpowers/specs/2026-11-12-docs-overhaul";
    internal const string InventoryFile = Epic + "/inventory.tsv";
    internal const string PhasesFile = Epic + "/check/phases.tsv";
    internal const string LedgerFolder = Epic + "/ledger";
    internal const string InventoryHeader = "id\tlast\tkind\twords\thash\tphase\tstart";
    internal const string PhasesHeader = "file\tfirst\tlast\tphase";
    internal const string LedgerHeader = "unit\tdisposition\tto\targ\twords\tnote";

    // The commit whose user documents the inventory lists.
    internal const string Baseline = "28e20e3ce8fc0a7f52abf5cd3ae99b9f8a22bf03";

    internal const string Readme = "README.md";
    internal const string ToolReadme = "src/SqlSource.Tool/README.md";
    internal const string Site = "site";

    internal static readonly string[] Kinds = ["heading", "para", "bullet", "row", "block", "quote"];

    internal static readonly string[] Dispositions =
    [
        "move",
        "move-split",
        "merge-pair",
        "restated-from",
        "removed-status",
        "removed-preamble",
        "corrected",
        "new",
    ];

    // The longest start of a unit that the inventory gives, for a reader.
    private const int StartLength = 60;

    // What stands in for a character of a code span while links are found.
    private const char Masked = '\u0001';

    // The program's arguments: the root of the repository, then the command and its own.
    private static int Main(string[] args) =>
        args.Length == 0 ? Usage(Console.Out) : Run(args[0], args[1..], Console.Out);

    internal static int Usage(TextWriter output)
    {
        output.WriteLine("usage: check.sh <command> [<nn> | <page>]");
        output.WriteLine("commands: inventory, coverage, move <nn>, rewrite <nn>, origin <page>, selftest");
        return 2;
    }

    // Runs one command on the tree at root.  The selftest calls it on its scratch copy, with a writer of its own.
    internal static int Run(string root, string[] args, TextWriter output)
    {
        try
        {
            return args switch
            {
                ["inventory"] => Inventory(root, output),
                ["coverage"] => Coverage(root, output),
                ["move", var number] => Move(root, number, output),
                ["rewrite", var number] => Rewrite(root, number, output),
                ["origin", var page] => Origin(root, page, output),
                ["selftest"] => Selftest(root, output),
                _ => Usage(output),
            };
        }
        catch (CheckException exception)
        {
            output.WriteLine($"check: {exception.Message}");
            return 2;
        }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The user documents and their units
    // ---------------------------------------------------------------------------------------------------------------

    // A user document: README.md, the tool's readme, or a .md of site/ at any depth but an AGENTS.md or a CLAUDE*.md.
    internal static bool IsUserDocument(string path)
    {
        var name = path[(path.LastIndexOf('/') + 1)..];
        return path is Readme or ToolReadme
            || (
                path.StartsWith(Site + "/", StringComparison.Ordinal)
                && path.EndsWith(".md", StringComparison.Ordinal)
                && name != "AGENTS.md"
                && !name.StartsWith("CLAUDE", StringComparison.Ordinal)
            );
    }

    // The order of the inventory: README.md, the tool's readme, then the pages of the site in ordinal order.
    private static List<string> InReadingOrder(IEnumerable<string> paths) =>
        [
            .. paths
                .Where(IsUserDocument)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path =>
                    path switch
                    {
                        Readme => 0,
                        ToolReadme => 1,
                        _ => 2,
                    }
                )
                .ThenBy(path => path, StringComparer.Ordinal),
        ];

    // Every user document at a commit, or in the working tree when commit is null, with its units.
    internal static Docs Read(string root, string? commit)
    {
        var texts = commit is null ? WorkingTreeTexts(root) : CommitTexts(root, commit);
        return new Docs(commit, [.. texts.Select(text => Document(text.File, SplitLines(text.Text)))]);
    }

    private static List<(string File, string Text)> WorkingTreeTexts(string root)
    {
        var site = Path.Combine(root, Site);
        var pages = Directory.Exists(site)
            ? Directory
                .EnumerateFiles(site, "*.md", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            : [];
        string[] readmes = [Readme, ToolReadme];
        return
        [
            .. InReadingOrder([.. readmes.Where(file => File.Exists(Path.Combine(root, file))), .. pages])
                .Select(file => (file, File.ReadAllText(Path.Combine(root, file), Encoding.UTF8))),
        ];
    }

    private static List<(string File, string Text)> CommitTexts(string root, string commit)
    {
        RequireCommit(root, commit);
        var files = Git(root, "ls-tree", "-r", "--name-only", "-z", commit, Readme, ToolReadme, Site)
            .Split('\0', StringSplitOptions.RemoveEmptyEntries);
        return [.. InReadingOrder(files).Select(file => (file, Git(root, "cat-file", "blob", $"{commit}:{file}")))];
    }

    // A commit that this clone does not have stops the command: its pages are unknown, not empty.
    internal static void RequireCommit(string root, string commit)
    {
        if (RunGit(root, "cat-file", "-e", $"{commit}^{{commit}}").Code != 0)
        {
            throw new CheckException(
                $"commit {commit} is not in this clone: fetch main (git fetch origin main, with --unshallow in a "
                    + "shallow clone) and run again"
            );
        }
    }

    // One document: its lines, its units, and the lines that hold only an <a id> anchor.
    internal static Doc Document(string file, string[] lines)
    {
        var anchorLines = new List<int>();
        var units = Scan(file, lines, anchorLines);
        return new Doc(file, lines, units, anchorLines);
    }

    // The units of one document, its lines given.
    internal static List<Unit> Units(string file, string[] lines) => Scan(file, lines, []);

    // A unit is a line that is not blank, outside a fenced block, or a whole fenced block from its opening fence to its
    // closing fence.  A line of only <a id> anchors, the rule line of a table, and front matter are no unit.  Fences
    // are read as UserDocs.Read of tests/SqlSource.Repository.Tests reads them: a closing fence is a line of the
    // opening fence's character, at least as long, at any indent, and a fence that is never closed runs to the end.
    private static List<Unit> Scan(string file, string[] lines, List<int> anchorLines)
    {
        var units = new List<Unit>();
        var index = FrontMatterEnd(lines);
        while (index < lines.Length)
        {
            var line = lines[index];
            var number = index + 1;
            if (OpeningPattern().Match(line) is { Success: true } opening && IsOpening(opening))
            {
                var fence = opening.Groups[2].Value;
                var close = index + 1;
                while (close < lines.Length && !IsClosing(lines[close], fence))
                {
                    close++;
                }

                var last = Math.Min(close, lines.Length - 1);
                var block = lines[index..(last + 1)];
                var text = Normalise(block, opening.Groups[1].Length);
                units.Add(new Unit(file, number, last + 1, "block", Words(block), text));
                index = last + 1;
                continue;
            }

            var trimmed = line.Trim();
            if (AnchorLinePattern().IsMatch(trimmed))
            {
                anchorLines.Add(number);
            }
            else if (trimmed.Length > 0 && !TableRulePattern().IsMatch(trimmed))
            {
                units.Add(new Unit(file, number, number, KindOf(trimmed), Words([line]), Normalise(line)));
            }

            index++;
        }

        return units;
    }

    // Front matter, between a first line of '---' and the next one, is for docfx: the index of the line after it.
    private static int FrontMatterEnd(string[] lines)
    {
        if (lines.Length == 0 || lines[0] != "---")
        {
            return 0;
        }

        var close = Array.IndexOf(lines, "---", 1);
        return close > 0 ? close + 1 : 0;
    }

    // A backtick fence's info string has no backtick: otherwise the line is a code span.
    private static bool IsOpening(Match opening) =>
        opening.Groups[2].Value[0] != '`' || !opening.Groups[3].Value.Contains('`', StringComparison.Ordinal);

    private static bool IsClosing(string line, string fence)
    {
        var trimmed = line.Trim();
        return trimmed.Length >= fence.Length && trimmed.All(character => character == fence[0]);
    }

    private static string Unindent(string line, int indent)
    {
        var skip = 0;
        while (skip < indent && skip < line.Length && line[skip] == ' ')
        {
            skip++;
        }

        return line[skip..];
    }

    private static string KindOf(string trimmed) =>
        trimmed switch
        {
            _ when HeadingPattern().IsMatch(trimmed) => "heading",
            ['>', ..] => "quote",
            ['|', ..] => "row",
            _ when BulletPattern().IsMatch(trimmed) => "bullet",
            _ => "para",
        };

    // What wc -w counts: runs of characters between white space.
    internal static int Words(IEnumerable<string> lines) =>
        lines.Sum(line => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length);

    // The normalised text of a line: no white space at its ends, no # marks when it is a heading, no <a id> anchor,
    // and each inline link or image outside a code span reduced to its text.  Nothing else changes.
    internal static string Normalise(string line)
    {
        var text = line.Trim();
        if (HeadingPattern().Match(text) is { Success: true } heading)
        {
            text = heading.Groups[1].Value;
        }

        return WithoutLinks(InlineAnchorPattern().Replace(text, "")).Trim();
    }

    // The normalised text of a fenced block: its lines as they are, the fences and the info string too, less the indent
    // of its opening fence, joined with line feeds.
    internal static string Normalise(IEnumerable<string> block, int indent) =>
        string.Join('\n', block.Select(line => Unindent(line, indent)));

    // Each inline link or image becomes its text, the innermost first, so that a badge, an image inside a link, becomes
    // the image's text.  The code spans are masked first: inside one nothing is a link, and a bracket is text.
    private static string WithoutLinks(string text)
    {
        var result = text;
        var mask = MaskCodeSpans(text);
        while (LinkPattern().Match(mask) is { Success: true } link)
        {
            var inner = link.Groups[1];
            var end = link.Index + link.Length;
            result = string.Concat(
                result.AsSpan(0, link.Index),
                result.AsSpan(inner.Index, inner.Length),
                result.AsSpan(end)
            );
            mask = string.Concat(mask.AsSpan(0, link.Index), mask.AsSpan(inner.Index, inner.Length), mask.AsSpan(end));
        }

        return result;
    }

    // The text, each character of a code span made Masked: what is left is Markdown.
    private static string MaskCodeSpans(string text)
    {
        var masked = text.ToCharArray();
        foreach (Match span in CodeSpanPattern().Matches(text))
        {
            Array.Fill(masked, Masked, span.Index, span.Length);
        }

        return new string(masked);
    }

    // The first 12 hex digits, in lower case, of the SHA-256 of the text in UTF-8.
    internal static string HashOf(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12];

    // The first characters of the first line of the text.
    internal static string StartOf(string text)
    {
        var first = text.Split('\n')[0];
        if (first.Length > StartLength)
        {
            first = first[..(char.IsHighSurrogate(first[StartLength - 1]) ? StartLength - 1 : StartLength)];
        }

        return Cell(first);
    }

    // A cell of a .tsv file is one line with no tab and no space at its end, and - when it is empty.
    internal static string Cell(string text)
    {
        var cell = text.Split('\n')[0].TrimEnd('\r').Replace('\t', ' ').TrimEnd();
        return cell.Length == 0 ? "-" : cell;
    }

    // The lines of a text, as File.ReadAllLines gives them: split at each line feed, with no carriage return.
    internal static string[] SplitLines(string text)
    {
        var lines = text.Split('\n');
        if (lines[^1].Length == 0)
        {
            lines = lines[..^1];
        }

        return [.. lines.Select(line => line.TrimEnd('\r'))];
    }

    // ---------------------------------------------------------------------------------------------------------------
    // inventory
    // ---------------------------------------------------------------------------------------------------------------

    // Writes inventory.tsv from the baseline and check/phases.tsv, unless a unit has no phase.
    private static int Inventory(string root, TextWriter output)
    {
        var ranges = LoadPhases(root);
        var docs = Read(root, Baseline);
        var text = new StringBuilder().Append(
            CultureInfo.InvariantCulture,
            $"# baseline {Baseline}\n{InventoryHeader}\n"
        );
        var unplaced = new List<Unit>();
        foreach (var unit in docs.Units)
        {
            if (PhaseOf(ranges, unit) is { } phase)
            {
                _ = text.Append(
                    CultureInfo.InvariantCulture,
                    $"{unit.Id}\t{unit.Last}\t{unit.Kind}\t{unit.Words}\t{unit.Hash}\t{phase}\t{unit.Start}\n"
                );
            }
            else
            {
                unplaced.Add(unit);
            }
        }

        output.WriteLine($"Units of the user documents at the baseline {Baseline[..8]}, by document:");
        foreach (var doc in docs.Files)
        {
            output.WriteLine($"  {doc.File, -34} {doc.Units.Count, 5}");
        }

        output.WriteLine("By kind:");
        foreach (var kind in Kinds)
        {
            output.WriteLine($"  {kind, -34} {docs.Units.Count(unit => unit.Kind == kind), 5}");
        }

        output.WriteLine($"  {"all", -34} {docs.Units.Count(), 5}");
        output.WriteLine(
            $"Lines of only an <a id> anchor, which are no unit: {docs.Files.Sum(doc => doc.AnchorLines.Count)}"
        );
        if (unplaced.Count > 0)
        {
            foreach (var unit in unplaced)
            {
                output.WriteLine($"no phase: {unit.Id}  {unit.Start}");
            }

            output.WriteLine($"{InventoryFile} is not written: {unplaced.Count} units have no phase in {PhasesFile}.");
            return 1;
        }

        File.WriteAllText(Path.Combine(root, InventoryFile), text.ToString());
        output.WriteLine($"{InventoryFile} is written.");
        return 0;
    }

    private static List<PhaseRange> LoadPhases(string root)
    {
        var path = Path.Combine(root, PhasesFile);
        if (!File.Exists(path))
        {
            throw new CheckException($"{PhasesFile} is missing");
        }

        var ranges = new List<PhaseRange>();
        var header = false;
        foreach (var (line, number) in Numbered(SplitLines(File.ReadAllText(path, Encoding.UTF8))))
        {
            if (line.StartsWith('#'))
            {
                continue;
            }

            if (!header)
            {
                header =
                    line == PhasesHeader
                        ? true
                        : throw new CheckException($"{PhasesFile}:{number} is not the header, {Spaced(PhasesHeader)}");
                continue;
            }

            var cells = line.Split('\t');
            if (
                cells.Length != 4
                || !int.TryParse(cells[1], CultureInfo.InvariantCulture, out var first)
                || !int.TryParse(cells[2], CultureInfo.InvariantCulture, out var last)
                || !int.TryParse(cells[3], CultureInfo.InvariantCulture, out var phase)
                || first > last
            )
            {
                throw new CheckException($"{PhasesFile}:{number} is not a row of a file, two lines and a phase");
            }

            ranges.Add(new PhaseRange(cells[0], first, last, phase));
        }

        return ranges;
    }

    // The phase of the narrowest range that holds the unit's first line.
    private static int? PhaseOf(List<PhaseRange> ranges, Unit unit) =>
        ranges
            .Where(range => range.File == unit.File && range.First <= unit.Line && unit.Line <= range.Last)
            .OrderBy(range => range.Last - range.First)
            .Select(range => (int?)range.Phase)
            .FirstOrDefault();

    // The rows of inventory.tsv.
    internal static List<Entry> LoadInventory(string root)
    {
        var path = Path.Combine(root, InventoryFile);
        if (!File.Exists(path))
        {
            throw new CheckException($"{InventoryFile} is missing: run check.sh inventory");
        }

        var lines = SplitLines(File.ReadAllText(path, Encoding.UTF8));
        if (lines.Length < 2 || lines[0] != $"# baseline {Baseline}" || lines[1] != InventoryHeader)
        {
            throw new CheckException($"{InventoryFile} does not start with its baseline line and its header");
        }

        var entries = new List<Entry>();
        foreach (var (line, number) in Numbered(lines).Skip(2))
        {
            var cells = line.Split('\t');
            if (
                cells.Length != 7
                || !int.TryParse(cells[1], CultureInfo.InvariantCulture, out var last)
                || !int.TryParse(cells[3], CultureInfo.InvariantCulture, out var words)
                || !int.TryParse(cells[5], CultureInfo.InvariantCulture, out var phase)
                || ParsePlace(cells[0]) is not { Outside: false, Part: null } place
                || place.First != place.Last
            )
            {
                throw new CheckException($"{InventoryFile}:{number} is not a row of the inventory");
            }

            entries.Add(new Entry(cells[0], last, cells[2], words, cells[4], phase, cells[6]));
        }

        return entries;
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The ledger and coverage
    // ---------------------------------------------------------------------------------------------------------------

    internal static string LedgerName(int phase) =>
        string.Create(CultureInfo.InvariantCulture, $"phase-{phase:00}.tsv");

    // The numbers of the ledger files, in order.  A file of the folder that is no ledger file is a problem.
    internal static List<int> LedgerPhases(string root, List<string> problems)
    {
        var folder = Path.Combine(root, LedgerFolder);
        var phases = new List<int>();
        if (!Directory.Exists(folder))
        {
            return phases;
        }

        foreach (var name in Directory.EnumerateFiles(folder).Select(Path.GetFileName).Order(StringComparer.Ordinal))
        {
            if (LedgerNamePattern().Match(name!) is { Success: true } match)
            {
                phases.Add(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture));
            }
            else if (!name!.StartsWith('.'))
            {
                problems.Add($"{LedgerFolder}/{name}: a ledger file is named phase-<nn>.tsv");
            }
        }

        return phases;
    }

    // ledger/phase-<nn>.tsv: its base, its rows with the line of each, and what in its lines is not well formed.  The
    // rows' cells are checked by coverage.
    internal static Ledger LoadLedger(string root, int phase)
    {
        var name = LedgerName(phase);
        var path = Path.Combine(root, LedgerFolder, name);
        if (!File.Exists(path))
        {
            throw new CheckException($"{LedgerFolder}/{name} does not exist");
        }

        var lines = SplitLines(File.ReadAllText(path, Encoding.UTF8));
        var problems = new List<string>();
        var rows = new List<Row>();
        var numbers = new List<int>();
        var baseLine = lines.Length > 0 ? BaseLinePattern().Match(lines[0]) : Match.Empty;
        if (!baseLine.Success)
        {
            problems.Add($"{name}:1: the first line is not '# base <sha>', the commit of main the phase starts from");
        }

        var header = false;
        foreach (var (line, number) in Numbered(lines).Skip(1))
        {
            if (line.StartsWith('#'))
            {
                continue;
            }

            if (!header)
            {
                header = true;
                if (line != LedgerHeader)
                {
                    problems.Add($"{name}:{number}: the header is not {Spaced(LedgerHeader)}");
                }

                continue;
            }

            var cells = line.Split('\t');
            if (cells.Length != 6)
            {
                problems.Add($"{name}:{number}: {cells.Length} cells, where a row has six");
            }
            else if (cells.Any(cell => cell.Trim().Length == 0))
            {
                problems.Add($"{name}:{number}: an empty cell, where - stands for nothing");
            }
            else
            {
                rows.Add(new Row(cells[0], cells[1], cells[2], cells[3], cells[4], cells[5]));
                numbers.Add(number);
            }
        }

        if (!header)
        {
            problems.Add($"{name}: no header line");
        }

        return new Ledger(phase, baseLine.Success ? baseLine.Groups[1].Value : "", rows, numbers, problems);
    }

    // Where a row's unit is: a unit of the inventory by its first line (file:line), a range of them (file:a-b), a part
    // of either (/n), or with @ a place outside the inventory, a whole file or lines of it.  Null when it is none.
    internal static Place? ParsePlace(string cell)
    {
        if (PlacePattern().Match(cell) is not { Success: true } match)
        {
            return null;
        }

        var outside = match.Groups["at"].Success;
        var whole = !match.Groups["first"].Success;
        var first = whole ? 1 : int.Parse(match.Groups["first"].Value, CultureInfo.InvariantCulture);
        var last = match.Groups["last"].Success
            ? int.Parse(match.Groups["last"].Value, CultureInfo.InvariantCulture)
            : first;
        int? part = match.Groups["part"].Success
            ? int.Parse(match.Groups["part"].Value, CultureInfo.InvariantCulture)
            : null;
        var unit = match.Groups["part"].Success ? cell[..cell.LastIndexOf('/')] : cell;
        return (whole && !outside) || last < first
            ? null
            : new Place(match.Groups["file"].Value, first, last, outside, whole, part, unit);
    }

    // The units of the inventory that a place names: those whose first line is in its lines.
    internal static List<Entry> EntriesAt(ILookup<string, Entry> inventory, Place place) =>
        place.Outside
            ? []
            : [.. inventory[place.File].Where(entry => place.First <= entry.Line && entry.Line <= place.Last)];

    // Reads the inventory and every ledger file.  A unit is due when its phase is at most the highest number of a
    // ledger file, and each unit that is due must have a row in one of them.
    private static int Coverage(string root, TextWriter output)
    {
        var inventory = LoadInventory(root);
        var byFile = inventory.ToLookup(entry => entry.File, StringComparer.Ordinal);
        var problems = new List<string>();
        var ledgers = LedgerPhases(root, problems).Select(phase => LoadLedger(root, phase)).ToList();
        var due = ledgers.Count == 0 ? -1 : ledgers.Max(ledger => ledger.Phase);
        var named = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ledger in ledgers)
        {
            problems.AddRange(ledger.Problems);
            CheckRows(root, ledger, byFile, named, problems);
        }

        var missing = inventory.Where(entry => entry.Phase <= due && !named.Contains(entry.Id)).ToList();

        var files = string.Join(", ", ledgers.Select(ledger => $"{ledger.Name} (base {Short(ledger.Base)})"));
        output.WriteLine(ledgers.Count == 0 ? "No ledger file." : $"Ledger files: {files}.");
        output.WriteLine(due < 0 ? "No unit is due." : $"Due: the units of phases {due} and below.");
        output.WriteLine();
        output.WriteLine($"  {"Document", -34} {"Units", 6} {"With a row", 11} {"Due, no row", 12} {"Not due", 8}");
        foreach (var file in byFile)
        {
            WriteCounts(output, file.Key, [.. file], due, named);
        }

        WriteCounts(output, "All", inventory, due, named);
        output.WriteLine();

        var rows = ledgers.SelectMany(ledger => ledger.Rows).ToList();
        output.WriteLine(rows.Count == 0 ? "Rows by disposition: none." : "Rows by disposition:");
        foreach (var disposition in Dispositions)
        {
            var count = rows.Count(row => row.Disposition == disposition);
            if (count > 0)
            {
                output.WriteLine($"  {disposition, -18} {count, 5}");
            }
        }

        foreach (var entry in missing)
        {
            output.WriteLine($"no row: {entry.Id}  {entry.Kind}  {entry.Start}");
        }

        foreach (var problem in problems)
        {
            output.WriteLine($"problem: {problem}");
        }

        output.WriteLine(
            problems.Count == 0 && missing.Count == 0
                ? "coverage passes."
                : $"coverage fails: {missing.Count} due units with no row, {problems.Count} problems of the ledger."
        );
        return problems.Count == 0 && missing.Count == 0 ? 0 : 1;
    }

    private static void WriteCounts(TextWriter output, string name, List<Entry> entries, int due, HashSet<string> named)
    {
        var withRow = entries.Count(entry => named.Contains(entry.Id));
        var missing = entries.Count(entry => entry.Phase <= due && !named.Contains(entry.Id));
        var notDue = entries.Count(entry => entry.Phase > due);
        output.WriteLine($"  {name, -34} {entries.Count, 6} {withRow, 11} {missing, 12} {notDue, 8}");
    }

    // Checks each row of one ledger file, and adds to named every unit of the inventory that a row names.
    private static void CheckRows(
        string root,
        Ledger ledger,
        ILookup<string, Entry> inventory,
        HashSet<string> named,
        List<string> problems
    )
    {
        var places = new Place?[ledger.Rows.Count];
        for (var index = 0; index < ledger.Rows.Count; index++)
        {
            var messages = new List<string>();
            places[index] = CheckRow(ledger.Rows[index], inventory, named, messages);
            problems.AddRange(messages.Select(message => $"{Where(ledger, index)}: {message}"));
        }

        // A move-split has its parts, <unit>/1 and on, unless it names a heading, which may have none.
        var parts = Enumerable
            .Range(0, places.Length)
            .Where(index => places[index]?.Part is not null)
            .ToLookup(index => places[index]!.Unit, StringComparer.Ordinal);
        var splits = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < places.Length; index++)
        {
            if (ledger.Rows[index].Disposition != "move-split" || places[index] is not { Part: null } place)
            {
                continue;
            }

            _ = splits.Add(place.Unit);
            var numbers = parts[place.Unit].Select(part => places[part]!.Part!.Value).Order().ToList();
            if (numbers.Count == 0)
            {
                if (!AllHeadings(root, ledger, inventory, place))
                {
                    problems.Add(
                        $"{Where(ledger, index)}: a move-split with no part names a heading: give it parts, "
                            + $"{place.Unit}/1 and on"
                    );
                }

                if (ledger.Rows[index].Note == "-")
                {
                    problems.Add(
                        $"{Where(ledger, index)}: a move-split with no part has a note: what stands in its place"
                    );
                }
            }
            else if (!numbers.SequenceEqual(Enumerable.Range(1, numbers.Count)))
            {
                problems.Add($"{Where(ledger, index)}: its parts are numbered from 1, with no gap and none twice");
            }
        }

        foreach (var index in parts.Where(group => !splits.Contains(group.Key)).SelectMany(group => group))
        {
            problems.Add($"{Where(ledger, index)}: a part of a unit that has no move-split row in {ledger.Name}");
        }
    }

    private static string Where(Ledger ledger, int index) =>
        $"{ledger.Name}:{ledger.Lines[index]}: {ledger.Rows[index].Unit}";

    // The checks of one row's cells, but for the parts of a move-split.  Gives the place of its unit.
    private static Place? CheckRow(
        Row row,
        ILookup<string, Entry> inventory,
        HashSet<string> named,
        List<string> messages
    )
    {
        if (!Dispositions.Contains(row.Disposition, StringComparer.Ordinal))
        {
            messages.Add($"'{row.Disposition}' is no disposition");
        }

        Place? place = null;
        var noUnit = row.Unit == "-";
        var isNew = row.Disposition == "new";
        if (noUnit || isNew)
        {
            if (noUnit != isNew)
            {
                messages.Add("a new row, and only a new row, has the unit -");
            }
        }
        else if (ParsePlace(row.Unit) is not { } parsed)
        {
            messages.Add("the unit is no file:line, file:first-last, part /n of either, or @ place of a .md file");
        }
        else
        {
            place = parsed;
            var entries = EntriesAt(inventory, parsed);
            if (!parsed.Outside && entries.Count == 0)
            {
                messages.Add(
                    parsed.First == parsed.Last
                        ? "no unit of the inventory starts at this line"
                        : "the range holds no unit of the inventory"
                );
            }

            named.UnionWith(entries.Select(entry => entry.Id));
        }

        var noted = row.Note != "-";
        switch (row.Disposition)
        {
            case "merge-pair" when !MergePairPattern().IsMatch(row.Arg):
                messages.Add("a merge-pair names its pair, MP-<nn>, in arg");
                break;
            case "restated-from":
                if (!OwnerPattern().IsMatch(row.Arg))
                {
                    messages.Add("a restated-from names the owner of its facts, <file>#<anchor>, in arg");
                }

                if (row.To == "-" && !noted)
                {
                    messages.Add("a restated-from with the to - has a note");
                }

                break;
            case "corrected":
                if (!CorrectionPattern().IsMatch(row.Arg))
                {
                    messages.Add("a corrected row has C-<nn> or - in arg");
                }

                if (!noted)
                {
                    messages.Add("a corrected row has a note");
                }

                break;
            case "move" when row.Arg is not ("fact" or "-"):
                messages.Add("a move has fact or - in arg");
                break;
            case "removed-status" or "removed-preamble" when !noted:
                messages.Add($"a {row.Disposition} row has a note");
                break;
            case "new" when row.To == "-":
                messages.Add("a new row says where the text stands, in to");
                break;
            default:
                break;
        }

        return place;
    }

    // Whether every unit that a place names is a heading, and there is one.  A place outside the inventory is read at
    // the ledger's base.
    private static bool AllHeadings(string root, Ledger ledger, ILookup<string, Entry> inventory, Place place)
    {
        if (!place.Outside)
        {
            var entries = EntriesAt(inventory, place);
            return entries.Count > 0 && entries.TrueForAll(entry => entry.Kind == "heading");
        }

        if (place.Whole || ledger.Base.Length == 0)
        {
            return false;
        }

        var (code, text, _) = RunGit(root, "cat-file", "blob", $"{ledger.Base}:{place.File}");
        var units =
            code == 0
                ? Units(place.File, SplitLines(text))
                    .Where(unit => place.First <= unit.Line && unit.Line <= place.Last)
                    .ToList()
                : [];
        return units.Count > 0 && units.TrueForAll(unit => unit.Kind == "heading");
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Headings, anchors and sections
    // ---------------------------------------------------------------------------------------------------------------

    // The level of a heading: its count of # marks.
    internal static int LevelOf(Doc doc, Unit heading) =>
        doc.Lines[heading.Line - 1].Trim().TakeWhile(character => character == '#').Count();

    // The headings of a document with the level and the anchor of each: the slug GitHub gives it, with -1 and on for a
    // slug seen before, as UserDocs.Read gives them.  A tab of docfx, a heading whose whole text links to #tab/<id>,
    // is no heading.
    internal static List<Heading> Headings(Doc doc)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var headings = new List<Heading>();
        foreach (var unit in doc.Units.Where(unit => unit.Kind == "heading"))
        {
            var text = HeadingPattern().Match(doc.Lines[unit.Line - 1].Trim()).Groups[1].Value;
            if (TabPattern().IsMatch(text))
            {
                continue;
            }

            var slug = Slug(text);
            var seen = counts.GetValueOrDefault(slug);
            counts[slug] = seen + 1;
            headings.Add(new Heading(unit, LevelOf(doc, unit), seen == 0 ? slug : $"{slug}-{seen}"));
        }

        return headings;
    }

    // What GitHub makes of a heading's text, as UserDocs.Slug of tests/SqlSource.Repository.Tests does.
    private static string Slug(string heading)
    {
        var slug = new StringBuilder();
        foreach (var character in WithoutLinks(heading).Trim().Select(char.ToLowerInvariant))
        {
            if (
                char.IsLetterOrDigit(character)
                || character is '-' or '_'
                || CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark
            )
            {
                _ = slug.Append(character);
            }
            else if (character == ' ')
            {
                _ = slug.Append('-');
            }
        }

        return slug.ToString();
    }

    // Each <a id> of a document, on a line of its own or inside a unit that is no block, with its line.
    internal static IEnumerable<(int Line, string Id)> AnchorIds(Doc doc) =>
        doc
            .AnchorLines.Concat(doc.Units.Where(unit => unit.Kind != "block").Select(unit => unit.Line))
            .Order()
            .SelectMany(line =>
                InlineAnchorPattern().Matches(doc.Lines[line - 1]).Select(match => (line, match.Groups["id"].Value))
            );

    // The lines of the section an anchor names, from the first to the one before End: from the heading that the
    // anchor names, a slug or the heading below an <a id>, to the next heading of the same or a higher level.  An
    // <a id> with no heading below it starts its section on its own line, which ends at the next heading.
    internal static (int First, int End)? AnchorSection(Doc doc, string anchor)
    {
        var headings = Headings(doc);
        var line = AnchorIds(doc).Where(found => found.Id == anchor).Select(found => found.Line).FirstOrDefault();
        if (line > 0)
        {
            var next = doc.Units.FirstOrDefault(unit => unit.Line >= line);
            return headings.Find(heading => heading.Unit == next) is { } below
                ? (below.Unit.Line, EndOf(headings, below))
                : (line, headings.Find(heading => heading.Unit.Line > line)?.Unit.Line ?? int.MaxValue);
        }

        return headings.Find(heading => heading.Slug == anchor) is { } named
            ? (named.Unit.Line, EndOf(headings, named))
            : null;
    }

    private static int EndOf(List<Heading> headings, Heading heading) =>
        headings.Find(other => other.Unit.Line > heading.Unit.Line && other.Level <= heading.Level)?.Unit.Line
        ?? int.MaxValue;

    // The sections of a page for rewrite: the text before the first h2, (top), then each h2 with all below it.  A
    // section is known by the first <a id> above its h2, else by the h2's slug.
    internal static List<Section> Sections(Doc? doc)
    {
        if (doc is null)
        {
            return [];
        }

        var h2 = Headings(doc).Where(heading => heading.Level == 2).ToDictionary(heading => heading.Unit.Line);
        var sections = new List<Section> { new(Top, []) };
        var previous = 0;
        foreach (var unit in doc.Units)
        {
            if (h2.TryGetValue(unit.Line, out var heading))
            {
                var anchor = doc
                    .AnchorLines.Where(line => line > previous && line < unit.Line)
                    .Select(line => InlineAnchorPattern().Match(doc.Lines[line - 1]).Groups["id"].Value)
                    .FirstOrDefault();
                var key = anchor ?? heading.Slug;
                var seen = sections.Count(section =>
                    section.Key == key || section.Key.StartsWith(key + "~", StringComparison.Ordinal)
                );
                sections.Add(new Section(seen == 0 ? key : $"{key}~{seen + 1}", []));
            }

            sections[^1].Units.Add(unit);
            previous = unit.Last;
        }

        return sections;
    }

    // The name of the text before the first h2 of a page.  A row's to names it by the file alone.
    private const string Top = "(top)";

    // ---------------------------------------------------------------------------------------------------------------
    // move and rewrite: a phase's ledger, its base and the working tree
    // ---------------------------------------------------------------------------------------------------------------

    // The words that a sentence of a rewritten section must not lose: one that holds them is listed when the new
    // section has no sentence like it that holds them too.
    private static readonly string[] Negations = ["not", "only", "never", "unless"];

    // Reads what move and rewrite compare: the phase's ledger and what each row names, the user documents at its base
    // with the names a row gives each unit, and those of the working tree.
    private static PhaseState LoadPhase(string root, string number)
    {
        if (number.Length is not (1 or 2) || !number.All(char.IsAsciiDigit))
        {
            throw new CheckException($"'{number}' is not the number of a phase, such as 03");
        }

        var ledger = LoadLedger(root, int.Parse(number, CultureInfo.InvariantCulture));
        if (ledger.Base.Length == 0)
        {
            throw new CheckException($"{LedgerFolder}/{ledger.Name} has no first line '# base <sha>'");
        }

        var inventory = LoadInventory(root);
        var byFile = inventory.ToLookup(entry => entry.File, StringComparer.Ordinal);
        var byHash = inventory.ToLookup(entry => entry.Hash, StringComparer.Ordinal);
        var problems = new List<string>(ledger.Problems);
        var rows = new List<LedgerRow>();
        for (var index = 0; index < ledger.Rows.Count; index++)
        {
            var row = ledger.Rows[index];
            var place = row.Unit == "-" ? null : ParsePlace(row.Unit);
            if (row.Unit != "-" && place is null)
            {
                problems.Add($"{Where(ledger, index)}: the unit is no place: check.sh coverage says why");
            }

            var ids = (place is { Outside: false } ? EntriesAt(byFile, place) : [])
                .Select(entry => entry.Id)
                .ToHashSet(StringComparer.Ordinal);
            rows.Add(new LedgerRow(row, $"{ledger.Name}:{ledger.Lines[index]}", place, ids));
        }

        var old = Read(root, ledger.Base);
        var oldUnits = old
            .Units.Select((unit, order) => new OldUnit(unit, order, [.. byHash[unit.Hash].Select(entry => entry.Id)]))
            .ToList();
        return new PhaseState(ledger, rows, old, oldUnits, Read(root, null), byFile, problems);
    }

    // The units that a row names, each with its name, its hash and its start: the units of the inventory in its
    // place, or with @ the units of the base there.
    private static List<(string Name, string Hash, string Start)> Named(PhaseState phase, LedgerRow row) =>
        row.Place switch
        {
            null => [],
            { Outside: false } place =>
            [
                .. EntriesAt(phase.Inventory, place).Select(entry => (entry.Id, entry.Hash, entry.Start)),
            ],
            var place =>
            [
                .. phase
                    .OldUnits.Where(old =>
                        old.Unit.File == place.File
                        && (place.Whole || (place.First <= old.Unit.Line && old.Unit.Line <= place.Last))
                    )
                    .Select(old => ($"@{old.Unit.Id}", old.Unit.Hash, old.Unit.Start)),
            ],
        };

    // The units of the working tree at a row's to, <file> or <file>#<anchor>, or why there are none.
    private static (List<Unit>? Units, string? Problem) UnitsAt(Docs docs, string to)
    {
        var hash = to.IndexOf('#', StringComparison.Ordinal);
        var file = hash < 0 ? to : to[..hash];
        if (docs.Files.FirstOrDefault(doc => doc.File == file) is not { } doc)
        {
            return (null, $"{file} is no user document of the working tree");
        }

        if (hash < 0)
        {
            return ([.. doc.Units], null);
        }

        var anchor = to[(hash + 1)..];
        return AnchorSection(doc, anchor) is { } section
            ? ([.. doc.Units.Where(unit => section.First <= unit.Line && unit.Line < section.End)], null)
            : (null, $"{file} has no anchor {anchor}");
    }

    // Whether a unit's text holds a part's words, the case of their first letter and a final full stop not compared.
    internal static bool Holds(string text, string words) => Find(text, words) >= 0;

    private static int Find(string text, string words)
    {
        var plain = words.EndsWith('.') ? words[..^1] : words;
        if (plain.Length == 0)
        {
            return 0;
        }

        var first = plain[0];
        var other = char.IsUpper(first) ? char.ToLowerInvariant(first) : char.ToUpperInvariant(first);
        var found = text.IndexOf(plain, StringComparison.Ordinal);
        return found >= 0 ? found : text.IndexOf(other + plain[1..], StringComparison.Ordinal);
    }

    // The row that judges each unit left over, and why the unit fails the command.  The units of one text are judged
    // together, since a row that names one of them by its id names them all: in the order of the ledger, each row
    // that lets a unit be left over covers as many of them as it names, and only the rest are judged by the first
    // row that does not.  A unit that list 1 found at its row's to is placed, and its row covers no other unit for
    // it.  Rows of the units come before those of their parts.  So the verdict does not depend on the order of the
    // rows.
    private static Dictionary<OldUnit, (LedgerRow? Row, string? Failure)> Verdicts(
        PhaseState phase,
        IEnumerable<OldUnit> leftOver,
        HashSet<(string Row, string Name)> placedNames
    )
    {
        var verdicts = new Dictionary<OldUnit, (LedgerRow? Row, string? Failure)>();
        foreach (var group in leftOver.GroupBy(unit => unit.Unit.Text, StringComparer.Ordinal))
        {
            var units = group.ToList();
            var naming = phase.Rows.Where(row => units.Any(row.Names)).ToList();
            var rows = naming.Any(row => !row.IsPart) ? naming.FindAll(row => !row.IsPart) : naming;
            var index = 0;
            foreach (var row in rows.Where(row => FailureOf(row) is null))
            {
                for (var covers = Covers(row, units, placedNames); covers > 0 && index < units.Count; covers--)
                {
                    verdicts[units[index++]] = (row, null);
                }
            }

            // A row that does not fit judges the rest only when a unit it names was not placed: a move whose copy
            // stands at its to is not to blame for another copy.
            var unfit = rows.Find(row => FailureOf(row) is not null && Covers(row, units, placedNames) > 0);
            (LedgerRow? Row, string? Failure) rest = (null, "no row names it");
            if (unfit is not null)
            {
                rest = (unfit, FailureOf(unfit));
            }
            else if (rows.Count > 0)
            {
                rest = (null, "each row that names its text placed its own unit or covers another unit left over");
            }

            for (; index < units.Count; index++)
            {
                verdicts[units[index]] = rest;
            }
        }

        return verdicts;
    }

    // How many units of one text a row covers: the units of the inventory that it names and that have the text, or
    // the units it names by their place at the base, less those that list 1 found at its to.
    private static int Covers(LedgerRow row, List<OldUnit> units, HashSet<(string Row, string Name)> placedNames) =>
        units[0].Ids.Count > 0
            ? units[0].Ids.Count(id => row.Ids.Contains(id) && !placedNames.Contains((row.Ref, id)))
            : units.Count(unit => row.Names(unit) && !placedNames.Contains((row.Ref, $"@{unit.Unit.Id}")));

    // Why a row does not let its unit be left over, or null when it does.
    private static string? FailureOf(LedgerRow row) =>
        row switch
        {
            { Row.Disposition: "move", IsFact: false, IsPosition: false } =>
                "its row is a move, but it is not at its place word for word: a move whose words changed has fact, "
                    + "or a note that starts with position",
            { Row.Note: "-" } => "its row has no note, which a unit left over needs",
            _ => null,
        };

    // The words of a unit of the base, or the start of a block.
    private static string Display(Unit unit) => unit.Kind == "block" ? unit.Start : unit.Text;

    // ---------------------------------------------------------------------------------------------------------------
    // move
    // ---------------------------------------------------------------------------------------------------------------

    // Compares the base of a phase with the working tree, and prints four lists: text not at the place its row says,
    // text left over, text added, and <a id> anchors gone.
    private static int Move(string root, string number, TextWriter output)
    {
        var phase = LoadPhase(root, number);
        var placed = new Dictionary<(string File, string Text), int>();
        var placedNames = new HashSet<(string Row, string Name)>();
        var misplaced = NotAtPlace(phase, placed, placedNames);
        misplaced.AddRange(PartsNotAtBase(phase));
        var leftOver = LeftOver(phase);
        var verdicts = Verdicts(phase, leftOver, placedNames);
        var added = Added(phase, placed).GroupBy(unit => unit.File, StringComparer.Ordinal).ToList();
        var failingFiles = added.Count(file => PlaceOfNewText(phase, file.Key) is null);
        var gone = AnchorsGone(phase);
        output.WriteLine(
            $"move {number}: {phase.Ledger.Name}, base {Short(phase.Ledger.Base)}, {phase.Rows.Count} rows; "
                + $"{phase.OldUnits.Count} units at the base, {phase.New.Units.Count()} in the working tree."
        );
        WriteList(output, 1, "Not at its place", misplaced);
        WriteList(output, 2, "Left over", [.. leftOver.Select(unit => LeftOverEntry(phase, unit, verdicts[unit]))]);
        WriteList(output, 3, "Added", [.. added.Select(file => AddedEntry(phase, file))]);
        WriteList(output, 4, "Anchors gone", gone);
        WriteFacts(output, phase);
        foreach (var problem in phase.Problems)
        {
            output.WriteLine($"problem: {problem}");
        }

        var failing = leftOver.Count(unit => verdicts[unit].Failure is not null);
        var failures = misplaced.Count + failing + failingFiles + phase.Problems.Count;
        output.WriteLine(
            failures == 0
                ? "move passes."
                : $"move fails: {misplaced.Count} not at their place, {failing} left over with no fit row, "
                    + $"{failingFiles} files added to with no row, {phase.Problems.Count} problems of the ledger."
        );
        return failures == 0 ? 0 : 1;
    }

    private static void WriteList(TextWriter output, int number, string title, List<string> entries)
    {
        output.WriteLine(entries.Count == 0 ? $"{number}. {title}: none." : $"{number}. {title}: {entries.Count}.");
        foreach (var line in entries.SelectMany(entry => entry.Split('\n')))
        {
            output.WriteLine($"  {line}");
        }
    }

    // The rows whose fact stands at their to in new words: move looks for their file and anchor alone.
    private static void WriteFacts(TextWriter output, PhaseState phase)
    {
        var facts = phase.Rows.Where(row => row.IsFact).ToList();
        if (facts.Count > 0)
        {
            output.WriteLine("Facts in new words, looked for by their file and anchor alone:");
            foreach (var row in facts)
            {
                output.WriteLine($"  {row.Describe()}");
            }
        }
    }

    // List 1: each row that places text, and what of it is not at its place.  Each unit found whole is counted in
    // placed, by file and text.
    // A unit that list 1 finds at its row's to is counted in placed by its text and file, and in placedNames by its
    // row and its name.
    private static List<string> NotAtPlace(
        PhaseState phase,
        Dictionary<(string File, string Text), int> placed,
        HashSet<(string Row, string Name)> placedNames
    )
    {
        var entries = new List<string>();
        foreach (var row in phase.Rows.Where(Places))
        {
            if (row.Row.To == "-")
            {
                entries.Add($"{row.Describe()}\n    it does not say where its text stands, in to");
                continue;
            }

            var (units, problem) = UnitsAt(phase.New, row.Row.To);
            if (units is null)
            {
                entries.Add($"{row.Describe()}\n    {problem}");
            }
            else if (row.IsPart || row.Row.Disposition == "corrected")
            {
                entries.AddRange(WordsNotAt(row, units));
            }
            else if (!row.IsFact && !row.IsPosition)
            {
                entries.AddRange(UnitsNotAt(phase, row, units, placed, placedNames));
            }
        }

        return entries;
    }

    // A row whose text list 1 looks for: a move, a merge-pair, a restated-from with a to, a corrected row with new
    // words or a to, and each such part.
    private static bool Places(LedgerRow row) =>
        row.Row.Disposition switch
        {
            "move" or "merge-pair" => true,
            "restated-from" => row.Row.To != "-",
            "corrected" => row.Row.To != "-" || row.Row.Words != "-",
            _ => false,
        };

    // A part's words, but *, must stand inside the text of each unit it names at the base, as the baseline has them,
    // compared as the words of a to are.  A fact or a position is by its place alone.
    private static List<string> PartsNotAtBase(PhaseState phase) =>
        [
            .. phase
                .Rows.Where(row => row.IsPart && !row.IsFact && !row.IsPosition && row.Row.Words is not ("*" or "-"))
                .SelectMany(row =>
                    phase
                        .OldUnits.Where(row.Names)
                        .Where(old => Find(old.Unit.Text, row.Row.Words) < 0)
                        .Select(old =>
                            $"{row.Describe()}\n    its words are not in {old.Unit.Id} at the base: {Display(old.Unit)}"
                        )
                ),
        ];

    // A part's or a correction's words must stand inside one unit at its to.  A fact or a position is looked for by
    // its place alone, and * or - is no words.
    private static List<string> WordsNotAt(LedgerRow row, List<Unit> units) =>
        row.IsFact
        || row.IsPosition
        || row.Row.Words is "*" or "-"
        || units.Exists(unit => Holds(unit.Text, row.Row.Words))
            ? []
            : [$"{row.Describe()}\n    its words are in no unit of {row.Row.To}"];

    // Each unit that a row names must be a unit at its to.
    private static List<string> UnitsNotAt(
        PhaseState phase,
        LedgerRow row,
        List<Unit> units,
        Dictionary<(string File, string Text), int> placed,
        HashSet<(string Row, string Name)> placedNames
    )
    {
        var named = Named(phase, row);
        if (named.Count == 0)
        {
            return [$"{row.Describe()}\n    it names no unit of the inventory or of the base"];
        }

        var entries = new List<string>();
        foreach (var (name, hash, start) in named)
        {
            if (units.Find(unit => unit.Hash == hash) is { } found)
            {
                placed[(found.File, found.Text)] = placed.GetValueOrDefault((found.File, found.Text)) + 1;
                _ = placedNames.Add((row.Ref, name));
            }
            else
            {
                entries.Add($"{row.Describe()}\n    {name} is not a unit of {row.Row.To}: {start}");
            }
        }

        return entries;
    }

    // List 2: the units that the base holds more often than the working tree.  Of the units of one text, those in a
    // file that lost some are taken first, then those whose row lets them be left over, then the first.
    private static List<OldUnit> LeftOver(PhaseState phase)
    {
        var newCounts = phase
            .New.Units.CountBy(unit => unit.Text, StringComparer.Ordinal)
            .ToDictionary(StringComparer.Ordinal);
        var newInFile = phase.New.Units.CountBy(unit => (unit.File, unit.Text)).ToDictionary();
        var oldInFile = phase.OldUnits.CountBy(old => (old.Unit.File, old.Unit.Text)).ToDictionary();
        return
        [
            .. phase
                .OldUnits.GroupBy(old => old.Unit.Text, StringComparer.Ordinal)
                .SelectMany(group =>
                    group
                        .OrderBy(old =>
                            oldInFile[(old.Unit.File, old.Unit.Text)]
                            > newInFile.GetValueOrDefault((old.Unit.File, old.Unit.Text))
                                ? 0
                                : 1
                        )
                        .ThenBy(old => Rank(phase, old))
                        .ThenBy(old => old.Order)
                        .Take(group.Count() - newCounts.GetValueOrDefault(group.Key))
                )
                .OrderBy(old => old.Order),
        ];
    }

    // 0 when a row that names the unit lets it be left over, 1 when only other rows name it, 2 when none does.
    private static int Rank(PhaseState phase, OldUnit unit)
    {
        var naming = phase.Rows.Where(row => row.Names(unit)).ToList();
        if (naming.Exists(row => FailureOf(row) is null))
        {
            return 0;
        }

        return naming.Count > 0 ? 1 : 2;
    }

    // The line of a unit left over that no row judges: "no row", and why, when rows name its text but none fits.
    private static string NoRow(string? failure) =>
        failure is null or "no row names it" ? "    no row" : $"    no row fits: {failure}";

    // A unit left over: its place at the base, its names, its text, its row and what fails it.  A unit with a
    // position note is printed with the unit at its to nearest to it, and a move-split with what no part holds.
    private static string LeftOverEntry(PhaseState phase, OldUnit unit, (LedgerRow? Row, string? Failure) verdict)
    {
        var (row, failure) = verdict;
        var lines = new List<string>
        {
            $"{unit.Unit.Id}  [{unit.Names}]  {Display(unit.Unit)}",
            row is null ? NoRow(failure) : $"    row {row.Describe()}",
        };
        if (row is not null && failure is not null)
        {
            lines.Add($"    fails: {failure}");
        }

        if (row is { IsPosition: true })
        {
            lines.Add($"    nearest: {Nearest(phase, row, unit.Unit)}");
        }

        if (row is { IsPart: false, Row.Disposition: "move-split" })
        {
            lines.AddRange(Unsplit(phase, unit));
        }

        return string.Join('\n', lines);
    }

    // The unit at a row's to whose words are most like the unit's.
    private static string Nearest(PhaseState phase, LedgerRow row, Unit unit)
    {
        var (units, problem) = UnitsAt(phase.New, row.Row.To);
        if (units is not { Count: > 0 })
        {
            return problem ?? $"{row.Row.To} has no unit";
        }

        var words = WordsOf(unit.Text);
        return Display(units.MaxBy(other => Likeness(words, WordsOf(other.Text)))!);
    }

    private static double Likeness(HashSet<string> one, HashSet<string> other) =>
        (double)one.Count(other.Contains) / Math.Max(1, Math.Max(one.Count, other.Count));

    // What of a move-split unit no part holds, and each part whose words are *, which is not compared.
    private static List<string> Unsplit(PhaseState phase, OldUnit unit)
    {
        var lines = new List<string>();
        var rest = unit.Unit.Text;
        foreach (var part in phase.Rows.Where(row => row.IsPart && row.Names(unit)))
        {
            if (part.Row.Words == "*")
            {
                lines.Add($"    part not compared: {part.Describe()}");
                continue;
            }

            var found = Find(rest, part.Row.Words);
            var length = part.Row.Words.EndsWith('.') ? part.Row.Words.Length - 1 : part.Row.Words.Length;
            rest = found < 0 ? rest : string.Concat(rest.AsSpan(0, found), "\0", rest.AsSpan(found + length));
        }

        lines.AddRange(
            rest.Split('\0')
                .Select(piece => piece.Trim().TrimStart('.', ',', ';', ':').Trim())
                .Where(piece => piece.Any(char.IsLetterOrDigit))
                .Select(piece => $"    not in a part: {piece}")
        );
        return lines;
    }

    // List 3: the units that the working tree holds more often than the base, less those that list 1 placed.  Of the
    // units of one text, those in a file that gained some are taken, in reading order.
    private static List<Unit> Added(PhaseState phase, Dictionary<(string File, string Text), int> placed)
    {
        var oldCounts = phase
            .OldUnits.CountBy(old => old.Unit.Text, StringComparer.Ordinal)
            .ToDictionary(StringComparer.Ordinal);
        var oldInFile = phase.OldUnits.CountBy(old => (old.Unit.File, old.Unit.Text)).ToDictionary();
        var added = new List<Unit>();
        foreach (var group in phase.New.Units.GroupBy(unit => unit.Text, StringComparer.Ordinal))
        {
            var surplus = group.Count() - oldCounts.GetValueOrDefault(group.Key);
            foreach (var file in group.GroupBy(unit => unit.File, StringComparer.Ordinal))
            {
                var key = (file.Key, group.Key);
                var gained = file.Count() - oldInFile.GetValueOrDefault(key) - placed.GetValueOrDefault(key);
                var take = Math.Min(Math.Max(gained, 0), Math.Max(surplus, 0));
                added.AddRange(file.Take(take));
                surplus -= take;
            }
        }

        var order = phase.New.Files.Select((doc, index) => (doc.File, index)).ToDictionary(StringComparer.Ordinal);
        return [.. added.OrderBy(unit => order[unit.File]).ThenBy(unit => unit.Line)];
    }

    // The row that makes a file a place of new text: a new or a corrected row, a part, a fact or a position whose to
    // is in the file.
    private static LedgerRow? PlaceOfNewText(PhaseState phase, string file) =>
        phase.Rows.FirstOrDefault(row =>
            row.Row.To != "-"
            && row.ToFile == file
            && (row.Row.Disposition is "new" or "corrected" || row.IsPart || row.IsFact || row.IsPosition)
        );

    private static string AddedEntry(PhaseState phase, IGrouping<string, Unit> file)
    {
        var row = PlaceOfNewText(phase, file.Key);
        var lines = new List<string>
        {
            row is null
                ? $"{file.Key}: no new, corrected, part, fact or position row has it as its to"
                : $"{file.Key}: the to of {row.Ref}",
        };
        lines.AddRange(file.Select(unit => $"    {unit.Id}  {Display(unit)}"));
        return string.Join('\n', lines);
    }

    // List 4: each <a id> of the base that no user document of the working tree has.
    private static List<string> AnchorsGone(PhaseState phase)
    {
        var kept = phase.New.Files.SelectMany(AnchorIds).Select(anchor => anchor.Id).ToHashSet(StringComparer.Ordinal);
        return
        [
            .. phase.Old.Files.SelectMany(doc =>
                AnchorIds(doc)
                    .Where(anchor => !kept.Contains(anchor.Id))
                    .Select(anchor => $"{doc.File}:{anchor.Line}  {anchor.Id}")
            ),
        ];
    }

    // ---------------------------------------------------------------------------------------------------------------
    // rewrite
    // ---------------------------------------------------------------------------------------------------------------

    // Compares each user document that differs between the base and the working tree, section by section.
    private static int Rewrite(string root, string number, TextWriter output)
    {
        var phase = LoadPhase(root, number);
        var olds = phase.Old.Files.ToDictionary(doc => doc.File, StringComparer.Ordinal);
        var news = phase.New.Files.ToDictionary(doc => doc.File, StringComparer.Ordinal);
        var pages = InReadingOrder([.. olds.Keys, .. news.Keys])
            .Where(file =>
                !olds.TryGetValue(file, out var old)
                || !news.TryGetValue(file, out var current)
                || !old.Lines.SequenceEqual(current.Lines, StringComparer.Ordinal)
            )
            .ToList();
        output.WriteLine(
            $"rewrite {number}: {phase.Ledger.Name}, base {Short(phase.Ledger.Base)}, {phase.Rows.Count} rows; "
                + $"{pages.Count} pages differ."
        );
        var failures = pages.Sum(file =>
            ComparePage(phase, file, olds.GetValueOrDefault(file), news.GetValueOrDefault(file), output)
        );
        output.WriteLine();
        output.WriteLine(
            $"Words of the pages that differ: {pages.Sum(file => WordCount(olds.GetValueOrDefault(file)))} > "
                + $"{pages.Sum(file => WordCount(news.GetValueOrDefault(file)))}."
        );
        foreach (var problem in phase.Problems)
        {
            output.WriteLine($"problem: {problem}");
        }

        output.WriteLine(
            failures + phase.Problems.Count == 0
                ? "rewrite passes."
                : $"rewrite fails: {failures} differences with no row, {phase.Problems.Count} problems of the ledger."
        );
        return failures + phase.Problems.Count == 0 ? 0 : 1;
    }

    private static int WordCount(Doc? doc) => doc?.Units.Sum(unit => unit.Words) ?? 0;

    // One page: each section that differs, then each unit of the old page that is not on the new page word for word
    // and has no row.  Gives the count of what fails.
    private static int ComparePage(PhaseState phase, string file, Doc? old, Doc? current, TextWriter output)
    {
        var olds = Sections(old);
        var news = Sections(current);
        output.WriteLine();
        output.WriteLine($"{file}: words {WordCount(old)} > {WordCount(current)}");
        var failures = olds.Select(section => section.Key)
            .Concat(news.Select(section => section.Key))
            .Distinct(StringComparer.Ordinal)
            .Sum(key =>
                CompareSection(
                    phase,
                    key == Top ? file : $"{file}#{key}",
                    olds.Find(section => section.Key == key)?.Units ?? [],
                    news.Find(section => section.Key == key)?.Units ?? [],
                    output
                )
            );
        foreach (
            var unit in Lost(phase, old?.Units ?? [], current?.Units ?? []).Where(unit => NamedBy(phase, unit) is null)
        )
        {
            output.WriteLine($"  no row: {unit.Id}  [{phase.OldById[unit.Id].Names}]  {Display(unit)}");
            failures++;
        }

        return failures;
    }

    // The units of old that new holds less often, those that a row names first.
    private static IEnumerable<Unit> Lost(PhaseState phase, IReadOnlyList<Unit> old, IReadOnlyList<Unit> current)
    {
        var counts = current.CountBy(unit => unit.Text, StringComparer.Ordinal).ToDictionary(StringComparer.Ordinal);
        return old.GroupBy(unit => unit.Text, StringComparer.Ordinal)
            .SelectMany(group =>
                group
                    .OrderBy(unit => NamedBy(phase, unit) is null ? 1 : 0)
                    .ThenBy(unit => unit.Line)
                    .Take(group.Count() - counts.GetValueOrDefault(group.Key))
            )
            .OrderBy(unit => unit.Line);
    }

    // The first row that names a unit of the base.
    private static LedgerRow? NamedBy(PhaseState phase, Unit unit) =>
        phase.Rows.FirstOrDefault(row => row.Names(phase.OldById[unit.Id]));

    // One section, known in a row's to as target: when its units differ, what changed in it and the rows that name
    // it.  Gives the count of what fails.
    private static int CompareSection(
        PhaseState phase,
        string target,
        List<Unit> old,
        List<Unit> current,
        TextWriter output
    )
    {
        if (
            old.Select(unit => unit.Text)
                .Order(StringComparer.Ordinal)
                .SequenceEqual(current.Select(unit => unit.Text).Order(StringComparer.Ordinal), StringComparer.Ordinal)
        )
        {
            return 0;
        }

        var key = target.Contains('#', StringComparison.Ordinal)
            ? target[(target.IndexOf('#', StringComparison.Ordinal) + 1)..]
            : Top;
        output.WriteLine($"  section {key}: words {old.Sum(unit => unit.Words)} > {current.Sum(unit => unit.Words)}");
        WriteCounted(output, "code span", CodeSpans(old), CodeSpans(current));
        WriteCounted(output, "diagnostic", DiagnosticIds(old), DiagnosticIds(current));
        WriteCounted(output, "numeral", Numerals(old), Numerals(current));
        WriteCounted(output, "table row", TableRows(old), TableRows(current));
        foreach (var sentence in Unpartnered(old, current))
        {
            output.WriteLine($"    sentence with no partner: {sentence}");
        }

        var failures = Blocks(phase, target, old, current, output);
        var rows = phase
            .Rows.Where(row => row.Row.To == target || old.Exists(unit => row.Names(phase.OldById[unit.Id])))
            .ToList();
        foreach (var row in rows)
        {
            output.WriteLine($"    row {row.Describe()}");
        }

        if (rows.Count == 0)
        {
            output.WriteLine("    no row names this section");
            failures++;
        }

        return failures;
    }

    // Each value whose count differs between old and new, with both counts.
    private static void WriteCounted(
        TextWriter output,
        string label,
        IEnumerable<string> old,
        IEnumerable<string> current
    )
    {
        var before = old.CountBy(value => value, StringComparer.Ordinal).ToDictionary(StringComparer.Ordinal);
        var after = current.CountBy(value => value, StringComparer.Ordinal).ToDictionary(StringComparer.Ordinal);
        foreach (var value in before.Keys.Concat(after.Keys).Distinct(StringComparer.Ordinal))
        {
            var (was, now) = (before.GetValueOrDefault(value), after.GetValueOrDefault(value));
            if (was != now)
            {
                output.WriteLine($"    {label} {value}: {was} > {now}");
            }
        }
    }

    private static IEnumerable<string> CodeSpans(List<Unit> units) =>
        units
            .Where(unit => unit.Kind != "block")
            .SelectMany(unit => CodeSpanPattern().Matches(unit.Text).Select(span => span.Value));

    private static IEnumerable<string> DiagnosticIds(List<Unit> units) =>
        units.SelectMany(unit => DiagnosticPattern().Matches(unit.Text).Select(id => id.Value));

    // Runs of digits, with an inner . or , outside code spans and blocks.
    private static IEnumerable<string> Numerals(List<Unit> units) =>
        units
            .Where(unit => unit.Kind != "block")
            .SelectMany(unit => NumeralPattern().Matches(MaskCodeSpans(unit.Text)).Select(numeral => numeral.Value));

    private static IEnumerable<string> TableRows(List<Unit> units) =>
        units.Where(unit => unit.Kind == "row").Select(unit => unit.Text);

    // Each sentence of old that holds a word of Negations and has no partner in new: a sentence that holds each of
    // those words it holds, and shares at least half of the words of each of the two.
    private static IEnumerable<string> Unpartnered(List<Unit> old, List<Unit> current)
    {
        var partners = Sentences(current).Select(WordsOf).ToList();
        foreach (var sentence in Sentences(old))
        {
            var words = WordsOf(sentence);
            var negations = Negations.Where(words.Contains).ToList();
            if (
                negations.Count > 0
                && !partners.Exists(other => negations.TrueForAll(other.Contains) && Shares(words, other))
            )
            {
                yield return sentence;
            }
        }
    }

    private static IEnumerable<string> Sentences(List<Unit> units) =>
        units.Where(unit => unit.Kind != "block").SelectMany(unit => SentenceBreakPattern().Split(unit.Text));

    private static HashSet<string> WordsOf(string text) =>
        WordPattern().Matches(text).Select(word => word.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static bool Shares(HashSet<string> one, HashSet<string> other)
    {
        var common = one.Count(other.Contains);
        return common * 2 >= one.Count && common * 2 >= other.Count;
    }

    // Each fenced block of a section that changed or went, which a row must name by its unit, and each that is new,
    // which a new or corrected row whose to is the section must name.  Gives the count of those with no row.
    private static int Blocks(PhaseState phase, string target, List<Unit> old, List<Unit> current, TextWriter output)
    {
        var failures = 0;
        var oldBlocks = old.Where(unit => unit.Kind == "block").ToList();
        var newBlocks = current.Where(unit => unit.Kind == "block").ToList();
        foreach (var unit in Lost(phase, oldBlocks, newBlocks))
        {
            var row = NamedBy(phase, unit);
            output.WriteLine($"    block changed or gone: {unit.Id}  {unit.Start}");
            output.WriteLine(row is null ? "      no row" : $"      row {row.Describe()}");
            failures += row is null ? 1 : 0;
        }

        var newRow = phase.Rows.FirstOrDefault(row =>
            row.Row.Disposition is "new" or "corrected" && row.Row.To == target
        );
        var counts = oldBlocks.CountBy(unit => unit.Text, StringComparer.Ordinal).ToDictionary(StringComparer.Ordinal);
        foreach (var group in newBlocks.GroupBy(unit => unit.Text, StringComparer.Ordinal))
        {
            foreach (var unit in group.Take(group.Count() - counts.GetValueOrDefault(group.Key)))
            {
                output.WriteLine($"    new block: {unit.Id}  {unit.Start}");
                output.WriteLine(newRow is null ? "      no row" : $"      row {newRow.Describe()}");
                failures += newRow is null ? 1 : 0;
            }
        }

        return failures;
    }

    // ---------------------------------------------------------------------------------------------------------------
    // origin
    // ---------------------------------------------------------------------------------------------------------------

    // Each unit of one page of the working tree, with the id of each unit of the baseline that has its text, or new.
    private static int Origin(string root, string page, TextWriter output)
    {
        var file = page.Replace('\\', '/');
        var path = Path.Combine(root, file);
        if (!File.Exists(path))
        {
            throw new CheckException($"{file} is not a file of the working tree");
        }

        var units = Units(file, SplitLines(File.ReadAllText(path, Encoding.UTF8)));
        var baseline = Read(root, Baseline).Units.ToLookup(unit => unit.Text, StringComparer.Ordinal);
        output.WriteLine(
            $"{file}: {units.Count} units, each with its line, its words, and the baseline units of its text"
        );
        var old = 0;
        var fresh = 0;
        foreach (var unit in units)
        {
            var ids = baseline[unit.Text].Select(match => match.Id).ToList();
            output.WriteLine(
                $"  {unit.Line, 5} {unit.Words, 5}  {(ids.Count == 0 ? "new" : string.Join(", ", ids))}  {unit.Start}"
            );
            if (ids.Count == 0)
            {
                fresh += unit.Words;
            }
            else
            {
                old += unit.Words;
            }
        }

        output.WriteLine($"Words of the units with an id: {old}");
        output.WriteLine($"Words of the units marked new: {fresh}");
        return 0;
    }

    // ---------------------------------------------------------------------------------------------------------------
    // selftest
    // ---------------------------------------------------------------------------------------------------------------

    // A scratch copy of the tree under test, outside the repository: a clone that shares the repository's objects,
    // with the user documents and the epic's folder of the working tree committed as its HEAD.  Each case starts from
    // that HEAD, and the folder goes when the selftest ends.
    private static int Selftest(string root, TextWriter output)
    {
        var scratch = Path.Combine(Path.GetTempPath(), $"docs-overhaul-check-{Guid.NewGuid():N}");
        try
        {
            MakeScratch(root, scratch);
            var failed = 0;
            foreach (var (name, test) in Cases)
            {
                Reset(scratch);
                var failure = Attempt(test, scratch);
                output.WriteLine($"{(failure is null ? "pass" : "FAIL")}  {name}");
                if (failure is not null)
                {
                    failed++;
                    output.WriteLine($"      {failure.ReplaceLineEndings("\n      ")}");
                }
            }

            output.WriteLine($"selftest: {Cases.Length - failed} passed, {failed} failed");
            return Math.Min(failed, 1);
        }
        finally
        {
            if (Directory.Exists(scratch))
            {
                Directory.Delete(scratch, recursive: true);
            }
        }
    }

    // Each case is given the scratch root and gives null when it passes, or what went wrong.
    private static readonly (string Name, Func<string, string?> Test)[] Cases =
    [
        ("units: lines, kinds, words and normalised text of each kind", UnitsCase),
        ("1: a section moves with new link targets, a new <a id> and a new level: move passes", SectionMovesCase),
        ("2: the same, with a sentence cut from a paragraph: move fails and names it", SentenceCutCase),
        ("3: the same, with a character changed in a fenced block: move fails and names it", BlockChangedCase),
        ("4: a unit removed passes with a removed-status row and fails without", UnitRemovedCase),
        ("5: a unit split in two parts that leave out a sentence: move passes and prints it", UnitSplitCase),
        ("6: coverage names a due unit with no row, and a row of no unit", CoverageCase),
        ("7: rewrite prints each difference, and fails until rows name the blocks and the section", RewriteCase),
        ("8: inventory, run a second time, changes no byte", InventoryCase),
        ("9: a move that mends 'above' passes with a position note and fails without", PositionCase),
        ("10: a move-split with no part may name a heading, not a paragraph", MoveSplitCase),
        ("11: one copy of a text that stands twice removed with no row: move fails and names it", RepeatedRowCase),
        ("12: a move-split part whose words carry a changed numeral: move fails and names the part", PartChangedCase),
        ("13: two copies of a text, named by a move and a removed-status row: each row covers one", CountedRowsCase),
        ("14: a restated-from, merge-pair or move row whose copy stands at its to covers no other copy", PlacedRowCase),
        ("15: the same, with rows that name the copies by their place, @<file>:<line>", PlacedPlaceRowCase),
    ];

    // A case that throws fails, with the exception's message.
    private static string? Attempt(Func<string, string?> test, string scratch)
    {
        try
        {
            return test(scratch);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return $"{exception.GetType().Name}: {exception.Message}";
        }
    }

    private static string? UnitsCase(string scratch)
    {
        string[] lines =
        [
            "## A heading",
            "",
            "A link [`[x]` value](a.md#b), an ![image](i.png) and `[a](b.md)`.",
            "",
            "- A bullet",
            "<a id=\"here\"></a>",
            "",
            "| A | B |",
            "|----|----|",
            "| 1 | 2 |",
            "",
            "> A quote",
            "",
            "1. A step:",
            "",
            "  ```sql postgres",
            "  select 1;",
            "    ```",
            "",
            "After the <a id=\"x\"></a>block.",
        ];
        (string Id, int Last, string Kind, int Words, string Text)[] expected =
        [
            ("t.md:1", 1, "heading", 3, "A heading"),
            ("t.md:3", 3, "para", 8, "A link `[x]` value, an image and `[a](b.md)`."),
            ("t.md:5", 5, "bullet", 3, "- A bullet"),
            ("t.md:8", 8, "row", 5, "| A | B |"),
            ("t.md:10", 10, "row", 5, "| 1 | 2 |"),
            ("t.md:12", 12, "quote", 3, "> A quote"),
            ("t.md:14", 14, "bullet", 3, "1. A step:"),
            ("t.md:16", 18, "block", 5, "```sql postgres\nselect 1;\n  ```"),
            ("t.md:20", 20, "para", 4, "After the block."),
        ];
        var actual = Units("t.md", lines)
            .Select(unit => (unit.Id, unit.Last, unit.Kind, unit.Words, unit.Text))
            .ToList();
        if (!actual.SequenceEqual(expected))
        {
            return $"expected\n{string.Join('\n', expected)}\ngot\n{string.Join('\n', actual)}";
        }

        // printf 'A heading' | shasum -a 256
        const string HashOfAHeading = "0fcfafb36c4b";
        if (Units("t.md", lines)[0].Hash != HashOfAHeading)
        {
            return $"the hash of 'A heading' is not {HashOfAHeading}";
        }

        var block = Units("t.md", lines)[7];
        return block.Start != "```sql postgres" ? $"the start of the block is {block.Start}" : null;
    }

    private static string? CoverageCase(string scratch)
    {
        var due = LoadInventory(scratch).Where(entry => entry.Phase <= 1).ToList();
        WriteLedger(scratch, 1, due.Select(MoveRow));
        var (code, text) = RunCommand(scratch, "coverage");
        if (code != 0)
        {
            return $"with a row for each unit of phase 1, coverage exits with {code}:\n{text}";
        }

        WriteLedger(scratch, 1, due.Skip(1).Select(MoveRow));
        (code, text) = RunCommand(scratch, "coverage");
        if (code != 1 || !text.Contains($"no row: {due[0].Id}", StringComparison.Ordinal))
        {
            return $"without the row of {due[0].Id}, coverage exits with {code}:\n{text}";
        }

        var units = LoadInventory(scratch).Select(entry => entry.Id).ToHashSet(StringComparer.Ordinal);
        var stray = Enumerable.Range(1, 100).Select(line => $"site/tool.md:{line}").First(id => !units.Contains(id));
        WriteLedger(scratch, 1, [.. due.Select(MoveRow), $"{stray}\tmove\tsite/tool.md\t-\t-\t-"]);
        (code, text) = RunCommand(scratch, "coverage");
        return code != 1 || !text.Contains($"problem: phase-01.tsv:{due.Count + 4}: {stray}", StringComparison.Ordinal)
            ? $"with a row of {stray}, which is no unit, coverage exits with {code}:\n{text}"
            : null;
    }

    private static string? InventoryCase(string scratch)
    {
        var path = Path.Combine(scratch, InventoryFile);
        var committed = File.ReadAllBytes(path);
        var (code, text) = RunCommand(scratch, "inventory");
        if (code != 0)
        {
            return $"inventory exits with {code}:\n{text}";
        }

        var first = File.ReadAllBytes(path);
        (code, text) = RunCommand(scratch, "inventory");
        var second = File.ReadAllBytes(path);
        if (code != 0)
        {
            return $"inventory, run again, exits with {code}:\n{text}";
        }

        if (!first.SequenceEqual(committed) || !second.SequenceEqual(first))
        {
            return "inventory.tsv differs from the one of the tree under test, or after a second run";
        }

        var status = Git(scratch, "status", "--porcelain");
        return status.Length > 0 ? $"the runs left changes:\n{status}" : null;
    }

    private static string? MoveSplitCase(string scratch)
    {
        var inventory = LoadInventory(scratch);
        var due = inventory.Where(entry => entry.Phase <= 1).Select(MoveRow).ToList();
        var heading = inventory.First(entry => entry.Kind == "heading");
        var paragraph = inventory.First(entry => entry.Kind == "para");
        WriteLedger(scratch, 1, [.. due, $"{heading.Id}\tmove-split\t-\t-\t-\theading only: the title goes"]);
        var (code, text) = RunCommand(scratch, "coverage");
        if (code != 0)
        {
            return $"with a move-split of no part on the heading {heading.Id}, coverage exits with {code}:\n{text}";
        }

        WriteLedger(scratch, 1, [.. due, $"{paragraph.Id}\tmove-split\t-\t-\t-\theading only: the title goes"]);
        (code, text) = RunCommand(scratch, "coverage");
        return code != 1 || !text.Contains($"{paragraph.Id}: a move-split with no part", StringComparison.Ordinal)
            ? $"with a move-split of no part on the paragraph {paragraph.Id}, coverage exits with {code}:\n{text}"
            : null;
    }

    // A row that moves a unit of the inventory to its own file.
    private static string MoveRow(Entry entry) => $"{entry.Id}\tmove\t{entry.File}\t-\t-\t-";

    // The new page that cases 1 to 3 and 9 move text to, and the pages that cases 7 and 9 commit first.
    private const string MovedPage = "site/selftest-moved.md";
    private const string RewritePage = "site/selftest-rewrite.md";
    private const string PositionPage = "site/selftest-position.md";

    private static string? SectionMovesCase(string scratch)
    {
        var move = MoveASection(scratch);
        move.Write(scratch);
        var (code, text) = RunCommand(scratch, "move", "01");
        return
            code != 0
            || !text.Contains("\n1. Not at its place: none.", StringComparison.Ordinal)
            || !text.Contains("\n2. Left over: none.", StringComparison.Ordinal)
            ? $"with the section of {move.File}:{move.Heading.Line} moved whole, move exits with {code}:\n{text}"
            : null;
    }

    private static string? SentenceCutCase(string scratch)
    {
        var move = MoveASection(scratch);
        var line = move.Moved[move.ParagraphIndex];
        move.Moved[move.ParagraphIndex] = line[..SentenceBreakPattern().Matches(line)[^1].Index];
        move.Write(scratch);
        var (code, text) = RunCommand(scratch, "move", "01");
        return code != 1 || !ListOf(text, 2).Contains($"  {move.Paragraph.Id}  ", StringComparison.Ordinal)
            ? $"with a sentence cut from {move.Paragraph.Id}, move exits with {code}:\n{text}"
            : null;
    }

    private static string? BlockChangedCase(string scratch)
    {
        var move = MoveASection(scratch);
        var line = move.Moved[move.BlockIndex];
        move.Moved[move.BlockIndex] = line[..^1] + (line[^1] == 'x' ? 'y' : 'x');
        move.Write(scratch);
        var (code, text) = RunCommand(scratch, "move", "01");
        return code != 1 || !ListOf(text, 2).Contains($"  {move.Block.Id}  ", StringComparison.Ordinal)
            ? $"with a character changed in the block {move.Block.Id}, move exits with {code}:\n{text}"
            : null;
    }

    private static string? UnitRemovedCase(string scratch)
    {
        var docs = Read(scratch, null);
        var unit = UniqueParagraph(docs, 1);
        var lines = LinesOf(scratch, unit.File);
        lines.RemoveAt(unit.Line - 1);
        WriteLines(scratch, unit.File, lines);
        WriteLedger(scratch, 1, [$"{NameOf(scratch, unit)}\tremoved-status\t-\t-\t-\tstatus: the selftest cuts it"]);
        var (code, text) = RunCommand(scratch, "move", "01");
        if (code != 0 || !ListOf(text, 2).Contains($"  {unit.Id}  ", StringComparison.Ordinal))
        {
            return $"with {unit.Id} removed and a removed-status row, move exits with {code}:\n{text}";
        }

        WriteLedger(scratch, 1, []);
        (code, text) = RunCommand(scratch, "move", "01");
        return code != 1 || !ListOf(text, 2).Contains($"  {unit.Id}  ", StringComparison.Ordinal)
            ? $"with {unit.Id} removed and no row, move exits with {code}:\n{text}"
            : null;
    }

    private static string? UnitSplitCase(string scratch)
    {
        var docs = Read(scratch, null);
        var unit = UniqueParagraph(docs, 2);
        var breaks = SentenceBreakPattern().Matches(unit.Text);
        var first = unit.Text[..breaks[0].Index];
        var leftOut = unit.Text[(breaks[0].Index + breaks[0].Length)..breaks[1].Index];
        var second = unit.Text[(breaks[1].Index + breaks[1].Length)..];
        var lines = LinesOf(scratch, unit.File);
        lines[unit.Line - 1] = first;
        lines.InsertRange(unit.Line, ["", second]);
        WriteLines(scratch, unit.File, lines);
        var name = NameOf(scratch, unit);
        WriteLedger(
            scratch,
            1,
            [
                $"{name}\tmove-split\t-\t-\t-\tsplit: the selftest leaves out a sentence",
                $"{name}/1\tmove\t{unit.File}\t-\t{first}\t-",
                $"{name}/2\tmove\t{unit.File}\t-\t{second}\t-",
            ]
        );
        var (code, text) = RunCommand(scratch, "move", "01");
        return code != 0 || !ListOf(text, 2).Contains(leftOut.TrimEnd('.'), StringComparison.Ordinal)
            ? $"with {unit.Id} split and '{leftOut}' left out, move exits with {code}:\n{text}"
            : null;
    }

    private static char Other(char digit) => digit == '9' ? '8' : '9';

    private static string? PartChangedCase(string scratch)
    {
        var docs = Read(scratch, null);
        var unit = UniqueParagraph(docs, 2);
        var breaks = SentenceBreakPattern().Matches(unit.Text);
        var first = unit.Text[..breaks[0].Index];
        var second = unit.Text[(breaks[1].Index + breaks[1].Length)..];
        var changed = string.Concat(
            first.Select(character => char.IsAsciiDigit(character) ? Other(character) : character)
        );
        if (changed == first)
        {
            changed += " 9";
        }

        var lines = LinesOf(scratch, unit.File);
        lines[unit.Line - 1] = changed;
        lines.InsertRange(unit.Line, ["", second]);
        WriteLines(scratch, unit.File, lines);
        var name = NameOf(scratch, unit);
        WriteLedger(
            scratch,
            1,
            [
                $"{name}\tmove-split\t-\t-\t-\tsplit: the selftest changes a numeral",
                $"{name}/1\tmove\t{unit.File}\t-\t{changed}\t-",
                $"{name}/2\tmove\t{unit.File}\t-\t{second}\t-",
            ]
        );
        var (code, text) = RunCommand(scratch, "move", "01");
        return code != 1 || !ListOf(text, 1).Contains($"{name}/1", StringComparison.Ordinal)
            ? $"with a changed numeral in the words of {name}/1, move exits with {code}:\n{text}"
            : null;
    }

    private static string? RewriteCase(string scratch)
    {
        string[] before =
        [
            "# Selftest rewrite page",
            "",
            "The selftest of the checker writes this page.",
            "",
            "<a id=\"first-section\"></a>",
            "",
            "## First section",
            "",
            "Run `selftest-rewrite --check` before the first step.",
            "",
            "The first step takes 12 seconds.",
            "",
            "The page is not a page of the manual.",
            "",
            "```console",
            "$ selftest-rewrite --old",
            "```",
            "",
            "## Second section",
            "",
            "The second section stays as it is.",
        ];
        WriteLines(scratch, RewritePage, before);
        CommitScratch(scratch, "A page to rewrite");
        string[] after =
        [
            .. before[..8],
            "Run the selftest before the first step.",
            "",
            "The first step takes 15 seconds.",
            "",
            "The page is a page of the manual.",
            "",
            "```console",
            "$ selftest-rewrite --new",
            "```",
            "",
            "```console",
            "$ selftest-rewrite --added",
            "```",
            .. before[17..],
        ];
        WriteLines(scratch, RewritePage, after);
        var section = $"{RewritePage}#first-section";
        string[] rows =
        [
            $"@{RewritePage}:9\tmove\t{section}\t-\t-\tthe code span goes",
            $"@{RewritePage}:11\tmove\t{section}\t-\t-\tthe time is 15 seconds",
            $"@{RewritePage}:13\tmove\t{section}\t-\t-\tthe page is one",
            $"@{RewritePage}:15\tcorrected\t{section}\t-\t*\tthe flag is --new",
            $"-\tnew\t{section}\t-\t-\ta second example",
        ];
        // A corrected row whose to is the section names its new blocks too: without it and the new row, the block of
        // line 15 has a row, a move, and the new blocks have none.
        string[] noBlockRow = [.. rows[..3], rows[4]];
        string[] noNewRow = [.. rows[..3], $"@{RewritePage}:15\tmove\t{section}\t-\t-\tthe flag is --new"];
        return RewriteDifferences(scratch)
            ?? RewriteFails(scratch, noBlockRow, $"block changed or gone: {RewritePage}:15  ```console\n      no row")
            ?? RewriteFails(scratch, noNewRow, $"new block: {RewritePage}:19  ```console\n      no row")
            ?? RewritePasses(scratch, rows)
            ?? RewriteSection(scratch, before);
    }

    // With no row, rewrite prints the five differences of RewriteCase and fails.
    private static string? RewriteDifferences(string scratch)
    {
        WriteLedger(scratch, 1, []);
        var (code, text) = RunCommand(scratch, "rewrite", "01");
        string[] expected =
        [
            "code span `selftest-rewrite --check`: 1 > 0",
            "numeral 12: 1 > 0",
            "numeral 15: 0 > 1",
            "sentence with no partner: The page is not a page of the manual.",
            $"block changed or gone: {RewritePage}:15  ```console",
            $"new block: {RewritePage}:19  ```console",
        ];
        var missing = expected.Where(line => !text.Contains(line, StringComparison.Ordinal)).ToList();
        return code != 1 || missing.Count > 0
            ? $"with no row, rewrite exits with {code} and does not print {string.Join("; ", missing)}:\n{text}"
            : null;
    }

    // With a row missing, rewrite fails and prints what that row would name.
    private static string? RewriteFails(string scratch, string[] rows, string expected)
    {
        WriteLedger(scratch, 1, rows);
        var (code, text) = RunCommand(scratch, "rewrite", "01");
        return code != 1 || !text.Contains(expected, StringComparison.Ordinal)
            ? $"with the rows {string.Join("; ", rows)}, rewrite exits with {code}, not printing {expected}:\n{text}"
            : null;
    }

    private static string? RewritePasses(string scratch, string[] rows)
    {
        WriteLedger(scratch, 1, rows);
        var (code, text) = RunCommand(scratch, "rewrite", "01");
        return code != 0 || !text.Contains("sentence with no partner", StringComparison.Ordinal)
            ? $"with every row, rewrite exits with {code}, or does not print the differences:\n{text}"
            : null;
    }

    // A paragraph added to a section, and nothing else: it fails until a row names the section.
    private static string? RewriteSection(string scratch, string[] before)
    {
        WriteLines(scratch, RewritePage, [.. before, "", "A paragraph that the rewrite adds."]);
        WriteLedger(scratch, 1, []);
        var (code, text) = RunCommand(scratch, "rewrite", "01");
        if (code != 1 || !text.Contains("section second-section", StringComparison.Ordinal))
        {
            return $"with a paragraph added to a section and no row, rewrite exits with {code}:\n{text}";
        }

        WriteLedger(scratch, 1, [$"-\tnew\t{RewritePage}#second-section\t-\t-\ta paragraph"]);
        (code, text) = RunCommand(scratch, "rewrite", "01");
        return code != 0 ? $"with a new row for the section, rewrite exits with {code}:\n{text}" : null;
    }

    private static string? PositionCase(string scratch)
    {
        WriteLines(scratch, PositionPage, ["# Selftest position page", "", "The list above names every flag."]);
        CommitScratch(scratch, "A page with a position");
        WriteLines(scratch, PositionPage, ["# Selftest position page"]);
        WriteLines(scratch, MovedPage, ["# Selftest moved page", "", "The list on this page names every flag."]);
        var row = $"@{PositionPage}:3\tmove\t{MovedPage}\t-\t-";
        WriteLedger(scratch, 1, [$"{row}\tposition: \"above\" > \"on this page\""]);
        var (code, text) = RunCommand(scratch, "move", "01");
        var listed = ListOf(text, 2);
        if (
            code != 0
            || !listed.Contains($"  {PositionPage}:3  ", StringComparison.Ordinal)
            || !listed.Contains("nearest: The list on this page names every flag.", StringComparison.Ordinal)
        )
        {
            return $"with a position note, move exits with {code}, or does not list the unit:\n{text}";
        }

        WriteLedger(scratch, 1, [$"{row}\t-"]);
        (code, text) = RunCommand(scratch, "move", "01");
        return code != 1 ? $"without the position note, move exits with {code}:\n{text}" : null;
    }

    private static string? RepeatedRowCase(string scratch)
    {
        var (repeated, units, _) = RepeatedText(scratch);
        RemoveUnits(scratch, units.Take(1));
        WriteLedger(scratch, 1, []);
        var (code, text) = RunCommand(scratch, "move", "01");
        return code != 1 || !ListOf(text, 2).Contains($"  {repeated}\n      no row", StringComparison.Ordinal)
            ? $"with one {repeated} removed and no row, move exits with {code}:\n{text}"
            : null;
    }

    // Two copies of one text, named by a move row and a removed-status row.  With one copy left over, move passes
    // whichever row comes first; with both left over, one is judged by the removed-status row and the other by the
    // move, whichever row comes first.
    private static string? CountedRowsCase(string scratch)
    {
        const string MoveFails = "fails: its row is a move";
        var (repeated, units, ids) = RepeatedText(scratch);
        string[] rows =
        [
            $"{ids[0]}\tmove\t{units[1].File}\t-\t-\t-",
            $"{ids[1]}\tremoved-status\t-\t-\t-\tstatus: the selftest cuts one copy",
        ];
        string[][] orders = [rows, [rows[1], rows[0]]];
        RemoveUnits(scratch, units.Take(1));
        foreach (var order in orders)
        {
            WriteLedger(scratch, 1, order);
            var (code, text) = RunCommand(scratch, "move", "01");
            if (code != 0)
            {
                return $"with one {repeated} left over and rows in this order, move exits with {code}:\n{text}";
            }
        }

        Reset(scratch);
        RemoveUnits(scratch, units.Take(2));
        foreach (var order in orders)
        {
            WriteLedger(scratch, 1, order);
            var (code, text) = RunCommand(scratch, "move", "01");
            var moves = ListOf(text, 2).Split('\n').Count(line => line.Contains(MoveFails, StringComparison.Ordinal));
            if (code != 1 || moves != 1)
            {
                return $"with both {repeated} left over, {moves} are judged by the move and move exits with {code}:\n"
                    + text;
            }
        }

        return null;
    }

    // Two copies of one text: a restated-from row with a to, a merge-pair row, each with a note, or a plain move row
    // names the copy that stays at its to word for word, and the other copy is cut with no row.  The placed copy's
    // row covers no other copy and is not blamed for it, so move fails saying so, and passes once a removed-status
    // row names the cut one.
    private static string? PlacedRowCase(string scratch)
    {
        var (repeated, units, ids) = RepeatedText(scratch);
        RemoveUnits(scratch, units.Take(1));
        string[] placing =
        [
            $"{ids[1]}\trestated-from\t{units[1].File}\t{units[1].File}#owner\t-\tthe rewrite of the page cuts it",
            $"{ids[1]}\tmerge-pair\t{units[1].File}\tMP-01\t-\tplaced beside its pair",
            $"{ids[1]}\tmove\t{units[1].File}\t-\t-\t-",
        ];
        var removed = $"{ids[0]}\tremoved-status\t-\t-\t-\tstatus: the selftest cuts one copy";
        foreach (var row in placing)
        {
            WriteLedger(scratch, 1, [row]);
            var (code, text) = RunCommand(scratch, "move", "01");
            if (
                code != 1
                || !ListOf(text, 2).Contains(repeated, StringComparison.Ordinal)
                || !ListOf(text, 2).Contains("placed its own unit", StringComparison.Ordinal)
            )
            {
                return $"with one {repeated} cut and only the row of the placed copy, move exits with {code}:\n{text}";
            }

            WriteLedger(scratch, 1, [row, removed]);
            (code, text) = RunCommand(scratch, "move", "01");
            if (code != 0)
            {
                return $"with a removed-status row for the cut {repeated}, move exits with {code}:\n{text}";
            }
        }

        return null;
    }

    // Two copies of a paragraph that the selftest writes twice into the base, so that the inventory has no id for it
    // and a row names each copy by its place, @<file>:<line>.  A restated-from, merge-pair or move row names the copy
    // that stays at its to, and the other copy is cut with no row: move fails, and passes once a removed-status row
    // names the cut copy.
    private static string? PlacedPlaceRowCase(string scratch)
    {
        const string Twice = "A paragraph that the selftest writes twice, which no page of the baseline holds.";
        var lines = LinesOf(scratch, Readme);
        lines.AddRange(["", Twice, "", Twice]);
        WriteLines(scratch, Readme, lines);
        CommitScratch(scratch, "the selftest writes a paragraph twice");
        var units = Read(scratch, null).Units.Where(unit => unit.Text == Twice).ToList();
        if (units.Count != 2)
        {
            return $"{Readme} holds the paragraph {units.Count} times, not twice";
        }

        RemoveUnits(scratch, units.Take(1));
        string[] placing =
        [
            $"@{units[1].Id}\trestated-from\t{Readme}\t{Readme}#owner\t-\tthe rewrite of the page cuts it",
            $"@{units[1].Id}\tmerge-pair\t{Readme}\tMP-01\t-\tplaced beside its pair",
            $"@{units[1].Id}\tmove\t{Readme}\t-\t-\t-",
        ];
        var removed = $"@{units[0].Id}\tremoved-status\t-\t-\t-\tstatus: the selftest cuts one copy";
        foreach (var row in placing)
        {
            WriteLedger(scratch, 1, [row]);
            var (code, text) = RunCommand(scratch, "move", "01");
            if (code != 1 || !ListOf(text, 2).Contains("placed its own unit", StringComparison.Ordinal))
            {
                return $"with one copy cut and only the @ row of the placed copy, move exits with {code}:\n{text}";
            }

            WriteLedger(scratch, 1, [row, removed]);
            (code, text) = RunCommand(scratch, "move", "01");
            if (code != 0)
            {
                return $"with an @ removed-status row for the cut copy, move exits with {code}:\n{text}";
            }
        }

        return null;
    }

    // A text that stands at least twice in the tree under test, as no heading and no block, with at least two ids of
    // the inventory: the text, its units in the tree and the ids.
    private static (string Text, List<Unit> Units, List<string> Ids) RepeatedText(string scratch)
    {
        var baseline = LoadInventory(scratch).ToLookup(entry => entry.Hash, StringComparer.Ordinal);
        foreach (
            var group in Read(scratch, null)
                .Units.Where(unit => unit.Kind is not ("heading" or "block"))
                .GroupBy(unit => unit.Text, StringComparer.Ordinal)
        )
        {
            var units = group.ToList();
            var ids = baseline[units[0].Hash].Select(entry => entry.Id).ToList();
            if (units.Count >= 2 && ids.Count >= 2)
            {
                return (group.Key, units, ids);
            }
        }

        throw new InvalidOperationException("no text stands twice in the tree under test with two ids");
    }

    // Takes the lines of units out of the tree under test, the last first so that the earlier lines keep their place.
    private static void RemoveUnits(string scratch, IEnumerable<Unit> units)
    {
        foreach (var unit in units.OrderByDescending(unit => unit.Line))
        {
            var lines = LinesOf(scratch, unit.File);
            lines.RemoveRange(unit.Line - 1, unit.Last - unit.Line + 1);
            WriteLines(scratch, unit.File, lines);
        }
    }

    // A section of the tree under test, moved to MovedPage: its <a id> renamed, its heading made a level 1, and each
    // link target in it changed.  Rows move each of its units to the new anchor.
    private sealed record SectionMove(
        string File,
        List<string> Source,
        List<string> Moved,
        List<string> Rows,
        Unit Heading,
        Unit Paragraph,
        int ParagraphIndex,
        Unit Block,
        int BlockIndex
    )
    {
        public void Write(string scratch)
        {
            WriteLines(scratch, File, Source);
            WriteLines(scratch, MovedPage, Moved);
            WriteLedger(scratch, 1, Rows);
        }
    }

    // The first section of the tree under test with an <a id> above a heading of level 2 or more, a link, a
    // paragraph of two sentences and a fenced block with a line in it.
    private static SectionMove MoveASection(string scratch)
    {
        var baseline = LoadInventory(scratch).ToLookup(entry => entry.Hash, StringComparer.Ordinal);
        return Read(scratch, null)
                .Files.SelectMany(doc => doc.AnchorLines.Select(line => MoveOf(doc, line, baseline)))
                .FirstOrDefault(move => move is not null)
            ?? throw new InvalidOperationException(
                "no section has an <a id>, a heading, a link, a paragraph of two sentences and a fenced block"
            );
    }

    private static SectionMove? MoveOf(Doc doc, int anchorLine, ILookup<string, Entry> baseline)
    {
        var heading = doc.Units.FirstOrDefault(unit => unit.Line > anchorLine);
        if (heading is not { Kind: "heading" } || LevelOf(doc, heading) < 2)
        {
            return null;
        }

        var level = LevelOf(doc, heading);
        var units = doc
            .Units.SkipWhile(unit => unit != heading)
            .TakeWhile(unit => unit == heading || unit.Kind != "heading" || LevelOf(doc, unit) > level)
            .ToList();
        var paragraph = units.Find(unit =>
            unit.Kind == "para" && SentenceBreakPattern().IsMatch(doc.Lines[unit.Line - 1].Trim())
        );
        var block = units.Find(unit =>
            unit.Kind == "block" && doc.Lines.Skip(unit.Line).Take(unit.Last - unit.Line - 1).Any(IsText)
        );
        var lines = units.Where(unit => unit.Kind != "block").Select(unit => doc.Lines[unit.Line - 1]).ToList();
        if (
            paragraph is null
            || block is null
            || !lines.Exists(line => Retarget(line) != line)
            || !lines.TrueForAll(line => Normalise(Retarget(line)) == Normalise(line))
        )
        {
            return null;
        }

        var id = InlineAnchorPattern().Match(doc.Lines[anchorLine - 1]).Groups["id"].Value;
        var moved = new List<string> { $"<a id=\"moved-{id}\"></a>", "" };
        var (paragraphIndex, blockIndex) = (0, 0);
        for (var line = heading.Line; line <= units[^1].Last; line++)
        {
            var text = doc.Lines[line - 1];
            paragraphIndex = line == paragraph.Line ? moved.Count : paragraphIndex;
            if (blockIndex == 0 && line > block.Line && line < block.Last && IsText(text))
            {
                blockIndex = moved.Count;
            }
            if (units.Exists(unit => unit.Kind == "block" && unit.Line <= line && line <= unit.Last))
            {
                moved.Add(text);
            }
            else
            {
                moved.Add(line == heading.Line ? "#" + Retarget(text.Trim().TrimStart('#')) : Retarget(text));
            }
        }

        var rows = units
            .Select(unit => $"{NameOf(baseline, unit)}\tmove\t{MovedPage}#moved-{id}\t-\t-\t-")
            .Distinct(StringComparer.Ordinal)
            .ToList();
        List<string> source = [.. doc.Lines.Take(anchorLine - 1), .. doc.Lines.Skip(units[^1].Last)];
        return new SectionMove(doc.File, source, moved, rows, heading, paragraph, paragraphIndex, block, blockIndex);
    }

    private static bool IsText(string line) => line.Trim().Length > 0;

    // Each link target of a line, outside code spans, made another.
    private static string Retarget(string line)
    {
        var text = new StringBuilder(line);
        foreach (var target in TargetStartPattern().Matches(MaskCodeSpans(line)).Reverse())
        {
            _ = text.Insert(target.Index + target.Length, "selftest/");
        }

        return text.ToString();
    }

    // The first paragraph of the tree under test whose text stands once, with at least the sentence breaks given.
    private static Unit UniqueParagraph(Docs docs, int breaks)
    {
        var counts = docs.Units.CountBy(unit => unit.Text, StringComparer.Ordinal).ToDictionary(StringComparer.Ordinal);
        return docs.Units.FirstOrDefault(unit =>
                unit.Kind == "para" && counts[unit.Text] == 1 && SentenceBreakPattern().Count(unit.Text) >= breaks
            ) ?? throw new InvalidOperationException($"no paragraph stands once with {breaks} sentence breaks");
    }

    // How a row names a unit of the tree under test: by the first id of the baseline that has its text, or by its
    // place.
    private static string NameOf(string scratch, Unit unit) =>
        NameOf(LoadInventory(scratch).ToLookup(entry => entry.Hash, StringComparer.Ordinal), unit);

    private static string NameOf(ILookup<string, Entry> baseline, Unit unit) =>
        baseline[unit.Hash].Select(entry => entry.Id).FirstOrDefault() ?? $"@{unit.File}:{unit.Line}";

    // The lines of list n of what move prints, from its title to the next list's.
    private static string ListOf(string text, int number)
    {
        var start = text.IndexOf($"\n{number}. ", StringComparison.Ordinal);
        if (start < 0)
        {
            return "";
        }

        var end = text.IndexOf($"\n{number + 1}. ", start + 1, StringComparison.Ordinal);
        return text[start..(end < 0 ? text.Length : end)];
    }

    private static List<string> LinesOf(string scratch, string file) =>
        [.. SplitLines(File.ReadAllText(Path.Combine(scratch, file), Encoding.UTF8))];

    private static void WriteLines(string scratch, string file, IEnumerable<string> lines)
    {
        var path = Path.Combine(scratch, file);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Concat(lines.Select(line => line + "\n")));
    }

    // Runs a command on the scratch copy, and gives its code and what it printed.
    internal static (int Code, string Output) RunCommand(string scratch, params string[] args)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        var code = Run(scratch, args, output);
        return (code, output.ToString());
    }

    // Writes ledger/phase-<nn>.tsv of the scratch copy, whose base is the scratch HEAD, with the rows given.
    internal static void WriteLedger(string scratch, int phase, IEnumerable<string> rows)
    {
        var head = Git(scratch, "rev-parse", "HEAD").Trim();
        var text = new StringBuilder().Append(
            CultureInfo.InvariantCulture,
            $"# base {head}\n# A ledger of the selftest.\n{LedgerHeader}\n"
        );
        foreach (var row in rows)
        {
            _ = text.Append(row).Append('\n');
        }

        var folder = Directory.CreateDirectory(Path.Combine(scratch, LedgerFolder));
        File.WriteAllText(Path.Combine(folder.FullName, LedgerName(phase)), text.ToString());
    }

    // Each case starts from the tree under test, the commit tagged UnderTest, though the case before committed.
    private static void Reset(string scratch)
    {
        _ = Git(scratch, "reset", "--quiet", "--hard", UnderTest);
        _ = Git(scratch, "clean", "--quiet", "--force", "-d");
    }

    private const string UnderTest = "under-test";

    private static readonly string[] ScratchPaths = [Readme, ToolReadme, Site, Epic];

    private static void MakeScratch(string root, string scratch)
    {
        _ = Git(root, "clone", "--quiet", "--shared", "--no-checkout", root, scratch);
        _ = Git(scratch, ["checkout", "HEAD", "--", .. ScratchPaths]);

        // The working tree as git sees it: tracked files and new ones that are not ignored, but not a deleted file.
        var files = Git(root, ["ls-files", "--cached", "--others", "--exclude-standard", "-z", "--", .. ScratchPaths])
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal)
            .Where(file => File.Exists(Path.Combine(root, file)))
            .ToList();
        foreach (var path in ScratchPaths.Select(path => Path.Combine(scratch, path)))
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        foreach (var file in files)
        {
            var target = Path.Combine(scratch, file);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(root, file), target);
        }

        CommitScratch(scratch, "The tree under test");
        _ = Git(scratch, "tag", UnderTest);
    }

    // Commits the scratch copy's user documents and the epic's folder: a case that does so gives its ledger that base.
    private static void CommitScratch(string scratch, string message)
    {
        _ = Git(scratch, ["add", "--all", "--", .. ScratchPaths]);
        _ = Git(
            scratch,
            "-c",
            "user.name=check",
            "-c",
            "user.email=check@example.invalid",
            "-c",
            "commit.gpgsign=false",
            "commit",
            "--quiet",
            "--allow-empty",
            "--no-verify",
            "--message",
            message
        );
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------------------------------

    private static IEnumerable<(string Line, int Number)> Numbered(string[] lines) =>
        lines.Select((line, index) => (line, index + 1));

    private static string Spaced(string header) => header.Replace('\t', ' ');

    private static string Short(string sha) => sha.Length >= 8 ? sha[..8] : "none";

    // Runs git in a folder and gives what it printed, or stops the command when git fails.
    internal static string Git(string directory, params string[] arguments)
    {
        var (code, output, error) = RunGit(directory, arguments);
        return code == 0
            ? output
            : throw new CheckException($"git {string.Join(' ', arguments)} failed: {error.Trim()}");
    }

    internal static (int Code, string Output, string Error) RunGit(string directory, params string[] arguments)
    {
#pragma warning disable S4036 // git is the one on the PATH, as for check.sh.
        var start = new ProcessStartInfo("git")
#pragma warning restore S4036
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        start.ArgumentList.Add("-C");
        start.ArgumentList.Add(directory);
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new CheckException("git did not start");
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output, error.GetAwaiter().GetResult());
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Patterns
    // ---------------------------------------------------------------------------------------------------------------

    // The start of a fenced block: three or more backticks or tildes, then the info string.
    [GeneratedRegex(@"^( *)(`{3,}|~{3,})(.*)$")]
    private static partial Regex OpeningPattern();

    // A heading of one to six # marks, and its text without them, a closing run of # included.
    [GeneratedRegex(@"^#{1,6}(?:[ \t]+(.*?))?(?:[ \t]+#+)?[ \t]*$")]
    private static partial Regex HeadingPattern();

    // An item of a list: -, * or +, or a number and . or ), then a space or nothing.
    [GeneratedRegex(@"^(?:[-*+]|\d{1,9}[.)])(?:[ \t]|$)")]
    private static partial Regex BulletPattern();

    // The rule line under the header of a table, such as |----|:---:|.
    [GeneratedRegex(@"^\|?(?:[ \t]*:?-+:?[ \t]*\|)+(?:[ \t]*:?-+:?[ \t]*)?$")]
    private static partial Regex TableRulePattern();

    // A line of only <a id="..."></a> anchors.
    [GeneratedRegex(@"^(?:<a\s+(?:id|name)\s*=\s*""[^""]*""\s*>\s*</a>\s*)+$")]
    private static partial Regex AnchorLinePattern();

    // An <a id="..."></a> anchor inside a line.
    [GeneratedRegex(@"<a\s+(?:id|name)\s*=\s*""(?<id>[^""]*)""\s*>\s*</a>")]
    private static partial Regex InlineAnchorPattern();

    // The space after a sentence's full stop, question mark or exclamation mark, before more text.
    [GeneratedRegex(@"(?<=[.!?])\s+(?=\S)")]
    private static partial Regex SentenceBreakPattern();

    // The start of a link's target, '](' when a target follows.
    [GeneratedRegex(@"\]\((?=[^)\s])")]
    private static partial Regex TargetStartPattern();

    // A code span: a run of backticks, then the same run again.
    [GeneratedRegex(@"(?<!`)(`+)(?!`).+?(?<!`)\1(?!`)")]
    private static partial Regex CodeSpanPattern();

    // An inline link or image with no bracket in its text, and its target, written between < and > or not, with an
    // optional title.
    [GeneratedRegex(@"!?\[([^\[\]]*)\]\(\s*(?:<[^>]*>|[^()\s<]*)(?:\s+(?:""[^""]*""|'[^']*'))?\s*\)")]
    private static partial Regex LinkPattern();

    // A heading that is a tab of docfx: its whole text links to #tab/<id>.
    [GeneratedRegex(@"^\[[^\]]*\]\(#tab/[^)\s]*\)$")]
    private static partial Regex TabPattern();

    // The id of a diagnostic.
    [GeneratedRegex(@"\bSQLSRC\d{3}(?!\d)")]
    private static partial Regex DiagnosticPattern();

    // A numeral: a run of digits, with an inner . or ,.
    [GeneratedRegex(@"\d+(?:[.,]\d+)*")]
    private static partial Regex NumeralPattern();

    // A word of a sentence: letters and digits, with an inner apostrophe.
    [GeneratedRegex(@"[\p{L}\p{N}]+(?:['’][\p{L}\p{N}]+)*")]
    private static partial Regex WordPattern();

    [GeneratedRegex(@"^# base ([0-9a-f]{40})$")]
    private static partial Regex BaseLinePattern();

    [GeneratedRegex(@"^phase-(\d{2})\.tsv$")]
    private static partial Regex LedgerNamePattern();

    // A row's unit: @ for a place outside the inventory, a .md file, a line or a range of lines, and a part.
    [GeneratedRegex(
        @"^(?<at>@)?(?<file>[^@:\s/][^:\s]*?\.md)"
            + @"(?::(?<first>[1-9]\d*)(?:-(?<last>[1-9]\d*))?)?(?:/(?<part>[1-9]\d*))?$"
    )]
    private static partial Regex PlacePattern();

    [GeneratedRegex(@"^MP-\d{2}$")]
    private static partial Regex MergePairPattern();

    [GeneratedRegex(@"^(?:C-\d{2}|-)$")]
    private static partial Regex CorrectionPattern();

    // The owner of a fact: <file>#<anchor>.
    [GeneratedRegex(@"^[^#\s]+\.md#[^#\s]+$")]
    private static partial Regex OwnerPattern();
}

// A unit of a user document: one line outside a fenced block, or one whole fenced block, from its first line to its
// last.  Text is its normalised text, which the hash and the start come from.
internal sealed record Unit(string File, int Line, int Last, string Kind, int Words, string Text)
{
    public string Id => $"{File}:{Line}";

    public string Hash => Check.HashOf(Text);

    public string Start => Check.StartOf(Text);
}

// A document and its units, and the lines that hold only an <a id> anchor.
internal sealed record Doc(
    string File,
    IReadOnlyList<string> Lines,
    IReadOnlyList<Unit> Units,
    IReadOnlyList<int> AnchorLines
);

// The user documents at a commit, or in the working tree when Commit is null.
internal sealed record Docs(string? Commit, IReadOnlyList<Doc> Files)
{
    public IEnumerable<Unit> Units => Files.SelectMany(file => file.Units);
}

// A row of check/phases.tsv.
internal sealed record PhaseRange(string File, int First, int Last, int Phase);

// A row of inventory.tsv.
internal sealed record Entry(string Id, int Last, string Kind, int Words, string Hash, int Phase, string Start)
{
    public string File => Id[..Id.LastIndexOf(':')];

    public int Line => int.Parse(Id[(Id.LastIndexOf(':') + 1)..], CultureInfo.InvariantCulture);
}

// A row of a ledger file, its six cells as written.
internal sealed record Row(string Unit, string Disposition, string To, string Arg, string Words, string Note);

// A ledger file: its phase, the sha of its # base line ("" when it has none), its rows with the line of each in the
// file, and what in its lines is not well formed.
internal sealed record Ledger(
    int Phase,
    string Base,
    IReadOnlyList<Row> Rows,
    IReadOnlyList<int> Lines,
    IReadOnlyList<string> Problems
)
{
    public string Name => Check.LedgerName(Phase);
}

// The place a row's unit names: its file, its lines, whether it is outside the inventory (@) and a whole file, the
// number of its part, and the unit without its part.
internal sealed record Place(string File, int First, int Last, bool Outside, bool Whole, int? Part, string Unit);

// A heading of a document: its unit, its level and its anchor.
internal sealed record Heading(Unit Unit, int Level, string Slug);

// A section of a page for rewrite: its name and its units.
internal sealed record Section(string Key, List<Unit> Units);

// What move and rewrite read for one phase: its ledger and rows, the user documents at its base (Old) and their units
// with their names, those of the working tree (New), the inventory by file, and what of the ledger is not well formed.
internal sealed record PhaseState(
    Ledger Ledger,
    IReadOnlyList<LedgerRow> Rows,
    Docs Old,
    IReadOnlyList<OldUnit> OldUnits,
    Docs New,
    ILookup<string, Entry> Inventory,
    IReadOnlyList<string> Problems
)
{
    public IReadOnlyDictionary<string, OldUnit> OldById { get; } =
        OldUnits.ToDictionary(old => old.Unit.Id, StringComparer.Ordinal);
}

// A unit of the base: Order is its place in reading order, and Ids the units of the inventory that have its text.  A
// row names it by one of those, or by its place at the base, @<file>:<line>, when it has none.
internal sealed record OldUnit(Unit Unit, int Order, IReadOnlyList<string> Ids)
{
    public string Names =>
        Ids.Count switch
        {
            0 => $"@{Unit.Id}",
            <= 3 => string.Join(", ", Ids),
            _ => $"{string.Join(", ", Ids.Take(3))} and {Ids.Count - 3} more",
        };
}

// A row of a phase's ledger: Ref is its file and line, Place what its unit names, and Ids the units of the inventory
// in that place.
internal sealed record LedgerRow(Row Row, string Ref, Place? Place, IReadOnlySet<string> Ids)
{
    public bool IsPart => Place?.Part is not null;

    public bool IsFact => Row.Arg == "fact";

    public bool IsPosition => Row.Note.StartsWith("position", StringComparison.Ordinal);

    public string ToFile => Row.To.Split('#')[0];

    // Whether the row names a unit of the base: by an id of the inventory that has its text, or by @ and its place
    // when it has none.
    public bool Names(OldUnit unit) =>
        Place switch
        {
            null => false,
            { Outside: false } => unit.Ids.Any(Ids.Contains),
            _ => unit.Ids.Count == 0
                && unit.Unit.File == Place.File
                && (Place.Whole || (Place.First <= unit.Unit.Line && unit.Unit.Line <= Place.Last)),
        };

    public string Describe() =>
        $"{Ref}: {string.Join("  ", Row.Unit, Row.Disposition, Row.To, Row.Arg, Row.Words, Row.Note)}";
}

// A command that cannot run: its message says why, and the checker exits with 2.  It derives from
// InvalidOperationException, and has the standard constructors, for S3871 and CA1032.
internal sealed class CheckException : InvalidOperationException
{
    public CheckException()
        : this("The command cannot run.") { }

    public CheckException(string message)
        : base(message) { }

    public CheckException(string message, Exception innerException)
        : base(message, innerException) { }
}
