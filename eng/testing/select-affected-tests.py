#!/usr/bin/env python3

from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict
from pathlib import Path


GLOBAL_FULL_RUN_PATHS = (
    ".github/actions/",
    ".github/workflows/",
    "CommunityToolkit.Aspire.slnx",
    "Directory.Build.props",
    "Directory.Build.targets",
    "eng/testing/generate-test-list-for-workflow.sh",
    "eng/testing/select-affected-tests.py",
    "global.json",
    "nuget.config",
    "tests/Directory.Build.props",
    "tests-app-hosts/Directory.Build.props",
)

IGNORED_SUFFIXES = {".md"}
TEST_INFRA_PROPS_PATH = "tests/Directory.Build.props"


def run_git(repo_root: Path, *args: str) -> str:
    result = subprocess.run(
        ["git", *args],
        cwd=repo_root,
        text=True,
        capture_output=True,
        check=True,
    )
    return result.stdout


def load_all_tests(repo_root: Path) -> list[str]:
    tests_dir = repo_root / "tests"
    hosting = sorted(tests_dir.glob("**/*Hosting.*.Tests.csproj"))
    client = sorted(
        path
        for path in tests_dir.glob("**/*.Tests.csproj")
        if "Hosting." not in path.name
    )

    def to_name(path: Path) -> str:
        return path.stem.removeprefix("CommunityToolkit.Aspire.")

    return [*map(to_name, hosting), *map(to_name, client)]


def parse_xml(path: Path) -> ET.Element:
    return ET.parse(path).getroot()


def tag_name(element: ET.Element) -> str:
    return element.tag.rsplit("}", 1)[-1]


def project_data(repo_root: Path) -> tuple[dict[str, set[str]], dict[str, set[str]], list[str]]:
    project_refs: dict[str, set[str]] = {}
    package_refs: dict[str, set[str]] = {}
    project_paths: list[str] = []

    for root_name in ("src", "tests", "examples", "tests-app-hosts"):
        for project_path in sorted((repo_root / root_name).glob("**/*.csproj")):
            relative = project_path.relative_to(repo_root).as_posix()
            project_paths.append(relative)
            refs: set[str] = set()
            packages: set[str] = set()
            xml_root = parse_xml(project_path)
            for element in xml_root.iter():
                name = tag_name(element)
                if name == "ProjectReference":
                    include = element.attrib.get("Include")
                    if include:
                        include_path = Path(include.replace("\\", "/"))
                        resolved = (project_path.parent / include_path).resolve().relative_to(repo_root.resolve())
                        refs.add(resolved.as_posix())
                elif name == "PackageReference":
                    include = element.attrib.get("Include")
                    if include:
                        packages.add(include)
            project_refs[relative] = refs
            package_refs[relative] = packages

    return project_refs, package_refs, project_paths


def build_node_to_tests(
    all_tests: list[str],
    project_refs: dict[str, set[str]],
) -> dict[str, set[str]]:
    node_to_tests: dict[str, set[str]] = defaultdict(set)
    test_projects = {
        project: Path(project).stem.removeprefix("CommunityToolkit.Aspire.")
        for project in project_refs
        if project.startswith("tests/") and Path(project).stem.removeprefix("CommunityToolkit.Aspire.") in all_tests
    }

    for project, test_name in test_projects.items():
        queue = [project]
        seen: set[str] = set()
        while queue:
            current = queue.pop()
            if current in seen:
                continue
            seen.add(current)
            node_to_tests[current].add(test_name)
            queue.extend(project_refs.get(current, ()))

    return node_to_tests


def build_package_to_tests(
    package_refs: dict[str, set[str]],
    node_to_tests: dict[str, set[str]],
) -> dict[str, set[str]]:
    package_to_tests: dict[str, set[str]] = defaultdict(set)
    for project, packages in package_refs.items():
        impacted = node_to_tests.get(project, set())
        for package in packages:
            package_to_tests[package].update(impacted)
    return package_to_tests


