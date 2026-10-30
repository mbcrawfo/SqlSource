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
        private readonly SettingsLevel _attribute;
        private int _fileIndex;
        private int _queryIndex;

        // The queries and the level of the file being walked, looked up once for the file.
        private EquatableArray<SqlQuery> _queries;
        private SettingsLevel _fileLevel;

        internal Enumerator(TypeQueries type, bool[] usable)
        {
            _type = type;
            _usable = usable;
            _attribute = type.Type.Type.Settings;
            _fileIndex = -1;
            _queryIndex = 0;
            _queries = EquatableArray<SqlQuery>.Empty;
            _fileLevel = SettingsLevel.None;
            Current = default;
        }

        public UsableQuery Current { get; private set; }

        public bool MoveNext()
        {
            _queryIndex++;
            while (_queryIndex >= _queries.Count)
            {
                // The next usable file, from its first query.
                do
                {
                    _fileIndex++;
                } while (_fileIndex < _usable.Length && !_usable[_fileIndex]);
                if (_fileIndex >= _usable.Length)
                {
                    return false;
                }

                _queries = _type.Files[_fileIndex].Queries;
                _fileLevel = _type.FileSettings[_fileIndex];
                _queryIndex = 0;
            }

            var query = _queries[_queryIndex];
            Current = new UsableQuery(
                _fileIndex,
                _queryIndex,
                query,
                QuerySettings.Resolve(query.Markers, _attribute, _fileLevel)
            );
            return true;
        }
    }
}
