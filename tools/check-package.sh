#!/usr/bin/env bash
# Checks the contents of the two packages.  SqlSource: the generator, the two MSBuild files that hand .sql files and
# the settings to the compiler, the readme, and nothing under lib/.  SqlSource.Tool: the tool with the generator's
# assembly and Roslyn beside it, the settings that name its command, and the readme.  Both have one version.
# Usage: check-package.sh [directory]   The directory holds one SqlSource.<version>.nupkg and one
# SqlSource.Tool.<version>.nupkg.  Without it, both are packed into a temporary directory first.
set -euo pipefail

GENERATOR_ID='SqlSource'
GENERATOR_REQUIRED=('build/SqlSource.props' 'build/SqlSource.targets' 'analyzers/dotnet/cs/SqlSource.dll' 'README.md')
TOOL_ID='SqlSource.Tool'
# A tool carries everything it loads: tools/check-tool-install.sh is what shows that the whole of it runs.
TOOL_REQUIRED=(
    'tools/net8.0/any/SqlSource.Tool.dll'
    'tools/net8.0/any/SqlSource.dll'
    'tools/net8.0/any/Microsoft.CodeAnalysis.CSharp.dll'
    'tools/net8.0/any/DotnetToolSettings.xml'
    'README.md'
)

if [[ $# -gt 1 ]]; then
    echo 'Usage: check-package.sh [directory]' >&2
    exit 2
fi

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [[ $# -eq 1 ]]; then
    # Resolved before the working directory changes, so that a relative path means what the caller meant.
    directory="$(cd "$1" && pwd)"
    cd "$repo_root"
else
    cd "$repo_root"
    directory="$(mktemp -d)"
    trap 'rm -rf "$directory"' EXIT
    dotnet pack src/SqlSource/SqlSource.csproj --output "$directory" --nologo --verbosity quiet
    dotnet pack src/SqlSource.Tool/SqlSource.Tool.csproj --output "$directory" --nologo --verbosity quiet
fi

status=0

# Prints the path of the one package of an id.  A version starts with a digit, and that is what keeps the pattern of
# SqlSource from matching SqlSource.Tool.<version>.nupkg as well.
find_package() {
    local id="$1"
    local found=()
    local package
    for package in "$directory/$id".[0-9]*.nupkg; do
        if [[ -f "$package" ]]; then
            found+=("$package")
        fi
    done

    if [[ ${#found[@]} -ne 1 ]]; then
        echo "check-package: expected one $id.<version>.nupkg in $directory, found ${#found[@]}" >&2
        return 1
    fi
    echo "${found[0]}"
}

# Fails for each required entry that a package lacks.
check_entries() {
    local package="$1"
    shift
    local entries
    entries="$(unzip -Z1 "$package")"
    local required
    for required in "$@"; do
        if ! grep --quiet --line-regexp --fixed-strings "$required" <<<"$entries"; then
            echo "check-package: $package lacks $required" >&2
            status=1
        fi
    done
}

generator="$(find_package "$GENERATOR_ID")" || exit 1
tool="$(find_package "$TOOL_ID")" || exit 1

check_entries "$generator" "${GENERATOR_REQUIRED[@]}"
check_entries "$tool" "${TOOL_REQUIRED[@]}"

# The generator is loaded by the compiler from analyzers/.  A file under lib/ would become a reference of the consumer.
# The listing is read from a variable: in a pipe, grep stops at its first match, unzip dies of the closed pipe, and
# pipefail makes that a failure, which reads here as "no file under lib/".
generator_entries="$(unzip -Z1 "$generator")"
if grep --quiet '^lib/' <<<"$generator_entries"; then
    echo "check-package: $generator holds files under lib/" >&2
    status=1
fi

# The two are released together, and a sidecar records the version of the tool for the generator to compare.
generator_version="$(basename "$generator" .nupkg)"
generator_version="${generator_version#"$GENERATOR_ID".}"
tool_version="$(basename "$tool" .nupkg)"
tool_version="${tool_version#"$TOOL_ID".}"
if [[ "$generator_version" != "$tool_version" ]]; then
    echo "check-package: $GENERATOR_ID is $generator_version and $TOOL_ID is $tool_version" >&2
    status=1
fi

if [[ "$status" -eq 0 ]]; then
    echo "check-package: $(basename "$generator") holds ${GENERATOR_REQUIRED[*]}"
    echo "check-package: $(basename "$tool") holds ${TOOL_REQUIRED[*]}"
fi
exit "$status"
