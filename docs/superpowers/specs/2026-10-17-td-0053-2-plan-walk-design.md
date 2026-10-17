# TD-0053 PR 2, the plan walk - design

Date: 2026-10-17

Part of [the TD-0053 design](2026-10-17-td-0053-postgresql-describer-patterns-design.md); the overview's rules for every PR apply here.  This PR fixes these parts of [TD-0053](../../tech-debt/TD-0053-duplicated-and-uneven-patterns-in-the-postgresql-describer.md):
- items 4 and 5;
- `RunSetOperation`'s seven parameters, from item 7;
- from item 9, the one-argument `PlanInstances.Find` and `WalkScope` without `ColumnTypes`.

It runs at the same time as PR 1, which changes `WithoutAlias` in `Walk/PlanPartitions.cs`.  This PR leaves that method alone; see "Order" in the overview.

Paths are under `src/SqlSource.Tool/Describing/Postgres/` unless they say otherwise.  Line numbers are those of `ae6e66f`.

## Done

- Every node type, relationship, join type and set-operation command the walk compares is spelled once, as a constant.
- `NullExtends` is an enum, and only `PostgresEvents` writes it as text, the same text as today.
- Each tree helper in item 5 has one definition.
- Every existing test passes, except where it names a member that this PR removes, renames or retypes.
- TD-0053 is rewritten without the parts above (overview, rule 3).

## Item 4: node types and relationships

**What is there.**  The item's literals, and the ones the check found it missed:

- `"Append"`: `Walk/PlanInstances.cs:104`, `Walk/PlanPartitions.cs:30`, `Walk/NullabilityWalk.SetOperations.cs:64, 324`.
- `"Merge Append"`: `PlanInstances:105`, `PlanPartitions:30`, `SetOperations:64`.
- `"SetOp"`: `PlanInstances:106`, and a const at `SetOperations:48`.
- `"Recursive Union"`: `PlanInstances:107`, `SetOperations:251`.
- `"Subquery Scan"`: `PlanInstances:278, 302`, `SetOperations:384`.
- `"CTE Scan"`: `PlanInstances:278`.
- `"ModifyTable"`: a const at `PlanInstances:94`, and literals at `NullabilityWalk.cs:533` and `SetOperations:190`.
- The node types of the `RelationScans` set (`PlanInstances:110-121`) and of the `NeverProject` set (`SetOperations:54-62`).
- Relationships:
  - `"Subquery"`: `PlanInstances:304`, `SetOperations:391`.
  - `"InitPlan"`: `PlanInstances:314`, `SetOperations:183`.
  - `"SubPlan"`: `SetOperations:183`.
  - `"Member"`: consts at `PlanPartitions:28` and `SetOperations:47`.
  - `"Outer"` and `"Inner"`: consts at `PlanInstances:90-91` and `SetOperations:45-46`, and literals at `NullabilityWalk.cs:344, 386`.
- Join types: `Left`, `Anti`, `Right`, `Right Anti`, `Full`, `Inner`, `Semi` and `Right Semi` (`PlanInstances:211-215`).
- Set-operation commands: `Intersect`, `Except` and their `All` forms (`SetOperations:277-278`).
- The `"CTE "` prefix of a subplan's name (`PlanInstances:309`).
- `NullExtends` text: `"inner"`, `"outer"`, `"both"` and `"none"` (`PlanInstances:92-93, 212-213, 285`, `NullabilityWalk.cs:323`).
- The `Appends` set is defined twice, identically (`PlanPartitions:30`, `SetOperations:64`).

**The fix.**

- **Constants, not predicates.**  Static classes of `const string`: `PlanNodeTypes`, `PlanRelationships`, `PlanJoinTypes` and one for the set-operation commands.  They go in one file in `Walk/`, or in `Walk/PlanNode.cs`; the builder chooses.  They must be `const`, because the code matches these values in patterns, for example `ParentRelationship is Outer or Inner` (`PlanInstances:194, 283`), `{ ParentRelationship: Outer }` (`PlanInstances:232`, `SetOperations:191, 313`) and the `switch` in `ExtendsOf`.
- **Join type and relationship are different constants.**  The `"Inner"` at `PlanInstances:215` is a join type, so it uses `PlanJoinTypes.Inner`, never `PlanRelationships.Inner`.
- **The sets of node types keep their own purposes.**  They are not copies of one rule:
  - `PlanInstances`' set-operation set (`104-107`) holds four types, `Recursive Union` included.
  - `ReachesSetOperation` (`SetOperations:75`) uses `Appends` and `SetOp`, without `Recursive Union`.
  - Merging the two would make a `Recursive Union` root answer `MayAnswerFromBranches` true, which makes an extra `relations` call and sends the columns down `RunSetOperation`.
  - So each named set (`SetOperations`, `Appends`, `RelationScans`, `NeverProject`) stays a set named for its purpose and is built from the constants.  `Appends` is defined once and used by both files.