def load_test_infra_packages(repo_root: Path) -> set[str]:
    packages: set[str] = set()
    xml_root = parse_xml(repo_root / TEST_INFRA_PROPS_PATH)
    for element in xml_root.iter():
        if tag_name(element) != "PackageReference":
            continue
        include = element.attrib.get("Include")
        if include:
            packages.add(include)
    return packages


def changed_files(repo_root: Path, base_sha: str, head_sha: str) -> list[str]:
    output = run_git(repo_root, "diff", "--name-only", f"{base_sha}...{head_sha}")
    paths: list[str] = []
    for line in output.splitlines():
        stripped = line.strip()
        if stripped:
            paths.append(stripped)
    return paths


def package_diff(repo_root: Path, base_sha: str, head_sha: str) -> tuple[set[str], bool]:
    output = run_git(
        repo_root,
        "diff",
        "--unified=0",
        f"{base_sha}...{head_sha}",
        "--",
        "Directory.Packages.props",
    )

    package_ids: set[str] = set()
    uncertain = False

    for raw_line in output.splitlines():
        if not raw_line or raw_line.startswith(("diff --git", "index ", "--- ", "+++ ", "@@")):
            continue

        if raw_line[0] not in {"+", "-"}:
            continue

        line = raw_line[1:].strip()
        if not line or line.startswith("<!--"):
            continue

        if "PackageVersion" in line and 'Include="' in line:
            include = line.split('Include="', 1)[1].split('"', 1)[0]
            package_ids.add(include)
            continue

        uncertain = True

    return package_ids, uncertain


def nearest_project(file_path: str, project_paths: list[str]) -> str | None:
    candidates: list[str] = []
    for project in project_paths:
        project_dir = Path(project).parent.as_posix()
        prefix = f"{project_dir}/"
        if file_path == project or file_path.startswith(prefix):
            candidates.append(project)
    if not candidates:
        return None
    return max(candidates, key=lambda candidate: len(Path(candidate).parent.as_posix()))


def matches_global_full_run(path: str) -> bool:
    return any(path == entry or path.startswith(entry) for entry in GLOBAL_FULL_RUN_PATHS)


def write_outputs(
    selected_tests: list[str],
    run_all: bool,
    reason: str,
) -> None:
    github_output = os.environ.get("GITHUB_OUTPUT")
    if not github_output:
        return

    with Path(github_output).open("a", encoding="utf-8") as handle:
        handle.write(f"selected_tests={json.dumps(selected_tests)}\n")
        handle.write(f"has_tests={'true' if bool(selected_tests) else 'false'}\n")
        handle.write(f"run_all={'true' if run_all else 'false'}\n")
        handle.write(f"reason={reason}\n")


