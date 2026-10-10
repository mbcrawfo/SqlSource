#!/usr/bin/env bash
# Installs the SqlSource package into a project the way a consumer does, runs the project and checks what it prints.
# Then asks the project what the sqlsource tool asks one: whether it uses SqlSource, and for its project manifest.
# Then removes a .sql file the project uses and checks that the next build, an incremental one, fails for it.
# The project is tools/package-install.  It is copied out of the repository first, so that nothing of the repository's
# own build applies to it, and it restores from the folder that holds the package and from nowhere else.
# Usage: check-package-install.sh [directory]   The directory holds one SqlSource.<version>.nupkg, and may hold the
# package of the tool beside it.  Without it, the package is packed into a temporary directory first.
set -euo pipefail

PACKAGE_ID='SqlSource'
FIXTURE='tools/package-install'
PROJECT='Consumer.csproj'
EXPECTED='expected-output.txt'
# A .sql file of the project and the member that Program.cs uses from it.
REMOVED='Queries/ByProperty.sql'
REMOVED_MEMBER='ByProperty'

if [[ $# -gt 1 ]]; then
    echo 'Usage: check-package-install.sh [directory]' >&2
    exit 2
fi

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

if [[ $# -eq 1 ]]; then
    # Resolved before the working directory changes, so that a relative path means what the caller meant.
    directory="$(cd "$1" && pwd)"
    cd "$repo_root"
else
    cd "$repo_root"
    directory="$work/feed"
    dotnet pack src/SqlSource/SqlSource.csproj --output "$directory" --nologo --verbosity quiet
fi

# A version starts with a digit, and that is what keeps the pattern from matching SqlSource.Tool.<version>.nupkg.
packages=()
for package in "$directory/$PACKAGE_ID".[0-9]*.nupkg; do
    if [[ -f "$package" ]]; then
        packages+=("$package")
    fi
done

if [[ ${#packages[@]} -ne 1 ]]; then
    echo "check-package-install: expected one $PACKAGE_ID.<version>.nupkg in $directory, found ${#packages[@]}" >&2
    exit 1
fi

package="$(basename "${packages[0]}")"
version="${package#"$PACKAGE_ID".}"
version="${version%.nupkg}"

consumer="$work/consumer"
cp -R "$FIXTURE" "$consumer"
# Left behind when the project was opened or built where it is.
rm -rf "${consumer:?}/bin" "${consumer:?}/obj"
# The same SDK as the repository, whatever else is installed.
cp global.json "$consumer/"

# The package folder is the only source, so the package under test is the one that is installed.  The packages folder
# is inside the temporary directory because a version is unpacked into it once: in the folder of the user, a package
# packed again with the same version would not replace the one from the last run.
cat >"$consumer/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <config>
        <add key="globalPackagesFolder" value="$work/packages" />
    </config>
    <packageSources>
        <clear />
        <add key="package-under-test" value="$directory" />
    </packageSources>
    <packageSourceMapping>
        <clear />
    </packageSourceMapping>
    <fallbackPackageFolders>
        <clear />
    </fallbackPackageFolders>
</configuration>
EOF

cd "$consumer"
# pre-commit-validation.sh sets locked mode for the repository's projects.  This project has no lock file.
unset RestoreLockedMode

# "dotnet add package" writes the reference a consumer gets, with the asset lists of a development dependency.
dotnet add "$PROJECT" package "$PACKAGE_ID" --version "$version" >"$work/add.log" 2>&1 || {
    cat "$work/add.log" >&2
    echo "check-package-install: $package could not be added to a project" >&2
    exit 1
}

dotnet build "$PROJECT" --nologo --verbosity quiet >"$work/build.log" 2>&1 || {
    cat "$work/build.log" >&2
    echo "check-package-install: a project that references $package does not build" >&2
    exit 1
}

dotnet run --project "$PROJECT" --no-build >"$work/actual-output.txt" || {
    cat "$work/actual-output.txt" >&2
    echo "check-package-install: a project that references $package does not run" >&2
    exit 1
}

if ! diff "$EXPECTED" "$work/actual-output.txt"; then
    echo "check-package-install: a project that references $package does not print $FIXTURE/$EXPECTED" >&2
    exit 1
fi

# The sqlsource tool asks a project two things, and the installed package answers both.  SqlSourceImported, which
# build/SqlSource.props sets, says that the project uses SqlSource: NuGet imported the props, which nothing else in
# the repository shows.  The target SqlSourceWriteManifest of build/SqlSource.targets writes the project manifest: the
# tool runs it by name, as here, with a file of its own, and nothing of a build runs before it but the package's trims.
imported="$(dotnet msbuild "$PROJECT" -nologo -getProperty:SqlSourceImported)"
if [[ "$imported" != 'true' ]]; then
    echo "check-package-install: a project that references $package does not have SqlSourceImported" >&2
    exit 1
fi

manifest="$work/consumer.manifest"
dotnet msbuild "$PROJECT" -nologo -t:SqlSourceWriteManifest "-p:SqlSourceManifestFile=$manifest" \
    >"$work/manifest.log" 2>&1 || {
    cat "$work/manifest.log" >&2
    echo "check-package-install: the target SqlSourceWriteManifest of $package fails" >&2
    exit 1
}

manifest_fails() {
    cat "$manifest" >&2
    echo "check-package-install: the project manifest that $package writes $1" >&2
    exit 1
}

if [[ "$(head -n 1 "$manifest")" != 'SqlSourceManifest=1' ]]; then
    manifest_fails 'does not start with its version'
fi

# A path is as MSBuild gives it, which on macOS is not the spelling of the temporary directory that mktemp gave.
if ! grep -q '^File=.*/Queries/Users\.sql$' "$manifest"; then
    manifest_fails 'does not list Queries/Users.sql'
fi

# The project writes this dialect over two lines, and Directory.Build.targets the property over four.  The lines
# under a File line are the metadata of that file.
if ! grep -A 10 '^File=.*/Queries/ByOption\.sql$' "$manifest" |
    grep -qx 'File.SqlSourceDialect=mysql, no-backslash-escapes'; then
    manifest_fails 'does not hold the trimmed dialect of Queries/ByOption.sql'
fi

if ! grep -qx 'Property.SqlSourceGeneratorParameters=sort-input no-token-validation' "$manifest"; then
    manifest_fails 'does not hold the trimmed property of Directory.Build.targets'
fi

# Directory.Build.targets adds this file from a hook of the build, which a target that is run by name does not run.
# docs/tech-debt/TD-0025.  The build above compiled it: expected-output.txt has its query.
if grep -q 'AddedByATarget\.sql' "$manifest"; then
    manifest_fails 'lists a file that a target adds from a hook of the build'
fi

# A .sql file that is removed has no timestamp left to compare, so the build after it compiles again only if an input
# of the compiler changed.  The target SqlSourceTrackAdditionalFiles of build/SqlSource.targets writes a hash of the
# list of AdditionalFiles to a file and names that file as an input: the hash, and so the file, changes when a .sql
# file goes.  A build that succeeds here did not compile, and kept the members of the removed file.
rm "$REMOVED"
if dotnet build "$PROJECT" --nologo --verbosity quiet >"$work/rebuild.log" 2>&1; then
    cat "$work/rebuild.log" >&2
    echo "check-package-install: the build after $REMOVED was removed did not compile again" >&2
    exit 1
fi

# No quotes around the member: the compiler writes them as its language does, "ByProperty" in German.
if ! grep -q "error CS0117: .*$REMOVED_MEMBER" "$work/rebuild.log"; then
    cat "$work/rebuild.log" >&2
    echo "check-package-install: the build after $REMOVED was removed failed without reporting $REMOVED_MEMBER" >&2
    exit 1
fi

echo "check-package-install: $package installs into a project, which builds and prints $FIXTURE/$EXPECTED,"
echo "check-package-install: which has SqlSourceImported and writes its project manifest,"
echo "check-package-install: and whose next build compiles again after a .sql file is removed"
