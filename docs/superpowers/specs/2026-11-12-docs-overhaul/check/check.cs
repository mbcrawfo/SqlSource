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
        output.WriteLine("commands: inventory, coverage, origin <page>, selftest");
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
        var masked = text.ToCharArray();
        foreach (Match span in CodeSpanPattern().Matches(text))
        {
            Array.Fill(masked, Masked, span.Index, span.Length);
        }

        var result = text;
        var mask = new string(masked);
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
        ("6: coverage names a due unit with no row, and a row of no unit", CoverageCase),
        ("8: inventory, run a second time, changes no byte", InventoryCase),
        ("10: a move-split with no part may name a heading, not a paragraph", MoveSplitCase),
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

    // Each case starts from the tree under test.
    private static void Reset(string scratch)
    {
        _ = Git(scratch, "reset", "--quiet", "--hard");
        _ = Git(scratch, "clean", "--quiet", "--force", "-d");
    }

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
            "The tree under test"
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
    [GeneratedRegex(@"<a\s+(?:id|name)\s*=\s*""[^""]*""\s*>\s*</a>")]
    private static partial Regex InlineAnchorPattern();

    // A code span: a run of backticks, then the same run again.
    [GeneratedRegex(@"(?<!`)(`+)(?!`).+?(?<!`)\1(?!`)")]
    private static partial Regex CodeSpanPattern();

    // An inline link or image with no bracket in its text, and its target, written between < and > or not, with an
    // optional title.
    [GeneratedRegex(@"!?\[([^\[\]]*)\]\(\s*(?:<[^>]*>|[^()\s<]*)(?:\s+(?:""[^""]*""|'[^']*'))?\s*\)")]
    private static partial Regex LinkPattern();

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
