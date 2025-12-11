import re
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "design" / "specs" / "error_codes.md"
TARGET = ROOT / "src" / "Xanthos" / "Core" / "ErrorCatalog.fs"

if not SOURCE.exists():
    raise SystemExit(f"Missing specification file: {SOURCE}")

CATEGORY_KEYWORDS = [
    ("input", ["パラメータ", "入力", "spec", "option", "key", "fromtime", "filepath", "dataspec", "parameter"]),
    ("authentication", ["認証", "利用キー", "同意", "有効期限", "license", "authentication"]),
    ("maintenance", ["メンテナンス", "maintenance"]),
    ("download", ["ダウンロード", "通信", "サーバー", "download", "server"]),
    ("internal", ["内部エラー", "internal", "レジストリ"]),
    ("state", ["行なわれていない", "呼ばれていない", "オープン中", "closed", "open", "state"])
]

CODE_MESSAGES = {
    -504: "Service is currently under maintenance.",
    -503: "Required file or temporary file was deleted before JVLink could process it.",
    -502: "Download failed because of a communication or disk error.",
    -501: "Setup media (CD/DVD) is invalid or missing.",
    -431: "Server reported an internal error.",
    -421: "Server returned a malformed response.",
    -413: "Server returned HTTP 403/other restricted status.",
    -412: "Server returned HTTP 403 Forbidden.",
    -411: "Server returned HTTP 404 or registry contents are invalid.",
    -403: "Downloaded data is corrupted.",
    -402: "Downloaded file has an invalid size.",
    -401: "JV-Link reported an internal error.",
    -305: "User agreement has not been accepted.",
    -304: "Movie license state is invalid.",
    -303: "Service key is not configured.",
    -302: "Service key has expired.",
    -301: "Authentication failure (invalid or duplicated service key).",
    -211: "Registry values are invalid or JVInit has not been executed.",
    -203: "JVOpen was not executed before the current call.",
    -202: "Previous JVOpen/JVRTOpen/JVMVOpen session is still open.",
    -201: "JVInit was not executed before the current call.",
    -118: "File path parameter is invalid or the directory does not exist.",
    -116: "Option and dataspec combination is invalid.",
    -115: "Option parameter is invalid.",
    -114: "Key parameter is invalid.",
    -113: "Fromtime (end) parameter is invalid.",
    -112: "Fromtime (start) parameter is invalid.",
    -111: "Dataspec parameter is invalid.",
    -103: "SID begins with a space.",
    -102: "SID exceeds 64 bytes.",
    -101: "SID is missing.",
    -100: "UI configuration was cancelled or could not be persisted.",
    -3: "Target files are still downloading.",
    -2: "Setup dialog was cancelled by the user.",
    -1: "No matching data exists for the current parameters.",
    0: "Operation completed successfully.",
    1: "Victory silks image was created successfully."
}

METHOD_MESSAGE_OVERRIDES = {
    -503: {
        "JVCLOSE": "Target file was already removed; closure can continue."
    },
    -411: {
        "JVFUKU": "Server returned HTTP 404/Not Found for the requested resource.",
        "JVMVPLAY": "Server returned HTTP 404/Not Found for the requested movie.",
        "JVMVPLAYWITHTYPE": "Server returned HTTP 404/Not Found for the requested movie."
    },
    -201: {
        "JVCLOSE": "JVInit must be executed before JVClose.",
        "JVCOURSEFILE": "JVInit must be executed before requesting course data.",
        "JVCOURSEFILE2": "JVInit must be executed before requesting course data.",
        "JVFUKU": "JVInit must be executed before requesting silks data.",
        "JVMVCHECK": "JVInit must be executed before calling movie APIs.",
        "JVMVCHECKWITHTYPE": "JVInit must be executed before calling movie APIs.",
        "JVMVOPEN": "JVInit must be executed before opening movie lists.",
        "JVMVPLAY": "JVInit must be executed before playing movies.",
        "JVMVPLAYWITHTYPE": "JVInit must be executed before playing movies.",
        "JVMVREAD": "JVInit must be executed before reading movie lists.",
        "JVOPEN": "JVInit must be executed before JVOpen.",
        "JVSETUIPROPERTIES": "JVInit must be executed before configuring UI settings."
    },
    -1: {
        "JVCLOSE": "File boundary reached; continue with the next file.",
        "JVOPEN": "File boundary reached; continue reading.",
        "JVREAD": "File boundary reached; continue reading.",
        "JVGETS": "File boundary reached; continue reading."
    },
    0: {
        "JVSETUIPROPERTIES": "Settings saved successfully.",
        "JVOPEN": "All files processed successfully."
    }
}


