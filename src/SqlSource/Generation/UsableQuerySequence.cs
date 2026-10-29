using SqlSource.Settings;

namespace SqlSource.Generation;

/// <summary>
/// The queries of the files a type can use, as <see cref="UsableQueries.Of" /> gives them.  It is walked with
/// <c>foreach</c> and allocates nothing.
/// </summary>
internal readonly struct UsableQuerySequence
{
    private readonly TypeQueries _type;
    private readonly bool[] _usable;

    internal UsableQuerySequence(TypeQueries type, bool[] usable)
    {
        _type = type;
        _usable = usable;
    }

    public Enumerator GetEnumerator() => new(_type, _usable);

    /// <summary>Walks the usable files and their queries in order.</summary>
    internal struct Enumerator
    {
        private readonly TypeQueries _type;
        private readonly bool[] _usable;
        private int _fileIndex;
        private int _queryIndex;

        internal Enumerator(TypeQueries type, bool[] usable)
        {
            _type = type;
            _usable = usable;
            _fileIndex = -1;
            _queryIndex = 0;
            Current = default;
        }

        public UsableQuery Current { get; private set; }

        public bool MoveNext()
        {
            _queryIndex++;
            while (true)
            {
                if (_fileIndex >= 0 && _queryIndex < _type.Files[_fileIndex].Queries.Count)
                {
                    var query = _type.Files[_fileIndex].Queries[_queryIndex];
                    var settings = QuerySettings.Resolve(
                        query.Markers,
                        _type.Type.Type.Settings,
                        _type.FileSettings[_fileIndex]
                    );
                    Current = new UsableQuery(_fileIndex, _queryIndex, query, settings);
                    return true;
                }

                // The next usable file, from its first query.
                do
                {
                    _fileIndex++;
                } while (_fileIndex < _usable.Length && !_usable[_fileIndex]);
                if (_fileIndex >= _usable.Length)
                {
                    return false;
                }

                _queryIndex = 0;
            }
        }
    }
}
