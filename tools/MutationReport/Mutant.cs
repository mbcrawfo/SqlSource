namespace SqlSource.MutationReport;

// What a mutation run found of a mutant, under the names Stryker's report gives.
internal enum MutantStatus
{
    Killed,
    Timeout,
    Survived,
    NoCoverage,
    Ignored,
    CompileError,
    RuntimeError,
    Pending,
}

// What tells one mutant from another in two reports of the same source: Stryker's ids are numbered again in every run.
// File is the path from the report's project root, with '/', so that reports made in two checkouts compare.  Ordinal
// numbers the mutants whose other fields are equal, from 0, in the order the report gives them.
internal sealed record MutantKey(
    string File,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn,
    string Mutator,
    string Replacement,
    int Ordinal
);

internal sealed record Mutant(MutantKey Key, MutantStatus Status);
