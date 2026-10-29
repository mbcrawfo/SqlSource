using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using SqlSource.Diagnostics;
using SqlSource.Generation.Converters;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Models;
using SqlSource.Parsing;
using SqlSource.Settings;

namespace SqlSource.Generation.Methods;

/// <summary>
/// Decides which queries of one type get a method, and what each method is written from, with the problems of those
/// that cannot have one: <c>SQLSRC272</c>, <c>SQLSRC274</c> and <c>SQLSRC275</c>, and, from
/// <see cref="ConversionPlanner" />, <c>SQLSRC279</c>, <c>SQLSRC280</c>, and <c>SQLSRC277</c> and <c>SQLSRC278</c> for
/// a converter of a referenced assembly.
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
    /// converters a method can have, and whose engine's driver the project references at or above the floor.  A type
    /// with a diagnostic of its own, or with no file, has none.
    /// </summary>
    public static TypeMethods Plan(MethodsInput input)
    {
        var typeQueries = input.Type;
        var type = typeQueries.Type.Type;
        var key = HintName.NameOf(type);
        var methods = ImmutableArray.CreateBuilder<QueryMethod>();
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var engines = CodeGenEngines.None;
        var warningIds = new SortedSet<string>(StringComparer.Ordinal);
        if (typeQueries.Type.Diagnostics.Count == 0 && typeQueries.Files.Count > 0)
        {
            engines = Collect(input, key, methods, diagnostics, warningIds);
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
            engines,
            new EquatableArray<string>(warningIds.ToImmutableArray())
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
        SortedSet<string> warningIds
    )
    {
        var typeQueries = input.Type;
        var queries = UsableQueries.Of(typeQueries, null);
        var written = WrittenNames(queries);
        var planned = new List<Planned>();
        var engines = CodeGenEngines.None;
        for (var index = 0; index < queries.Count; index++)
        {
            var (fileIndex, queryIndex, query, settings) = queries[index];
            if (settings.Output != OutputKind.CodeGen)
            {
                continue;
            }

            // Only a describable dialect's query has an entry, so only it can have a method or need a driver.
            var file = typeQueries.Files[fileIndex];
            if (!SqlDescribableDialects.Contains(file.Dialect))
            {
                continue;
            }

            var engine = SqlDescribableDialects.EngineOf(file.Dialect);
            engines |= engine.ToCodeGenEngines();
            if (Plan(input, key, file, fileIndex, queryIndex, query, settings, written, engine) is { } plan)
            {
                planned.Add(plan);
            }
        }

        AddPlanned(planned, methods, diagnostics, warningIds);
        return engines;
    }

    // The names of the members the type's file writes for the queries whose output is not CodeGen: each one's constant
    // or token method, in the type itself or in its nested class.
    private static HashSet<string> WrittenNames(List<UsableQuery> queries)
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
    // it no method.
    private static Planned? Plan(
        MethodsInput input,
        string key,
        ParsedSqlFile file,
        int fileIndex,
        int queryIndex,
        SqlQuery query,
        QuerySettings settings,
        HashSet<string> written,
        DatabaseEngine engine
    )
    {
        // A query without models has its own error already, reported by its file.
        if (input.Files[fileIndex].Find(queryIndex, query.Name) is not { } models)
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
        var reading = new Reading(engine, isUsable, query.NameLocation, problems);
        var hasInput = TryReadInput(input, key, file, queryIndex, query, models, reading, out var methodInput);
        var hasOutput = TryReadOutput(input, key, file, queryIndex, settings, models, reading, out var methodOutput);
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
        MethodsInput input,
        string key,
        ParsedSqlFile file,
        int queryIndex,
        SqlQuery query,
        QueryModels models,
        Reading reading,
        out MethodInput? methodInput
    )
    {
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
            var write = reading.ConversionOf(property, false, input.Converters, ref isBlocked);
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
        MethodsInput input,
        string key,
        ParsedSqlFile file,
        int queryIndex,
        QuerySettings settings,
        QueryModels models,
        Reading reading,
        out MethodOutput? methodOutput
    )
    {
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
            var read = reading.ConversionOf(property, true, input.Converters, ref isBlocked);
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
        var probe = new ModelAssignment(key, path, queryIndex, role, string.Empty, default);
        var (low, high) = (0, assignments.Count - 1);
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var order = ModelAssignment.Order.Compare(assignments[middle], probe);
            if (order == 0)
            {
                return assignments[middle];
            }

            if (order < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return null;
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
    // and the rest of the methods when their driver allows them, with the ids of their warnings.
    private static void AddPlanned(
        List<Planned> planned,
        ImmutableArray<QueryMethod>.Builder methods,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        SortedSet<string> warningIds
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

    // What deciding the converters of one query's properties reads: the engine of its file, whether its driver allows
    // a method, without which nothing is decided, the query's name, where a problem goes, and its problems.
    private readonly record struct Reading(
        DatabaseEngine Engine,
        bool IsDriverUsable,
        LocationInfo At,
        List<DiagnosticInfo> Problems
    )
    {
        // The property's conversion, null with nothing decided when the driver allows no method.  A block is OR-ed in.
        public Conversion? ConversionOf(
            ModelProperty property,
            bool isRead,
            EquatableArray<Converter> converters,
            ref bool isBlocked
        )
        {
            if (!IsDriverUsable)
            {
                return null;
            }

            var conversion = ConversionPlanner.Plan(
                property.Type,
                isRead,
                Engine,
                converters,
                property.Subject,
                At,
                Problems,
                out var blocked
            );
            isBlocked |= blocked;
            return conversion;
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
