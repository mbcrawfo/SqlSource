using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SqlSource.MutationReport;

// Builds each slice's evidence from what a run measured.
internal static class SliceEvidences
{
    public static IEnumerable<SliceEvidence> Of(Measurements measured, Func<string, bool> isBroad)
    {
        var sliceOf = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var slice = 0; slice < measured.Slices.Count; slice++)
        {
            foreach (var (path, _) in measured.Slices[slice].Files)
            {
                sliceOf[path] = slice;
            }
        }

        var kills = KillEvidence.Of(measured.Matrix, isBroad).ToLookup(method => method.File, StringComparer.Ordinal);
        var reach = Reach.Of(measured.Matrix, sliceOf, measured.Slices.Count);

        for (var slice = 0; slice < measured.Slices.Count; slice++)
        {
            var (name, files) = measured.Slices[slice];
            var paths = files.Select(file => file.Path).ToHashSet(StringComparer.Ordinal);
            var scopes = measured
                .Scopes.Where(scope => scope.Matrix.Tests.Any(test => paths.Contains(test.File)))
                .Select(ScopeOf)
                .ToList();
            // The times are those of the slice's own project: another project may compile the same files again, as
            // tests/SqlSource.Tests.RoslynFloor does those of tests/SqlSource.Tests/Generator/, and its results then
            // name them too.
            var project = ProjectOf(measured, files[0].Path);
            var times = (measured.Times.GetValueOrDefault(project) ?? []).ToLookup(
                test => test.File,
                StringComparer.Ordinal
            );
            var ran = files.SelectMany(file => times[file.Path]).ToList();
            yield return new SliceEvidence(
                name,
                [
                    .. files.Select(file => new EvidenceFile(
                        file.Path,
                        file.Lines,
                        MethodsOf(kills[file.Path], times[file.Path])
                    )),
                ],
                scopes,
                reach.Survivors[slice],
                reach.SourceOf(slice),
                project,
                measured.Times.TryGetValue(project, out var all) ? all.Sum(test => test.Seconds) : null,
                [
                    .. ran.GroupBy(test => test.Type[(test.Type.LastIndexOf('.') + 1)..], StringComparer.Ordinal)
                        .Select(type => new EvidenceTime(type.Key, type.Sum(test => test.Seconds)))
                        .OrderByDescending(type => type.Seconds)
                        .ThenBy(type => type.Class, StringComparer.Ordinal),
                ]
            );
        }
    }

    // The test methods of a file: those a scope ran and those its project's run timed, by name.
    private static List<EvidenceMethod> MethodsOf(IEnumerable<MethodEvidence> kills, IEnumerable<TestTime> times)
    {
        var killed = kills.ToDictionary(method => method.Name, StringComparer.Ordinal);
        var timed = times.ToLookup(test => $"{test.Type}.{test.Method}", StringComparer.Ordinal);
        return
        [
            .. killed
                .Keys.Union(timed.Select(method => method.Key), StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .Select(name =>
                {
                    var rows = timed[name].ToList();
                    var evidence = killed.GetValueOrDefault(name);
                    return new EvidenceMethod(
                        EvidenceWriter.Short(name),
                        evidence?.Rows ?? rows.Count,
                        rows.Count == 0 ? null : rows.Sum(row => row.Seconds),
                        rows.Count > 0 && rows.All(row => row.Result == "Skip"),
                        evidence
                    );
                }),
        ];
    }

    private static EvidenceScope ScopeOf(MeasuredScope scope)
    {
        var tested = scope.Matrix.Mutants.Where(MutantKinds.IsTested).ToList();
        return new EvidenceScope(
            scope.Name,
            scope.Matrix.Tests.Count,
            Outermost(scope.Matrix.Tests.Select(test => FolderOf(test.File))),
            tested.Count,
            Outermost(tested.Select(mutant => FolderOf(Reach.FileOf(mutant))))
        );
    }

    private static string FolderOf(string file) => file[..Math.Max(file.LastIndexOf('/'), 0)];

    // The folders that are in no other of them, in order.
    private static List<string> Outermost(IEnumerable<string> folders)
    {
        var all = folders.Distinct(StringComparer.Ordinal).ToList();
        return
        [
            .. all.Where(folder => !all.Exists(other => folder.StartsWith(other + "/", StringComparison.Ordinal)))
                .Order(StringComparer.Ordinal),
        ];
    }

    // The test project a file is in: the project of the run's results that holds it, or else the nearest folder
    // above it with a project file, or else its first two folders.
    private static string ProjectOf(Measurements measured, string file)
    {
        var known = measured
            .Times.Keys.Where(project => file.StartsWith(project + "/", StringComparison.Ordinal))
            .MaxBy(project => project.Length);
        if (known is not null)
        {
            return known;
        }

        var folder = FolderOf(file);
        while (folder.Contains('/', StringComparison.Ordinal))
        {
            var path = Path.Combine(measured.RepositoryRoot, folder);
            if (Directory.Exists(path) && Directory.EnumerateFiles(path, "*.csproj").Any())
            {
                return folder;
            }

            folder = FolderOf(folder);
        }

        return string.Join('/', file.Split('/').Take(2));
    }
}

