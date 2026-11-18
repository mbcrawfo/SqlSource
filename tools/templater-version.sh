#!/usr/bin/env bash
# Prints the version of the sqlfluff templater, in PEP 440 form, from VersionPrefix in Directory.Build.props and the
# VersionSuffix environment variable that the workflows set for the packages: empty gives X.Y.Z, beta.N gives X.Y.ZbN
# and pr-P.N gives X.Y.Z.devN.  Any other suffix fails, so that a new kind of build cannot publish the wrong version.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

pattern='.*<VersionPrefix>\([0-9][0-9]*\.[0-9][0-9]*\.[0-9][0-9]*\)</VersionPrefix>.*'
prefix="$(sed -n "s|$pattern|\1|p" "$repo_root/Directory.Build.props")"
if [[ -z "$prefix" ]]; then
    echo 'templater-version.sh: VersionPrefix of Directory.Build.props is not of the form X.Y.Z.' >&2
    exit 1
fi

suffix="${VersionSuffix:-}"
if [[ -z "$suffix" ]]; then
    echo "$prefix"
elif [[ "$suffix" =~ ^beta\.([0-9]+)$ ]]; then
    echo "${prefix}b${BASH_REMATCH[1]}"
elif [[ "$suffix" =~ ^pr-[0-9]+\.([0-9]+)$ ]]; then
    echo "${prefix}.dev${BASH_REMATCH[1]}"
else
    echo "templater-version.sh: VersionSuffix '$suffix' is neither empty, beta.N nor pr-P.N." >&2
    exit 1
fi
