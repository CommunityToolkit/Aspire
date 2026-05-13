#!/usr/bin/env bash

set -euo pipefail

# dogfood-pr.sh - Download and install NuGet packages from a PR's build artifacts
# Usage: ./dogfood-pr.sh PR_NUMBER [OPTIONS]

readonly REPO="CommunityToolkit/Aspire"
readonly CI_WORKFLOW="dotnet-ci.yml"
readonly ARTIFACT_NAME="nuget-packages"

# --- Output Helpers ---

RED=$'\033[0;31m'; GREEN=$'\033[0;32m'; YELLOW=$'\033[0;33m'
BOLD=$'\033[1m'; DIM=$'\033[2m'; RESET=$'\033[0m'

link() { printf '\e]8;;%s\e\\%s\e]8;;\e\\' "$1" "$2"; }
display_path() { echo "${1/#$HOME/\~}"; }

spin() {
    local msg="$1"; shift
    "$@" &>/dev/null &
    local pid=$!
    local chars='⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏'
    local i=0
    while kill -0 "$pid" 2>/dev/null; do
        printf "\033[1A\033[K%s %s\n" "${chars:i++%${#chars}:1}" "$msg" >&2
        sleep 0.1
    done
    wait "$pid"
    local rc=$?
    printf "\033[1A\033[K" >&2
    return $rc
}

say()      { echo -e "$*" >&2; }
say_err()  { echo -e "${RED}✗ $*${RESET}" >&2; }
say_warn() { echo -e "${YELLOW}⚠ $*${RESET}" >&2; }
verbose()  { [[ "$VERBOSE" == true ]] && echo -e "${DIM}  $*${RESET}" >&2 || true; }

show_help() {
    cat <<EOF
Usage: dogfood-pr.sh PR_NUMBER [OPTIONS]

Download and install NuGet packages from a PR's build artifacts for local testing.

OPTIONS:
    -r, --run-id ID             Workflow run ID (skip PR resolution)
    --install-path PATH         Install prefix (default: \$HOME/.aspire)
    -v, --verbose               Verbose output
    -k, --keep-archive          Keep temp download directory
    -h, --help                  Show this help

EXAMPLES:
    dogfood-pr.sh 1129
    dogfood-pr.sh 1129 --run-id 12345678
    dogfood-pr.sh 1129 --install-path ./local-packages

REQUIREMENTS:
    - GitHub CLI (gh) authenticated: https://cli.github.com
EOF
}

# --- Core Functions ---

parse_arguments() {
    PR_NUMBER=""
    RUN_ID=""
    INSTALL_PREFIX=""
    VERBOSE=false
    KEEP_ARCHIVE=false

    while [[ $# -gt 0 ]]; do
        case "$1" in
            -h|--help)       show_help; exit 0 ;;
            -r|--run-id)
                if [[ $# -lt 2 || "$2" == -* ]]; then
                    say_err "--run-id requires a value"
                    show_help
                    exit 1
                fi
                RUN_ID="$2"; shift 2
                ;;
            --install-path)
                if [[ $# -lt 2 || "$2" == -* ]]; then
                    say_err "--install-path requires a value"
                    show_help
                    exit 1
                fi
                INSTALL_PREFIX="$2"; shift 2
                ;;
            -v|--verbose)    VERBOSE=true; shift ;;
            -k|--keep-archive) KEEP_ARCHIVE=true; shift ;;
            -*)              say_err "Unknown option: $1"; show_help; exit 1 ;;
            *)
                if [[ -z "$PR_NUMBER" ]]; then
                    PR_NUMBER="$1"; shift
                else
                    say_err "Unexpected argument: $1"; exit 1
                fi
                ;;
        esac
    done

    if [[ -z "$PR_NUMBER" ]]; then
        say_err "PR number is required"
        show_help
        exit 1
    fi

    if ! [[ "$PR_NUMBER" =~ ^[0-9]+$ ]]; then
        say_err "PR number must be a positive integer"
        exit 1
    fi
}

check_prerequisites() {
    if ! command -v gh &>/dev/null; then
        say_err "GitHub CLI (gh) is required. Install from: https://cli.github.com"
        exit 1
    fi

    if ! gh auth status &>/dev/null; then
        say_err "GitHub CLI is not authenticated. Run: gh auth login"
        exit 1
    fi

    if ! command -v unzip &>/dev/null; then
        say_err "unzip is required but not found"
        exit 1
    fi
}

