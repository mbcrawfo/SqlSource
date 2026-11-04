#!/usr/bin/env bash
# Installs the SqlSource package into a project the way a consumer does, runs the project and checks what it prints.
# Then asks the project what the sqlsource tool asks one: whether it uses SqlSource, and for its project manifest.
# Then removes a .sql file the project uses and checks that the next build, an incremental one, fails for it.
# Then publishes a web project that references the package, and checks that no .sql file, no sidecar and no settings
# file is copied.
# Then builds a project of two target frameworks that references the package, with a stand-in for the sqlsource tool,
# and checks that the outer build ran the tool once, for the first framework, before it dispatched the inner builds.
# NuGet imports buildMultiTargeting/SqlSource.targets only for a package reference, so no other test reaches it.
# Last, installs the packed tool of the same version into a project of one query, with a tool manifest, and builds it
# under each value of SqlSourceOnline, with no database: the packed package starts the packed tool, and the build
# shows what the tool printed, its summary, its offline line and its errors.
# The project is tools/package-install.  It is copied out of the repository first, so that nothing of the repository's
# own build applies to it, and it restores from the folder that holds the packages and from nowhere else.
# Usage: check-package-install.sh [directory]   The directory holds one SqlSource.<version>.nupkg and one
# SqlSource.Tool.<version>.nupkg of the same version.  Without it, both are packed into a temporary directory first.
set -euo pipefail

# MSBuild writes its own lines in the language of the user's interface, "0 Warnung(en)" in German, and the checks
# below read some of them, so every run of dotnet is in English, whatever the machine's language.
export DOTNET_CLI_UI_LANGUAGE=en

PACKAGE_ID='SqlSource'
TOOL_PACKAGE_ID='SqlSource.Tool'
FIXTURE='tools/package-install'
PROJECT='Consumer.csproj'
EXPECTED='expected-output.txt'
# A .sql file of the project and the member that Program.cs uses from it.
REMOVED='Queries/ByProject.sql'
REMOVED_MEMBER='ByProject'
# The query that the last project copies, with its sidecar beside it: GetUser, of the database postgres.
ONLINE_QUERY='examples/PostgresExamples/Models/Users.sql'

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
    for project in src/SqlSource/SqlSource.csproj src/SqlSource.Tool/SqlSource.Tool.csproj; do
        dotnet pack "$project" --output "$directory" --nologo --verbosity quiet
    done
fi

# A version starts with a digit, and that is what keeps the pattern of SqlSource from matching
# SqlSource.Tool.<version>.nupkg.
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

# The tool of the same version, which the last project installs from the same folder.
tool_package="$TOOL_PACKAGE_ID.$version.nupkg"
if [[ ! -f "$directory/$tool_package" ]]; then
    echo "check-package-install: expected $tool_package beside $package in $directory, and it is missing" >&2
    exit 1
fi

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

# The online build is off unless a connection is configured, so a project with no connection and no SqlSourceOnline
# builds as it did before the tool ran in the build: no warning, and the tool is never started.  A stand-in is named as
# the tool so that a run would show in its log, and a build that is not incremental runs every target of the package.
# This build and the ones below need none of the variables that turn the online build off, and CI sets the first, or
# on, and no developer's connection.
unset CI TF_BUILD TEAMCITY_VERSION JENKINS_URL GITHUB_ACTIONS SQLSOURCE_LOG
while IFS= read -r variable; do
    unset "$variable"
done < <(compgen -v SQLSOURCE_CONNECTION)

stand_in="$work/stand-in.sh"
stand_in_log="$work/stand-in.log"
# The tool prints a line of its summary when it runs, and silence reads as a tool that could not start.  The arguments
# of each call are logged, one call to a line.
cat >"$stand_in" <<EOF
#!/bin/sh
echo "\$*" >>"$stand_in_log"
echo 'sqlsource: summary: orders (postgres): 0 described, 1 skipped, 0 failed'
EOF
chmod +x "$stand_in"

dotnet build "$PROJECT" --nologo --no-incremental "-p:SqlSourceToolCommand=$stand_in" >"$work/offline.log" 2>&1 || {
    cat "$work/offline.log" >&2
    echo "check-package-install: a project with no connection that references $package does not build" >&2
    exit 1
}

if [[ -e "$stand_in_log" ]] || ! grep -q '^ *0 Warning(s)$' "$work/offline.log"; then
    cat "$work/offline.log" >&2
    echo "check-package-install: a project with no connection that references $package ran the tool or warned" >&2
    exit 1
