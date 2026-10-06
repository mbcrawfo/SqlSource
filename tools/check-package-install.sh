#!/usr/bin/env bash
# Installs the SqlSource package into a project the way a consumer does, runs the project and checks what it prints.
# Then removes a .sql file the project uses and checks that the next build, an incremental one, fails for it.
# The project is tools/package-install.  It is copied out of the repository first, so that nothing of the repository's
# own build applies to it, and it restores from the folder that holds the package and from nowhere else.
# Usage: check-package-install.sh [directory]   The directory holds one SqlSource.*.nupkg.  Without it, the package is
# packed into a temporary directory first.
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

packages=()
for package in "$directory/$PACKAGE_ID".*.nupkg; do
    if [[ -f "$package" ]]; then
        packages+=("$package")
    fi
done

if [[ ${#packages[@]} -ne 1 ]]; then
    echo "check-package-install: expected one $PACKAGE_ID.*.nupkg in $directory, found ${#packages[@]}" >&2
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

# A .sql file that is removed has no timestamp left to compare, so the build after it compiles again only if something
# else changed.  That is the file the SDK writes for the compiler, which holds a section for each .sql file because
# build/SqlSource.props declares metadata for them.  A build that succeeds here kept the members of the removed file.
rm "$REMOVED"
if dotnet build "$PROJECT" --nologo --verbosity quiet >"$work/rebuild.log" 2>&1; then
    cat "$work/rebuild.log" >&2
    echo "check-package-install: the build after $REMOVED was removed did not compile again" >&2
    exit 1
fi

if ! grep -q "error CS0117: .*'$REMOVED_MEMBER'" "$work/rebuild.log"; then
    cat "$work/rebuild.log" >&2
    echo "check-package-install: the build after $REMOVED was removed failed without reporting $REMOVED_MEMBER" >&2
    exit 1
fi

echo "check-package-install: $package installs into a project, which builds and prints $FIXTURE/$EXPECTED,"
echo "check-package-install: and whose next build compiles again after a .sql file is removed"
