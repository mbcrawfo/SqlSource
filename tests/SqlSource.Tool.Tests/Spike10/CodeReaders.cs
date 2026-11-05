using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SqlSource.Tool.Tests.Spike10;

// Who reads a record member of an engine's calls, and who reads a key of a plan, found with the compiler's own symbols
// over the tool's sources.  A reader is "<file under Describing/>:<member>"; a file of a live connection is `live`
// (it runs only against a server), one of the events is `log` (it only writes the log), the rest `code`.
internal sealed class CodeReaders
{
    private static readonly Lazy<CodeReaders> Shared = new(static () => new CodeReaders());

    private readonly CSharpCompilation _compilation;
    private readonly string _describing;

    private CodeReaders()
    {
        var root = RepositoryRoot();
        _describing = Path.Combine(root, "src", "SqlSource.Tool", "Describing") + Path.DirectorySeparatorChar;
        var parse = new CSharpParseOptions(LanguageVersion.Preview);
        var trees = Directory
            .EnumerateFiles(Path.Combine(root, "src", "SqlSource.Tool"), "*.cs", SearchOption.AllDirectories)
            .Where(static f =>
                !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            )
            .Where(static f =>
                !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            )
            .Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), parse, f))
            .ToList();
        trees.Add(
            CSharpSyntaxTree.ParseText(
                "global using System; global using System.Collections.Generic; global using System.IO;"
                    + " global using System.Linq; global using System.Threading; global using System.Threading.Tasks;",
                parse,
                "usings.cs"
            )
        );
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(static p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();
        _compilation = CSharpCompilation.Create(
            "SqlSource.Tool",
            trees,
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );
    }

    // The errors of the compilation of the tool's sources, which show where a symbol could not be bound.
    public IReadOnlyList<string> Errors() =>
        [
            .. _compilation
                .GetDiagnostics()
                .Where(static d => d.Severity == DiagnosticSeverity.Error)
                .Select(static d => d.ToString()),
        ];

    public static CodeReaders Instance => Shared.Value;

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SqlSource.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root is not found.");
    }

    // The key "<method>\t<side>\t<path>" of every member the records of the engine's calls can hold, with who reads it.
    public IReadOnlyDictionary<string, IReadOnlyList<string>> MembersOf(
        string engine,
        IReadOnlyDictionary<string, (string Request, string Answer)> types
    )
    {
        var callsFile = engine == "postgres" ? "PostgresCalls.cs" : "SqlServerCalls.cs";
        var all = new List<(string Key, IPropertySymbol Property)>();
        foreach (var (method, (request, answer)) in types)
        {
            foreach (var (side, typeName) in new[] { ("request", request), ("answer", answer) })
            {
                Walk(Find(typeName), method, side, side, all, [with(SymbolEqualityComparer.Default)]);
            }
        }

        var tracked = all.Select(static p => p.Property)
            .Distinct(SymbolEqualityComparer.Default)
            .Cast<IPropertySymbol>()
            .ToList();
        var found = ReadersOf(tracked, callsFile);
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var (key, property) in all)
        {
            result[key] = found.TryGetValue(property.OriginalDefinition, out var set) ? [.. set] : [];
        }

        return result;
    }

    // The readers of the properties of one type, for the members of a plan node.
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ReadersOfMembers(string typeName, string ownFile)
    {
        var type = Find(typeName);
        var properties = type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(static p => !p.IsStatic && !p.IsImplicitlyDeclared && p.ExplicitInterfaceImplementations.Length == 0)
            .ToList();
        var found = ReadersOf(properties, ownFile);
        return properties.ToDictionary(
            static p => p.Name,
            p => (IReadOnlyList<string>)(found.TryGetValue(p.OriginalDefinition, out var set) ? [.. set] : []),
            StringComparer.Ordinal
        );
    }

    // The files and members whose string literals and nameof()s equal the key, in the folder of an engine.
    public IReadOnlyList<string> LiteralReaders(string key, string folder)
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);
        foreach (
            var tree in _compilation.SyntaxTrees.Where(t =>
                t.FilePath.StartsWith(_describing + folder, StringComparison.Ordinal)
            )
        )
        {
            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                var hit = node switch
                {
                    LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } literal => literal
                        .Token
                        .ValueText == key,
                    InvocationExpressionSyntax
                    {
                        Expression: IdentifierNameSyntax { Identifier.Text: "nameof" }
                    } invoke => invoke.ArgumentList.Arguments.Count == 1
                        && invoke.ArgumentList.Arguments[0].ToString() == key,
                    _ => false,
                };
                if (hit)
                {
                    _ = found.Add(Describe(tree, node));
                }
            }
        }

        return [.. found];
    }

    private INamedTypeSymbol Find(string name) =>
        _compilation.GetSymbolsWithName(name, SymbolFilter.Type).OfType<INamedTypeSymbol>().First();

    // The properties of a call's record, recursively, by the path the capture writes them at.
    private static void Walk(
        INamedTypeSymbol type,
        string method,
        string side,
        string prefix,
        List<(string Key, IPropertySymbol Property)> into,
        HashSet<INamedTypeSymbol> seen
    )
    {
        if (!seen.Add(type))
        {
            return;
        }

        foreach (
            var property in type.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(static p =>
                    !p.IsStatic && !p.IsImplicitlyDeclared && p.ExplicitInterfaceImplementations.Length == 0
                )
        )
        {
            var path = prefix + "." + JsonNamingPolicy.CamelCase.ConvertName(property.Name);
            into.Add(($"{method}\t{side}\t{path}", property));
            var inner = Unwrap(property.Type, out var isArray);
            if (isArray)
            {
                path += "[]";
                if (inner is not INamedTypeSymbol element || !IsRecordOfCalls(element))
                {
                    into.Add(($"{method}\t{side}\t{path}", property));
                }
            }

            if (inner is INamedTypeSymbol nested && IsRecordOfCalls(nested))
            {
                Walk(nested, method, side, path, into, seen);
            }
        }

        _ = seen.Remove(type);
    }

    private static bool IsRecordOfCalls(INamedTypeSymbol type) =>
        type.IsRecord
        && type.DeclaringSyntaxReferences.Any(static r =>
            r.SyntaxTree.FilePath.EndsWith("PostgresCalls.cs", StringComparison.Ordinal)
            || r.SyntaxTree.FilePath.EndsWith("SqlServerCalls.cs", StringComparison.Ordinal)
        );

    private static ITypeSymbol Unwrap(ITypeSymbol type, out bool isArray)
    {
        isArray = false;
        if (type is INamedTypeSymbol { IsGenericType: true } generic)
        {
            if (generic.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                return Unwrap(generic.TypeArguments[0], out isArray);
            }

            if (generic.Name is "IReadOnlyList" or "IReadOnlyCollection" or "IEnumerable" or "List")
            {
                isArray = true;
                return generic.TypeArguments[0];
            }
        }

        return type;
    }

    // Who reads each property: the identifiers that the compiler binds to it, or to the interface member it implements.
    private Dictionary<IPropertySymbol, SortedSet<string>> ReadersOf(
        IReadOnlyList<IPropertySymbol> properties,
        string ownFile
    )
    {
        var map = new Dictionary<IPropertySymbol, List<IPropertySymbol>>(SymbolEqualityComparer.Default);
        foreach (var property in properties)
        {
            AddTarget(map, property.OriginalDefinition, property.OriginalDefinition);
            foreach (var @interface in property.ContainingType.AllInterfaces)
            {
                foreach (var member in @interface.GetMembers().OfType<IPropertySymbol>())
                {
                    var impl = property.ContainingType.FindImplementationForInterfaceMember(member) as IPropertySymbol;
                    var explicitOf =
                        impl is not null && impl.ExplicitInterfaceImplementations.Length > 0
                            ? property.ContainingType.GetMembers("Suggested" + member.Name).FirstOrDefault()
                            : null;
                    if (
                        SymbolEqualityComparer.Default.Equals(impl, property)
                        || SymbolEqualityComparer.Default.Equals(explicitOf, property)
                    )
                    {
                        AddTarget(map, member.OriginalDefinition, property.OriginalDefinition);
                    }
                }
            }
        }

        var names = map.Keys.Select(static k => k.Name).ToHashSet(StringComparer.Ordinal);
        var found = new Dictionary<IPropertySymbol, SortedSet<string>>(SymbolEqualityComparer.Default);
        foreach (var tree in _compilation.SyntaxTrees)
        {
            var kind = KindOf(tree.FilePath, ownFile);
            if (kind is null)
            {
                continue;
            }

            SemanticModel? model = null;
            foreach (var identifier in tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (!names.Contains(identifier.Identifier.Text) || InNameof(identifier))
                {
                    continue;
                }

                model ??= _compilation.GetSemanticModel(tree);
                if (
                    model.GetSymbolInfo(identifier).Symbol is not IPropertySymbol symbol
                    || !map.TryGetValue(symbol.OriginalDefinition, out var target)
                )
                {
                    continue;
                }

                foreach (var one in target)
                {
                    if (!found.TryGetValue(one, out var set))
                    {
                        set = [with(StringComparer.Ordinal)];
                        found[one] = set;
                    }

                    _ = set.Add(kind + Describe(tree, identifier));
                }
            }
        }

        return found;
    }

    private static void AddTarget(
        Dictionary<IPropertySymbol, List<IPropertySymbol>> map,
        IPropertySymbol from,
        IPropertySymbol to
    )
    {
        if (!map.TryGetValue(from, out var list))
        {
            list = [];
            map[from] = list;
        }

        list.Add(to);
    }

    private static bool InNameof(SyntaxNode node) =>
        node.Ancestors()
            .OfType<InvocationExpressionSyntax>()
            .Any(static i => i.Expression is IdentifierNameSyntax { Identifier.Text: "nameof" });

    // The prefix of a file's readers: "" for code, "live " and "log " for the others, null for a file that is no
    // reader.
    private string? KindOf(string path, string ownFile)
    {
        if (!path.StartsWith(_describing, StringComparison.Ordinal))
        {
            return path.Contains("SqlSource.Tool", StringComparison.Ordinal) ? "other " : null;
        }

        var relative = path[_describing.Length..];
        if (
            relative.StartsWith("Capture", StringComparison.Ordinal)
            || relative.EndsWith(Path.DirectorySeparatorChar + ownFile, StringComparison.Ordinal)
            || relative == ownFile
        )
        {
            return null;
        }

        var file = Path.GetFileName(path);
        if (file.Contains("Live", StringComparison.Ordinal))
        {
            return "live ";
        }

        return file.EndsWith("Events.cs", StringComparison.Ordinal) ? "log " : string.Empty;
    }

    private string Describe(SyntaxTree tree, SyntaxNode node)
    {
        var relative = tree.FilePath.StartsWith(_describing, StringComparison.Ordinal)
            ? tree.FilePath[_describing.Length..]
            : "../" + Path.GetFileName(tree.FilePath);
        var member = node.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault();
        var name = member switch
        {
            MethodDeclarationSyntax m => m.Identifier.Text,
            ConstructorDeclarationSyntax c => c.Identifier.Text,
            PropertyDeclarationSyntax p => p.Identifier.Text,
            FieldDeclarationSyntax f => f.Declaration.Variables[0].Identifier.Text,
            OperatorDeclarationSyntax => "operator",
            BaseTypeDeclarationSyntax t => t.Identifier.Text,
            _ => "?",
        };
        return relative.Replace(Path.DirectorySeparatorChar, '/') + ":" + name;
    }
}