fi

# The sqlsource tool asks a project two things, and the installed package answers both.  SqlSourceImported, which
# build/SqlSource.props sets, says that the project uses SqlSource: NuGet imported the props, which nothing else in
# the repository shows.  The target SqlSourceWriteManifest of build/SqlSource.targets writes the project manifest: the
# tool runs it by name, as here, with a file of its own, and nothing of a build runs before it but the package's own
# targets that it depends on.
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

if [[ "$(head -n 1 "$manifest")" != 'SqlSourceManifest=2' ]]; then
    manifest_fails 'does not start with its version'
fi

# A path is as MSBuild gives it, which on macOS is not the spelling of the temporary directory that mktemp gave.
if ! grep -q '^File=.*/Queries/Users\.sql$' "$manifest"; then
    manifest_fails 'does not list Queries/Users.sql'
fi

# The settings file of the project, by the physical path of the copy, which is how MSBuild gives it.
settings="$(pwd -P)/sqlsource.json"
if ! grep -qxF "Settings=$settings" "$manifest"; then
    manifest_fails "does not hold Settings=$settings"
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

# No quotes around the member: the compiler writes them as its language does, "ByProject" in German.
if ! grep -q "error CS0117: .*$REMOVED_MEMBER" "$work/rebuild.log"; then
    cat "$work/rebuild.log" >&2
    echo "check-package-install: the build after $REMOVED was removed failed without reporting $REMOVED_MEMBER" >&2
    exit 1
fi

# A sidecar and a settings file are inputs of the generator alone.  The Web SDK lists a .json file as Content, which it
# copies to the output and publish folders, and build/SqlSource.targets says Never for both.  The project claims no
# file, so the generator reads neither the sidecar nor the .sql file, and nothing else would copy a .sql file.
web="$work/web"
mkdir -p "$web/Queries"
cp global.json nuget.config "$web/"
cat >"$web/Web.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk.Web">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="$PACKAGE_ID" Version="$version" PrivateAssets="all" />
    </ItemGroup>
</Project>
EOF
echo 'System.Console.WriteLine();' >"$web/Program.cs"
echo 'SELECT 1 AS one;' >"$web/Queries/Users.sql"
echo '{}' >"$web/Queries/Users.sql.json"
echo '{}' >"$web/Queries/sqlsource.json"

dotnet publish "$web/Web.csproj" --output "$work/published" --nologo --verbosity quiet >"$work/publish.log" 2>&1 || {
    cat "$work/publish.log" >&2
    echo "check-package-install: a web project that references $package does not publish" >&2
    exit 1
}

for folder in "$web/bin" "$work/published"; do
    copied="$(find "$folder" -name '*.sql' -o -name '*.sql.json' -o -name 'sqlsource.json')"
    if [[ -n "$copied" ]]; then
        echo "$copied" >&2
        echo "check-package-install: a web project that references $package copies its queries to $folder" >&2
        exit 1
    fi
done

# NuGet imports buildMultiTargeting/SqlSource.targets into the outer build of a project with several target
# frameworks, and build/ into the inner builds, which no other build tells apart.  The outer build runs the tool once,
# before it dispatches the inner builds, so that no inner build waits on another's compile.  The first framework is
# netstandard2.1, whose reference assemblies the SDK brings along with net10.0's, so that the restore still needs no
# feed but the package's.  netstandard2.1 takes a library, not a program.
multi="$work/multi"
mkdir -p "$multi/Queries"
cp global.json nuget.config "$multi/"
cat >"$multi/Multi.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>netstandard2.1;net10.0</TargetFrameworks>
        <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="$PACKAGE_ID" Version="$version" PrivateAssets="all" />
    </ItemGroup>
</Project>
EOF
echo 'public static class Marker { }' >"$multi/Marker.cs"
echo 'SELECT 1 AS one;' >"$multi/Queries/Users.sql"
echo '{ "root": true, "dialect": "postgres", "output": "Sql" }' >"$multi/sqlsource.json"

rm -f "$stand_in_log"
# The console shows the errors alone, and the log of the order of the targets is a file of its own.
dotnet build "$multi/Multi.csproj" --nologo --verbosity quiet "-flp:v=diag;logfile=$work/multi.log" \
    -p:SqlSourceOnline=true "-p:SqlSourceToolCommand=$stand_in" >"$work/multi-console.log" 2>&1 || {
    cat "$work/multi-console.log" >&2
    echo "check-package-install: a project of two target frameworks that references $package does not build" >&2
    exit 1
}

