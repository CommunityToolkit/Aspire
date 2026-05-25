#! /bin/bash

set -euo pipefail

MODE="${1:---workflow}"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TESTS_DIR="${REPO_ROOT}/tests"
INDENT="            "

get_test_names() {
    find "${TESTS_DIR}" -type f -name '*Hosting.*.Tests.csproj' | sort | while read -r file; do
        basename "$file" .csproj | sed 's/^CommunityToolkit\.Aspire\.//'
    done

    find "${TESTS_DIR}" -type f -name '*.Tests.csproj' ! -name '*Hosting.*.Tests.csproj' | sort | while read -r file; do
        basename "$file" .csproj | sed 's/^CommunityToolkit\.Aspire\.//'
    done
}

case "${MODE}" in
    --plain)
        get_test_names
        ;;
    --json)
        mapfile -t tests < <(get_test_names)
        python - <<'PY' "${tests[@]}"
import json
import sys

print(json.dumps(sys.argv[1:]))
PY
        ;;
    --workflow)
        echo "${INDENT}# Hosting integration tests"
        find "${TESTS_DIR}" -type f -name '*Hosting.*.Tests.csproj' | sort | while read -r file; do
            base=$(basename "$file" .csproj)
            echo "${INDENT}${base#CommunityToolkit.Aspire.},"
        done

        echo

        echo "${INDENT}# Client integration tests"
        find "${TESTS_DIR}" -type f -name '*.Tests.csproj' ! -name '*Hosting.*.Tests.csproj' | sort | while read -r file; do
            base=$(basename "$file" .csproj)
            echo "${INDENT}${base#CommunityToolkit.Aspire.},"
        done
        ;;
    *)
        echo "Unknown mode: ${MODE}" >&2
        echo "Usage: $0 [--workflow|--plain|--json]" >&2
        exit 1
        ;;
esac