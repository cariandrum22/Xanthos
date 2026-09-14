"""Reviewable VSTest inventory. Python 3 standard library only.

The display name is retained verbatim, including every Theory argument. Updating
the checked-in inventory is an explicit review operation, never a CI fallback.
"""
import argparse
import collections
import hashlib
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def write_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def case_id(project, fqn, display):
    key = "\n".join((project, fqn, display))
    return "T-" + hashlib.sha256(key.encode("utf-8")).hexdigest()[:20]


def read_trx(path, project):
    root = ET.parse(path).getroot()
    definitions = {}
    for node in root.findall("t:TestDefinitions/t:UnitTest", NS):
        method = node.find("t:TestMethod", NS)
        definitions[node.attrib["id"]] = method.attrib["className"] + "." + method.attrib["name"]
    cases = []
    for result in root.findall("t:Results/t:UnitTestResult", NS):
        display = result.attrib["testName"]
        fqn = definitions[result.attrib["testId"]]
        cases.append({"id": case_id(project, fqn, display), "project": project,
                      "fqn": fqn, "displayName": display,
                      "outcome": result.attrib["outcome"],
                      "duration": result.attrib.get("duration"),
                      "message": result.findtext("t:Output/t:ErrorInfo/t:Message", "", NS)})
    if not cases:
        raise ValueError(f"No individual test results: {path}")
    times = root.find("t:Times", NS)
    return cases, {"source": str(path), "sha256": hashlib.sha256(Path(path).read_bytes()).hexdigest(),
                   "times": times.attrib, "counts": dict(collections.Counter(c["outcome"] for c in cases)),
                   "discovered": len(cases)}


def discovery(path):
    # Only adapter-indented names; do not remove parameter lists or normalize data.
    lines = Path(path).read_text(encoding="utf-8-sig").splitlines()
    return [line[4:] for line in lines if line.startswith("    Xanthos.")]


def validate(plan, discoveries):
    cases = plan["cases"]
    ids = [c["id"] for c in cases]
    if len(ids) != len(set(ids)):
        raise ValueError("Duplicate logical case ID")
    identities = [(c["project"], c["displayName"]) for c in cases]
    if len(identities) != len(set(identities)):
        raise ValueError("Ambiguous result identity")
    for case in cases:
        if case["id"] != case_id(case["project"], case["fqn"], case["displayName"]):
            raise ValueError(f"FQN/identity mismatch: {case['id']}")
        # Theory data follow the exact method name; plus is xUnit's nested type separator.
        if not (case["displayName"] == case["fqn"] or
                case["displayName"].startswith(case["fqn"] + "(") or
                case["displayName"].startswith(case["fqn"] + "<")):
            raise ValueError(f"FQN not present in discovery identity: {case['fqn']}")
        if not case["groups"] or not case["profiles"] or not case["tfm"] or not case["os"]:
            raise ValueError(f"Unclassified case: {case['id']}")
        if case["requirement"] == "optional-fixture" and not case.get("skipReason"):
            raise ValueError(f"Optional case without exact reason: {case['id']}")
    for project, names in discoveries.items():
        if len(names) != len(set(names)):
            raise ValueError(f"Ambiguous discovery: {project}")
        expected = {c["displayName"] for c in cases if c["project"] == project}
        actual = set(names)
        if actual != expected:
            raise ValueError(f"Discovery mismatch {project}: missing={sorted(actual-expected)[:4]}, obsolete={sorted(expected-actual)[:4]}")
    if set(discoveries) != {c["project"] for c in cases}:
        raise ValueError("Missing project discovery")
    optional = sorted(c["id"] for c in cases if c["requirement"] == "optional-fixture")
    if optional != sorted(plan["optionalSkipAllowlist"]):
        raise ValueError("Optional skip allowlist differs from classified cases")
    known = set(ids)
    for contract in plan["contracts"]:
        for guarantee in contract["guarantees"]:
            if not guarantee.get("caseIds") and not guarantee.get("outOfScopeReason"):
                raise ValueError(f"Unmapped guarantee: {contract['id']}")
            if not set(guarantee.get("caseIds", [])) <= known:
                raise ValueError(f"Unknown case in contract: {contract['id']}")
    return {"status": "pass", "cases": len(cases), "optional": len(optional), "contracts": len(plan["contracts"])}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--plan", default="tests/test-plan.json")
    parser.add_argument("--discovery", action="append", required=True, metavar="PROJECT=PATH")
    parser.add_argument("--output")
    args = parser.parse_args()
    inputs = dict(item.split("=", 1) for item in args.discovery)
    result = validate(read_json(args.plan), {p: discovery(f) for p, f in inputs.items()})
    if args.output:
        write_json(args.output, result)
    print(json.dumps(result))


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, OSError, ET.ParseError) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        sys.exit(1)
