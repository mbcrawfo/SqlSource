#!/usr/bin/env bash
# Installs the packed sqlsource tool the way a user does, into an empty folder outside the repository, and runs it.
# A tool carries everything it loads, so only a run of the packed tool shows that all of it is there and loads, and
# that a tool built for .NET 8 starts on a machine whose only runtime is a later one.
# A describer loads its driver only when it connects, so one run connects through Microsoft.Data.SqlClient to a port
# that refuses, which loads SqlClient's copy for the platform under runtimes/.  Its native SNI, which only Windows
# loads, is shown to be in the package by tools/check-package.sh and not shown to load.
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

# A project of one mssql query, and a connection to the port 1 of this machine, which refuses at once.  The tool loads
# SqlClient to connect, and its copy for the platform must be the one it loads: the copy outside runtimes/ throws
# PlatformNotSupportedException at the first use, which the tool reports as SQLSRC224 with another text.  The project
# imports SqlSource.props and .targets, copied from src/SqlSource/build/ with SqlSource.AfterSdk.targets, which the
# props have the SDK import, and the repository's global.json beside them, and is never restored.
mssql="$work/mssql"
mkdir -p "$mssql/build" "$mssql/App"
cp "$repo_root/global.json" "$mssql/"
cp "$repo_root/src/SqlSource/build/SqlSource.props" "$repo_root/src/SqlSource/build/SqlSource.targets" \
    "$repo_root/src/SqlSource/build/SqlSource.AfterSdk.targets" "$mssql/build/"
cat >"$mssql/App/App.csproj" <<'PROJECT'
<Project Sdk="Microsoft.NET.Sdk">
    <Import Project="../build/SqlSource.props" />
    <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
        <SqlSourceDialect>mssql</SqlSourceDialect>
        <SqlSourceDatabase>orders</SqlSourceDatabase>
    </PropertyGroup>
    <Import Project="../build/SqlSource.targets" />
</Project>
PROJECT
printf -- '-- name: GetOrders\nSELECT 1 AS one;\n' >"$mssql/App/Orders.sql"
printf '[SqlSource.SqlSourceGenerate]\ninternal static partial class Queries;\n' >"$mssql/App/Queries.cs"
cd "$mssql"
code=0
"$tool" describe App/App.csproj --connection 'orders=Data Source=127.0.0.1,1;Connect Timeout=5;Encrypt=False' \
    >"$work/mssql.out" 2>"$work/mssql.err" || code=$?
summary='orders (mssql): 0 described, 0 skipped, 1 failed'
if [[ "$code" -ne 1 ]] || ! grep --quiet --fixed-strings "$summary" "$work/mssql.out" ||
    ! grep --quiet ': error SQLSRC224: ' "$work/mssql.err" ||
    ! grep --quiet --fixed-strings 'SqlClient could not reach the server' "$work/mssql.err" ||
    grep --quiet 'SQLSRC200' "$work/mssql.err"; then
    cat "$work/mssql.out" "$work/mssql.err" >&2
    echo "check-tool-install: $COMMAND describe of an mssql query exited with $code, and not with 1 and SQLSRC224" >&2
    echo "check-tool-install: that says SqlClient could not reach the server: SqlClient's copy did not load" >&2
    exit 1
fi

# A machine that has a .NET 8 runtime ran the tool on it above.  This run takes the newest runtime the machine has,
# which is what a machine without .NET 8 does by the RollForward of the project.
export DOTNET_ROLL_FORWARD=LatestMajor
check_version 'on the newest runtime'

echo "check-tool-install: $package installs as a tool, and $COMMAND runs: --version, --help, and describe, which"
echo "check-tool-install: reports SQLSRC201 in an empty folder, and SQLSRC224 through SqlClient for an mssql query"
echo "check-tool-install: whose server refuses.  It runs on the newest runtime as well."
