#!/usr/bin/env bash
# Checks the contents of the SqlSource package: the generator, the MSBuild file that hands .sql files to the
# compiler, the readme, and nothing under lib/.
# Usage: check-package.sh [directory]   The directory holds one SqlSource.*.nupkg.  Without it, the package is packed
# into a temporary directory first.
set -euo pipefail

REQUIRED=('build/SqlSource.props' 'analyzers/dotnet/cs/SqlSource.dll' 'README.md')

if [[ $# -gt 1 ]]; then
    echo 'Usage: check-package.sh [directory]' >&2
    exit 2
fi

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

if [[ $# -eq 1 ]]; then
    directory="$1"
else
    directory="$(mktemp -d)"
    trap 'rm -rf "$directory"' EXIT
    dotnet pack src/SqlSource/SqlSource.csproj --output "$directory" --nologo --verbosity quiet
fi

packages=()
for package in "$directory"/SqlSource.*.nupkg; do
    if [[ -f "$package" ]]; then
        packages+=("$package")
    fi
done

if [[ ${#packages[@]} -ne 1 ]]; then
    echo "check-package: expected one SqlSource.*.nupkg in $directory, found ${#packages[@]}" >&2
    exit 1
fi

package="${packages[0]}"
entries="$(unzip -Z1 "$package")"
status=0

for required in "${REQUIRED[@]}"; do
    if ! grep --quiet --line-regexp --fixed-strings "$required" <<<"$entries"; then
        echo "check-package: $package lacks $required" >&2
        status=1
    fi
done

# The generator is loaded by the compiler from analyzers/.  A file under lib/ would become a reference of the consumer.
if grep --quiet '^lib/' <<<"$entries"; then
    echo "check-package: $package holds files under lib/" >&2
    status=1
fi

if [[ "$status" -eq 0 ]]; then
    echo "check-package: $(basename "$package") holds ${REQUIRED[*]}"
fi
exit "$status"
