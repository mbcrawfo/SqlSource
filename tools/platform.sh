# shellcheck shell=bash
# The conversions a script needs to run under Git Bash on Windows, sourced by every script that runs docker or hands a
# host path to a Windows program, and no-ops on Linux and macOS.  Sourcing it runs nothing.
#
# Git Bash rewrites an argument that looks like a Unix path into a Windows path before it starts a Windows program, so
# docker would get C:/Program Files/Git/mnt for the container's /mnt; run_docker turns that off for the one command.
# It rewrites only arguments, never a path written into a file or inside an argument's value, such as the value of
# -p:Name=/tmp/x; native_path gives the spelling a Windows program reads there.  ScriptPortabilityTests of
# tests/SqlSource.Repository.Tests/Validation/ fails on a script that does neither where it must.

# Succeeds under Git Bash or Cygwin on Windows.
on_windows() {
    case "${OSTYPE:-}" in
    msys* | cygwin*) return 0 ;;
    *) return 1 ;;
    esac
}

# The path in the spelling a Windows program reads, with forward slashes (C:/Users/...), or the path unchanged.
native_path() {
    if on_windows; then
        cygpath -m -- "$1"
    else
        printf '%s\n' "$1"
    fi
}

# The path as a Windows program prints it, with backslashes (C:\Users\...), or the path unchanged: for comparing a path
# with what such a program wrote.
windows_path() {
    if on_windows; then
        cygpath -w -- "$1"
    else
        printf '%s\n' "$1"
    fi
}

# docker with its arguments as written, so that a path inside the container reaches it unchanged.  A host path given to
# it goes through native_path.
run_docker() {
    MSYS_NO_PATHCONV=1 docker "$@"
}
