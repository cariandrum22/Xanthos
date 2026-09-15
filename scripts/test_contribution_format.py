"""Behavior checks for templates, historical descriptions and GitHub events."""

import contextlib
import io
import json
import tempfile
import unittest
from pathlib import Path

from contribution_format import event_metadata, main, validate


PR = """## Summary

Decode valid CP932 text.

## Changes

Preserve raw bytes and reject malformed sequences.

## Validation

- [ ] Windows COM verification remains pending.
- Unit tests passed.

## Related issues

Closes #38.
"""
ISSUE = """### Summary

文字化けする。

### Details

Observed on JV-Link 5.0 / Windows x64.

### Expected outcome

Valid CP932 characters are preserved.
"""


class ContributionFormatTests(unittest.TestCase):
    def test_valid_pr_titles(self):
        for title in ("fix: decode text", "feat(interop)!: change defaults", "release: v0.3.0", "docs: 日本語の説明", "revert: restore behavior"):
            with self.subTest(title=title):
                self.assertEqual([], validate("pull_request", title, PR))

    def test_invalid_pr_titles(self):
        for title in ("Release v0.3.0", "Fix: test", "fix:", "fix: ", "fix: x\n::error::injected"):
            with self.subTest(title=title):
                self.assertTrue(validate("pull_request", title, PR))

    def test_issue_form_headings_and_optional_fields(self):
        self.assertEqual([], validate("issue", "CP932 failure", ISSUE + "\n### Environment\n\nWindows\n"))

    def test_missing_and_duplicate_sections_fail(self):
        self.assertTrue(validate("pull_request", "fix: text", PR.replace("## Validation", "## Testing")))
        self.assertTrue(validate("pull_request", "fix: text", PR + "\n## Summary\n\nDuplicate\n"))

    def test_out_of_order_sections_fail(self):
        self.assertTrue(validate("pull_request", "fix: text", PR.replace("## Summary", "## Changes", 1).replace("## Changes\n\nPreserve", "## Summary\n\nPreserve")))

    def test_empty_comments_and_placeholders_fail(self):
        for text in ("", "<!-- Report evidence here. -->", "_No response_", "TBD", "TODO", "...", "N/A"):
            with self.subTest(text=text):
                self.assertTrue(validate("pull_request", "fix: text", PR.replace("Decode valid CP932 text.", text)))

    def test_explained_unperformed_validation_is_accepted(self):
        body = PR.replace("- [ ] Windows COM verification remains pending.\n- Unit tests passed.", "Not run: documentation-only change.")
        self.assertEqual([], validate("pull_request", "docs: explain setup", body))

    def test_fenced_headings_do_not_count(self):
        for fence in ("```", "~~~~"):
            with self.subTest(fence=fence):
                body = PR.replace("## Validation", f"{fence}markdown\n## Validation\n{fence}")
                self.assertTrue(validate("pull_request", "test: fixtures", body))

    def test_fence_must_close_with_matching_marker_and_length(self):
        body = PR.replace("## Validation", "````markdown\n```\n~~~\n## Validation\n````")
        self.assertTrue(validate("pull_request", "test: fixtures", body))

    def test_headings_in_comments_and_nested_sections_do_not_count(self):
        for heading in ("<!--\n## Validation\n-->", "### Validation", "    ## Validation"):
            with self.subTest(heading=heading):
                self.assertTrue(validate("pull_request", "test: fixtures", PR.replace("## Validation", heading)))

    def test_commented_duplicate_does_not_break_valid_body(self):
        self.assertEqual([], validate("pull_request", "docs: examples", PR + "\n<!-- ## Summary -->"))

    def test_github_null_body_is_empty(self):
        self.assertEqual(("issue", "A bug", ""), event_metadata({"issue": {"title": "A bug", "body": None}}))
        self.assertTrue(validate(*event_metadata({"issue": {"title": "A bug", "body": None}})))

    def test_invalid_event_metadata_is_rejected(self):
        for event in ({}, {"issue": {"title": 1, "body": "text"}}, {"pull_request": {"title": "fix: text", "body": []}}):
            with self.subTest(event=event), self.assertRaises(ValueError):
                event_metadata(event)

    def test_event_payload_is_data_and_cannot_inject_ci_commands(self):
        with tempfile.TemporaryDirectory() as directory:
            event = Path(directory) / "event.json"
            event.write_text(json.dumps({"pull_request": {"title": "::error::injected\n$(exit 1)", "body": PR}}), encoding="utf-8")
            output = io.StringIO()
            with contextlib.redirect_stdout(output), contextlib.redirect_stderr(output):
                result = main(["--event", str(event)])
            self.assertEqual(1, result)
            self.assertNotIn("::error::injected", output.getvalue())

    def test_cli_local_and_event_inputs(self):
        with tempfile.TemporaryDirectory() as directory:
            body = Path(directory) / "body.md"
            body.write_text(ISSUE, encoding="utf-8-sig")
            event = Path(directory) / "event.json"
            event.write_text(json.dumps({"issue": {"title": "A bug", "body": ISSUE}}), encoding="utf-8")
            with contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(0, main(["--kind", "issue", "--title", "A bug", "--body-file", str(body)]))
                self.assertEqual(0, main(["--event", str(event)]))
            event.write_text("[]", encoding="utf-8")
            with contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(2, main(["--event", str(event)]))
                self.assertEqual(2, main(["--event", str(Path(directory) / "missing")]))

    def test_pr_template_requires_contributor_input(self):
        template = (Path(__file__).resolve().parents[1] / ".github/pull_request_template.md").read_text(encoding="utf-8")
        errors = validate("pull_request", "fix: example", template)
        self.assertEqual(4, len(errors))
        self.assertTrue(all("Fill in" in error for error in errors))


if __name__ == "__main__":
    unittest.main()
