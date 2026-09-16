"""Check completed FSharpLint output against the reviewed diagnostic baseline."""

import argparse
from collections import Counter
import json
import os
from pathlib import Path
import re
import subprocess
import sys


BANNER = re.compile(r"Running FSharpLint with (\d+) rules \((\d+) enabled, (\d+) disabled\)\.\.\.")
START = re.compile(r"========== Linting (.+) ==========")
FINISH = re.compile(r"========== Finished: (\d+) warnings ==========")
SUMMARY = re.compile(r"========== Summary: (\d+) warnings ==========")
WARNING = re.compile(r"(.+)\((\d+),(\d+),(\d+),(\d+)\):FSharpLint warning (FL\d+): (.+)")
KEY_FIELDS = ("path", "rule", "message", "source")


def parse_result(stdout: str, stderr: str, exit_code: int, root: Path) -> list[dict]:
    """A warning exit is valid only after every file and the final summary agree."""
    if stderr.strip():
        raise ValueError("FSharpLint wrote to stderr; inspect stderr.log for tool or parsing errors")
    root = root.resolve()
    banner = False
    active = None
    file_warnings = 0
    completed_files = 0
    summary = None
    findings = []
    sources = {}
    for line in stdout.splitlines():
        if not line.strip():
            continue
        if summary is not None:
            raise ValueError("Unexpected output after the final FSharpLint summary")
        if match := BANNER.fullmatch(line):
            total, enabled, disabled = map(int, match.groups())
            if banner or total != enabled + disabled or enabled == 0:
                raise ValueError("Invalid FSharpLint rule inventory")
            banner = True
        elif not banner:
            raise ValueError("Missing FSharpLint rule inventory")
        elif match := START.fullmatch(line):
            if active is not None:
                raise ValueError("A file was not completely analysed")
            active = Path(match[1]).resolve()
            if not active.is_file():
                raise ValueError("FSharpLint reported a nonexistent input file")
            file_warnings = 0
        elif match := WARNING.fullmatch(line):
            path = Path(match[1]).resolve()
            if path != active:
                raise ValueError("Diagnostic does not belong to the active input file")
            relative = path.relative_to(root).as_posix()
            start_line, column, end_line, end_column = map(int, match.groups()[1:5])
            if path not in sources:
                sources[path] = path.read_text(encoding="utf-8-sig").splitlines()
            lines = sources[path]
            if not (1 <= start_line <= end_line <= len(lines)) or (start_line == end_line and column > end_column):
                raise ValueError("Diagnostic has an invalid source range")
            source = lines[start_line - 1].strip()
            findings.append({"path": relative, "rule": match[6], "message": match[7],
                             "source": source, "line": start_line, "column": column})
            file_warnings += 1
        elif match := FINISH.fullmatch(line):
            if active is None or int(match[1]) != file_warnings:
                raise ValueError("Per-file warning count does not match diagnostics")
            active = None
            completed_files += 1
        elif match := SUMMARY.fullmatch(line):
            summary = int(match[1])
            if active is not None or completed_files == 0 or summary != len(findings):
                raise ValueError("Incomplete or inconsistent FSharpLint summary")
        else:
            raise ValueError("Unrecognised FSharpLint output; inspect stdout.log")
    if summary is None:
        raise ValueError("FSharpLint did not produce a final summary")
    expected_exits = {-1, 255, 4294967295} if findings else {0}
    if exit_code not in expected_exits:
        raise ValueError(f"FSharpLint exit code {exit_code} disagrees with its completed report")
    return findings


def diagnostic_counts(findings: list[dict]) -> Counter:
    # Source line text, not line numbers, identifies a finding when nearby code moves.
    return Counter(tuple(finding[field] for field in KEY_FIELDS) for finding in findings)


def baseline_document(findings: list[dict], version: str) -> dict:
    return {"schemaVersion": 1, "toolVersion": version, "target": "Xanthos.sln",
            "diagnostics": [dict(zip(KEY_FIELDS, key), count=count)
                            for key, count in sorted(diagnostic_counts(findings).items())]}


