using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using SqlSource.Diagnostics;
using SqlSource.Settings;

namespace SqlSource.Generation.Placement;

/// <summary>
/// Where the members of one type's queries go: which of its files it can use, the nested classes those files' queries
/// fill and the accessibility of each, and each query's settings, resolved once.  <c>TypeEmitter</c> and
/// <c>MethodPlanner</c> each run it on the same <see cref="TypeQueries" />, so the type's file and its methods' file
/// agree on every nested class.  It is made inside a step and never cached between steps.
/// </summary>
internal sealed class TypePlacement
{
    private const string AsyncSuffix = "Async";

    // What has the name of a member on the type, as SQLSRC125 says it.
    private const string DeclaredReason = "the type declares a member, a nested type or a type parameter of that name";

    private const string NestedClassReason = "a nested class of the type's queries has that name";

    private readonly TargetType _type;

    // The settings of each query, by its file's index and then its own.
    private readonly QuerySettings[][] _settings;

    private TypePlacement(
        TargetType type,
        QuerySettings[][] settings,
        bool[] usable,
        IReadOnlyList<NestedClassPlacement> nestedClasses,
        string? emptyNestedClass
    )
    {
        _type = type;
        _settings = settings;
        Usable = usable;
        NestedClasses = nestedClasses;
        EmptyNestedClass = emptyNestedClass;
    }

    /// <summary>One element for each of the type's files: whether the type can use it.</summary>
    public bool[] Usable { get; }

    /// <summary>
    /// The nested classes that the queries of the usable files put members in, each once, in the order of their first
    /// query.
    /// </summary>
    public IReadOnlyList<NestedClassPlacement> NestedClasses { get; }

    /// <summary>
    /// The name of the empty nested class that a type with no usable query writes, so that a reference to it reports
    /// the missing member and not a missing type: the first file's level's <c>nestedClass</c>, else <c>Sql</c>.  Null
    /// when a query is usable, or when the type has a member of that name.
    /// </summary>
    public string? EmptyNestedClass { get; }

    /// <summary>The settings in effect for a query, resolved once from its markers and its file's level.</summary>
    /// <param name="fileIndex">The index of the query's file among the type's files.</param>
    /// <param name="queryIndex">The query's index among the queries of its file.</param>
    public QuerySettings SettingsOf(int fileIndex, int queryIndex) => _settings[fileIndex][queryIndex];

    /// <summary>
    /// Where the members of a query of a usable file go: its location, and its visibility, written or the default of
    /// its location; under <see cref="MemberLocation.NestedClass" />, its class with the class's visibility.
    /// </summary>
    /// <param name="settings">The query's settings, as <see cref="SettingsOf" /> gives them.</param>
    public QueryPlacement PlacementOf(QuerySettings settings)
    {
        var location = settings.MemberLocation;
        if (location == MemberLocation.NestedClass)
        {
            return new QueryPlacement(location, VisibilityOfClass(settings.NestedClass), settings.NestedClass);
        }

        return new QueryPlacement(
            location,
            settings.WrittenVisibility?.Value
                ?? (
                    settings.Output == OutputKind.Repository
                        ? Visibility.Public
                        : PlacementChecks.DefaultVisibility(location, _type.IsPublic)
                ),
            null
        );
    }

    /// <summary>
    /// The placement of the type's queries.  A file contributes all of its queries or none.  It is left out when one
    /// of its queries cannot be used here, with the diagnostics when <paramref name="diagnostics" /> is given, in this
    /// order: for each file and each query, <c>SQLSRC009</c> for a member named like the class that holds it, else
    /// <c>SQLSRC125</c> for a constant or token method on the type named like a member, a nested type or a type
    /// parameter that the type declares, else <c>SQLSRC008</c>, then the errors of its kept form
    /// (<see cref="SqlQuery.KeptErrors" />), then <c>SQLSRC122</c>, else <c>SQLSRC123</c> for its own visibility; then,
    /// over the files left, <c>SQLSRC124</c> for a nested class given two visibilities, <c>SQLSRC007</c> for a nested
    /// class named like a member of the type, and <c>SQLSRC125</c> for a member on the type named like a nested class.
    /// Each of the last three leaves out every file it names.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="files">The type's files, in member order.</param>
    /// <param name="fileSettings">
    /// Each file's level, the chain of its folder, one for each of <paramref name="files" />.
    /// </param>
    /// <param name="diagnostics">Where the diagnostics go, or null when the caller does not report them.</param>
    public static TypePlacement Of(
        TargetType type,
        EquatableArray<ParsedSqlFile> files,
        EquatableArray<SettingsLevel> fileSettings,
        ImmutableArray<DiagnosticInfo>.Builder? diagnostics
    )
    {
        var analysis = new Analysis(type, files, fileSettings, diagnostics);
        analysis.CheckQueries();
        analysis.CheckClassVisibilities();
        analysis.CheckTakenClassNames();
        analysis.CheckMembersNamedLikeClasses();
        return new TypePlacement(
            type,
            analysis.Settings,
            analysis.Usable,
            analysis.FindNestedClasses(),
            analysis.FindEmptyNestedClass()
        );
    }

