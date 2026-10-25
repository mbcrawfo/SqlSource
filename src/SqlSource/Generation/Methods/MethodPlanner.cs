using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using SqlSource.Diagnostics;
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
    /// <see cref="OutputKind.CodeGen" />, whose entry has the models it needs, and whose name and tokens a method can
    /// have.  A type with a diagnostic of its own, or with no file, has none.
    /// </summary>
    public static TypeMethods Plan(MethodsInput input)
    {
        var typeQueries = input.Type;
        var type = typeQueries.Type.Type;
        var key = HintName.NameOf(type);
        var methods = ImmutableArray.CreateBuilder<QueryMethod>();
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var engines = CodeGenEngines.None;
        if (typeQueries.Type.Diagnostics.Count == 0 && typeQueries.Files.Count > 0)
        {
            engines = Collect(input, key, methods, diagnostics);
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
            engines
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
        ImmutableArray<DiagnosticInfo>.Builder diagnostics
    )
    {
        var typeQueries = input.Type;
        var type = typeQueries.Type.Type;
        var usable = TypeEmitter.FindUsableFiles(
            type,
            typeQueries.Files,
            typeQueries.FileSettings,
            input.Property,
            null
        );
        var queries = Resolve(input, usable);
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

            var file = typeQueries.Files[fileIndex];
            var engine = EngineOf(file.Dialect);
            engines |= engine;
            if (Plan(input, key, file, fileIndex, queryIndex, query, settings, written, engine) is { } plan)
            {
                planned.Add(plan);
            }
        }

        AddPlanned(planned, methods, diagnostics);
        return engines;
    }

    // Each query of the usable files, in order, with the settings in effect for it here.
    private static List<(int File, int Query, SqlQuery Value, QuerySettings Settings)> Resolve(
        MethodsInput input,
        bool[] usable
    )
    {
        var typeQueries = input.Type;
        var type = typeQueries.Type.Type;
        var queries = new List<(int File, int Query, SqlQuery Value, QuerySettings Settings)>();
        for (var fileIndex = 0; fileIndex < typeQueries.Files.Count; fileIndex++)
        {
            if (!usable[fileIndex])
            {
                continue;
            }

            var file = typeQueries.Files[fileIndex];
            for (var queryIndex = 0; queryIndex < file.Queries.Count; queryIndex++)
            {
                var query = file.Queries[queryIndex];
                var settings = QuerySettings.Resolve(
                    query.Markers,
                    type.Settings,
                    typeQueries.FileSettings[fileIndex],
                    input.Property
                );
                queries.Add((fileIndex, queryIndex, query, settings));
            }
        }

        return queries;
    }

    // The names of the members the type's file writes for the queries that get no method: each one's constant or token
    // method, in the type itself or in its nested class.
    private static HashSet<string> WrittenNames(
        List<(int File, int Query, SqlQuery Value, QuerySettings Settings)> queries
    )
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (_, _, query, settings) in queries)
        {
            if (settings.Output != OutputKind.CodeGen)
            {
                _ = names.Add(query.Name);
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
        CodeGenEngines engine
    )
    {
        // A query without models has its own error already, reported by its file.
        if (FindModels(input.Files[fileIndex], queryIndex, query.Name) is not { } models)
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
        var planned = new Planned(problems, name, query.Name, query.NameLocation, null, false);
        var databaseEngine = SqlDescribableDialects.EngineOf(file.Dialect);
        var hasInput = TryReadInput(
            input,
            key,
            file,
            queryIndex,
            query,
            models,
            databaseEngine,
            problems,
            out var methodInput
        );
        var hasOutput = TryReadOutput(
            input,
            key,
            file,
            queryIndex,
            settings,
            models,
            databaseEngine,
            problems,
            out var methodOutput
        );
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
            databaseEngine,
            settings.KeepComments ? query.KeptSegments ?? query.Segments : query.Segments,
            settings.ValidateTokens,
            methodInput,
            methodOutput,
            query.NameLocation
        );
        var isUsable =
            (engine == CodeGenEngines.Postgres && input.Drivers.Postgres)
            || (engine == CodeGenEngines.SqlServer && input.Drivers.SqlServer);
        return planned with { Method = method, IsDriverUsable = isUsable };
    }

    // The query's models, matched by index and name as TypeModels matches them.  The models of a file are in the order
    // of its queries, so a search finds them.
    private static QueryModels? FindModels(FileModels file, int queryIndex, string name)
    {
        var (low, high) = (0, file.Queries.Count - 1);
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var models = file.Queries[middle];
            if (models.Index == queryIndex)
            {
                return string.Equals(models.Name, name, StringComparison.Ordinal) ? models : null;
            }

            if (models.Index < queryIndex)
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
        DatabaseEngine engine,
        List<DiagnosticInfo> problems,
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
            var write = ConversionPlanner.Plan(
                property.Type,
                false,
                engine,
                input.Converters,
                property.Subject,
                query.NameLocation,
                problems,
                out var blocked
            );
            isBlocked |= blocked;
            parameters.Add(
                new MethodParameter(
                    query.Parameters[index].Name,
                    property.Name,
                    property.Type.IsValueType,
                    property.IsNullable,
                    write
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
        DatabaseEngine engine,
        List<DiagnosticInfo> problems,
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
            var read = ConversionPlanner.Plan(
                property.Type,
                true,
                engine,
                input.Converters,
                property.Subject,
                models.NameLocation,
                problems,
                out var blocked
            );
            isBlocked |= blocked;
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
    // and the rest of the methods when their driver allows them.
    private static void AddPlanned(
        List<Planned> planned,
        ImmutableArray<QueryMethod>.Builder methods,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics
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
            }
        }
    }

    // The engine of a describable dialect: the methods of no other have an entry.
    private static CodeGenEngines EngineOf(SqlDialect dialect)
    {
        if (dialect == SqlDialect.PostgreSql)
        {
            return CodeGenEngines.Postgres;
        }

        return dialect == SqlDialect.SqlServer ? CodeGenEngines.SqlServer : CodeGenEngines.None;
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

    // One query's problems, the name its method would have, and its method, with whether its driver allows it, when it
    // has no problem and its models were read.
    private sealed record Planned(
        List<DiagnosticInfo> Problems,
        string Name,
        string QueryName,
        LocationInfo NameLocation,
        QueryMethod? Method,
        bool IsDriverUsable
    );
}
