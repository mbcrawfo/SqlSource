using System.Collections.Generic;

namespace SqlSource.Tool.Testing.Realistic;

// What the server returned for a case's query over the seed: the names of its columns, whether each held a NULL in
// any row, and how many rows there were.
internal sealed record NullabilityRun(IReadOnlyList<string> Names, IReadOnlyList<bool> SawNull, int Rows);