// What the tests of each slice reach of the source: the survivors and the timeouts under them, and the files they
// reach, with what of each file no test reaches and no run can test.
internal sealed class Reach
{
    private readonly HashSet<string>[] _files;
    private readonly Dictionary<string, SortedSet<int>>[] _timeouts;
    private readonly Dictionary<
        string,
        (SortedSet<int> Uncovered, SortedSet<int> Uncompiled, SortedSet<int> Static)
    > _gaps = [];

    private Reach(int slices)
    {
        _files = [.. Enumerable.Range(0, slices).Select(_ => new HashSet<string>(StringComparer.Ordinal))];
        _timeouts =
        [
            .. Enumerable.Range(0, slices).Select(_ => new Dictionary<string, SortedSet<int>>(StringComparer.Ordinal)),
        ];
        Survivors = [.. Enumerable.Range(0, slices).Select(_ => new List<EvidenceSurvivor>())];
    }

    public List<EvidenceSurvivor>[] Survivors { get; }

    // A mutant's file from the repository root.
    public static string FileOf(MatrixMutant mutant) => $"{mutant.Project}/{mutant.Key.File}";

    public static Reach Of(KillMatrix matrix, Dictionary<string, int> sliceOf, int slices)
    {
        var reach = new Reach(slices);
        var sliceOfTest = matrix.Tests.Select(test => sliceOf.GetValueOrDefault(test.File, -1)).ToArray();
        foreach (var mutant in matrix.Mutants.Order(KillEvidence.ByPlace))
        {
            var file = FileOf(mutant);
            if (!MutantKinds.IsTested(mutant) || mutant.Status == MutantStatus.NoCoverage)
            {
                reach.AddGap(file, mutant);
                continue;
            }

            foreach (
                var tests in mutant.CoveredBy.Concat(mutant.KilledBy).Distinct().GroupBy(test => sliceOfTest[test])
            )
            {
                if (tests.Key < 0)
                {
                    continue;
                }

                _ = reach._files[tests.Key].Add(file);
                if (mutant.Status == MutantStatus.Survived)
                {
                    reach
                        .Survivors[tests.Key]
                        .Add(
                            new EvidenceSurvivor(
                                mutant,
                                [
                                    .. tests
                                        .Select(test => matrix.Tests[test].Name)
                                        .Distinct(StringComparer.Ordinal)
                                        .Order(StringComparer.Ordinal),
                                ]
                            )
                        );
                }
                else if (mutant.Status == MutantStatus.Timeout)
                {
                    if (!reach._timeouts[tests.Key].TryGetValue(file, out var lines))
                    {
                        lines = [];
                        reach._timeouts[tests.Key].Add(file, lines);
                    }

                    _ = lines.Add(mutant.Key.StartLine);
                }
            }
        }

        return reach;
    }

    public List<EvidenceSource> SourceOf(int slice) =>
        [
            .. _files[slice]
                .Order(StringComparer.Ordinal)
                .Select(file =>
                {
                    var (uncovered, uncompiled, statics) = _gaps.GetValueOrDefault(file);
                    return new EvidenceSource(
                        file,
                        LineRanges.Of(uncovered ?? []),
                        LineRanges.Of(uncompiled ?? []),
                        LineRanges.Of(statics ?? []),
                        [.. _timeouts[slice].GetValueOrDefault(file) ?? []]
                    );
                }),
        ];

    // The lines of a mutant that no test reaches, that does not compile or that is static and so never tested.
    private void AddGap(string file, MatrixMutant mutant)
    {
        if (mutant.Status != MutantStatus.NoCoverage && !MutantKinds.IsBlind(mutant))
        {
            return;
        }

        if (!_gaps.TryGetValue(file, out var gaps))
        {
            gaps = ([], [], []);
            _gaps.Add(file, gaps);
        }

        var lines = gaps.Static;
        if (mutant.Status == MutantStatus.NoCoverage)
        {
            lines = gaps.Uncovered;
        }
        else if (mutant.Status == MutantStatus.CompileError)
        {
            lines = gaps.Uncompiled;
        }

        lines.UnionWith(
            Enumerable.Range(mutant.Key.StartLine, Math.Max(mutant.Key.EndLine - mutant.Key.StartLine, 0) + 1)
        );
    }
}
