using System;
using System.Collections.Generic;
using System.Globalization;
using Bogus;
using SqlSource.Snapshot;
using static SqlSource.Tests.Snapshot.TestSidecars;

namespace SqlSource.Tests.Snapshot;

// Builds sidecars from a seed, so that a failure can be run again.  Strict, they are what the tool builds: one of
// the two engines, every "always" key with a value, and every value inside the limits of the schema.  Lenient, they
// are what a reader may find: another engine, a kind and a provenance value it does not know, and nulls where the
// reader allows them.  Neither holds what the writer's conditions leave out.
internal sealed class SidecarFaker(int seed, bool lenient)
{
    public const string UnknownEngine = "sqlite";

    public const string UnknownKind = "pseudo";

    private const string Letters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_";

    private static readonly string[] Awkward =
    [
        "a\"b",
        "back\\slash",
        "tab\there",
        "line\nbreak",
        "\u0001",
        "größe",
        "日本語",
        "😀",
        "a/b",
        "{{x}}",
        " ",
        "deleted_at?",
    ];

    private static readonly string[] Kinds =
    [
        SidecarValues.Kind.Base,
        SidecarValues.Kind.Array,
        SidecarValues.Kind.Domain,
        SidecarValues.Kind.Enum,
        SidecarValues.Kind.Range,
        SidecarValues.Kind.Multirange,
        SidecarValues.Kind.Composite,
    ];

    private static readonly string[] Plans =
    [
        SidecarValues.Plan.NotNeeded,
        SidecarValues.Plan.Walked,
        SidecarValues.Plan.Unavailable,
        SidecarValues.Plan.Skipped,
    ];

    private static readonly string[] TableMatches =
    [
        SidecarValues.TableMatch.Matched,
        SidecarValues.TableMatch.NoOrigin,
        SidecarValues.TableMatch.SeveralTables,
        SidecarValues.TableMatch.NamesDiffer,
        SidecarValues.TableMatch.ColumnsDiffer,
        SidecarValues.TableMatch.OrderDiffers,
        SidecarValues.TableMatch.NullabilityDiffers,
    ];

    private static readonly string[] TypeSources =
    [
        SidecarValues.TypeSource.Inferred,
        SidecarValues.TypeSource.InferredFromCopies,
        SidecarValues.TypeSource.Declared,
    ];

    private static readonly string[] NullableSources =
    [
        SidecarValues.NullableSource.Server,
        SidecarValues.NullableSource.Catalog,
        SidecarValues.NullableSource.OuterJoin,
        SidecarValues.NullableSource.View,
        SidecarValues.NullableSource.NoOrigin,
        SidecarValues.NullableSource.Heuristic,
    ];

    private readonly Randomizer _random = new(seed);

    // Counts the PostgreSQL types built, so that each kind comes in turn and a few sidecars hold them all.
    private int _types;

    public Sidecar Next()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var entries = new List<SidecarEntry>();
        var count = _random.Int(1, 4);
        while (entries.Count < count)
        {
            var name = Text();
            if (names.Add(name))
            {
                entries.Add(Entry(name));
            }
        }

