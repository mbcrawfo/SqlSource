using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using SqlSource.Diagnostics;
using SqlSource.Generation.Converters;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Models;
using SqlSource.Generation.Types.Drivers;
using SqlSource.Parsing;
using SqlSource.Settings;
using SqlSource.Settings.Libraries;

namespace SqlSource.Generation.Methods;

/// <summary>
/// Decides which queries of one type get a method, and what each method is written from, with the problems of those
/// that cannot have one: <c>SQLSRC272</c>, <c>SQLSRC274</c> and <c>SQLSRC275</c>, and, from
/// <see cref="ConversionPlanner" />, <c>SQLSRC279</c>, <c>SQLSRC280</c>, and <c>SQLSRC277</c> and <c>SQLSRC278</c> for
/// a converter of a referenced assembly.  It also reports <c>SQLSRC281</c> for a library that is on for a query and
/// that the project cannot use, which leaves the query its method, with the base map's types: at a marker as a
/// diagnostic, and for a settings file as a <see cref="SettingsFileLibraryUse" />, which is located at the end.  A query
/// that uses a library older than its floor gets no method and reports nothing here: the project gets
/// <c>SQLSRC282</c> from the libraries that the type's queries use.
/// </summary>
internal static class MethodPlanner
{
    private const string AsyncSuffix = "Async";

    private const string ExtensionsSuffix = "Extensions";

    // The names of the arguments that every method has, which no token may take.
    private static readonly string[] FixedArguments =
    [
        "connection",
        "parameters",
        "transaction",
        "commandTimeout",
        "cancellationToken",
    ];