    /// <summary>The index of the class of the name among the classes, or -1.</summary>
    public static int IndexOf(IReadOnlyList<NestedClassPlacement> classes, string name)
    {
        for (var index = 0; index < classes.Count; index++)
        {
            if (string.Equals(classes[index].Name, name, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    // The visibility of the class of a usable query.  A query of a file that is not usable has none, and gets private.
    private Visibility VisibilityOfClass(string name) =>
        IndexOf(NestedClasses, name) is >= 0 and var index ? NestedClasses[index].Visibility : Visibility.Private;

    // The checks, in their order, over the state they share.
    private sealed class Analysis(
        TargetType type,
        EquatableArray<ParsedSqlFile> files,
        EquatableArray<SettingsLevel> fileSettings,
        ImmutableArray<DiagnosticInfo>.Builder? diagnostics
    )
    {
        public QuerySettings[][] Settings { get; } = new QuerySettings[files.Count][];

        public bool[] Usable { get; } = new bool[files.Count];

        // The type in full, as the diagnostics that name it give it, made only when one does.
        private string TypeName => field ??= PlacementChecks.NameOf(type);

        // Each query's own checks, file by file: a query named like what holds its member, like what the type declares
        // when its member goes on the type, or like a query of an earlier usable file; the errors of its kept form;
        // and a location or a visibility that does not fit it.
        public void CheckQueries()
        {
            var fileOfName = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var index = 0; index < files.Count; index++)
            {
                var file = files[index];
                var settings = file.Queries.Count == 0 ? [] : new QuerySettings[file.Queries.Count];
                Settings[index] = settings;
                var isUsable = true;
                for (var queryIndex = 0; queryIndex < file.Queries.Count; queryIndex++)
                {
                    var query = file.Queries[queryIndex];
                    var resolved = QuerySettings.Resolve(query.Markers, fileSettings[index]);
                    settings[queryIndex] = resolved;
                    isUsable &= CheckQuery(query, resolved, fileOfName);
                }

                if (!isUsable)
                {
                    continue;
                }

                Usable[index] = true;
                foreach (var query in file.Queries)
                {
                    fileOfName.Add(query.Name, file.FileName);
                }
            }
        }

        // SQLSRC124 at each query whose class another query of the type gave another visibility first, and every file
        // with a query in such a class left out.  A visibility that is only the default takes the written one.
        public void CheckClassVisibilities()
        {
            var firstWritten = new Dictionary<string, (Placed<Visibility> Visibility, string Query)>(
                StringComparer.Ordinal
            );
            var conflicting = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (_, query, settings) in UsableQueries(MemberLocation.NestedClass))
            {
                if (settings.WrittenVisibility is not { } written)
                {
                    continue;
                }

                if (!firstWritten.TryGetValue(settings.NestedClass, out var first))
                {
                    firstWritten.Add(settings.NestedClass, (written, query.Name));
                    continue;
                }

                if (first.Visibility.Value == written.Value)
                {
                    continue;
                }

                _ = conflicting.Add(settings.NestedClass);
                diagnostics?.Add(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.NestedClassVisibilities,
                        query.VisibilityMarker ?? query.NameLocation,
                        settings.NestedClass,
                        TypeName,
                        PlacementChecks.KeywordOf(written.Value),
                        PlaceText.Of(written.Place),
                        query.Name,
                        PlaceText.Of(first.Visibility.Place),
                        PlacementChecks.KeywordOf(first.Visibility.Value),
                        first.Query
                    )
                );
            }

            if (conflicting.Count > 0)
            {
                LeaveOutFilesOf(conflicting);
            }
        }

        // SQLSRC007 at the attribute once for each nested class that is named like a member of the type, and every
        // file with a query in such a class left out.
        public void CheckTakenClassNames()
        {
            var taken = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (_, _, settings) in UsableQueries(MemberLocation.NestedClass))
            {
                var name = settings.NestedClass;
                if (
                    SortedSearch.IndexOf(type.TakenNames, name, static taken => taken, StringComparer.Ordinal) < 0
                    || !taken.Add(name)
                )
                {
                    continue;
                }

                diagnostics?.Add(
                    DiagnosticInfo.Create(SqlDiagnostics.NestedClassNameTaken, type.AttributeLocation, type.Name, name)
                );
            }

            if (taken.Count > 0)
            {
                LeaveOutFilesOf(taken);
            }
        }

        // SQLSRC125 at each query on the type, other than one with a method, named like a nested class that a usable
        // query fills, and its file left out.  The method planner judges a method's name.
        public void CheckMembersNamedLikeClasses()
        {
            var classes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (_, _, settings) in UsableQueries(MemberLocation.NestedClass))
            {
                _ = classes.Add(settings.NestedClass);
            }

            if (classes.Count == 0)
            {
                return;
            }

            var leftOut = new List<int>();
            foreach (var (fileIndex, query, settings) in UsableQueries(MemberLocation.Class))
            {
                if (settings.Output.HasMethod() || !classes.Contains(query.Name))
                {
                    continue;
                }

                diagnostics?.Add(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.MemberNameTaken,
                        query.NameLocation,
                        query.Name,
                        TypeName,
                        NestedClassReason
                    )
                );
                leftOut.Add(fileIndex);
            }

