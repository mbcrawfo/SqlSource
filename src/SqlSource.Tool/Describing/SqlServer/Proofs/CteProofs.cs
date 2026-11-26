using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlSource.Tool.Describing.SqlServer.Proofs;

/// <summary>
/// The proofs that the columns of a CTE, of one branch or of several, a recursive one among them, cannot be
/// <c>NULL</c>: what they ask the server in <c>operands</c>, and the value of each <see cref="Proof.CteColumn" /> as
/// the answer decides.
/// </summary>
/// <remarks>
/// <para>
/// A row of a CTE is a row of one of its branches, so a column is not null when every branch proves its item at
/// that place.  Each branch is proved as a <c>SELECT</c> of its own, by <see cref="ExpressionProofs" /> over its own
/// <c>FROM</c> and <c>WHERE</c>, with one <c>operands</c> call behind the <c>WITH</c> clause cut after its CTE: the
/// branch sees only the CTEs before its own and its own, so a later CTE of the name of a table it reads must not hide
/// that table.  Its lone leaf is asked, since what <c>columns</c> answered is the query's column, read from the CTE,
/// and not the branch's item.
/// </para>
/// <para>
/// A recursive member reads the CTE's own rows, and a column it reads is a <see cref="Proof.CteColumn" /> of the same
/// CTE.  The columns proved are the largest set of which every branch proves each column, with the CTE's own columns
/// in that set taken as not null.  That holds by induction over the recursion: the anchor members read no row of the
/// CTE, so they prove the set over their rows outright, and a recursive member reads only rows already made, which
/// hold the set not null, so it proves the set over the rows it makes.  The set is found by starting from every column
/// asked and taking away, until none goes, each column that a branch does not prove.  A branch's column of an earlier
/// CTE is decided first, by its own set.
/// </para>
/// <para>
/// Only the columns that a proof of the query reaches are proved, and the columns their branches read in turn.
/// </para>
/// </remarks>
internal sealed class CteProofs
{
    // For each CTE proved, by its place: for each of its columns proved, by its place, the proof of each branch.
    private readonly IReadOnlyDictionary<int, IReadOnlyDictionary<int, IReadOnlyList<Proof>>> _columns;

    private CteProofs(
        IReadOnlyDictionary<int, IReadOnlyDictionary<int, IReadOnlyList<Proof>>> columns,
        IReadOnlyList<ColumnProofs.OperandQuery> operandQueries
    )
    {
        _columns = columns;
        OperandQueries = operandQueries;
    }

    /// <summary>No CTE to prove.</summary>
    public static CteProofs None { get; } =
        new(new Dictionary<int, IReadOnlyDictionary<int, IReadOnlyList<Proof>>>(), []);

    /// <summary>
    /// The <c>operands</c> calls to make, in order, one for each branch that has operands, after the query's own.
    /// </summary>
    public IReadOnlyList<ColumnProofs.OperandQuery> OperandQueries { get; }