    /// <summary>
    /// The methods of the type: one for each query of a file it can use whose output is
    /// <see cref="OutputKind.CodeGen" />, whose entry has the models it needs, whose shape, name, tokens and
    /// converters a method can have, whose engine's driver the project references at or above the floor, and which
    /// uses no library older than its floor.  A type with a diagnostic of its own, or with no file, has none.
    /// </summary>
    public static TypeMethods Plan(MethodsInput input)
    {
        var typeQueries = input.Type;
        var type = typeQueries.Type.Type;
        var key = HintName.NameOf(type);
        var methods = ImmutableArray.CreateBuilder<QueryMethod>();
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var libraryUses = ImmutableArray.CreateBuilder<SettingsFileLibraryUse>();
        var engines = CodeGenEngines.None;
        var warningIds = new SortedSet<string>(StringComparer.Ordinal);
        // Made only when a first library is added, so that a type that uses none allocates nothing for them.
        SortedSet<SqlLibrary>? conversionLibraries = null;
        SortedSet<SqlLibrary>? usedLibraries = null;
        if (typeQueries.Type.Diagnostics.Count == 0 && typeQueries.Files.Count > 0)
        {
            engines = Collect(
                input,
                key,
                methods,
                diagnostics,
                libraryUses,
                warningIds,
                ref conversionLibraries,
                ref usedLibraries
            );
        }

        return new TypeMethods(
            key,
            HintName.ForMethods(typeQueries.HintName),
            type.MethodPlacement,
            type.NamespaceValue,
            type.Namespace,
            type.Types,
            type.Name + ExtensionsSuffix,
            type.IsPublic,
            type.AttributeLocation,
            new EquatableArray<QueryMethod>(methods.ToImmutable()),
            new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()),
            new EquatableArray<SettingsFileLibraryUse>(libraryUses.ToImmutable()),
            engines,
            new EquatableArray<string>(warningIds.ToImmutableArray()),
            ToValues(conversionLibraries),
            ToValues(usedLibraries)
        );
    }

    /// <summary>
    /// The problems of every type's methods, each once, by the place, the id and the text: two types that claim one
    /// file plan its queries alike.  A problem that names the type stays one for each type.
    /// </summary>
    public static EquatableArray<DiagnosticInfo> Distinct(ImmutableArray<EquatableArray<DiagnosticInfo>> diagnostics) =>
        new(
            diagnostics
                .SelectMany(static each => each)
                .OrderBy(static diagnostic => diagnostic.Location.Path, StringComparer.Ordinal)
                .ThenBy(static diagnostic => diagnostic.Location.Span.Start)
                .ThenBy(static diagnostic => diagnostic.Descriptor.Id, StringComparer.Ordinal)
                .ThenBy(static diagnostic => string.Join("\n", diagnostic.Arguments), StringComparer.Ordinal)
                .Distinct()
                .ToImmutableArray()
        );

    private static CodeGenEngines Collect(
        MethodsInput input,
        string key,
        ImmutableArray<QueryMethod>.Builder methods,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        ImmutableArray<SettingsFileLibraryUse>.Builder libraryUses,
        SortedSet<string> warningIds,
        ref SortedSet<SqlLibrary>? conversionLibraries,
        ref SortedSet<SqlLibrary>? usedLibraries
    )
    {
        var typeQueries = input.Type;
        var queries = UsableQueries.Of(typeQueries, null);
        var written = WrittenNames(queries);
        var planned = new List<Planned>();
        var engines = CodeGenEngines.None;
        var served = new ServedConverters(input.Converters);
        foreach (var usable in queries)
        {
            if (usable.Settings.Output != OutputKind.CodeGen)
            {
                continue;
            }

            // Only a describable dialect's query has an entry, so only it can have a method or need a driver.
            var file = typeQueries.Files[usable.FileIndex];
            if (!SqlDescribableDialects.Contains(file.Dialect))
            {
                continue;
            }

            var engine = SqlDescribableDialects.EngineOf(file.Dialect);
            engines |= engine.ToCodeGenEngines();
            var context = new QueryContext(input, key, file, engine, usable);
            AddUnavailableLibraries(context, diagnostics, libraryUses);
            var target = ConverterTargetsExtensions.Of(engine, input.Drivers.PostgresAtLeast10);
            if (Plan(context, written, target, served.Of(target), ref usedLibraries) is { } plan)
            {
                planned.Add(plan);
            }
        }

        AddPlanned(planned, methods, diagnostics, warningIds, ref conversionLibraries);
        return engines;
    }

    // The names of the members the type's file writes for the queries whose output is not CodeGen: each one's constant
    // or token method, in the type itself or in its nested class.
    private static HashSet<string> WrittenNames(UsableQuerySequence queries)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var usable in queries)
        {
            if (usable.Settings.Output != OutputKind.CodeGen)
            {
                _ = names.Add(usable.Query.Name);
            }
        }

        return names;
    }

    // The plan of one query whose output is CodeGen, or null when it gets nothing and reports nothing here.  Every
    // problem of SQLSRC272, SQLSRC275, SQLSRC274 and its converters that the query has is reported, and any one leaves
    // it no method.  Its conversions see the converters of the type's slice that serve its target.  The libraries it
    // uses are added to the type's, and one older than its floor leaves it no method and no problem.
    private static Planned? Plan(
        QueryContext context,
        HashSet<string> written,
        ConverterTargets target,
        EquatableArray<Converter> converters,
        ref SortedSet<SqlLibrary>? usedLibraries
    )
    {
        var (input, _, file, engine, usable) = context;
        var (_, queryIndex, query, settings) = usable;
        // A query without models has its own error already, reported by its file.
        if (input.Files[usable.FileIndex].Find(queryIndex, query.Name) is not { } models)
        {
            return null;
        }

        var problems = new List<DiagnosticInfo>();
        var shape = query.Shape ?? (models.Output is null ? ResultShape.RowCount : ResultShape.Many);
        if ((shape is ResultShape.One or ResultShape.OneOptional or ResultShape.Many) && models.Output is null)
        {
            problems.Add(
                DiagnosticInfo.Create(
                    SqlDiagnostics.QueryShapeNeedsRows,
                    query.NameLocation,
                    query.Name,
                    ShapeName(shape)
                )
            );
        }

        problems.AddRange(FindTokenProblems(query));
        var name = query.Name + ((settings.Parameters & GeneratorParameters.AsyncMethodSuffix) != 0 ? AsyncSuffix : "");
        var reason = FindTakenReason(input, name, written);
        if (reason is not null)
        {
            problems.Add(
                DiagnosticInfo.Create(SqlDiagnostics.MethodNameTaken, query.NameLocation, query.Name, name, reason)
            );
        }

        // A query that gets no method takes no name, so it is compared with the other methods' only once it has one.
        // Its models are read whatever its other problems, so that it gets every problem of its converters too.
        var planned = new Planned(problems, name, query.Name, query.NameLocation, null, false, []);
        // A query whose driver is missing gets no method, which SQLSRC273 says, so its converters are not decided.
        var isUsable = input.Drivers.Allows(engine);
        var reading = new Reading(
            isUsable,
            new ConversionContext(engine, target, converters, query.NameLocation, problems)
        );
        var hasInput = TryReadInput(context, models, reading, out var methodInput);
        var hasOutput = TryReadOutput(context, models, reading, out var methodOutput);
        if (AddUsedLibraries(input, models, methodInput, methodOutput, ref usedLibraries))
        {
            // SQLSRC282, for the project, says why the query has no method, so its own problems are left unsaid.
            return planned with { Problems = [] };
        }

        if (problems.Count > 0 || !hasInput || !hasOutput)
        {
            return planned;
        }

        var method = new QueryMethod(
            name,
            query.Name,
            TypeEmitter.GetSummaryXml(query, file.FileName),
            shape,
            settings.CollectionType,
            engine,
            settings.KeepComments ? query.KeptSegments ?? query.Segments : query.Segments,
            settings.ValidateTokens,
            settings.UseArrayPool,
            methodInput,
            methodOutput,
            query.NameLocation
        );
        return planned with
        {
            Method = method,
            IsDriverUsable = isUsable,
            WarningIds = WarningIdsOf(models, methodInput, methodOutput),
        };
    }

    // SQLSRC281 for each entry that turns a library on for the query whose assemblies the project does not reference
    // for its engine: at a marker of the query's file, whose text any edit of the marker parses again, or, for a
    // settings file's member, as a use without its position, which SettingsFileLibraryUse.Locate finds in the file as
    // it is now.  Distinct makes each one for each place and engine, whatever the number of queries and types it
    // reaches.  A query without models has its own error.
    private static void AddUnavailableLibraries(
        QueryContext context,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        ImmutableArray<SettingsFileLibraryUse>.Builder libraryUses
    )
    {
        var (input, _, _, engine, usable) = context;
        if (input.Files[usable.FileIndex].Find(usable.QueryIndex, usable.Query.Name) is not { } models)
        {
            return;
        }

        foreach (var entry in models.UnavailableLibraries)
        {
            var missing = LibraryProbe.MissingAssembliesOf(entry.Library, engine);
            if (entry.Marker is not { } marker)
            {
                // An entry without a position is a settings file's, whose place has its path.
                libraryUses.Add(new SettingsFileLibraryUse(entry.Place.Path!, entry.Library, engine, missing));
                continue;
            }

            diagnostics.Add(
                DiagnosticInfo.Create(
                    SqlDiagnostics.LibraryNotReferenced,
                    new LocationInfo(models.NameLocation.Path, marker.Span, marker.LineSpan),
                    LibraryName(entry.Library),
                    engine.DatabaseName(),
                    missing
                )
            );
        }
    }

    /// <summary>The library's name as a diagnostic gives it.</summary>
    public static string LibraryName(SqlLibrary library) =>
        library switch
        {
            SqlLibrary.NodaTime or _ => "NodaTime",
        };

    // The libraries that the query uses, added to the type's, which are made only when the first is added: of each
    // property that a library's row typed, and of each conversion of its parameters and columns that is a library's
    // generated one.  True when one of them is older than its floor.  By index, so that the passes box no enumerator.
    private static bool AddUsedLibraries(
        MethodsInput input,
        QueryModels models,
        MethodInput? methodInput,
        MethodOutput? methodOutput,
        ref SortedSet<SqlLibrary>? used
    )
    {
        var isBelowTheFloor = false;
        AddProperties(input, models.Input, ref used, ref isBelowTheFloor);
        AddProperties(input, models.Output, ref used, ref isBelowTheFloor);
        if (methodInput is { Parameters: var parameters })
        {
            for (var index = 0; index < parameters.Count; index++)
            {
                Add(input, parameters[index].Write?.Library, ref used, ref isBelowTheFloor);
            }
        }

        if (methodOutput is { Columns: var columns })
        {
            for (var index = 0; index < columns.Count; index++)
            {
                Add(input, columns[index].Read?.Library, ref used, ref isBelowTheFloor);
            }
        }

        return isBelowTheFloor;
    }

    private static void AddProperties(
        MethodsInput input,
        EquatableArray<ModelProperty>? properties,
        ref SortedSet<SqlLibrary>? used,
        ref bool isBelowTheFloor
    )
    {
        if (properties is not { } each)
        {
            return;
        }

        for (var index = 0; index < each.Count; index++)
        {
            Add(input, each[index].Type.Library, ref used, ref isBelowTheFloor);
        }
    }

    private static void Add(
        MethodsInput input,
        SqlLibrary? library,
        ref SortedSet<SqlLibrary>? used,
        ref bool isBelowTheFloor
    )
    {
        if (library is not { } value)
        {
            return;
        }

        _ = (used ??= []).Add(value);
        isBelowTheFloor |= input.IsBelowTheFloor(value);
    }

    // The libraries in order, or the empty array when there are none.
    private static EquatableArray<LibraryValue> ToValues(SortedSet<SqlLibrary>? libraries) =>
        libraries is null
            ? EquatableArray<LibraryValue>.Empty
            : new EquatableArray<LibraryValue>(
                libraries.Select(static library => new LibraryValue(library)).ToImmutableArray()
            );

    // The ids of the warnings that a method gives: of each property type it reads or binds, which it names as its
    // model does, and of each converter it calls.
    private static List<string> WarningIdsOf(QueryModels models, MethodInput? input, MethodOutput? output)
    {
        var ids = new List<string>();
        AddTypes(models.Input);
        AddTypes(models.Output);
        foreach (var parameter in input?.Parameters ?? EquatableArray<MethodParameter>.Empty)
        {
            ids.AddRange(parameter.Write?.WarningIds ?? EquatableArray<string>.Empty);
        }

        foreach (var column in output?.Columns ?? EquatableArray<MethodColumn>.Empty)
        {
            ids.AddRange(column.Read?.WarningIds ?? EquatableArray<string>.Empty);
        }

        return ids;

        void AddTypes(EquatableArray<ModelProperty>? properties)
        {
            foreach (var property in properties ?? EquatableArray<ModelProperty>.Empty)
            {
                ids.AddRange(property.Type.WarningIds);
            }
        }
    }

    // SQLSRC275 for each token named as a fixed argument.
    private static List<DiagnosticInfo> FindTokenProblems(SqlQuery query) =>
        [
            .. query
                .Tokens.Where(static token => Array.IndexOf(FixedArguments, token.Name) >= 0)
                .Select(token =>
                    DiagnosticInfo.Create(
                        SqlDiagnostics.TokenNamedAsAnArgument,
                        query.NameLocation,
                        token.Name,
                        query.Name
                    )
                ),
        ];

    // The input model, or null when the query has no parameters.  False when it needs one and has none, when its
    // properties do not match its parameters, which is never thrown on, or when a parameter's converter blocks it.
    private static bool TryReadInput(
        QueryContext context,
        QueryModels models,
        Reading reading,
        out MethodInput? methodInput
    )
    {
        var (input, key, file, _, usable) = context;
        var (_, queryIndex, query, _) = usable;
        methodInput = null;
        if (models.Input is not { } properties)
        {
            return true;
        }

        if (
            FindAssignment(input.Assignments, key, file.NormalizedPath, queryIndex, ModelRole.Input) is not { } model
            || properties.Count != query.Parameters.Count
        )
        {
            return false;
        }

        var parameters = ImmutableArray.CreateBuilder<MethodParameter>(properties.Count);
        var isBlocked = false;
        for (var index = 0; index < properties.Count; index++)
        {
            var property = properties[index];
            var write = reading.ConversionOf(property, false, ref isBlocked);
            parameters.Add(
                new MethodParameter(
                    query.Parameters[index].Name,
                    property.Name,
                    property.Type.Text,
                    property.Type.IsValueType,
                    property.IsNullable,
                    write,
                    models.ParameterTypes?[index]
                )
            );
        }

        methodInput = new MethodInput(
            model.TypeText,
            new EquatableArray<MethodParameter>(parameters.MoveToImmutable())
        );
        return !isBlocked;
    }

    // The output model, or null when the query's entry has no rows.  False when it needs one and has none, when its
    // columns' names do not match its properties, or when a column's converter blocks it.
    private static bool TryReadOutput(
        QueryContext context,
        QueryModels models,
        Reading reading,
        out MethodOutput? methodOutput
    )
    {
        var (input, key, file, _, usable) = context;
        var (_, queryIndex, _, settings) = usable;
        methodOutput = null;
        if (models.Output is not { } properties)
        {
            return true;
        }

        if (
            models.ColumnNames is not { } names
            || names.Count != properties.Count
            || FindAssignment(input.Assignments, key, file.NormalizedPath, queryIndex, ModelRole.Output)
                is not { } model
        )
        {
            return false;
        }

        var columns = ImmutableArray.CreateBuilder<MethodColumn>(properties.Count);
        var isBlocked = false;
        for (var index = 0; index < properties.Count; index++)
        {
            var property = properties[index];
            var read = reading.ConversionOf(property, true, ref isBlocked);
            columns.Add(new MethodColumn(names[index], property.Name, property.Type.Text, property.IsNullable, read));
        }

        methodOutput = new MethodOutput(
            model.TypeText,
            model.Kind is ModelKind.Record or ModelKind.SealedRecord,
            new EquatableArray<MethodColumn>(columns.MoveToImmutable()),
            ConstructorOrder(properties, (settings.Parameters & GeneratorParameters.SortOutput) != 0)
        );
        return !isBlocked;
    }

    // The ordinals in the order of the model's properties: by name, ordinally, when the model is sorted, as
    // TypeModels sorts it; the names are distinct ignoring case, so there are no ties.
    private static EquatableArray<int> ConstructorOrder(EquatableArray<ModelProperty> properties, bool isSorted)
    {
        var order = new int[properties.Count];
        for (var index = 0; index < order.Length; index++)
        {
            order[index] = index;
        }

        if (isSorted)
        {
            Array.Sort(order, (left, right) => string.CompareOrdinal(properties[left].Name, properties[right].Name));
        }

        return new EquatableArray<int>(ImmutableArray.Create(order));
    }

    // The type's assignment of the query and role, by a search of the type's run.
    private static ModelAssignment? FindAssignment(
        EquatableArray<ModelAssignment> assignments,
        string key,
        string path,
        int queryIndex,
        ModelRole role
    )
    {
        var at = ModelAssignment.IndexOf(assignments, key, path, queryIndex, role);
        return at >= 0 ? assignments[at] : null;
    }

    // What takes the name of a method of the type, as SQLSRC274 says it, or null when nothing does.  Another method of
    // the type is looked for once every query is planned.
    private static string? FindTakenReason(MethodsInput input, string name, HashSet<string> written)
    {
        var type = input.Type.Type.Type;
        if (type.MethodPlacement == MethodPlacement.ExtensionClass)
        {
            if (string.Equals(name, type.Name + ExtensionsSuffix, StringComparison.Ordinal))
            {
                return "the class that holds the method has that name";
            }

            return
                SortedSearch.IndexOf(DbConnectionMembers.Names, name, static member => member, StringComparer.Ordinal)
                >= 0
                ? "DbConnection has a member of that name, which a call on a connection finds before the method"
                : null;
        }

        // The type by its full name, so that two types of one name that claim one file each report their own.
        var typeName = TypeModels.ClaimingTypeOf(type).Name;
        if (string.Equals(name, type.Name, StringComparison.Ordinal))
        {
            return "it is the name of the type '" + typeName + "' that holds the method";
        }

        if (HasTypeParameter(type.Types[type.Types.Count - 1].TypeParameters, name))
        {
            return "'" + typeName + "' has a type parameter of that name";
        }

        if (SortedSearch.IndexOf(input.MemberNames, name, static member => member, StringComparer.Ordinal) >= 0)
        {
            return "'" + typeName + "' has a member of that name";
        }

        if (type.Placement == MemberPlacement.Direct)
        {
            return written.Contains(name)
                ? "the query '" + name + "' of '" + typeName + "' has a constant or method of that name"
                : null;
        }

        return written.Count > 0 && string.Equals(name, type.NestedClass, StringComparison.Ordinal)
            ? "the class of the queries of '" + typeName + "' has that name"
            : null;
    }

    // Whether the type parameters as written, "<TKey, @TValue>", have the name, as a value.
    private static bool HasTypeParameter(string typeParameters, string name) =>
        typeParameters.Length > 2
        && typeParameters
            .Substring(1, typeParameters.Length - 2)
            .Split(',')
            .Any(parameter => string.Equals(parameter.Trim().TrimStart('@'), name, StringComparison.Ordinal));

    // The planned queries in order: the problems of each, then SQLSRC274 at each of two or more methods of one name,
    // and the rest of the methods when their driver allows them, with the ids of their warnings and the libraries
    // whose conversions they call.
    private static void AddPlanned(
        List<Planned> planned,
        ImmutableArray<QueryMethod>.Builder methods,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        SortedSet<string> warningIds,
        ref SortedSet<SqlLibrary>? conversionLibraries
    )
    {
        var byName = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var plan in planned.Where(static plan => plan.Method is not null))
        {
            if (!byName.TryGetValue(plan.Name, out var queries))
            {
                queries = [];
                byName.Add(plan.Name, queries);
            }

            queries.Add(plan.QueryName);
        }

        foreach (var plan in planned)
        {
            diagnostics.AddRange(plan.Problems);
            if (plan.Method is not null && byName[plan.Name] is { Count: > 1 } queries)
            {
                var other = queries[0] == plan.QueryName ? queries[1] : queries[0];
                diagnostics.Add(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.MethodNameTaken,
                        plan.NameLocation,
                        plan.QueryName,
                        plan.Name,
                        "the method of the query '" + other + "' has that name too"
                    )
                );
            }
            else if (plan is { Method: { } method, IsDriverUsable: true })
            {
                methods.Add(method);
                warningIds.UnionWith(plan.WarningIds);
                AddConversionLibraries(method, ref conversionLibraries);
            }
        }
    }

    // The library of each conversion of the method that is a library's generated one, a column's read or a
    // parameter's write, into a set made only when the first is added.
    private static void AddConversionLibraries(QueryMethod method, ref SortedSet<SqlLibrary>? libraries)
    {
        foreach (var parameter in method.Input?.Parameters ?? EquatableArray<MethodParameter>.Empty)
        {
            if (parameter.Write?.Library is { } library)
            {
                _ = (libraries ??= []).Add(library);
            }
        }

        foreach (var column in method.Output?.Columns ?? EquatableArray<MethodColumn>.Empty)
        {
            if (column.Read?.Library is { } library)
            {
                _ = (libraries ??= []).Add(library);
            }
        }
    }

    // The shape as the name marker writes it.
    private static string ShapeName(ResultShape shape) =>
        shape switch
        {
            ResultShape.One => "one",
            ResultShape.OneOptional => "one-optional",
            ResultShape.Many => "many",
            ResultShape.None => "none",
            ResultShape.RowCount or _ => "rowcount",
        };

    // The values that planning one query reads from its type and its file: the type's input and key, the query's file
    // and the engine of its dialect, and the query with its place and settings.
    private readonly record struct QueryContext(
        MethodsInput Input,
        string Key,
        ParsedSqlFile File,
        DatabaseEngine Engine,
        UsableQuery Query
    );

    // What deciding the converters of one query's properties reads: whether its driver allows a method, without which
    // nothing is decided, and the context of its conversions.
    private readonly record struct Reading(bool IsDriverUsable, ConversionContext Conversions)
    {
        // The property's conversion, null with nothing decided when the driver allows no method.  A block is OR-ed in.
        public Conversion? ConversionOf(ModelProperty property, bool isRead, ref bool isBlocked)
        {
            if (!IsDriverUsable)
            {
                return null;
            }

            var conversion = ConversionPlanner.Plan(
                property.Type,
                isRead,
                property.Subject,
                Conversions,
                out var blocked
            );
            isBlocked |= blocked;
            return conversion;
        }
    }

    // The converters of the type's slice that serve each target, filtered once for the type, and only for a target that
    // one of its queries is on: at most three lists, never one for each query.  A slice without a library's edge is
    // every target's as it is, so a type without one allocates nothing.
    private struct ServedConverters(EquatableArray<Converter> all)
    {
        private readonly bool _hasLibraryEdge = HasLibraryEdge(all);

        private EquatableArray<Converter>? _sqlServer;

        private EquatableArray<Converter>? _npgsqlBefore10;

        private EquatableArray<Converter>? _npgsql10;

        // The converters that serve the target, in the order of the slice.
        public EquatableArray<Converter> Of(ConverterTargets target)
        {
            if (!_hasLibraryEdge)
            {
                return all;
            }

            if (target == ConverterTargets.SqlServer)
            {
                return _sqlServer ??= Serving(all, target);
            }

            return target == ConverterTargets.NpgsqlBefore10
                ? _npgsqlBefore10 ??= Serving(all, target)
                : _npgsql10 ??= Serving(all, target);
        }

        // By index, so that the search boxes no enumerator.
        private static bool HasLibraryEdge(EquatableArray<Converter> converters)
        {
            for (var index = 0; index < converters.Count; index++)
            {
                if (converters[index].Library is not null)
                {
                    return true;
                }
            }

            return false;
        }

        // The converters that serve the target, or the slice itself when every one does.
        private static EquatableArray<Converter> Serving(EquatableArray<Converter> converters, ConverterTargets target)
        {
            var serving = ImmutableArray.CreateBuilder<Converter>(converters.Count);
            serving.AddRange(converters.Where(converter => converter.Serves(target)));

            return serving.Count == converters.Count
                ? converters
                : new EquatableArray<Converter>(serving.ToImmutable());
        }
    }

    // One query's problems, the name its method would have, and its method, with whether its driver allows it and the
    // ids of the warnings it gives, when it has no problem and its models were read.
    private sealed record Planned(
        List<DiagnosticInfo> Problems,
        string Name,
        string QueryName,
        LocationInfo NameLocation,
        QueryMethod? Method,
        bool IsDriverUsable,
        List<string> WarningIds
    );
}
