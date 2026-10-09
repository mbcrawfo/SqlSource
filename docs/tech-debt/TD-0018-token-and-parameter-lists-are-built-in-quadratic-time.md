# TD-0018 - The token list and the parameter list of a query are built in quadratic time

## Problem

[`SqlTokenList.Create`](../../src/SqlSource/Parsing/SqlTokenList.cs) counts a query's distinct tokens by comparing each occurrence with every one before it (`AppearedBefore`), and finds a token by name with a linear search of the tokens built so far (`IndexOf`, and `FirstWithDefault` for the occurrences).  Both are quadratic in the number of occurrences of tokens in one query.  [`SqlParameterList`](../../src/SqlSource/Parsing/SqlParameterList.cs) looks a parameter up by name in the same way, with a linear search of the parameters found so far, so it is quadratic in the number of parameters of one query.

A probe with 6,000 distinct tokens in one query took about 0.45 s to parse.  A query with a few tokens, which is every query that people write, is not measurably slower.

## Why it exists

The lists are searched in place so that the parse allocates nothing for a name: a dictionary or a set would allocate for every query that has a token or a parameter, and [`SqlFileParserAllocationTests`](../../tests/SqlSource.Tests/Parsing/SqlFileParserAllocationTests.cs) holds the parse to a budget of bytes for each character.  A query has a handful of tokens and parameters, where a linear search is the fastest and the smallest.

## Impact

None for a query that a person wrote.  A generated query with thousands of tokens or parameters would make the parse of its file take a fraction of a second, on every edit of that file, in the compiler's process.

## Proposed fix

Switch to a set or a dictionary of names above a size, for example 16 occurrences, and keep the search below it, so that an ordinary query still allocates nothing for the lookup.  The name can be looked up from a span of the SQL with an alternate lookup on a newer runtime only; on `netstandard2.0` a lookup by a span needs a hash of the span of the code's own.  Measure the allocation of a parse with `SqlFileParserAllocationTests` and the time of a query with thousands of tokens before and after.

## Trigger

A report of a slow compiler or IDE with a very large generated `.sql` file.  A change that makes the number of tokens or parameters of a query grow, such as generated queries.
