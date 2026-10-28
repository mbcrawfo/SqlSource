# TD-0084 - The sidecar's contract is documented only in a spec

## Problem

The sidecar is a contract between `sqlsource describe` and the generator, two packages versioned apart, and with every committed `<file>.sql.json`.  [`schemas/sidecar-v1.schema.json`](../../schemas/sidecar-v1.schema.json) defines its shape, and nothing current defines the rest of it, which is written down only in [the sidecar format design](../superpowers/specs/2026-10-07-sidecar-format-design.md), a spec that is never kept current:

- the reader rules (its section 1.5): what a missing key means, what makes a file malformed, and why the reader accepts more than the schema;
- the compatibility rules (its section 3): which changes keep `formatVersion` and which raise it;
- what values mean beyond their type, such as the input of `hash`, `toolVersion` and `serverVersion`, and why SQL Server writes no `bindName`.

Pages of `docs/architecture/` link those sections for them: `README.md`, `generator.md`, `postgres-describer.md`, `postgres-nullability.md` and `sqlserver-describer.md`.

## Why it exists

The format was designed in that spec, which stayed its only full description.

## Impact

Medium.  A change to the format has no current document to follow or update, and the rule that decides whether it needs a new `formatVersion` is in a file nobody keeps true.

## Proposed fix

Write the reader rules, the compatibility rules and the meaning of each value where they are kept current, in a page of `docs/architecture/` or in the schema's descriptions, and point the pages above and the AGENTS.md rules of `src/SqlSource/Snapshot/` and `schemas/` at it.

## Trigger

The next change to the sidecar format or to its reader.
