"""Regression checks for incomplete lint runs and diagnostic baseline enforcement."""

import contextlib
import io
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

from check_fsharplint import baseline_document, check, compare_baseline, parse_result


class FSharpLintTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix="xanthos-lint-")
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name).resolve()
        self.source = self.root / "src/Parser 日本語.fs"
        self.source.parent.mkdir()
        self.source.write_text("module Example\nlet bad_name = 1\nlet other_name = 2\n", encoding="utf-8")
        (self.root / ".config").mkdir()
        (self.root / ".config/dotnet-tools.json").write_text(
            json.dumps({"tools": {"dotnet-fsharplint": {"version": "0.27.0"}}}), encoding="utf-8")
        self.baseline_path = self.root / ".config/fsharplint-baseline.json"

    def output(self, rows=(2,), message="Use camelCase.", source=None):
        source = source or self.source
        warnings = "".join(f"{source}({row},4,{row},12):FSharpLint warning FL0034: {message}\n" for row in rows)
        return ("Running FSharpLint with 98 rules (42 enabled, 56 disabled)...\n"
                f"========== Linting {source} ==========\n{warnings}"
                f"========== Finished: {len(rows)} warnings ==========\n"
                f"========== Summary: {len(rows)} warnings ==========\n")

    def findings(self, **kwargs):
        return parse_result(self.output(**kwargs), "", 255, self.root)

    def record_baseline(self):
        baseline = baseline_document(self.findings(), "0.27.0")
        self.baseline_path.write_text(json.dumps(baseline), encoding="utf-8")
        return baseline

    def run_check(self, result=None, update=False, error=None):
        result = result or subprocess.CompletedProcess([], 255, self.output(), "")
        with patch("check_fsharplint.subprocess.run", return_value=result, side_effect=error), \
                patch.dict(os.environ, {"GITHUB_STEP_SUMMARY": str(self.root / "summary.md")}), \
                contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            return check(self.root, update)

    def test_complete_warning_results_on_windows_and_unix(self):
        for exit_code in (-1, 255, 4294967295):
            with self.subTest(exit_code=exit_code):
                findings = parse_result(self.output(), "", exit_code, self.root)
                self.assertEqual("src/Parser 日本語.fs", findings[0]["path"])
                self.assertEqual("let bad_name = 1", findings[0]["source"])

    def test_complete_clean_run(self):
        self.assertEqual([], parse_result(self.output(rows=()), "", 0, self.root))

    def test_exit_code_must_agree_with_completed_output(self):
        for exit_code in (0, 1, 2, 134, -9):
            with self.subTest(exit_code=exit_code), self.assertRaises(ValueError):
                parse_result(self.output(), "", exit_code, self.root)
        with self.assertRaises(ValueError):
            parse_result(self.output(rows=()), "", 255, self.root)

    def test_stderr_failure_is_not_accepted_as_existing_warnings(self):
        with self.assertRaises(ValueError):
            parse_result(self.output(), "FSharpLint error: failed to parse input", 255, self.root)

    def test_truncated_empty_and_inconsistent_reports_fail(self):
        valid = self.output()
        bad_outputs = [
            "", valid.split("========== Summary:")[0],
            valid.replace("Summary: 1", "Summary: 0"),
            valid.replace("Finished: 1", "Finished: 0"),
            valid.replace("========== Finished: 1 warnings ==========\n", ""),
            valid + "Unexpected output\n",
            valid.replace("42 enabled, 56 disabled", "0 enabled, 98 disabled"),
            valid.replace("42 enabled, 56 disabled", "42 enabled, 0 disabled"),
            valid.replace("Running FSharpLint", "Unknown FSharpLint"),
            valid.replace("========== Summary", "Unrecognised line\n========== Summary"),
            valid.splitlines()[0] + "\n========== Summary: 0 warnings ==========\n",
            valid.replace("========== Finished", f"========== Linting {self.source} ==========\n========== Finished"),
            valid.replace("(2,4,2,12)", "(200,4,200,12)"),
            valid.replace("(2,4,2,12)", "(2,12,2,4)"),
        ]
        for output in bad_outputs:
            with self.subTest(output=output), self.assertRaises(ValueError):
                parse_result(output, "", 255, self.root)

    def test_diagnostic_must_belong_to_the_active_file(self):
        other = self.root / "src/Other.fs"
        other.write_text("module Other\nlet other_name = 1\n", encoding="utf-8")
        with self.assertRaises(ValueError):
            parse_result(self.output().replace(f"{self.source}(2,", f"{other}(2,"), "", 255, self.root)

    def test_diagnostic_outside_repository_fails(self):
        nested_root = self.root / "nested"
        nested_root.mkdir()
        with self.assertRaises(ValueError):
            parse_result(self.output(), "", 255, nested_root)

    def test_missing_input_file_fails(self):
        with self.assertRaises(ValueError):
            parse_result(self.output(source=self.root / "Missing.fs"), "", 255, self.root)

    def test_exact_baseline_passes(self):
        findings = self.findings()
        new, resolved = compare_baseline(findings, baseline_document(findings, "0.27.0"), "0.27.0")
        self.assertFalse(new)
        self.assertFalse(resolved)

    def test_same_total_cannot_hide_a_different_warning(self):
        baseline = self.record_baseline()
        for findings in (self.findings(rows=(3,)), self.findings(message="A different diagnostic.")):
            with self.subTest(findings=findings):
                new, resolved = compare_baseline(findings, baseline, "0.27.0")
                self.assertEqual(1, sum(new.values()))
                self.assertEqual(1, sum(resolved.values()))

    def test_repeated_warning_count_cannot_increase(self):
        new, _ = compare_baseline(self.findings(rows=(2, 2)), self.record_baseline(), "0.27.0")
        self.assertEqual(1, sum(new.values()))

    def test_line_shifts_do_not_create_new_findings(self):
        baseline = self.record_baseline()
        self.source.write_text("\n" + self.source.read_text(encoding="utf-8"), encoding="utf-8")
        new, resolved = compare_baseline(self.findings(rows=(3,)), baseline, "0.27.0")
        self.assertFalse(new)
        self.assertFalse(resolved)

    def test_resolved_warning_is_reported(self):
        new, resolved = compare_baseline([], self.record_baseline(), "0.27.0")
        self.assertFalse(new)
        self.assertEqual(1, sum(resolved.values()))

    def test_tool_upgrade_requires_baseline_review(self):
        with self.assertRaises(ValueError):
            compare_baseline(self.findings(), self.record_baseline(), "0.28.0")

    def test_invalid_and_duplicate_baseline_entries_fail(self):
        baseline = self.record_baseline()
        entry = baseline["diagnostics"][0]
        for entries in ([entry, entry], [dict(entry, count=0)], [dict(entry, count=True)], [dict(entry, rule="")], None):
            with self.subTest(entries=entries), self.assertRaises(ValueError):
                compare_baseline(self.findings(), dict(baseline, diagnostics=entries), "0.27.0")

    def test_checker_preserves_successful_warning_report_and_baseline(self):
        self.record_baseline()
        before = self.baseline_path.read_bytes()
        self.assertEqual(0, self.run_check())
        report = json.loads((self.root / ".artifacts/lint/report.json").read_text(encoding="utf-8"))
        self.assertEqual(("pass", 255, 1, 0), (report["status"], report["toolExitCode"], report["totalWarnings"], report["newWarnings"]))
        self.assertEqual(before, self.baseline_path.read_bytes())
        self.assertEqual(self.output(), (self.root / ".artifacts/lint/stdout.log").read_text(encoding="utf-8"))
        self.assertIn("does not mean", (self.root / "summary.md").read_text(encoding="utf-8"))

    def test_checker_blocks_new_diagnostics_without_changing_baseline(self):
        self.record_baseline()
        before = self.baseline_path.read_bytes()
        self.assertEqual(1, self.run_check(subprocess.CompletedProcess([], 255, self.output(rows=(3,)), "")))
        self.assertEqual(before, self.baseline_path.read_bytes())

    def test_baseline_update_refuses_tool_failure(self):
        failure = subprocess.CompletedProcess([], 255, self.output(), "FSharpLint error: parse failed")
        self.assertEqual(1, self.run_check(failure, update=True))
        self.assertFalse(self.baseline_path.exists())
        self.assertIn("parse failed", (self.root / ".artifacts/lint/stderr.log").read_text(encoding="utf-8"))

    def test_explicit_baseline_update_records_completed_diagnostics(self):
        self.assertEqual(0, self.run_check(update=True))
        self.assertEqual(baseline_document(self.findings(), "0.27.0"), json.loads(self.baseline_path.read_text(encoding="utf-8")))

    def test_missing_tool_and_timeout_fail_with_evidence(self):
        for error in (FileNotFoundError("dotnet missing"), subprocess.TimeoutExpired("dotnet", 900, b"partial output", b"")):
            with self.subTest(error=error):
                self.assertEqual(1, self.run_check(error=error))
                report = json.loads((self.root / ".artifacts/lint/report.json").read_text(encoding="utf-8"))
                self.assertEqual("error", report["status"])
        self.assertEqual("partial output", (self.root / ".artifacts/lint/stdout.log").read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
