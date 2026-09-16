"""Publication gates reject incomplete notes and unsigned or mismatched tags."""

import contextlib
import copy
import io
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from urllib.error import URLError

from release_notes import ROOT, check_files, main, validate_notes, validate_tag, verify_remote_tag


VERSION = "0.3.2"
COMMIT = "a" * 40
TAG_SHA = "b" * 40
REFERENCE = {"object": {"type": "tag", "sha": TAG_SHA}}
TAG = {
    "sha": TAG_SHA,
    "tag": "v0.3.2",
    "object": {"type": "commit", "sha": COMMIT},
    "verification": {"verified": True, "reason": "valid"},
}
NOTES = """## Summary

Xanthos 0.3.2 improves release verification.

## Highlights

- Check reviewed notes before publication.

## Compatibility and migration

No API changes; the package still targets .NET 10.

## Verification

Native COM: not run for this workflow-only change.

## Known limitations

The existing SDK communication limitation remains; see the verification issue.

## Links

- [NuGet package](https://www.nuget.org/packages/Xanthos/0.3.2)
- [Changelog](https://github.com/cariandrum22/Xanthos/blob/v0.3.2/CHANGELOG.md)
"""


class ReleaseNotesTests(unittest.TestCase):
    def test_complete_notes_and_explained_unperformed_checks_are_accepted(self):
        self.assertEqual([], validate_notes(VERSION, NOTES))

    def test_missing_duplicate_reordered_and_wrong_depth_sections_fail(self):
        for body in (
            NOTES.replace("## Highlights", "## Changes"),
            NOTES + "\n## Summary\n\nDuplicate.\n",
            NOTES.replace("## Highlights", "## Known limitations", 1).replace("## Known limitations\n\nThe", "## Highlights\n\nThe"),
            NOTES.replace("## Highlights", "# Highlights"),
            NOTES.replace("## Verification", "### Verification"),
        ):
            with self.subTest(body=body):
                self.assertTrue(validate_notes(VERSION, body))

    def test_comments_and_fences_cannot_supply_missing_sections(self):
        for heading in ("<!-- ## Verification -->", "```markdown\n## Verification\n```", "~~~~\n## Verification\n~~~~"):
            with self.subTest(heading=heading):
                self.assertTrue(validate_notes(VERSION, NOTES.replace("## Verification", heading)))

    def test_empty_and_unfinished_sections_fail(self):
        for content in ("", "<!-- Add details. -->", "### Checks", "TBD", "TODO", "N/A", "{{results}}", "Tests: TBD"):
            with self.subTest(content=content):
                self.assertTrue(validate_notes(VERSION, NOTES.replace("Native COM: not run for this workflow-only change.", content)))

    def test_version_and_package_links_must_match_exactly(self):
        for body in (
            NOTES.replace("Xanthos 0.3.2", "Xanthos 0.3.20"),
            NOTES.replace("Xanthos 0.3.2", "Xanthos 0.3.2-rc.1"),
            NOTES.replace("packages/Xanthos/0.3.2", "packages/Xanthos/0.3.1"),
            NOTES.replace("blob/v0.3.2/", "blob/develop/"),
            NOTES.replace("[NuGet package](https://www.nuget.org/packages/Xanthos/0.3.2)", "https://www.nuget.org/packages/Xanthos/0.3.2"),
        ):
            with self.subTest(body=body):
                self.assertTrue(validate_notes(VERSION, body))

    def test_invalid_versions_are_rejected_before_path_or_network_access(self):
        with patch("release_notes.urlopen") as request:
            for version in ("v0.3.2", "00.3.2", "0.3", "0.3.2-rc.1", "0.3.2+build", "../../secret", "0.3.2\n"):
                with self.subTest(version=version):
                    self.assertTrue(validate_notes(version, NOTES))
                    self.assertTrue(check_files(ROOT, version))
                    self.assertTrue(verify_remote_tag(version, "owner/repo", COMMIT))
            request.assert_not_called()

    def test_template_requires_release_specific_content(self):
        body = (ROOT / ".github/release-notes-template.md").read_text(encoding="utf-8").replace("{{version}}", VERSION)
        self.assertTrue(validate_notes(VERSION, body))

    def test_failed_verification_can_be_recorded_without_claiming_success(self):
        body = NOTES.replace("Native COM: not run for this workflow-only change.", "Native COM: 14 passed, 1 failed; accepted SDK communication limitation.")
        self.assertEqual([], validate_notes(VERSION, body))


class ReleaseFilesTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        (self.root / "Directory.Build.props").write_text("<Project><PropertyGroup><VersionPrefix>0.3.2</VersionPrefix></PropertyGroup></Project>", encoding="utf-8")
        self.notes = self.root / "docs/releases/v0.3.2.md"
        self.notes.parent.mkdir(parents=True)
        self.notes.write_text(NOTES, encoding="utf-8-sig")

    def run_cli(self, *args):
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            return main(["--root", str(self.root), *args])

    def test_source_version_and_utf8_bom_notes_pass(self):
        self.assertEqual(0, self.run_cli("--check-all"))
        self.assertEqual(0, self.run_cli("--version", VERSION))

    def test_version_bump_requires_matching_notes(self):
        props = self.root / "Directory.Build.props"
        props.write_text(props.read_text(encoding="utf-8").replace(VERSION, "0.3.3"), encoding="utf-8")
        self.assertEqual(1, self.run_cli("--check-all"))

    def test_all_notes_are_checked_and_filenames_are_restricted(self):
        older = self.notes.with_name("v0.3.1.md")
        older.write_text(NOTES.replace(VERSION, "0.3.1"), encoding="utf-8")
        self.assertEqual(0, self.run_cli("--check-all"))
        older.write_text("Incomplete notes", encoding="utf-8")
        self.assertEqual(1, self.run_cli("--check-all"))
        older.unlink()
        self.notes.with_name("arbitrary.md").write_text(NOTES, encoding="utf-8")
        self.assertEqual(1, self.run_cli("--check-all"))

    def test_missing_notes_and_malformed_props_fail(self):
        self.assertEqual(2, self.run_cli("--version", "0.3.3"))
        (self.root / "Directory.Build.props").write_text("<Project>", encoding="utf-8")
        self.assertEqual(2, self.run_cli("--check-all"))

    def test_invalid_notes_stop_before_signature_lookup(self):
        self.notes.write_text("Incomplete notes", encoding="utf-8")
        with patch("release_notes.verify_remote_tag") as verify:
            self.assertEqual(1, self.run_cli("--version", VERSION, "--verify-tag", "--repository", "owner/repo", "--commit", COMMIT))
            verify.assert_not_called()

    def test_signature_lookup_errors_fail_without_logging_sensitive_details(self):
        output = io.StringIO()
        with patch("release_notes.verify_remote_tag", side_effect=URLError("sensitive response")), contextlib.redirect_stderr(output):
            result = main(["--root", str(self.root), "--version", VERSION, "--verify-tag", "--repository", "owner/repo", "--commit", COMMIT])
        self.assertEqual(2, result)
        self.assertNotIn("sensitive response", output.getvalue())

    def test_incomplete_signature_arguments_are_usage_errors(self):
        for args in (("--check-all", "--verify-tag"), ("--version", VERSION, "--verify-tag"), ("--version", VERSION, "--repository", "owner/repo")):
            with self.subTest(args=args), self.assertRaises(SystemExit) as error:
                self.run_cli(*args)
            self.assertEqual(2, error.exception.code)


class ReleaseSignatureTests(unittest.TestCase):
    def test_verified_annotated_tag_at_exact_source_commit_passes(self):
        self.assertEqual([], validate_tag(VERSION, COMMIT, REFERENCE, TAG))

    def test_unsigned_invalid_or_unverified_tags_fail(self):
        for verification in (None, {}, {"verified": False, "reason": "unsigned"}, {"verified": True, "reason": "unknown_key"}, {"verified": 1, "reason": "valid"}):
            with self.subTest(verification=verification):
                self.assertTrue(validate_tag(VERSION, COMMIT, REFERENCE, dict(TAG, verification=verification)))

    def test_verified_commit_cannot_replace_an_annotated_signed_tag(self):
        reference = {"object": {"type": "commit", "sha": COMMIT}, "verification": {"verified": True, "reason": "valid"}}
        self.assertTrue(validate_tag(VERSION, COMMIT, reference, TAG))

    def test_signature_cannot_authorize_a_different_tag_or_source(self):
        for change in ({"sha": "c" * 40}, {"tag": "v0.3.1"}, {"object": {"type": "commit", "sha": "d" * 40}}, {"object": {"type": "tag", "sha": COMMIT}}, {"object": None}):
            with self.subTest(change=change):
                self.assertTrue(validate_tag(VERSION, COMMIT, REFERENCE, dict(TAG, **change)))

    def test_invalid_response_objects_fail_closed(self):
        for tag in ([], dict(TAG, object=["unexpected"]), dict(TAG, verification="valid")):
            with self.subTest(tag=tag), self.assertRaises(ValueError):
                validate_tag(VERSION, COMMIT, REFERENCE, tag)

    def test_remote_lookup_uses_only_requested_tag_and_validates_its_signature(self):
        responses = [io.BytesIO(json.dumps(value).encode()) for value in (REFERENCE, TAG)]
        with patch.dict(os.environ, {"GH_TOKEN": "test-token"}), patch("release_notes.urlopen", side_effect=responses) as request:
            self.assertEqual([], verify_remote_tag(VERSION, "owner/repo", COMMIT))
        self.assertEqual([
            "https://api.github.com/repos/owner/repo/git/ref/tags/v0.3.2",
            f"https://api.github.com/repos/owner/repo/git/tags/{TAG_SHA}",
        ], [call.args[0].full_url for call in request.call_args_list])

    def test_missing_token_and_invalid_repository_or_commit_do_not_make_requests(self):
        with patch.dict(os.environ, {}, clear=True), patch("release_notes.urlopen") as request:
            for repository, commit in (("owner/repo", COMMIT), ("owner/repo/../secret", COMMIT), ("owner/repo", "HEAD")):
                with self.subTest(repository=repository, commit=commit):
                    self.assertTrue(verify_remote_tag(VERSION, repository, commit))
            request.assert_not_called()

    def test_unexpected_tag_sha_does_not_become_a_request_path(self):
        reference = copy.deepcopy(REFERENCE)
        reference["object"]["sha"] = "../commits/HEAD"
        with patch.dict(os.environ, {"GH_TOKEN": "test-token"}), patch("release_notes.urlopen", return_value=io.BytesIO(json.dumps(reference).encode())) as request:
            with self.assertRaises(ValueError):
                verify_remote_tag(VERSION, "owner/repo", COMMIT)
            self.assertEqual(1, request.call_count)


if __name__ == "__main__":
    unittest.main()
