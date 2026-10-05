#!/bin/bash

# Read hook input from stdin
HOOK_INPUT=$(cat)

# Without jq the command cannot be read, and every commit would pass unchecked
if ! command -v jq >/dev/null 2>&1; then
    if [[ "$HOOK_INPUT" == *git*commit* ]]; then
        echo "Error: jq is required to validate commits" >&2
        exit 2
    fi
    exit 0
fi

# Extract the bash command from the JSON input
CMD=$(echo "$HOOK_INPUT" | jq -r '.tool_input.command // empty' 2>/dev/null)

# Only intercept git commit commands, including ones with global options before
# the subcommand: git -C <path> commit, git -c <name>=<value> commit, git --no-pager commit
VALUE="(\"[^\"]*\"|'[^']*'|[^[:space:]]+)"
GLOBAL_OPTION="(-[Cc]|--git-dir|--work-tree|--namespace|--config-env)[[:space:]]+${VALUE}|-[^[:space:]]+"
GIT_COMMIT="git([[:space:]]+(${GLOBAL_OPTION}))*[[:space:]]+commit"
if ! [[ "$CMD" =~ $GIT_COMMIT ]]; then
    exit 0 # Allow non-commit commands to proceed
fi

# The tree being committed comes from the hook input.  CLAUDE_PROJECT_DIR stays at the launch checkout
# for EnterWorktree and for subagents, so it is only the fallback.
CWD=$(echo "$HOOK_INPUT" | jq -r '.cwd // empty' 2>/dev/null)
ROOT=$(git -C "${CWD:-$CLAUDE_PROJECT_DIR}" rev-parse --show-toplevel 2>/dev/null) || ROOT="$CLAUDE_PROJECT_DIR"

cd "$ROOT" || {
    echo "Error: Cannot access project directory $ROOT" >&2
    exit 2
}

echo "Running ./pre-commit-validation.sh..." >&2

# Run verification checks
if ! bash pre-commit-validation.sh 2>&1; then
    echo "❌ Commit blocked: Validation failed" >&2
    echo "Commits must pass all checks enforced by ./pre-commit-validation.sh. Run that command for details of the failures." >&2
    exit 2
fi

echo "✓ Validation passed - proceeding with commit" >&2
exit 0
