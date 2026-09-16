#!/usr/bin/env python3
"""Validate GitHub contribution metadata without executing contributor content."""

import argparse
import json
import re
import sys
from pathlib import Path


SECTIONS = {
    "issue": (3, ("Summary", "Details", "Expected outcome")),
    "pull_request": (2, ("Summary", "Changes", "Validation", "Related issues")),
}
PR_TITLE = re.compile(
    r"^(feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert|release)"
    r"(?:\([a-zA-Z0-9][a-zA-Z0-9._/-]*\))?!?: \S(?:[^\r\n]*\S)?$"
)
PLACEHOLDERS = {"", "_no response_", "no response", "tbd", "todo", "n/a", "..."}


def headings(body):
    """Yield ATX headings outside HTML comments and fenced code blocks."""
    body = re.sub(r"<!--.*?(?:-->|\Z)", "", body, flags=re.S)
    lines = body.splitlines()
    fence = None
    found = []
    for index, line in enumerate(lines):
        marker = re.match(r"^ {0,3}(`{3,}|~{3,})(.*)$", line)
        if fence:
            if marker and marker[1][0] == fence[0] and len(marker[1]) >= fence[1] and not marker[2].strip():
                fence = None
            continue
        if marker:
            fence = (marker[1][0], len(marker[1]))
            continue
        match = re.match(r"^ {0,3}(#{1,6})\s+(.+?)\s*$", line)
        if match:
            name = re.sub(r"\s+#+\s*$", "", match[2])
            found.append((index, len(match[1]), name))
    return lines, found


def validate(kind, title, body):
    """Return actionable errors; never include untrusted text in CI diagnostics."""
    errors = []
    if not title.strip():
        errors.append("A descriptive title is required.")
    elif kind == "pull_request" and not PR_TITLE.fullmatch(title):
        errors.append("Use a Conventional Commit PR title, for example 'fix(records): decode CP932 aliases' or 'release: v0.3.0'.")

    level, required = SECTIONS[kind]
    lines, found = headings(body)
    canonical = [(index, name) for index, depth, name in found if depth == level and name in required]
    prefix = "#" * level
    for name in required:
        matches = [index for index, heading in canonical if heading == name]
        if len(matches) != 1:
            errors.append(f"Include exactly one '{prefix} {name}' heading.")
            continue
        start = matches[0]
        end = next((index for index, depth, _ in found if index > start and depth <= level), len(lines))
        content = "\n".join(lines[start + 1:end]).strip()
        if content.lower().rstrip(".") in PLACEHOLDERS:
            errors.append(f"Fill in '{name}'; comments and placeholders do not count.")

    if len(canonical) == len(required) and set(name for _, name in canonical) == set(required):
        if tuple(name for _, name in canonical) != required:
            errors.append("Keep the required headings in the documented order.")
    return errors


def event_metadata(event):
    for kind in ("pull_request", "issue"):
        item = event.get(kind)
        if isinstance(item, dict):
            title, body = item.get("title"), item.get("body")
            if not isinstance(title, str) or (body is not None and not isinstance(body, str)):
                raise ValueError("Event title/body must be strings (a null body is treated as empty).")
            return kind, title, body or ""
    raise ValueError("Expected an issue or pull_request event object.")


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--event", type=Path, help="GitHub event JSON file")
    source.add_argument("--kind", choices=SECTIONS)
    parser.add_argument("--title", help="Contribution title for local validation")
    parser.add_argument("--body-file", type=Path, help="UTF-8 Markdown for local validation")
    args = parser.parse_args(argv)
    if args.event and (args.title is not None or args.body_file):
        parser.error("--event cannot be combined with --title or --body-file")
    if args.kind and (args.title is None or args.body_file is None):
        parser.error("--kind requires --title and --body-file")
    try:
        if args.event:
            event = json.loads(args.event.read_text(encoding="utf-8-sig"))
            if not isinstance(event, dict):
                raise ValueError("Event JSON must be an object.")
            kind, title, body = event_metadata(event)
        else:
            kind, title = args.kind, args.title
            body = args.body_file.read_text(encoding="utf-8-sig")
    except (OSError, ValueError) as error:
        # Do not echo event content into workflow commands or logs.
        print(f"Cannot read contribution metadata ({type(error).__name__}).", file=sys.stderr)
        return 2
    errors = validate(kind, title, body)
    for error in errors:
        print(f"Contribution format: {error}", file=sys.stderr)
    if errors:
        return 1
    print("Contribution format is valid.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
