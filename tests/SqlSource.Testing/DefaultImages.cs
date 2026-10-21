namespace SqlSource.Testing;

// The default image of each engine's tests: one constant for each, so that the tests and the scripts read an image from
// one place.
internal static class DefaultImages
{
    // tools/record-fixtures.sh and tools/describe-end-to-end.sh read this line by its shape, with sed, and
    // AppContainer of tests/SqlSource.Tests.Postgres.Npgsql8 builds on it.
    // examples/PostgresExamples/docker-compose.yml repeats the tag, which rule 5 of AGENTS.md keeps in step.  It names
    // a major and a minor, as every image of the tests does: the recorded fixtures carry the version they were recorded
    // against, and a moving tag would run another.
    // See tests/SqlSource.Tests.Postgres/README.md.
    public const string Postgres = "postgres:18.6";
}
