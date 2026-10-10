using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.Parsing;

namespace SqlSource.Tool.Describing;

/// <summary>
/// The describers of a run, one for a dialect.  The real host's has none until phase 3 registers PostgreSQL's.
/// </summary>
internal sealed class DescriberRegistry
{
    private readonly Dictionary<SqlDialect, IQueryDescriber> _describers;

    public DescriberRegistry(IEnumerable<IQueryDescriber> describers)
    {
        var given = describers.ToList();
        var twice = given.GroupBy(describer => describer.Dialect).FirstOrDefault(group => group.Count() > 1);
        if (twice is not null)
        {
            throw new ArgumentException($"Two describers are given for the dialect {twice.Key}.", nameof(describers));
        }

        _describers = given.ToDictionary(describer => describer.Dialect);
    }

    /// <summary>A registry with no describer.</summary>
    public static DescriberRegistry Empty { get; } = new([]);

    /// <summary>The describer of a dialect, or null when this version has none for it.</summary>
    public IQueryDescriber? Find(SqlDialect dialect) => _describers.GetValueOrDefault(dialect);
}