- **A `NullExtension` enum** with `None`, `Inner`, `Outer` and `Both` replaces the string in `VisitedNode` (`PlanInstances:30`).
  - `ExtendsOf` returns `NullExtension?`. Null means an unknown join type, as today, and `Visit` stores `extends ?? NullExtension.None`, as it does today with the string (`PlanInstances:244`).
  - The change reaches `ExtendsOf`, `Visit`, `BareAliasOf`, `ExtendsUnder` (`PlanInstances:193`), `NullabilityWalk.cs:323` and the side comparison at `PlanInstances:285-286`.
  - `PostgresEvents.cs:296` writes it as lower-case text, `inner`, `outer`, `both` or `none`, by an explicit mapping.  It does not use `ToString().ToLowerInvariant()`, so renaming a member cannot change the log.
  - The log contract in `docs/superpowers/specs/2026-10-12-*-design.md:150` lists these four words, and it stays true.
  - `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/PlanInstancesTests.cs:338-356` changes to compare enum values.
  - `PostgresEventsPlanTests.cs:96, 105` must pass unchanged, since they pin the logged text.

## Item 5: tree helpers

All the copies the item names behave the same, so unifying them changes no answer.

- **`All(PlanNode)`** is in `PlanInstances:339`, `PlanPartitions:193` and `PlanRelations:30`, and is `node.Plans.SelectMany(All).Prepend(node)` each time.
  - It becomes `PlanNode.SelfAndDescendants()` on the record in `Walk/PlanNode.cs`.
  - The name says that it includes the node itself, which `Of` and `MembersOf` need.
- **The `Subquery` child of a `Subquery Scan`** is found the same way in `PlanInstances:302-306` and `SetOperations:384, 391`.
  - It becomes a property, `PlanNode.SubqueryChild`: the one child whose relationship is `Subquery`, when the node is a `Subquery Scan`; otherwise null.
  - It is computed on each read, not stored. A `with` copy would carry a stored value over, and the tests build nodes with `with { NodeType = … }`.
  - `LeafBranchOf` keeps its own checks of the alias.
- **The Outer and Inner children** are filtered at `PlanInstances:194, 283` and `NullabilityWalk.cs:344, 386`.
  - They become one property, `PlanNode.JoinChildren`.
  - `RowChildrenOf` (`SetOperations:182`, every child but `InitPlan` and `SubPlan`) is a different rule and stays apart.
- **The distinct sort of relation names**, `Distinct()` and then ordinal by `Schema` and by `Name`, is in `PlanRelations:25-27` and `PlanPartitions:42-44`.
  - It becomes one helper, on `PostgresRelationName` (`PostgresCalls.cs:298`) or in `PlanRelations`.
- **The empty partitions dictionary** is made at `PlanInstances:96`, `NullabilityWalk.cs:514` and `SetOperations:98`.
  - `PlanInstances.NoPartitions` becomes `internal` and replaces the other two.
- **`PlanInstances` built twice.**  `MayAnswerFromBranches` builds it (`SetOperations:97-99`, called from `PostgresSession.cs:458`), and `Run` builds it again (`NullabilityWalk.cs:118-119`).
  - `NullabilityWalk.Plans._instances` (`NullabilityWalk.cs:517-528`) is already a cache keyed by node. It is private to `Run`, and is built from the `WalkScope`, whose `Tables` are final only after `RelationsOfAsync`, the step that needs `MayAnswerFromBranches` first.
  - The cache moves out as its own type, built from the tree and the partitions alone, since those are all `InstancesOf` reads.
  - `WalkAsync` builds it after `PartitionsOfAsync` and passes it to `MayAnswerFromBranches`, then to `Run`, through the `WalkScope` or as an argument; the builder chooses.
  - The `Run` calls in `NullabilityWalkTests` change to match.
- **The guard of `NullableAt` and `Preserved`** is the same depth test, Outer/Inner filter and `OpaqueAndNotPreserved` check at `NullabilityWalk.cs:339-348` and `381-390`.
  - They differ only in what they stop with: null in one and false in the other, and both mean "unknown".
  - One helper returns the join children, or null to stop.

**Not in item 5:** `PlanPartitions.MembersOf` and `NullabilityWalk.MembersOf` (`NullabilityWalk.cs:200`) share a name and mean different things.  `NullabilityWalk`'s is renamed for what it returns.

## Item 7, one part: `RunSetOperation`

`RunSetOperation` (`Walk/NullabilityWalk.SetOperations.cs:132`) takes seven parameters.  It is cut to four or fewer by grouping the values that travel together into a record.  The builder chooses which values those are.  `WalkAsync`'s parameters are PR 3's.

## Item 9, two parts

- The one-argument `PlanInstances.Find` (`PlanInstances.cs:174`) is removed.  Its about 40 calls in `PlanInstancesTests.cs` go through a helper local to that test class.
- `WalkScope.ColumnTypes` (`Walk/NullabilityWalk.cs:68`) loses its default of null and becomes required.  Production already passes it (`PostgresSession.cs:325`).
  - The tests that leave it out (`NullabilityWalkTests.cs:26`, the `Scope` helper, and `NullabilityWalkSetOperationTests.cs:685`) pass `[]`.
  - That gives the same answer, since the count check at `SetOperations:112-113` then fails as null does today.
  - `WalkScope.Partitions` keeps its null, which production uses (`PostgresSession.cs:320, 404`).

## Tests

No new behaviour, so no new answers to pin.  New tests are added for:
- `SelfAndDescendants` (the order and that the node itself is included);
- `SubqueryChild` (a `Subquery Scan` with one `Subquery` child, with none, and another node type);
- `JoinChildren`;
- the shared sort;
- the `NullExtension` text mapping, all four values.

## Left to the builder

The names of the new members and types, where the constants live, how the cache reaches `Run`, which values of `RunSetOperation` are grouped, and the commits.