            foreach (var fileIndex in leftOut)
            {
                Usable[fileIndex] = false;
            }
        }

        // The nested classes of the usable files' queries in the order of their first, each with its first written
        // visibility, else private.  After the checks, two written visibilities of one class never remain.
        public List<NestedClassPlacement> FindNestedClasses()
        {
            List<NestedClassPlacement> classes = [];
            List<bool> isWritten = [];
            foreach (var (_, _, settings) in UsableQueries(MemberLocation.NestedClass))
            {
                var at = IndexOf(classes, settings.NestedClass);
                if (at < 0)
                {
                    classes.Add(
                        new NestedClassPlacement(
                            settings.NestedClass,
                            settings.WrittenVisibility?.Value ?? Visibility.Private
                        )
                    );
                    isWritten.Add(settings.WrittenVisibility is not null);
                }
                else if (!isWritten[at] && settings.WrittenVisibility is { } written)
                {
                    classes[at] = classes[at] with { Visibility = written.Value };
                    isWritten[at] = true;
                }
            }

            return classes;
        }

        // The empty class of a type with no usable query, unless the type has a member of its name.
        public string? FindEmptyNestedClass()
        {
            for (var index = 0; index < files.Count; index++)
            {
                if (Usable[index] && files[index].Queries.Count > 0)
                {
                    return null;
                }
            }

            var name = (files.Count > 0 ? fileSettings[0].NestedClass : null) ?? QuerySettings.DefaultNestedClass;
            return SortedSearch.IndexOf(type.TakenNames, name, static taken => taken, StringComparer.Ordinal) < 0
                ? name
                : null;
        }

        // Every check of one query, each reported; false when any fails.
        private bool CheckQuery(SqlQuery query, QuerySettings settings, Dictionary<string, string> fileOfName)
        {
            var isUsable = true;
            var location = settings.MemberLocation;
            if (
                ContainerNameOf(settings, location) is { } container
                && IsNamed(container, query.Name, Suffix(settings))
            )
            {
                diagnostics?.Add(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.QueryNamedLikeContainingType,
                        query.NameLocation,
                        type.Name,
                        query.Name
                    )
                );
                isUsable = false;
            }
            else if (IsDeclaredOnType(settings, location, query.Name))
            {
                diagnostics?.Add(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.MemberNameTaken,
                        query.NameLocation,
                        query.Name,
                        TypeName,
                        DeclaredReason
                    )
                );
                isUsable = false;
            }
            else if (fileOfName.TryGetValue(query.Name, out var firstFile))
            {
                diagnostics?.Add(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.DuplicateQueryName,
                        query.NameLocation,
                        query.Name,
                        firstFile,
                        type.Name
                    )
                );
                isUsable = false;
            }

            if (query.KeptErrors.Count > 0 && settings.KeepComments)
            {
                diagnostics?.AddRange(query.KeptErrors);
                isUsable = false;
            }

            // A constant or a token method extends nothing, and a repository method is a member of the type.  The
            // location was written, since the default of neither is the one refused.
            if (
                settings.WrittenMemberLocation is { } writtenLocation
                && (
                    (location == MemberLocation.ExtensionClass && settings.Output != OutputKind.Methods)
                    || (location == MemberLocation.NestedClass && settings.Output == OutputKind.Repository)
                )
            )
            {
                diagnostics?.Add(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.MemberLocationNotFit,
                        query.MemberLocationMarker ?? query.NameLocation,
                        query.Name,
                        TypeName,
                        location == MemberLocation.NestedClass ? "nested class" : "extension class",
                        PlaceText.Of(writtenLocation.Place),
                        SettingValue.NameOf(settings.Output),
                        settings.Output == OutputKind.Repository
                            ? "A repository method is an instance method of the type.  Use class."
                            : "Only a query method extends the connection.  Use class or nested-class."
                    )
                );
                return false;
            }

            // The default of a location always fits it, so only a written visibility is checked.
            if (
                settings.WrittenVisibility is { } written
                && PlacementChecks.VisibilityReason(type, location, written.Value) is { } reason
            )
            {
                diagnostics?.Add(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.VisibilityNotFit,
                        query.VisibilityMarker ?? query.MemberLocationMarker ?? query.NameLocation,
                        query.Name,
                        TypeName,
                        PlacementChecks.KeywordOf(written.Value),
                        PlaceText.Of(written.Place),
                        PlacementChecks.TextOf(location),
                        reason
                    )
                );
                isUsable = false;
            }

            return isUsable;
        }

        // The name of the class that would hold the query's member, which the member cannot have (CS0542): the nested
        // class's, for a query of any output there; the type's, for a constant or a token method on it, as the
        // method planner judges a method's name there.  Null in the extension class.
        private string? ContainerNameOf(QuerySettings settings, MemberLocation location) =>
            location switch
            {
                MemberLocation.NestedClass => settings.NestedClass,
                MemberLocation.Class => settings.Output.HasMethod() ? null : type.Name,
                MemberLocation.ExtensionClass => null,
                _ => throw UnexpectedEnumValueException.Of(location),
            };

        // Whether the constant or token method of the query goes on the type and takes a name the type declares, which
        // it cannot have (CS0102), nor overload a method of the type with.  The type's own name is SQLSRC009, found
        // first, and the method planner judges a method's name.
        private bool IsDeclaredOnType(QuerySettings settings, MemberLocation location, string name) =>
            location == MemberLocation.Class
            && !settings.Output.HasMethod()
            && SortedSearch.IndexOf(type.TakenNames, name, static taken => taken, StringComparer.Ordinal) >= 0;

        // What a query's method adds to its name: Async under async-method-suffix.  A constant adds nothing.
        private static string Suffix(QuerySettings settings) =>
            settings.Output.HasMethod() && (settings.Parameters & GeneratorParameters.AsyncMethodSuffix) != 0
                ? AsyncSuffix
                : string.Empty;

        // Whether the name is the member's with the suffix, with no string made.
        private static bool IsNamed(string name, string member, string suffix) =>
            name.Length == member.Length + suffix.Length
            && name.StartsWith(member, StringComparison.Ordinal)
            && name.EndsWith(suffix, StringComparison.Ordinal);

        // Every file that has a query in one of the classes, left out.
        private void LeaveOutFilesOf(HashSet<string> classes)
        {
            var leftOut = new List<int>();
            foreach (var (fileIndex, _, settings) in UsableQueries(MemberLocation.NestedClass))
            {
                if (classes.Contains(settings.NestedClass))
                {
                    leftOut.Add(fileIndex);
                }
            }

            foreach (var fileIndex in leftOut)
            {
                Usable[fileIndex] = false;
            }
        }

        // The queries of the usable files whose members go to the location, with their files' indices and settings.
        private IEnumerable<(int FileIndex, SqlQuery Query, QuerySettings Settings)> UsableQueries(
            MemberLocation location
        )
        {
            for (var index = 0; index < files.Count; index++)
            {
                if (!Usable[index])
                {
                    continue;
                }

                var queries = files[index].Queries;
                for (var queryIndex = 0; queryIndex < queries.Count; queryIndex++)
                {
                    var settings = Settings[index][queryIndex];
                    if (settings.MemberLocation == location)
                    {
                        yield return (index, queries[queryIndex], settings);
                    }
                }
            }
        }
    }
}