multi_fails() {
    cat "$stand_in_log" >&2 || true
    echo "check-package-install: the build of a project of two target frameworks that references $package $1" >&2
    exit 1
}

if [[ ! -f "$stand_in_log" ]] || [[ "$(wc -l <"$stand_in_log")" -ne 1 ]]; then
    multi_fails 'did not run the tool exactly once'
fi

# The manifest of the run is the first framework's, whose folder under obj/ names it.
manifest_argument='^describe --manifest .*/netstandard2\.1/Multi\.csproj\.SqlSource\.manifest --online true$'
if ! grep -q "$manifest_argument" "$stand_in_log"; then
    multi_fails 'did not run the tool for the first framework'
fi

# The target of buildMultiTargeting/SqlSource.targets finished before the outer build dispatched the inner ones.
# Without that file an inner build calls the run itself, once and for the same framework, which the checks above
# cannot tell from this one.  A line that is not in the log is empty, and a grep with no match is not an error here.
log_line() {
    grep -n -m 1 -F "$1" "$work/multi.log" | cut -d: -f1 || true
}
done_line="$(log_line 'Done building target "SqlSourceRunOnlineBeforeDispatch"')"
dispatch_line="$(log_line 'Target "DispatchToInnerBuilds:')"
if [[ -z "$done_line" || -z "$dispatch_line" || "$done_line" -ge "$dispatch_line" ]]; then
    multi_fails 'did not run the tool in the outer build before it dispatched the inner builds'
fi

