#!/usr/bin/env bash
# Recursively delete all `bin` and `obj` folders from the repository.
# Usage: ./eng/clean-bin-obj.sh
# Optional flags:
#   --dry-run   Show what would be removed without deleting.
#   --quiet     Suppress per-folder output; only show summary.
#   --help      Display help.
#
# The script resolves the repo root based on its own location so it can be
# invoked from any working directory.

set -euo pipefail

print_help() {
    cat <<'EOF'
Clean bin/obj folders

Deletes ALL directories named `bin` or `obj` under the repository root.

Flags:
  --dry-run   List directories that would be deleted.
  --quiet     Only print summary information.
  --help      Show this help text.

Examples:
  ./eng/clean-bin-obj.sh
  ./eng/clean-bin-obj.sh --dry-run
  ./eng/clean-bin-obj.sh --quiet
EOF
}

DRY_RUN=0
QUIET=0
for arg in "$@"; do
    case "$arg" in
        --dry-run) DRY_RUN=1 ;;
        --quiet) QUIET=1 ;;
        --help|-h) print_help; exit 0 ;;
        *) echo "Unknown argument: $arg" >&2; exit 1 ;;
    esac
done

# Determine repo root (parent of this script's directory)
SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
REPO_ROOT=$(cd "${SCRIPT_DIR}/.." && pwd)

cd "$REPO_ROOT"

# Collect bin/obj directories excluding anything under .git to be safe.
# Use -prune to avoid descending into matched directories after they are found.
mapfile -t TARGETS < <(find . -type d \( -name bin -o -name obj \) -not -path '*/.git/*' -prune -print)

COUNT=${#TARGETS[@]}
if [[ $COUNT -eq 0 ]]; then
    [[ $QUIET -eq 0 ]] && echo "No bin/obj directories found under $REPO_ROOT." || true
    exit 0
fi

if [[ $DRY_RUN -eq 1 ]]; then
    [[ $QUIET -eq 0 ]] && printf '%s\n' "Dry run: the following $COUNT directories would be deleted:" || true
    printf '%s\n' "${TARGETS[@]}"
    exit 0
fi

# Delete directories.
DELETED=0
for dir in "${TARGETS[@]}"; do
    if [[ $QUIET -eq 0 ]]; then
        echo "Removing: $dir"
    fi
    rm -rf "$dir" || {
        echo "Failed to remove: $dir" >&2
        continue
    }
    # Increment without triggering set -e early exit (arithmetic exit status is 1 when result is 0 for post-increment)
    ((DELETED++)) || true
done

if [[ $QUIET -eq 0 ]]; then
    echo "Removed $DELETED bin/obj directories under $REPO_ROOT."
else
    echo "Removed $DELETED directories."  # Always show a minimal summary in quiet mode.
fi

exit 0