        var version = string.Create(
            CultureInfo.InvariantCulture,
            $"{_random.Int(0, 20)}.{_random.Int(0, 20)}.{_random.Int(0, 20)}"
        );
        return new Sidecar(SidecarFormat.Version, version, Of(entries.ToArray()));
    }

    private SidecarEntry Entry(string name)
    {
        var engine = lenient
            ? Pick(SidecarValues.Engine.Postgres, SidecarValues.Engine.SqlServer, UnknownEngine)
            : Pick(SidecarValues.Engine.Postgres, SidecarValues.Engine.SqlServer);
        var rows = _random.Bool();
        var hash = _random.String2(64, "0123456789abcdef");
        var database = OrNull(Word());
        var serverVersion = OrNull("16.4");
        var table = rows && _random.Bool() ? new SidecarTable(MaybeWord(), OrNull(Word())) : null;
        var plan = rows ? Provenance(Plans) : null;
        var tableMatch = rows ? Provenance(TableMatches) : null;
        var parameters = Many(0, 3, index => Parameter(index, engine));
        var fewest = lenient ? 0 : 1;
        var columns = rows ? Many(fewest, 4, index => Column(index, engine)) : null;
        return new SidecarEntry(
            name,
            default,
            hash,
            engine,
            database,
            serverVersion,
            rows ? SidecarResultKind.Rows : SidecarResultKind.None,
            table,
            plan,
            tableMatch,
            Of(parameters),
            columns is null ? null : Of(columns)
        );
    }

    private SidecarParameter Parameter(int index, string engine)
    {
        var name = Text();
        var type = _random.Bool(0.9f) ? Type(engine) : null;
        var nullable = Pick<bool?>(true, false, null);
        return new SidecarParameter(name, index, type, nullable, type is null ? null : Provenance(TypeSources));
    }

    private SidecarColumn Column(int index, string engine)
    {
        var name = Text(orEmpty: true);
        var type = Type(engine);
        var nullable = Pick<bool?>(true, false, null);
        var nullableSource = Provenance(NullableSources);
        var origin = _random.Bool() ? new SidecarOrigin(MaybeWord(), OrNull(Word()), OrNull(Word())) : null;
        var identity = Pick<bool?>(true, false, null);
        var computed = Pick<bool?>(true, false, null);
        return new SidecarColumn(index, name, type, nullable, nullableSource, origin, identity, computed);
    }

    private SidecarType Type(string engine) =>
        engine switch
        {
            SidecarValues.Engine.Postgres => PostgresOf(0),
            SidecarValues.Engine.SqlServer => SqlServerOf(),
            _ => new OtherEngineType(Word()),
        };

    private PostgresType PostgresOf(int depth)
    {
        // A type two levels inside another is a leaf.
        var kind = depth >= 2 ? SidecarValues.Kind.Base : Kinds[_types++ % Kinds.Length];
        if (lenient && _random.Bool(0.1f))
        {
            kind = UnknownKind;
        }

        var name = Text();
        var schema = Word();
        var internalName = Word();
        var length = Facet(0, 10_485_760);
        var precision = Facet(0, 1000);
        var scale = Facet(-1000, 1000);
        var element = kind == SidecarValues.Kind.Array ? PostgresOf(depth + 1) : null;
        var baseType = kind == SidecarValues.Kind.Domain ? PostgresOf(depth + 1) : null;
        EquatableArray<string>? labels =
            kind == SidecarValues.Kind.Enum ? Of(Many(0, 3, _ => Text(orEmpty: true))) : null;
        var subtype = kind is SidecarValues.Kind.Range or SidecarValues.Kind.Multirange ? PostgresOf(depth + 1) : null;
        return new PostgresType(
            name,
            kind,
            schema,
            internalName,
            length,
            precision,
            scale,
            element,
            baseType,
            labels,
            subtype
        );
    }

    private SqlServerType SqlServerOf()
    {
        var name = Word();
        var maxLength = _random.Int(-1, 8000);
        var precision = _random.Int(0, 38);
        var scale = _random.Int(0, 38);
        var userType = _random.Bool(0.3f) ? new SqlServerUserType(OrNull(Word()), OrNull(Word()), MaybeWord()) : null;
        return new SqlServerType(name, maxLength, precision, scale, userType);
    }

    private int? Facet(int minimum, int maximum) => _random.Bool(0.3f) ? _random.Int(minimum, maximum) : null;

    private T Pick<T>(params T[] items) => _random.ArrayElement(items);

    // The value, or null where a reader may find null.
    private string? OrNull(string value) => lenient && _random.Bool(0.3f) ? null : value;

    private string? Provenance(string[] values) =>
        lenient && _random.Bool(0.3f) ? Pick(null, "future-value") : Pick(values);

    private T[] Many<T>(int minimum, int maximum, Func<int, T> create)
    {
        var items = new T[_random.Int(minimum, maximum)];
        for (var index = 0; index < items.Length; index++)
        {
            items[index] = create(index);
        }

        return items;
    }

    private string Word() => _random.String2(_random.Int(1, 10), Letters);

    private string? MaybeWord() => _random.Bool() ? Word() : null;

    // A name: mostly a word, sometimes one with a character that must be escaped or that is not ASCII.
    private string Text(bool orEmpty = false)
    {
        if (orEmpty && _random.Bool(0.05f))
        {
            return string.Empty;
        }

        return _random.Bool(0.3f) ? Pick(Awkward) : Word();
    }
}
