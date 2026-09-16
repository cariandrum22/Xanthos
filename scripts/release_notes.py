#!/usr/bin/env python3
"""Check reviewed release notes and, when publishing, the signed source tag."""

import argparse
import json
import os
from pathlib import Path
import re
import sys
from urllib.error import URLError
from urllib.request import Request, urlopen
import xml.etree.ElementTree as ET

from contribution_format import headings, PLACEHOLDERS


ROOT = Path(__file__).resolve().parents[1]
VERSION = re.compile(r"(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)")
SHA = re.compile(r"[0-9a-f]{40}")
SECTIONS = ("Summary", "Highlights", "Compatibility and migration", "Verification", "Known limitations", "Links")


def validate_notes(version, body):
    if not VERSION.fullmatch(version):
        return ["Use a stable X.Y.Z version without a v prefix or leading zeroes."]
    lines, found = headings(body)
    errors = []
    if tuple((depth, name) for _, depth, name in found if depth <= 2) != tuple((2, name) for name in SECTIONS):
        errors.append("Use exactly these level-two headings, in order: " + ", ".join(SECTIONS) + ".")
    sections = {}
    for start, depth, name in found:
        if depth != 2 or name not in SECTIONS:
            continue
        end = next((index for index, level, _ in found if index > start and level <= 2), len(lines))
        content = "\n".join(lines[start + 1:end]).strip()
        prose = re.sub(r"(?m)^ {0,3}#{1,6}\s+.*$", "", content).strip()
        if prose.lower().rstrip(".") in PLACEHOLDERS:
            errors.append(f"Fill in '{name}'; comments, empty subsections and placeholders do not count.")
        if re.search(r"\{\{|\}\}|\b(?:TODO|TBD)\b", content):
            errors.append(f"Replace every template placeholder in '{name}'.")
        sections[name] = content
    if not re.search(r"\bXanthos " + re.escape(version) + r"(?![\w.+-])", sections.get("Summary", "")):
        errors.append("Identify Xanthos and the exact release version in Summary.")
    for label, url in (
        ("NuGet", f"https://www.nuget.org/packages/Xanthos/{version}"),
        ("changelog", f"https://github.com/cariandrum22/Xanthos/blob/v{version}/CHANGELOG.md"),
    ):
        if not re.search(r"\[[^\]\n]+\]\(" + re.escape(url) + r"\)", sections.get("Links", "")):
            errors.append(f"Include the version-specific {label} Markdown link in Links.")
    return errors


def check_files(root, version=None):
    directory = root / "docs" / "releases"
    if version is not None:
        if not VERSION.fullmatch(version):
            return ["Use a stable X.Y.Z version without a v prefix or leading zeroes."]
        paths = [directory / f"v{version}.md"]
    else:
        source = ET.parse(root / "Directory.Build.props").findtext(".//VersionPrefix")
        if not source or not VERSION.fullmatch(source):
            return ["Directory.Build.props must declare a stable VersionPrefix."]
        paths = sorted(directory.glob("*.md"))
        if directory / f"v{source}.md" not in paths:
            return ["Add release notes matching the source VersionPrefix."]
    errors = []
    for path in paths:
        name = re.fullmatch(r"v(.+)\.md", path.name)
        if not name or not VERSION.fullmatch(name[1]):
            errors.append("Release note filenames must be vX.Y.Z.md.")
            continue
        body = path.read_text(encoding="utf-8-sig")
        errors.extend(f"v{name[1]}: {error}" for error in validate_notes(name[1], body))
    return errors


def json_object(value):
    if not isinstance(value, dict):
        raise ValueError("Expected a GitHub object.")
    return value


def validate_tag(version, commit, reference, tag):
    """A verified commit does not substitute for a verified annotated tag."""
    reference_object = json_object(json_object(reference).get("object") or {})
    tag = json_object(tag)
    if reference_object.get("type") != "tag":
        return ["Create an annotated signed release tag with git tag -s."]
    errors = []
    if tag.get("sha") != reference_object.get("sha") or tag.get("tag") != f"v{version}":
        errors.append("The resolved tag object must match the requested release tag.")
    target = json_object(tag.get("object") or {})
    if target.get("type") != "commit" or target.get("sha") != commit:
        errors.append("The release tag must point to the exact source commit being published.")
    verification = json_object(tag.get("verification") or {})
    if verification.get("verified") is not True or verification.get("reason") != "valid":
        errors.append("GitHub must verify the release tag signature before publication.")
    return errors


def verify_remote_tag(version, repository, commit):
    if not VERSION.fullmatch(version):
        return ["Use a stable X.Y.Z version without a v prefix or leading zeroes."]
    if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repository) or not SHA.fullmatch(commit):
        return ["Supply a GitHub owner/repository and the full source commit SHA."]
    token = os.environ.get("GH_TOKEN")
    if not token:
        return ["Set GH_TOKEN with repository read access to verify the release tag."]

    def get_json(path):
        request = Request(
            f"https://api.github.com/repos/{repository}/{path}",
            headers={"Authorization": f"Bearer {token}", "Accept": "application/vnd.github+json", "User-Agent": "Xanthos-release-check"},
        )
        with urlopen(request, timeout=30) as response:
            value = json.load(response)
        return json_object(value)

    reference = get_json(f"git/ref/tags/v{version}")
    reference_object = json_object(reference.get("object") or {})
    if reference_object.get("type") != "tag":
        return ["Create an annotated signed release tag with git tag -s."]
    tag_sha = reference_object.get("sha", "")
    if not isinstance(tag_sha, str) or not SHA.fullmatch(tag_sha):
        raise ValueError("Invalid tag object SHA.")
    tag = get_json(f"git/tags/{tag_sha}")
    return validate_tag(version, commit, reference, tag)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    selection = parser.add_mutually_exclusive_group(required=True)
    selection.add_argument("--version", help="Stable source version, without v")
    selection.add_argument("--check-all", action="store_true", help="Check all notes and require notes for VersionPrefix")
    parser.add_argument("--root", type=Path, default=ROOT, help="Repository root")
    parser.add_argument("--verify-tag", action="store_true", help="Require the published source tag to be verified on GitHub")
    parser.add_argument("--repository")
    parser.add_argument("--commit")
    args = parser.parse_args(argv)
    if args.verify_tag and not (args.version and args.repository and args.commit):
        parser.error("--verify-tag requires --version, --repository and --commit")
    if not args.verify_tag and (args.repository or args.commit):
        parser.error("--repository and --commit require --verify-tag")
    try:
        errors = check_files(args.root, args.version)
        if not errors and args.verify_tag:
            errors = verify_remote_tag(args.version, args.repository, args.commit)
    except (OSError, URLError, ValueError, ET.ParseError, TypeError, KeyError) as error:
        # Do not echo tokens, response bodies or contributor-controlled file content.
        print(f"Cannot verify release metadata ({type(error).__name__}).", file=sys.stderr)
        return 2
    for error in errors:
        print(f"Release metadata: {error}", file=sys.stderr)
    if errors:
        return 1
    print("Release notes and tag signature are valid." if args.verify_tag else "Release note format is valid.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