def compare_baseline(findings: list[dict], baseline: dict, version: str) -> tuple[Counter, Counter]:
    if baseline.get("schemaVersion") != 1 or baseline.get("toolVersion") != version or baseline.get("target") != "Xanthos.sln":
        raise ValueError("Baseline schema, tool version or solution does not match")
    entries = baseline.get("diagnostics")
    if not isinstance(entries, list):
        raise ValueError("Baseline diagnostics must be an explicit list")
    known = Counter()
    for entry in entries:
        if not isinstance(entry, dict) or any(not isinstance(entry.get(field), str) or not entry[field] for field in KEY_FIELDS):
            raise ValueError("Invalid baseline diagnostic")
        key = tuple(entry[field] for field in KEY_FIELDS)
        if key in known or type(entry.get("count")) is not int or entry["count"] <= 0:
            raise ValueError("Duplicate baseline entry or invalid diagnostic count")
        known[key] = entry["count"]
    actual = diagnostic_counts(findings)
    return actual - known, known - actual


def write_json(path: Path, value: dict) -> None:
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def check(root: Path, update_baseline: bool = False) -> int:
    root = root.resolve()
    output = root / ".artifacts/lint"
    output.mkdir(parents=True, exist_ok=True)
    for name in ("stdout", "stderr"):
        (output / f"{name}.log").write_text("", encoding="utf-8")
    report = {"status": "error"}
    try:
        tools = json.loads((root / ".config/dotnet-tools.json").read_text(encoding="utf-8-sig"))
        version = tools["tools"]["dotnet-fsharplint"]["version"]
        result = subprocess.run(["dotnet", "fsharplint", "--format", "MSBuild", "lint", "Xanthos.sln"],
                                cwd=root, capture_output=True, text=True, encoding="utf-8", timeout=900)
        (output / "stdout.log").write_text(result.stdout, encoding="utf-8")
        (output / "stderr.log").write_text(result.stderr, encoding="utf-8")
        report.update(toolVersion=version, toolExitCode=result.returncode)
        findings = parse_result(result.stdout, result.stderr, result.returncode, root)
        report.update(totalWarnings=len(findings), diagnostics=findings)
        baseline_path = root / ".config/fsharplint-baseline.json"
        if update_baseline:
            write_json(baseline_path, baseline_document(findings, version))
            report.update(status="baseline-updated", newWarnings=None, resolvedWarnings=None)
            print(f"Recorded {len(findings)} findings. Review the baseline diff before committing.")
            return 0
        baseline = json.loads(baseline_path.read_text(encoding="utf-8"))
        new, resolved = compare_baseline(findings, baseline, version)
        report.update(newWarnings=sum(new.values()), resolvedWarnings=sum(resolved.values()))
        if new:
            for (path, rule, message, source), count in sorted(new.items()):
                print(f"New finding ({count}): {path} {rule}: {message}")
            raise ValueError(f"{sum(new.values())} new FSharpLint findings; fix them or explicitly review a baseline change")
        report["status"] = "pass"
        print(f"FSharpLint: {len(findings)} existing findings, no new findings, {sum(resolved.values())} resolved.")
        return 0
    except (OSError, ValueError, KeyError, subprocess.SubprocessError) as error:
        report["error"] = str(error)
        if isinstance(error, subprocess.TimeoutExpired):
            for name, data in (("stdout", error.stdout), ("stderr", error.stderr)):
                if isinstance(data, bytes):
                    data = data.decode("utf-8", errors="replace")
                (output / f"{name}.log").write_text(data or "", encoding="utf-8")
        print("FSharpLint check failed: " + str(error).replace("\n", " "), file=sys.stderr)
        return 1
    finally:
        write_json(output / "report.json", report)
        if summary_path := os.environ.get("GITHUB_STEP_SUMMARY"):
            with open(summary_path, "a", encoding="utf-8") as summary_file:
                summary_file.write("\n### FSharpLint\n\n")
                summary_file.write(f"Result: **{report['status']}**. Tool exit code: `{report.get('toolExitCode', 'unavailable')}`.\n\n")
                if "totalWarnings" in report:
                    summary_file.write(f"Existing/baseline findings remain visible: **{report['totalWarnings']}** total, "
                                       f"**{report.get('newWarnings', 'unverified')}** new, "
                                       f"**{report.get('resolvedWarnings', 'unverified')}** resolved.\n\n")
                summary_file.write("See the `fsharplint-report` artifact for diagnostics and complete stdout/stderr. "
                                   "A passing baseline check does not mean that the source has no lint warnings.\n")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--update-baseline", action="store_true", help="Record current findings for explicit review; never used by CI")
    args = parser.parse_args()
    return check(Path(__file__).resolve().parents[1], args.update_baseline)


if __name__ == "__main__":
    sys.exit(main())
