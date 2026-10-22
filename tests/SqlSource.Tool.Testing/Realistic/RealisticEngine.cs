using System.Collections.Generic;

namespace SqlSource.Tool.Testing.Realistic;

// The engine of a realistic corpus: its name in a sidecar and in a fixture, as SidecarValues.Engine spells it, the name
// of its product in a message, and the majors that CI runs and that a case's committed entry is pinned for, which a
// checkedFrom names one of.
internal sealed record RealisticEngine(string Engine, string Product, IReadOnlyList<int> RecordedMajors);