def infer_category(text: str) -> str:
    lower = text.lower()
    for category, keywords in CATEGORY_KEYWORDS:
        if any(keyword.lower() in lower for keyword in keywords):
            return category
    return "other"


def normalize_methods(raw: str) -> list[str]:
    parts = re.split(r"[,/]\s*|\s+and\s+|、|，", raw)
    cleaned = [p.strip() for p in parts if p.strip()]
    return cleaned or ["(Unknown)"]


def normalize_code(raw: str) -> int | None:
    text = raw.replace("―", "-").replace("−", "-")
    match = re.search(r"-?\d+", text)
    return int(match.group(0)) if match else None


def insert_sentence_breaks(text: str) -> str:
    """Insert 。 at sentence boundaries that were lost during PDF extraction.

    Common patterns where line breaks became missing periods:
    - 失敗既に -> 失敗。既に
    - 不正既に -> 不正。既に
    - されている次 -> されている。次
    - ください次 -> ください。次
    """
    # Patterns: sentence-ending word followed by sentence-starting word (no existing punctuation)
    # Don't add period if one already exists
    patterns = [
        # 失敗 + new sentence start
        (r'(失敗)(?=[^\u3002。])(既に|また|この|その|正しく|サンプル|JV|パラメータ|利用|レジストリ)', r'\1。'),
        # 不正 + new sentence start (but not 不正あるいは which is a continuation)
        (r'(不正)(?!あるいは)(?=[^\u3002。])(既に|また|この|その|正しく|サンプル|JV|パラメータ|利用|レジストリ|データ)', r'\1。'),
        # されている + new sentence start
        (r'(されている)(?=[^\u3002。])(既に|また|この|その|正しく|サンプル|JV|パラメータ|利用|レジストリ)', r'\1。'),
        # ください + new sentence start
        (r'(ください)(?=[^\u3002。])(既に|また|この|その|正しく|サンプル|JV|パラメータ|利用|レジストリ)', r'\1。'),
        # です/ます + new sentence start
        (r'(です|ます)(?=[^\u3002。])(既に|また|この|その|正しく|サンプル|JV|パラメータ|利用|レジストリ)', r'\1。'),
    ]
    result = text
    for pattern, replacement in patterns:
        result = re.sub(pattern, replacement, result)
    return result


def normalize_text(raw: str) -> str:
    cleaned = (raw or "").replace("同上同上", "").replace("同上", "")
    cleaned = re.sub(r"(?<=[\u3040-\u30FF\u4E00-\u9FFF])\s+(?=[\u3040-\u30FF\u4E00-\u9FFF])", "", cleaned)
    cleaned = re.sub(r"(?<=[\u3040-\u30FF\u4E00-\u9FFF])\s+(?=[A-Za-z0-9])", "", cleaned)
    cleaned = re.sub(r"(?<=[A-Za-z0-9])\s+(?=[\u3040-\u30FF\u4E00-\u9FFF])", "", cleaned)
    cleaned = re.sub(r"\s+\(", "(", cleaned)
    cleaned = re.sub(r"\(\s+", "(", cleaned)
    cleaned = re.sub(r"\s+\)", ")", cleaned)
    cleaned = re.sub(r"\)\s+", ")", cleaned)
    cleaned = re.sub(r"\s+", " ", cleaned)
    cleaned = insert_sentence_breaks(cleaned)
    return cleaned.strip()


def normalize_doc(meaning: str, notes: str) -> str:
    combined = " ".join(part for part in (meaning, notes) if part)
    return normalize_text(combined)


records: list[dict] = []

with SOURCE.open(encoding="utf-8") as handle:
    for line in handle:
        row = line.strip()
        if not row.startswith("|"):
            continue
        parts = [segment.strip() for segment in row.split("|")[1:-1]]
        if len(parts) < 3:
            continue
        if parts[0].startswith("Method") or parts[1] in ("Code", "コード"):
            continue
        methods_raw, code_raw, meaning_raw = parts[:3]
        notes = parts[3] if len(parts) > 3 else ""
        code = normalize_code(code_raw)
        if code is None:
            continue
        methods = normalize_methods(methods_raw)
        category = infer_category(f"{meaning_raw} {notes}")
        meaning = normalize_text(meaning_raw)
        notes_clean = normalize_text(notes)
        documentation = normalize_doc(meaning_raw, notes)
        records.append(
            {
                "methods": methods,
                "code": code,
                "category": category,
                "meaning": meaning,
                "notes": notes_clean,
                "documentation": documentation,
            }
        )

catalog: dict[int, dict] = {}

