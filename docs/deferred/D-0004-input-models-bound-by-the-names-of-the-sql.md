# D-0004 - Input models bound by the names of the SQL

## Planned

[Sub-phase 5.3](../superpowers/specs/2026-10-18-query-generation-phase-5-3-models-design.md) of the query generation epic names an input model's properties by PascalCasing the query's parameters: `@account_id` gives `AccountId`.  The [epic outline](../superpowers/specs/2026-10-07-query-generation-epic-design.md) says that a project can run its models through Dapper.  Dapper sends a property of a parameter object only when the SQL names it as `@` and the property's name, ignoring case but not `_`, so an input model whose query writes snake_case parameters binds nothing through Dapper.  The review of 5.3's spec proposed a method on every input model, `ToParameters()`, that gives the parameters keyed by the names the SQL uses, which Dapper accepts and which needs no dependency on Dapper.

## Delivered instead

The input model alone, named by PascalCase.  A Dapper user names the query's parameters in camelCase, `@accountId`, which Dapper binds to `AccountId`, or builds the parameters themselves.  The manual's Dapper recipe says so.  Phase 8's generated methods bind every parameter themselves and are not affected.

## Why deferred

The owner expects most users to name their parameters to suit the library, and the method costs a call and an allocation on every query a user runs through Dapper.

## Remaining work

A way for a Dapper user to pass an input model whose query writes snake_case parameters, without naming the parameters again: the `ToParameters()` method above, or another form that Dapper accepts, emitted for every input model or under a setting, with tests that run such a query through Dapper in the PostgreSQL end-to-end project.

## Trigger

A strong demand from users to run queries with snake_case parameters through Dapper.