# Under bin/<configuration>/: CI sets CONFIGURATION, which MSBuild reads as the property.
for framework in netstandard2.1 net10.0; do
    compiled=("$multi"/bin/*/"$framework"/Multi.dll)
    if [[ ! -f "${compiled[0]}" ]]; then
        multi_fails "did not compile for $framework"
    fi
done

# The packed tool, installed with a tool manifest of the project as a user installs it, run by the packed package's
# target as the build starts it, with no database.  The query and its sidecar are the example's, which
# tools/describe-end-to-end.sh keeps current for the tool of VersionPrefix, so its entry is current for this tool and no
# database is needed to build it.  The variables that turn the online build off, or that hold a connection, were
# cleared above.  Every check asserts a line that only the real tool writes, so that a build that never ran it fails.
online="$work/online"
mkdir -p "$online/Queries"
cp global.json nuget.config "$online/"
cat >"$online/Online.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <Nullable>enable</Nullable>
        <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="$PACKAGE_ID" Version="$version" PrivateAssets="all" />
    </ItemGroup>
</Project>
EOF
echo '{ "root": true, "dialect": "postgres" }' >"$online/sqlsource.json"
cat >"$online/Queries.cs" <<'EOF'
[SqlSource.SqlSourceGenerate(Source = "Queries", Output = SqlSource.GeneratorOutput.Models)]
internal static partial class Queries;
EOF

online_fails() {
    cat "$2" >&2
    echo "check-package-install: the project that installs $tool_package $1" >&2
    exit 1
}

for file in "$ONLINE_QUERY" "$ONLINE_QUERY.json"; do
    cp "$repo_root/$file" "$online/Queries/" 2>"$work/online-copy.log" ||
        online_fails "could not copy $file" "$work/online-copy.log"
done

cd "$online"
# dotnet keeps a cache under DOTNET_CLI_HOME, the user's home by default, of where each local tool of a version is,
# and runs the tool it finds there: a package packed again with the same version, or one installed into another
# temporary folder, would run in place of this one.  Each run of this script has a cache of its own.
export DOTNET_CLI_HOME="$work/dotnet-home"
export DOTNET_NOLOGO=1
dotnet new tool-manifest >"$work/online-manifest.log" 2>&1 ||
    online_fails 'could not create its tool manifest' "$work/online-manifest.log"
dotnet tool install "$TOOL_PACKAGE_ID" --version "$version" >"$work/online-install.log" 2>&1 ||
    online_fails "could not install $TOOL_PACKAGE_ID $version" "$work/online-install.log"

# The tool prints the package's version, alone or followed by "+" and the commit.
dotnet sqlsource --version >"$work/online-version.log" 2>&1 ||
    online_fails 'cannot run dotnet sqlsource --version' "$work/online-version.log"
tool_version="$(cat "$work/online-version.log")"
if [[ "$tool_version" != "$version" && "$tool_version" != "$version+"* ]]; then
    online_fails "runs a tool that prints '$tool_version' for --version, and the package is $version" \
        "$work/online-version.log"
fi

# A .env is a connection source, so auto runs the tool, and it holds no key the tool reads.  It is written before the
# build with false, so that false is told apart from auto there: auto would start the tool, and false must not.
echo 'APP_SETTING=1' >.env

# false never starts the tool, whose command line a detailed log shows when it does.
dotnet build Online.csproj --nologo -tl:off -v:d -p:SqlSourceOnline=false >"$work/online-false.log" 2>&1 ||
    online_fails 'does not build with SqlSourceOnline false' "$work/online-false.log"
if grep -q -F 'describe --manifest' "$work/online-false.log"; then
    online_fails 'ran the tool with SqlSourceOnline false' "$work/online-false.log"
fi

# Under auto, with the .env, the tool runs.  The entry is current, so the database needs no connection and the tool's
# summary says so.
dotnet build Online.csproj --nologo -tl:off -v:d >"$work/online-auto.log" 2>&1 ||
    online_fails 'does not build under auto with a .env and current sidecars' "$work/online-auto.log"
if ! grep -q 'sqlsource: summary: ' "$work/online-auto.log"; then
    online_fails 'did not show the summary of the tool under auto with a .env' "$work/online-auto.log"
fi

# A query whose SQL changed has a stale entry, which needs a connection to describe again.
sed 's/^SELECT \*$/SELECT id, name/' "$repo_root/$ONLINE_QUERY" >Queries/Users.sql
if cmp -s "$repo_root/$ONLINE_QUERY" Queries/Users.sql; then
    online_fails "could not change the SQL of $ONLINE_QUERY, which no longer has a line SELECT *" Queries/Users.sql
fi

# The tool on its own, with no connection, reports the query's database as having none.
code=0
dotnet sqlsource describe >"$work/online-describe.log" 2>&1 || code=$?
if [[ "$code" -ne 1 ]] || ! grep -q 'Users\.sql([0-9]*,[0-9]*): error SQLSRC213: ' "$work/online-describe.log"; then
    online_fails "exited with $code from dotnet sqlsource describe, not with 1 and SQLSRC213" \
        "$work/online-describe.log"
fi

# auto goes on offline with the committed sidecars, and the generator then reports the stale entry.
if dotnet build Online.csproj --nologo -tl:off -v:d >"$work/online-stale.log" 2>&1; then
    online_fails 'built under auto with a stale entry' "$work/online-stale.log"
fi
offline='sqlsource: offline: no connection for postgres; the build uses the committed sidecars'
if ! grep -q -F "$offline" "$work/online-stale.log"; then
    online_fails 'did not show the offline line of the tool under auto with a stale entry' "$work/online-stale.log"
fi
if ! grep -q 'Queries/Users\.sql([0-9]*,[0-9]*): error SQLSRC244: ' "$work/online-stale.log"; then
    online_fails 'did not report the stale entry at its query under auto' "$work/online-stale.log"
fi

# true needs a connection for every database it describes, and the build shows the tool's error on one line, as a
# compiler's, with nothing of Exec's.
rm .env
if dotnet build Online.csproj --nologo -tl:off -p:SqlSourceOnline=true >"$work/online-true.log" 2>&1; then
    online_fails 'built with SqlSourceOnline true and no connection' "$work/online-true.log"
fi
if ! grep -q 'Users\.sql([0-9]*,[0-9]*): error SQLSRC213: .*; see: https://' "$work/online-true.log" ||
    grep -q 'MSB3073' "$work/online-true.log"; then
    online_fails 'did not show the error SQLSRC213 of the tool on one line under true' "$work/online-true.log"
fi

echo "check-package-install: $package installs into a project, which builds and prints $FIXTURE/$EXPECTED,"
echo "check-package-install: which has SqlSourceImported and writes its project manifest,"
echo "check-package-install: and whose next build compiles again after a .sql file is removed,"
echo "check-package-install: and into a web project, whose build and publish copy no .sql, sidecar or settings file,"
echo "check-package-install: and into a project of two target frameworks, whose outer build runs the tool once,"
echo "check-package-install: and runs the packed tool through the build of a project," \
    'under each value of SqlSourceOnline'
