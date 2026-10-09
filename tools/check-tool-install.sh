#!/usr/bin/env bash
# Installs the packed sqlsource tool the way a user does, into an empty folder outside the repository, and runs it.
# A tool carries everything it loads, so only a run of the packed tool shows that all of it is there and loads, and
# that a tool built for .NET 8 starts on a machine whose only runtime is a later one.
# It restores from the folder that holds the package and from nowhere else.
# Usage: check-tool-install.sh [directory]   The directory holds one SqlSource.Tool.<version>.nupkg.  Without it, the
# package is packed into a temporary directory first.
set -euo pipefail

PACKAGE_ID='SqlSource.Tool'
COMMAND='sqlsource'

if [[ $# -gt 1 ]]; then
    echo 'Usage: check-tool-install.sh [directory]' >&2
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
    dotnet pack src/SqlSource.Tool/SqlSource.Tool.csproj --output "$directory" --nologo --verbosity quiet
fi

packages=()
for package in "$directory/$PACKAGE_ID".[0-9]*.nupkg; do
    if [[ -f "$package" ]]; then
        packages+=("$package")
    fi
done

if [[ ${#packages[@]} -ne 1 ]]; then
    echo "check-tool-install: expected one $PACKAGE_ID.<version>.nupkg in $directory, found ${#packages[@]}" >&2
    exit 1
fi

package="$(basename "${packages[0]}")"
version="${package#"$PACKAGE_ID".}"
version="${version%.nupkg}"

install="$work/install"
mkdir "$install"
# The same SDK as the repository, whatever else is installed.
cp global.json "$install/"

# The package folder is the only source, so the package under test is the one that is installed.  The packages folder
# is inside the temporary directory because a version is unpacked into it once: in the folder of the user, a package
# packed again with the same version would not replace the one from the last run.
cat >"$install/nuget.config" <<CONFIG
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
CONFIG

cd "$install"
# pre-commit-validation.sh sets locked mode for the repository's projects.  The install has no lock file.
unset RestoreLockedMode

dotnet tool install "$PACKAGE_ID" --version "$version" --tool-path "$work/tool" --configfile nuget.config \
    >"$work/install.log" 2>&1 || {
    cat "$work/install.log" >&2
    echo "check-tool-install: $package could not be installed" >&2
    exit 1
}

tool="$work/tool/$COMMAND"

# Runs the tool and fails unless it prints the package's version, alone or followed by "+" and the commit.
check_version() {
    local what="$1"
    local actual
    actual="$("$tool" --version)" || {
        echo "check-tool-install: $COMMAND --version failed $what" >&2
        exit 1
    }
    if [[ "$actual" != "$version" && "$actual" != "$version+"* ]]; then
        echo "check-tool-install: $COMMAND --version printed '$actual' $what, and the package is $version" >&2
        exit 1
    fi
}

check_version 'on the runtime it chose'

"$tool" --help >"$work/help.txt" || {
    cat "$work/help.txt" >&2
    echo "check-tool-install: $COMMAND --help failed" >&2
    exit 1
}
if ! grep --quiet --fixed-strings 'describe' "$work/help.txt"; then
    cat "$work/help.txt" >&2
    echo "check-tool-install: $COMMAND --help does not list describe" >&2
    exit 1
fi

# A folder with no project in it: the tool must find nothing, say so in the compiler's format, and exit with 1.
mkdir "$work/empty"
cd "$work/empty"
code=0
"$tool" describe >"$work/describe.out" 2>"$work/describe.err" || code=$?
if [[ "$code" -ne 1 ]] || [[ -s "$work/describe.out" ]] ||
    ! grep --quiet '^sqlsource : error SQLSRC201: ' "$work/describe.err"; then
    cat "$work/describe.out" "$work/describe.err" >&2
    echo "check-tool-install: $COMMAND describe in an empty folder exited with $code, not with 1 and SQLSRC201" >&2
    exit 1
fi

# A machine that has a .NET 8 runtime ran the tool on it above.  This run takes the newest runtime the machine has,
# which is what a machine without .NET 8 does by the RollForward of the project.
export DOTNET_ROLL_FORWARD=LatestMajor
check_version 'on the newest runtime'

echo "check-tool-install: $package installs as a tool, and $COMMAND runs: --version, --help, and describe, which"
echo "check-tool-install: reports SQLSRC201 in an empty folder.  It runs on the newest runtime as well."