def write_summary(summary_path: Path, summary: str) -> None:
    summary_path.write_text(summary, encoding="utf-8")
    step_summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if step_summary:
        with Path(step_summary).open("a", encoding="utf-8") as handle:
            handle.write(summary)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", required=True)
    parser.add_argument("--base-sha", required=True)
    parser.add_argument("--head-sha", required=True)
    parser.add_argument("--output-dir", required=True)
    args = parser.parse_args()

    repo_root = Path(args.repo_root).resolve()
    output_dir = Path(args.output_dir).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)

    all_tests = load_all_tests(repo_root)
    project_refs, package_refs, project_paths = project_data(repo_root)
    node_to_tests = build_node_to_tests(all_tests, project_refs)
    package_to_tests = build_package_to_tests(package_refs, node_to_tests)
    test_infra_packages = load_test_infra_packages(repo_root)

    selected: set[str] = set()
    ignored_files: list[str] = []
    reasons: list[str] = []
    run_all = False
    reason = "No affected tests were detected."

    try:
        diff_files = changed_files(repo_root, args.base_sha, args.head_sha)
    except subprocess.CalledProcessError as exc:
        selected_tests = all_tests
        reason = "Fell back to the full test matrix because git diff failed."
        reasons.append(reason)
        payload = {
            "changedFiles": [],
            "ignoredFiles": [],
            "reason": reason,
            "reasons": reasons,
            "runAll": True,
            "selectedTests": selected_tests,
            "stderr": exc.stderr,
        }
    else:
        for file_path in diff_files:
            suffix = Path(file_path).suffix.lower()
            if suffix in IGNORED_SUFFIXES:
                ignored_files.append(file_path)
                continue

            if file_path == "Directory.Packages.props":
                package_ids, uncertain = package_diff(repo_root, args.base_sha, args.head_sha)
                if uncertain or not package_ids:
                    run_all = True
                    reason = "Fell back to the full test matrix because package impact could not be determined safely."
                    reasons.append(reason)
                    break

                if package_ids & test_infra_packages:
                    run_all = True
                    reason = "Fell back to the full test matrix because shared test infrastructure packages changed."
                    reasons.append(reason)
                    break

                impacted = set().union(*(package_to_tests.get(package_id, set()) for package_id in package_ids))
                missing = sorted(package_id for package_id in package_ids if package_id not in package_to_tests)
                if missing:
                    run_all = True
                    reason = f"Fell back to the full test matrix because package usage for {', '.join(missing)} could not be resolved safely."
                    reasons.append(reason)
                    break

                selected.update(impacted)
                reasons.append(
                    f"Selected {len(impacted)} tests from package changes in Directory.Packages.props: {', '.join(sorted(package_ids))}."
                )
                continue

            if matches_global_full_run(file_path):
                run_all = True
                reason = f"Fell back to the full test matrix because {file_path} is a global CI/build input."
                reasons.append(reason)
                break

            project = nearest_project(file_path, project_paths)
            if project:
                impacted = node_to_tests.get(project, set())
                selected.update(impacted)
                if impacted:
                    reasons.append(
                        f"Selected {len(impacted)} tests because {file_path} belongs to {project}."
                    )
                continue

            if file_path.startswith(("src/", "tests/", "examples/", "tests-app-hosts/")):
                run_all = True
                reason = f"Fell back to the full test matrix because {file_path} could not be mapped to a project safely."
                reasons.append(reason)
                break

        selected_tests = all_tests if run_all else [test for test in all_tests if test in selected]

        if not run_all and selected_tests:
            reason = f"Selected {len(selected_tests)} affected tests from {len(diff_files)} changed files."
        elif not run_all and ignored_files and not selected_tests:
            reason = "No tests were selected because only ignored documentation changes were detected."

        payload = {
            "changedFiles": diff_files,
            "ignoredFiles": ignored_files,
            "reason": reason,
            "reasons": reasons,
            "runAll": run_all,
            "selectedTests": selected_tests,
        }

    (output_dir / "selection.json").write_text(json.dumps(payload, indent=2), encoding="utf-8")
    (output_dir / "changed-files.txt").write_text(
        "\n".join(payload.get("changedFiles", [])) + ("\n" if payload.get("changedFiles") else ""),
        encoding="utf-8",
    )

    summary_lines = [
        "## Affected test selection",
        "",
        f"- Mode: {'full' if payload['runAll'] else 'selective'}",
        f"- Reason: {payload['reason']}",
        f"- Selected tests: {len(payload['selectedTests'])}",
        "",
        "### Changed files",
    ]

    if payload["changedFiles"]:
        summary_lines.extend(f"- `{file_path}`" for file_path in payload["changedFiles"])
    else:
        summary_lines.append("- None")

    if payload["ignoredFiles"]:
        summary_lines.extend(("", "### Ignored files"))
        summary_lines.extend(f"- `{file_path}`" for file_path in payload["ignoredFiles"])

    if payload["selectedTests"]:
        summary_lines.extend(("", "### Selected tests"))
        summary_lines.extend(f"- `{test_name}`" for test_name in payload["selectedTests"])

    if payload["reasons"]:
        summary_lines.extend(("", "### Selection details"))
        summary_lines.extend(f"- {entry}" for entry in payload["reasons"])

    summary = "\n".join(summary_lines) + "\n"
    summary_path = output_dir / "selection-summary.md"
    write_summary(summary_path, summary)
    write_outputs(payload["selectedTests"], payload["runAll"], payload["reason"])

    return 0


if __name__ == "__main__":
    sys.exit(main())
