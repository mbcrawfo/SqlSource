using System;
using System.Collections.Generic;
using SqlSource.Parsing;

namespace SqlSource.Tool.Bundles.Renaming;

/// <summary>
/// One renaming of one bundle: its mapping, its SQL renamer over the query's parameters, and the first refusal, after
/// whose step the renaming stops.  A string renamed at a place is given a scope of that place, which an engine's part
/// is given too.
/// </summary>
/// <remarks>
/// A name of a member is found by its spelling as the engine compares it.  A catalog holds a name as the engine keeps
/// it, which is its quoted spelling; a query's, a parameter's or a logical database's name is the query file's own,
/// which reads it unquoted, so that one spelling of it has one name in the file and in the capture.  A token's name is
/// renamed as quoted, since the generator compares it as written: two of its spellings that differ by case then have
/// replacements that differ, by ordinal at least, whatever the engine.
/// </remarks>
internal sealed class RenamingRun
{
    private readonly IEngineRenaming _engine;
    private readonly SqlRenamer _sql;
    private readonly IReadOnlyList<string> _parameters;

    private RenamingRun(
        IEngineRenaming engine,
        NameMapping mapping,
        IReadOnlyList<string> parameters,
        RenamingCopies copies
    )
    {
        _engine = engine;
        _parameters = parameters;
        Mapping = mapping;
        _sql = new SqlRenamer(engine, mapping, new HashSet<string>(parameters, SqlParameterName.Comparer), copies);
    }

    /// <summary>The bundle's one mapping.</summary>
    public NameMapping Mapping { get; }

    /// <summary>The first refusal, or null while there is none.</summary>
    public RenamingRefusal? Refusal { get; private set; }

    /// <summary>
    /// A run over the parameters of a query file, the names of its parameter lexemes in their order, and the copies of
    /// them that the raw capture records.  A file that does not lex has no parameter here, and its renaming refuses it.
    /// </summary>
    public static RenamingRun For(IEngineRenaming engine, NameMapping mapping, string query, RenamingCopies copies)
    {
        var lexed = SqlLexer.Lex(query, SqlDialectRules.For(engine.Dialect));
        List<string> parameters = [];
        if (lexed.Error is null)
        {
            foreach (var lexeme in lexed.Lexemes)
            {
                if (lexeme.Kind == SqlLexemeKind.Parameter)
                {
                    var name = lexeme.ParameterName;
                    parameters.Add(query.Substring(name.Start, name.Length));
                }
            }
        }

        return new RenamingRun(engine, mapping, parameters, copies);
    }

    /// <summary>
    /// Whether a name of the class is compared as its quoted spelling: a catalog's name, and a token's, which the
    /// generator compares as written.  Another of the query file's own is compared as it reads there, unquoted.
    /// </summary>
    public static bool IsQuoted(NameClass cls) =>
        cls switch
        {
            NameClass.Schema
            or NameClass.Table
            or NameClass.Column
            or NameClass.Alias
            or NameClass.Function
            or NameClass.Type
            or NameClass.Index
            or NameClass.Token => true,
            NameClass.Parameter or NameClass.Query or NameClass.Database or NameClass.Literal => false,
            _ => throw UnexpectedEnumValueException.Of(cls),
        };

    /// <summary>Records a refusal, unless one is recorded already.</summary>
    public void Refuse(RenamingRefusal refusal) => Refusal ??= refusal;

    /// <summary>The scope of a place, as a refusal names it.</summary>
    public IRenamingScope At(string place) => new Scope(this, place);

    /// <summary>Registers the query's parameters as names of parameters, after every name of a catalog.</summary>
    public void RegisterParameters()
    {
        foreach (var parameter in _parameters)
        {
            _ = _sql.ParameterName(parameter);
        }
    }

    /// <summary>
    /// Registers the name a member holds: kept when the row that holds it is the engine's, else given its replacement.
    /// </summary>
    public void Register(string value, NameClass cls, bool engineRow)
    {
        if (value == SqlRenamer.UnnamedColumn)
        {
            return;
        }

        var (stem, _) = MarkOf(value);
        if (engineRow)
        {
            Mapping.KeepFromEngineRow(stem, IsQuoted(cls));
        }
        else
        {
            _ = _sql.NameOf(stem, IsQuoted(cls), cls);
        }
    }