    /// <summary>The proofs of the CTE columns that the query's proofs reach.</summary>
    /// <param name="sql">The query's SQL.</param>
    /// <param name="tokens">The query's tokens.</param>
    /// <param name="statement">The query, whose <c>WITH</c> clause holds the CTEs.</param>
    /// <param name="proofs">The proofs of the query's columns.</param>
    /// <param name="asked">
    /// The number of operands the query's own calls ask, which the branches' are numbered after.
    /// </param>
    public static CteProofs Read(
        string sql,
        IReadOnlyList<TsqlToken> tokens,
        SelectStatement statement,
        IEnumerable<Proof> proofs,
        int asked
    )
    {
        var read =
            new SortedDictionary<
                int,
                (IReadOnlyList<SelectBody> Bodies, List<FilteredRows> Filtered, List<ExpressionProofs> Proofs)
            >();
        var parts = new Dictionary<int, Dictionary<int, IReadOnlyList<Proof>>>();
        var work = new Queue<Proof.CteColumn>(proofs.SelectMany(ColumnsIn));
        while (work.TryDequeue(out var column))
        {
            if (!read.TryGetValue(column.Cte, out var cte))
            {
                var ctes = statement.Ctes.Take(column.Cte + 1).ToList();
                var bodies = statement.Ctes[column.Cte].Branches!;
                List<FilteredRows> filtered =
                [
                    .. bodies.Select(body => FilteredRows.Read(tokens, body, ctes, makesJoinsInner: false)),
                ];
                cte = (
                    bodies,
                    filtered,
                    [
                        .. bodies.Select(
                            (body, at) => new ExpressionProofs(tokens, body, filtered[at], asksLoneLeaf: true)
                        ),
                    ]
                );
                read[column.Cte] = cte;
                parts[column.Cte] = [];
            }

            if (parts[column.Cte].ContainsKey(column.Column))
            {
                continue;
            }

            Proof[] ofEach =
            [
                .. cte.Bodies.Select((body, at) => cte.Proofs[at].Of(body.Items[column.Column].Expression)),
            ];
            parts[column.Cte][column.Column] = ofEach;
            foreach (var next in ofEach.SelectMany(ColumnsIn))
            {
                work.Enqueue(next);
            }
        }

        // Each branch numbers its operands from 0, and the answers are one list, so a proof moves to the places of its
        // operands' answers, after the operands of the calls before its own.
        var queries = new List<ColumnProofs.OperandQuery>();
        var columns = new Dictionary<int, IReadOnlyDictionary<int, IReadOnlyList<Proof>>>();
        foreach (var (place, cte) in read)
        {
            var with = new TokenRange(statement.With.Start, statement.Ctes[place].Close + 1);
            var places = new Func<int, int>[cte.Bodies.Count];
            for (var at = 0; at < cte.Bodies.Count; at++)
            {
                var (calls, of) = OperandsQuery.Calls(
                    sql,
                    tokens,
                    with,
                    cte.Bodies[at],
                    cte.Proofs[at],
                    cte.Filtered[at],
                    cuts: null
                );
                var before = asked;
                places[at] = index => before + of[index];
                queries.AddRange(calls);
                asked += calls.Sum(static call => call.Count);
            }

            columns[place] = parts[place]
                .ToDictionary(
                    static pair => pair.Key,
                    IReadOnlyList<Proof> (pair) => [.. pair.Value.Select((part, at) => part.Renumbered(places[at]))]
                );
        }

        return new CteProofs(columns, queries);
    }

    /// <summary>
    /// True when the proof holds, each <see cref="Proof.CteColumn" /> in it as the answers decide it.
    /// </summary>
    /// <param name="proof">A proof of the query's column.</param>
    /// <param name="answers">
    /// The answers of every <c>operands</c> call, the query's and these, as <see cref="Proof.Holds" /> reads them.
    /// </param>
    public bool Holds(Proof proof, IReadOnlyList<OperandFacts>? answers)
    {
        var decided = new Dictionary<int, HashSet<int>>();
        return Resolved(proof, column => NotNull(column.Cte, decided, answers).Contains(column.Column)).Holds(answers);
    }

    // The columns of the CTE in the place that its branches prove, the greatest set that every branch proves with the
    // set's own columns taken as not null.
    private HashSet<int> NotNull(int cte, Dictionary<int, HashSet<int>> decided, IReadOnlyList<OperandFacts>? answers)
    {
        if (decided.TryGetValue(cte, out var known))
        {
            return known;
        }

        if (!_columns.TryGetValue(cte, out var columns))
        {
            return [];
        }

        var held = new HashSet<int>(columns.Keys);
        bool IsHeld(Proof.CteColumn column) =>
            column.Cte == cte
                ? held.Contains(column.Column)
                : column.Cte < cte && NotNull(column.Cte, decided, answers).Contains(column.Column);

        // A proof has no negation, so a column that fails with the set fails with any smaller one: none of the
        // greatest set is ever taken away.
        while (true)
        {
            var failed = held.Where(column => !columns[column].All(part => Resolved(part, IsHeld).Holds(answers)))
                .ToList();
            if (failed.Count == 0)
            {
                break;
            }

            held.ExceptWith(failed);
        }

        decided[cte] = held;
        return held;
    }

    // The proof with each CTE column it holds replaced by Yes or No.
    private static Proof Resolved(Proof proof, Func<Proof.CteColumn, bool> isHeld) =>
        proof switch
        {
            Proof.CteColumn column => isHeld(column) ? Proof.Yes : Proof.No,
            Proof.AnyOf any => new Proof.AnyOf([.. any.Parts.Select(part => Resolved(part, isHeld))]),
            Proof.AllOf all => new Proof.AllOf([.. all.Parts.Select(part => Resolved(part, isHeld))]),
            _ => proof,
        };

    private static IEnumerable<Proof.CteColumn> ColumnsIn(Proof proof) =>
        proof switch
        {
            Proof.CteColumn column => [column],
            Proof.AnyOf any => any.Parts.SelectMany(ColumnsIn),
            Proof.AllOf all => all.Parts.SelectMany(ColumnsIn),
            _ => [],
        };
}
