using SqlSource.Generation.Placement;

namespace SqlSource.Generation;

/// <summary>
/// The queries of the files a type can use, as <see cref="UsableQueries.Of" /> gives them, with the type's placement.
/// It is walked with <c>foreach</c> and allocates nothing.
/// </summary>
internal readonly struct UsableQuerySequence
{
    private readonly TypeQueries _type;

    internal UsableQuerySequence(TypeQueries type, TypePlacement placement)
    {
        _type = type;
        Placement = placement;
    }

    /// <summary>Which files the type can use, its nested classes, and each query's settings and placement.</summary>
    public TypePlacement Placement { get; }

    public Enumerator GetEnumerator() => new(_type, Placement);

    /// <summary>Walks the usable files and their queries in order.</summary>
    internal struct Enumerator
    {
        private readonly TypeQueries _type;
        private readonly TypePlacement _placement;
        private int _fileIndex;
        private int _queryIndex;

        // The queries of the file being walked, looked up once for the file.
        private EquatableArray<SqlQuery> _queries;

        internal Enumerator(TypeQueries type, TypePlacement placement)
        {
            _type = type;
            _placement = placement;
            _fileIndex = -1;
            _queryIndex = 0;
            _queries = EquatableArray<SqlQuery>.Empty;
            Current = default;
        }

        public UsableQuery Current { get; private set; }

        public bool MoveNext()
        {
            var usable = _placement.Usable;
            _queryIndex++;
            while (_queryIndex >= _queries.Count)
            {
                // The next usable file, from its first query.
                do
                {
                    _fileIndex++;
                } while (_fileIndex < usable.Length && !usable[_fileIndex]);
                if (_fileIndex >= usable.Length)
                {
                    return false;
                }

                _queries = _type.Files[_fileIndex].Queries;
                _queryIndex = 0;
            }

            var settings = _placement.SettingsOf(_fileIndex, _queryIndex);
            Current = new UsableQuery(
                _fileIndex,
                _queryIndex,
                _queries[_queryIndex],
                settings,
                _placement.PlacementOf(settings)
            );
            return true;
        }
    }
}