    /// <summary>The query file renamed; a refusal is at <c>query.sql</c>.</summary>
    public string RenameQueryFile(string text) => _sql.RenameQueryFile(text, Refuse).Text;

    /// <summary>
    /// A logical database, as the query file's <c>-- database:</c> is renamed: kept when it is the dialect's name,
    /// which is the tool's word, else the mapping's database.
    /// </summary>
    public string Database(string value) =>
        value == SqlDialectName.Canonical(_engine.Dialect)
            ? value
            : Mapping.Name(value, quoted: false, NameClass.Database);

    /// <summary>A string renamed by its member's rule.</summary>
    /// <param name="rule">The member's rule, which is not <see cref="FieldRuleKind.Drop" />.</param>
    /// <param name="field">The member.</param>
    /// <param name="engineRow">Whether the row that holds it is the engine's, whose names are kept.</param>
    /// <param name="place">Where it is, as a refusal names it.</param>
    public string Field(FieldRule rule, RenamingField field, bool engineRow, string place)
    {
        var value = field.Value;
        return rule.Kind switch
        {
            FieldRuleKind.Drop => throw new InvalidOperationException(
                $"The member at {place} is dropped by the first step, and has nothing to rename."
            ),
            FieldRuleKind.Fixed or FieldRuleKind.Closed => value,
            FieldRuleKind.Sql or FieldRuleKind.TypeSpelling => At(place).Sql(value, ClassOf(rule)).Text,
            FieldRuleKind.Label => Mapping.Literal(value),
            FieldRuleKind.Parameter => Parameter(value),
            FieldRuleKind.Name => Name(value, ClassOf(rule), engineRow),
            FieldRuleKind.WholeName => _sql.NameOf(value, quoted: true, ClassOf(rule)),
            FieldRuleKind.Own => _engine.RenameOwn(field, At(place)),
            _ => throw UnexpectedEnumValueException.Of(rule.Kind),
        };
    }

    /// <summary>A name's stem and its nullability mark, <c>!</c> or <c>?</c>, or the name whole and no mark.</summary>
    public static (string Stem, string Mark) MarkOf(string name) =>
        name.Length > 1 && name[^1] is '!' or '?' ? (name[..^1], name[^1..]) : (name, string.Empty);

    /// <summary>
    /// The class of the names a rule makes: a rule of a name, of SQL or of a type's spelling has one.
    /// </summary>
    public static NameClass ClassOf(FieldRule rule) =>
        rule.Class ?? throw new InvalidOperationException($"A rule of the kind {rule.Kind} has no class.");

    // A parameter's name, after the dialect's prefix when it has one.
    private string Parameter(string value)
    {
        var prefix = SqlDialectRules.For(_engine.Dialect).ParameterPrefix;
        return value.Length > 1 && value[0] == prefix
            ? prefix + _sql.ParameterName(value[1..])
            : _sql.ParameterName(value);
    }

    // A name keeps its nullability mark after its renamed stem, and PostgreSQL's ?column? is kept whole, as in the SQL.
    // A name of the engine's row is kept unless the mapping holds it.
    private string Name(string value, NameClass cls, bool engineRow)
    {
        if (value == SqlRenamer.UnnamedColumn)
        {
            return value;
        }

        var (stem, mark) = MarkOf(value);
        var quoted = IsQuoted(cls);
        if (engineRow && !Mapping.IsMapped(stem, quoted))
        {
            return value;
        }

        return _sql.NameOf(stem, quoted, cls) + mark;
    }

    // What an engine's part is given at a place.  Every name and literal comes from the run's mapping.
    private sealed class Scope(RenamingRun run, string place) : IRenamingScope
    {
        public string Name(string spelling, bool quoted, NameClass cls) => run._sql.NameOf(spelling, quoted, cls);

        public void Keep(string spelling, bool quoted) => run.Mapping.Keep(spelling, quoted);

        public bool IsMapped(string spelling, bool quoted) => run.Mapping.IsMapped(spelling, quoted);

        public string Literal(string content) => run.Mapping.Literal(content);

        public RenamedText Sql(string sql, NameClass newWords) => run._sql.Rename(sql, newWords, place, run.Refuse);

        public string Parameter(string name) => run._sql.ParameterName(name);

        public string ParameterOf(string name) => run._sql.ParameterOf(name);

        public void Refuse(RenamingRefusal refusal) => run.Refuse(refusal);
    }
}