for record in records:
    methods = record["methods"]
    code = record["code"]
    category = record["category"]
    documentation = record["documentation"]
    method_keys = {m.upper() for m in methods}
    if code not in CODE_MESSAGES:
        raise SystemExit(f"Missing English message for code {code}")
    method_message_map = {
        key.upper(): value
        for key, value in METHOD_MESSAGE_OVERRIDES.get(code, {}).items()
    }
    if code not in catalog:
        catalog[code] = {
            "base_category": category,
            "base_message": CODE_MESSAGES[code],
            "base_doc": documentation,
            "methods": set(method_keys),
            "overrides": {}
        }
    entry = catalog[code]
    entry["methods"].update(method_keys)
    for method in method_keys:
        override = entry["overrides"].setdefault(method, {"category": None, "message": None, "doc": None})
        if category != entry["base_category"]:
            override["category"] = category
        if method in method_message_map:
            override["message"] = method_message_map[method]
        if documentation and documentation != entry["base_doc"]:
            override["doc"] = documentation

sorted_codes = sorted(catalog.keys())

header = """namespace Xanthos.Core

open System
open System.Collections.Generic

/// Auto-generated from design/specs/error_codes.md; do not edit by hand.
type JvErrorCategory =
    | Input
    | Authentication
    | Maintenance
    | Download
    | Internal
    | State
    | Other

type JvErrorOverride =
    { Category: JvErrorCategory option
      Message: string option
      Documentation: string option }

type JvErrorBase =
    { Code: int
      Category: JvErrorCategory
      Message: string
      Documentation: string }

type JvErrorInfo =
    { Base: JvErrorBase
      Methods: string list
      Overrides: Map<string, JvErrorOverride> }

module ErrorCatalog =
    let entries : JvErrorInfo array = [|
"""

def format_category(cat: str) -> str:
    return cat.capitalize()

def quote(text: str) -> str:
    return text.replace("\\", "\\\\").replace("\"", "\\\"")


body_lines = []
for code in sorted_codes:
    entry = catalog[code]
    methods_expr = "[ " + "; ".join(f"\"{name}\"" for name in sorted(entry["methods"])) + " ]"
    overrides_filtered = {
        method: data
        for method, data in entry["overrides"].items()
        if data["category"] is not None or data["message"] is not None
    }
    if overrides_filtered:
        override_items = []
        for method, data in sorted(overrides_filtered.items()):
            category_expr = (
                f"Some JvErrorCategory.{format_category(data['category'])}"
                if data["category"] else "None"
            )
            message_expr = (
                f"Some \"{quote(data['message'])}\""
                if data["message"] else "None"
            )
            doc_expr = (
                f"Some \"{quote(data['doc'])}\""
                if data["doc"] else "None"
            )
            override_items.append(
                f"(\"{method}\", {{ Category = {category_expr}; Message = {message_expr}; Documentation = {doc_expr} }})"
            )
        overrides_expr = "Map.ofList [ " + "; ".join(override_items) + " ]"
    else:
        overrides_expr = "Map.empty"
    body_lines.append(
        f"        {{ Base = {{ Code = {code}; Category = JvErrorCategory.{format_category(entry['base_category'])}; Message = \"{quote(entry['base_message'])}\"; Documentation = \"{quote(entry['base_doc'])}\" }}; Methods = {methods_expr}; Overrides = {overrides_expr} }};"
    )

footer = """    |]

    let private index =
        entries
        |> Array.collect (fun info ->
            info.Methods
            |> List.toArray
            |> Array.map (fun m -> ((m.ToUpperInvariant(), info.Base.Code), info)))
        |> dict

    let tryFind methodName code =
        let normalizedName =
            if String.IsNullOrWhiteSpace methodName then "(UNKNOWN)" else methodName
        let key = (normalizedName.ToUpperInvariant(), code)
        match index.TryGetValue key with
        | true, info -> Some info
        | _ ->
            entries |> Array.tryFind (fun info -> info.Base.Code = code)
"""

TARGET.write_text(header + "\n".join(body_lines) + footer, encoding="utf-8")
print(f"Generated {TARGET.relative_to(ROOT)} with {len(sorted_codes)} entries")


def escape_markdown(text: str) -> str:
    return text.replace("|", "\\|")


sanitized_lines = [
    "# JV-Link Error Codes",
    "",
    "Source: `JV-Link4901` specification (v4.9.0.1).",
    "",
    "| Method(s) | Code | Meaning | Notes |",
    "| --- | --- | --- | --- |",
]

for record in records:
    methods = ", ".join(record["methods"])
    sanitized_lines.append(
        f"| {methods} | {record['code']} | {escape_markdown(record['meaning'])} | {escape_markdown(record['notes'])} |"
    )

SOURCE.write_text("\n".join(sanitized_lines) + "\n", encoding="utf-8")
print(f"Normalized {SOURCE.relative_to(ROOT)}")