resolve_pull_request() {
    local pr_url="https://github.com/${REPO}/pull/${PR_NUMBER}"
    head_sha=$(gh api "repos/${REPO}/pulls/${PR_NUMBER}" --jq '.head.sha' 2>/dev/null) || {
        say_err "PR #${PR_NUMBER} not found in ${REPO}"
        exit 1
    }

    local pr_title pr_author
    pr_title=$(gh api "repos/${REPO}/pulls/${PR_NUMBER}" --jq '.title' 2>/dev/null)
    pr_author=$(gh api "repos/${REPO}/pulls/${PR_NUMBER}" --jq '.user.login' 2>/dev/null)

    local author_display
    author_display="$(link "https://github.com/${pr_author}" "@${pr_author}")"

    say ""
    local cols=${COLUMNS:-$(tput cols 2>/dev/null || echo 80)}
    local prefix="PR #${PR_NUMBER} — "
    local suffix=" by @${pr_author}"
    local max_title=$((cols - ${#prefix} - ${#suffix} - 2))
    if [[ ${#pr_title} -gt $max_title && $max_title -gt 3 ]]; then
        pr_title="${pr_title:0:$((max_title - 1))}…"
    fi
    say "$(link "$pr_url" "PR #${PR_NUMBER}") — ${BOLD}${pr_title}${RESET} ${DIM}by ${author_display}${RESET}"
    verbose "Head commit: $(link "https://github.com/${REPO}/commit/${head_sha}" "${head_sha:0:7}")"
    say ""
}

find_workflow_run() {
    if [[ -n "$RUN_ID" ]]; then
        workflow_run_id="$RUN_ID"
        return
    fi

    workflow_run_id=$(gh api "repos/${REPO}/actions/workflows/${CI_WORKFLOW}/runs?event=pull_request&head_sha=${head_sha}" \
        --jq '.workflow_runs | sort_by(.created_at, .updated_at) | reverse | .[0].id' 2>/dev/null)

    if [[ -z "$workflow_run_id" || "$workflow_run_id" == "null" ]]; then
        say_err "No workflow run found for PR #${PR_NUMBER} (SHA: ${head_sha})"
        say "  Check: https://github.com/${REPO}/actions/workflows/${CI_WORKFLOW}"
        exit 1
    fi

    verbose "Workflow run: $(link "https://github.com/${REPO}/actions/runs/${workflow_run_id}" "$workflow_run_id")"
}

download_artifacts() {
    download_dir="${temp_dir}/nuget-packages"
    local run_url="https://github.com/${REPO}/actions/runs/${workflow_run_id}"

    say ""
    if ! spin "📦 Downloading packages..." gh run download "$workflow_run_id" -R "$REPO" --name "$ARTIFACT_NAME" -D "$download_dir"; then
        say_err "Failed to download artifacts — build may still be in progress or artifacts may have expired"
        say "   ${DIM}$(link "$run_url" "View workflow run")${RESET}"
        exit 1
    fi

    pkg_count=$(find "$download_dir" -name '*.nupkg' | wc -l)
    if [[ "$pkg_count" -eq 0 ]]; then
        say_err "No NuGet packages found in downloaded artifacts"
        exit 1
    fi

    local download_size
    download_size=$(du -sh "$download_dir" 2>/dev/null | cut -f1 | sed 's/K$/ KB/;s/M$/ MB/;s/G$/ GB/')
    say "📦 Downloaded ${BOLD}${pkg_count}${RESET} packages (${download_size})"
}

install_packages() {
    mkdir -p "$hive_dir"
    find "$download_dir" -name '*.nupkg' -exec cp {} "$hive_dir/" \;
    say "📂 Installed to $(display_path "$hive_dir")"

    version=""
    local first_pkg
    first_pkg=$(find "$download_dir" -name '*.nupkg' -print -quit)
    if [[ -n "$first_pkg" ]]; then
        version=$(unzip -p "$first_pkg" "*.nuspec" 2>/dev/null \
            | sed -n 's:.*<version>\([^<]*\)</version>.*:\1:p' \
            | head -n 1)
    fi
}

configure_nuget_source() {
    nuget_config=""

    if ! command -v dotnet &>/dev/null; then
        say_warn "dotnet CLI not found — configure NuGet source manually:"
        say "   ${DIM}dotnet nuget add source \"${hive_dir}\" --name \"${source_name}\"${RESET}"
        return
    fi

    if dotnet nuget list source 2>/dev/null | grep -Fq "$source_name"; then
        dotnet nuget update source "$source_name" --source "$hive_dir" &>/dev/null
    else
        dotnet nuget add source "$hive_dir" --name "$source_name" &>/dev/null
    fi

    local config_display=""
    nuget_config=$(dotnet nuget config paths 2>/dev/null | head -n 1)
    if [[ -n "$nuget_config" ]]; then
        config_display=" in $(display_path "$nuget_config")"
    fi
    say "🔧 Configured source ${BOLD}${source_name}${RESET}${config_display}"
}

print_summary() {
    say ""
    if [[ -n "$version" ]]; then
        say "🐶 ${GREEN}Ready${RESET} — use version ${BOLD}${version}${RESET} to test these changes"
    fi

    say ""
    say "${DIM}To undo:${RESET}"
    if [[ -n "${nuget_config:-}" ]]; then
        say "  ${DIM}dotnet nuget remove source \"${source_name}\" --configfile \"${nuget_config}\" > /dev/null && rm -rf \"${INSTALL_PREFIX}/hives/community-toolkit-pr-${PR_NUMBER}\"${RESET}"
    else
        say "  ${DIM}dotnet nuget remove source \"${source_name}\" > /dev/null && rm -rf \"${INSTALL_PREFIX}/hives/community-toolkit-pr-${PR_NUMBER}\"${RESET}"
    fi

    if [[ "$KEEP_ARCHIVE" == true ]]; then
        say ""
        say "Archive kept at: ${DIM}${temp_dir}${RESET}"
    fi

    if [[ "$VERBOSE" == true ]]; then
        say ""
        say "${DIM}Packages:${RESET}"
        find "$hive_dir" -name '*.nupkg' -exec basename {} \; | sed 's/^/  /' | sort >&2
    fi
}

# --- Entry Point ---

main() {
    parse_arguments "$@"
    check_prerequisites

    INSTALL_PREFIX="${INSTALL_PREFIX:-$HOME/.aspire}"
    hive_dir="${INSTALL_PREFIX}/hives/community-toolkit-pr-${PR_NUMBER}/packages"
    source_name="CommunityToolkit-PR-${PR_NUMBER}"

    temp_dir=$(mktemp -d -t dogfood-pr-XXXXXX)
    [[ "$KEEP_ARCHIVE" != true ]] && trap 'rm -rf "$temp_dir"' EXIT

    resolve_pull_request
    find_workflow_run
    download_artifacts
    install_packages
    configure_nuget_source
    print_summary
}

main "$@"
