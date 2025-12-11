#!/usr/bin/env python3

"""
Generate Markdown API documentation from the official JV-Link and JV-Data
specifications shipped under design/source-docs.
"""

from __future__ import annotations

import json
import re
import unicodedata
import xml.etree.ElementTree as ET
from collections import defaultdict
from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable, Dict, Iterable, List, Optional
from zipfile import ZipFile

BASE_DIR = Path(__file__).resolve().parent.parent
OFFICIAL_DIR = BASE_DIR / "design" / "source-docs"
API_DIR = BASE_DIR / "design" / "specs"

HTML_NS = {"x": "http://www.w3.org/1999/xhtml"}
SHEET_NS = {"m": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"}

METHOD_NAMES = [
    "JVInit",
    "JVSetUIProperties",
    "JVSetServiceKey",
    "JVSetSaveFlag",
    "JVSetSavePath",
    "JVOpen",
    "JVRTOpen",
    "JVStatus",
    "JVRead",
    "JVGets",
    "JVSkip",
    "JVCancel",
    "JVClose",
    "JVFiledelete",
    "JVFukuFile",
    "JVFuku",
    "JVMVCheck",
    "JVMVCheckWithType",
    "JVMVPlay",
    "JVMVPlayWithType",
    "JVMVOpen",
    "JVMVRead",
    "JVCourseFile",
    "JVCourseFile2",
    "JVWatchEvent",
    "JVWatchEventClose",
]


def normalize(text: Optional[str]) -> str:
    """Normalize text by applying NFKC and trimming whitespace."""
    return unicodedata.normalize("NFKC", text or "").strip()


def iter_text_from_html(html_path: Path) -> List[str]:
    """Extract a flat list of textual lines from the JV-Link HTML specification."""
    root = ET.parse(html_path).getroot()
    lines: List[str] = []

    def text_content(elem: ET.Element) -> str:
        parts: List[str] = []
        if elem.text:
            parts.append(elem.text)
        for child in elem:
            parts.append(text_content(child))
            if child.tail:
                parts.append(child.tail)
        return "".join(parts)

    def handle_table(table: ET.Element) -> List[str]:
        table_lines: List[str] = []
        for tr in table.findall(".//x:tr", HTML_NS):
            cells: List[str] = []
            for cell in tr.findall("x:td", HTML_NS) + tr.findall("x:th", HTML_NS):
                cell_text = normalize(text_content(cell))
                cells.append(cell_text)
            if cells:
                table_lines.append(" | ".join(cells))
        return table_lines

    body = root.find("x:body", HTML_NS)
    if body is None:
        return lines

    for elem in body.iter():
        tag = elem.tag.split("}", 1)[-1]
        if tag in {"h1", "h2", "h3", "h4", "h5", "h6"}:
            heading = normalize(text_content(elem))
            if heading:
                level = int(tag[1])
                lines.append("#" * level + " " + heading)
        elif tag in {"p", "li"}:
            content = normalize(text_content(elem))
            if content:
                lines.append(content)
        elif tag == "table":
            lines.extend(handle_table(elem))

    return [line for line in (normalize(line) for line in lines) if line]


def parse_methods(html_path: Path) -> List[Dict]:
    """Parse JV-Link method definitions into grouped structures."""
    raw_lines = iter_text_from_html(html_path)
    # Convert hierarchical text into a simple list of lines similar to the PDF order.
    processed: List[str] = []
    for line in raw_lines:
        processed.extend(line.splitlines())

    processed = [normalize(line) for line in processed if normalize(line)]

    # Merge fragments like "JVSetUIPropertie" + "s"
    method_set = set(METHOD_NAMES)

    merged: List[str] = []
    i = 0
    while i < len(processed):
        line = processed[i]
        if i + 1 < len(processed):
            combined = (line + processed[i + 1]).strip()
            if combined in method_set and line not in method_set:
                merged.append(combined)
                i += 2
                continue
        merged.append(line)
        i += 1

    # Find the second occurrence of JVInit which corresponds to method detail section.
    indices = [idx for idx, value in enumerate(merged) if value == "JVInit"]
    if len(indices) < 2:
        raise RuntimeError("Unable to locate method detail section.")
    detail_lines = merged[indices[1] :]

    methods: List[Dict] = []
    current: Optional[Dict] = None
    method_idx = 0
    i = 0
    while i < len(detail_lines) and method_idx < len(METHOD_NAMES):
        line = detail_lines[i]
        if line == METHOD_NAMES[method_idx]:
            if current:
                methods.append(current)
            current = {
                "name": line,
                "summary": "",
                "sections": {},
            }
            method_idx += 1
            i += 1
            # Skip empty lines
            while i < len(detail_lines) and not detail_lines[i]:
                i += 1
            if (
                i < len(detail_lines)
                and detail_lines[i] not in method_set
                and not detail_lines[i].startswith("【")
            ):
                current["summary"] = detail_lines[i]
                i += 1
            continue

        if current is None:
            i += 1
            continue

        if line.startswith("【") and line.endswith("】"):
            key = line[1:-1]
            i += 1
            block: List[str] = []
            while i < len(detail_lines):
                nxt = detail_lines[i]
                if not nxt:
                    block.append("")
                    i += 1
                    continue
                if nxt.startswith("【") and nxt.endswith("】"):
                    break
                if method_idx < len(METHOD_NAMES) and nxt == METHOD_NAMES[method_idx]:
                    break
                block.append(nxt)
                i += 1
            current["sections"][key] = "\n".join(block).strip()
            continue

        i += 1

    if current:
        methods.append(current)

    # Group related methods that share sections (e.g., *WithType variants).
    grouping = [
        ("JVMVCheck", "JVMVCheckWithType"),
        ("JVMVPlay", "JVMVPlayWithType"),
        ("JVWatchEvent", "JVWatchEventClose"),
    ]
    grouped_methods: List[Dict] = []
    grouped_names = set()
    method_lookup = {m["name"]: m for m in methods}

    for primary, secondary in grouping:
        if primary not in method_lookup or secondary not in method_lookup:
            continue
        grouped_names.add(primary)
        grouped_names.add(secondary)
        merged_sections = {}
        for key in set(
            list(method_lookup[primary]["sections"].keys())
            + list(method_lookup[secondary]["sections"].keys())
        ):
            merged_sections[key] = (
                method_lookup[primary]["sections"].get(key)
                or method_lookup[secondary]["sections"].get(key)
                or ""
            )
        grouped_methods.append(
            {
                "title": f"{primary} / {secondary}",
                "methods": [
                    {
                        "name": primary,
                        "summary": method_lookup[primary]["summary"]
                        or "See shared details below.",
                    },
                    {
                        "name": secondary,
                        "summary": method_lookup[secondary]["summary"]
                        or "See shared details below.",
                    },
                ],
                "sections": merged_sections,
            }
        )

    for name in METHOD_NAMES:
        if name in grouped_names:
            continue
        info = method_lookup.get(name)
        if not info:
            continue
        grouped_methods.append(
            {
                "title": name,
                "methods": [
                    {"name": name, "summary": info["summary"] or "See details below."}
                ],
                "sections": info["sections"],
            }
        )

    return grouped_methods


def load_workbook() -> Dict[str, List[List[str]]]:
    """Load the JV-Data workbook sheets into normalized row lists."""
    workbook_path = OFFICIAL_DIR / "JV-Data4901.xlsx"
    if not workbook_path.exists():
        raise FileNotFoundError(workbook_path)

    def col_to_index(col: str) -> int:
        idx = 0
        for ch in col:
            if ch.isalpha():
                idx = idx * 26 + (ord(ch) - 64)
        return idx

    sheets: Dict[str, List[List[str]]] = {}

    with ZipFile(workbook_path) as zf:
        shared_strings = ET.fromstring(zf.read("xl/sharedStrings.xml"))
        shared_values = [
            normalize("".join((t.text or "") for t in si.findall(".//m:t", SHEET_NS)))
            for si in shared_strings.findall("m:si", SHEET_NS)
        ]

        rels_root = ET.fromstring(zf.read("xl/_rels/workbook.xml.rels"))
        relationships = {
            rel.attrib["Id"]: rel.attrib["Target"]
            for rel in rels_root.findall(
                "{http://schemas.openxmlformats.org/package/2006/relationships}Relationship"
            )
        }

        workbook_root = ET.fromstring(zf.read("xl/workbook.xml"))
        for sheet in workbook_root.findall("m:sheets/m:sheet", SHEET_NS):
            name = sheet.attrib["name"]
            rel_id = sheet.attrib[
                "{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id"
            ]
            target = relationships[rel_id]
            sheet_xml = ET.fromstring(zf.read("xl/" + target))
            rows: List[List[str]] = []
            for row in sheet_xml.findall("m:sheetData/m:row", SHEET_NS):
                row_data: Dict[int, str] = {}
                for cell in row.findall("m:c", SHEET_NS):
                    ref = cell.attrib.get("r", "")
                    letters = "".join(ch for ch in ref if ch.isalpha()) or "A"
                    col_idx = col_to_index(letters)
                    cell_type = cell.attrib.get("t")
                    text = ""
                    if cell_type == "s":
                        v = cell.find("m:v", SHEET_NS)
                        if v is not None and v.text is not None:
                            text = shared_values[int(v.text)]
                    elif cell_type == "inlineStr":
                        t_elem = cell.find("m:is/m:t", SHEET_NS)
                        text = normalize(t_elem.text if t_elem is not None else "")
                    else:
                        v = cell.find("m:v", SHEET_NS)
                        text = normalize(v.text if v is not None else "")
                    row_data[col_idx] = normalize(text)
                if row_data:
                    max_idx = max(row_data.keys())
                    rows.append([row_data.get(i, "") for i in range(1, max_idx + 1)])
            sheets[name] = rows
    return sheets


def escape_cell(text: str) -> str:
    """Escape Markdown control characters inside table cells."""
    return text.replace("|", "\\|").replace("\n", "<br>")


def collapse_table_rows(
    rows: List[List[str]], is_new_row: Optional[callable] = None
) -> List[List[str]]:
    if not rows:
        return rows
    header = rows[0]
    collapsed = [header]
    current: Optional[List[str]] = None
    if is_new_row is None:
        is_new_row = lambda r: bool(r and r[0])  # type: ignore
    for row in rows[1:]:
        padded = row + [""] * max(0, len(header) - len(row))
        if is_new_row(row):
            current = padded[: len(header)]
            collapsed.append(current)
        elif current:
            for idx, value in enumerate(padded[: len(header)]):
                if value:
                    current[idx] = (current[idx] + " " + value).strip() if current[idx] else value
    return collapsed


def split_record_title(raw: str) -> Dict[str, str]:
    match = re.match(r"^(?P<num>\\d+)\\.(?P<title>.+)$", raw)
    if match:
        return {"number": match.group("num"), "title": match.group("title").strip()}
    return {"number": "", "title": raw.strip()}


def parse_record_formats(rows: List[List[str]]) -> List[Dict]:
    records: List[Dict] = []
    current: Optional[Dict] = None

    for row in rows:
        if not any(row):
            continue
        title_cell = row[1] if len(row) > 1 else ""
        if title_cell and len(row) > 6 and row[5] == "レコード長":
            details = split_record_title(title_cell)
            raw_title = title_cell
            records.append(
                {
                    "number": details["number"],
                    "title": details["title"],
                    "raw_title": raw_title,
                    "length": row[7] if len(row) > 7 else "",
                    "fields": [],
                }
            )
            current = records[-1]
            continue
        if title_cell.startswith("項番"):
            # Header row; nothing to store directly.
            continue
        if not current:
            continue
        no = row[1] if len(row) > 1 else ""
        field_name = row[4] if len(row) > 4 else ""
        if no or field_name:
            entry = {
                "no": no,
                "key": row[3] if len(row) > 3 else "",
                "name": field_name,
                "position": row[5] if len(row) > 5 else "",
                "repeat": row[6] if len(row) > 6 else "",
                "bytes": row[7] if len(row) > 7 else "",
                "total": row[8] if len(row) > 8 else "",
                "default": row[9] if len(row) > 9 else "",
                "description": row[10] if len(row) > 10 else "",
            }
            current["fields"].append(entry)
        elif current["fields"]:
            addition = " ".join(cell for cell in row[1:] if cell)
            if addition:
                current["fields"][-1]["description"] = (
                    (current["fields"][-1]["description"] + "\n" + addition).strip()
                )
    return records


def render_record_formats(records: List[Dict]) -> str:
    lines: List[str] = [
        "# JV-Data Record Formats",
        "",
        "Source: `JV-Data4901.xlsx` (official JV-Data specification).",
        "",
    ]
    for record in records:
        title = record.get("raw_title") or record["title"] or "Unnamed"
        length = record["length"]
        header = [
            "| No | Key | Field | Position | Repeat | Bytes | Total | Default | Description |",
            "| --- | --- | --- | --- | --- | --- | --- | --- | --- |",
        ]
        lines.append(f"## {title} (Record Length: {length} bytes)")
        lines.extend(header)
        for field in record["fields"]:
            row = "| {no} | {key} | {name} | {pos} | {rep} | {bytes} | {total} | {default} | {desc} |".format(
                no=escape_cell(field["no"]),
                key=escape_cell(field["key"]),
                name=escape_cell(field["name"]),
                pos=escape_cell(field["position"]),
                rep=escape_cell(field["repeat"]),
                bytes=escape_cell(field["bytes"]),
                total=escape_cell(field["total"]),
                default=escape_cell(field["default"]),
                desc=escape_cell(field["description"]),
            )
            lines.append(row)
        lines.append("")
    return "\n".join(lines).strip() + "\n"


def trim_leading_empty(cells: List[str]) -> List[str]:
    idx = 0
    while idx < len(cells) and not cells[idx]:
        idx += 1
    return cells[idx:]


def parse_generic_tables(rows: List[List[str]]) -> List[Dict]:
    tables: List[Dict] = []
    current: Optional[Dict] = None
    base_header: Optional[List[str]] = None

    for row in rows:
        if not any(row):
            continue
        label = row[1] if len(row) > 1 else ""
        if label and label[0].isdigit() and "." in label:
            current = {"title": label, "header": [], "rows": []}
            tables.append(current)
            base_header = None
            continue
        if not current:
            continue
        cells = trim_leading_empty(row)
        if not cells:
            continue
        if base_header is None and any("バイト数" in cell for cell in cells):
            base_header = [cell for cell in cells if cell]
            continue
        if current["header"] == []:
            header = [cell for cell in cells if cell] or base_header or ["Column 1"]
            if base_header and len(header) > len(base_header):
                header = base_header[:2] + header[2:]
            current["header"] = header
            continue
        current["rows"].append(cells)
    return tables


def render_generic_tables(tables: List[Dict], title: str) -> str:
    lines = [f"# {title}", "", f"Source: `JV-Data4901.xlsx` – {title}.", ""]
    for table in tables:
        lines.append(f"## {table['title']}")
        header = table["header"] or ["Column 1"]
        counts = [len(header)] + [len(row) for row in table["rows"]]
        col_count = max(counts) if counts else len(header)
        header = header + [""] * (col_count - len(header))
        lines.append("| " + " | ".join(escape_cell(cell) or " " for cell in header) + " |")
        lines.append("| " + " | ".join("---" for _ in range(col_count)) + " |")
        for row in table["rows"]:
            padded = row + [""] * (col_count - len(row))
            lines.append("| " + " | ".join(escape_cell(cell) for cell in padded) + " |")
        lines.append("")
    return "\n".join(lines).strip() + "\n"


def parse_data_type_list(rows: List[List[str]]) -> Dict[str, List[List[str]]]:
    sections: Dict[str, List[List[str]]] = defaultdict(list)
    current_key = "蓄積系データ"
    for row in rows:
        if not any(row):
            continue
        if row[0].startswith("(1)"):
            current_key = "蓄積系データ"
            continue
        if row[0].startswith("(2)"):
            current_key = "速報系データ"
            continue
        if row[0].startswith("(3)"):
            current_key = "リアルタイム系データ"
            continue
        if row[1].startswith("データ種別") and row[2] == "":
            # header row
            sections[current_key].append(["名称", "データ種別ID", "フォーマットNo", "レコード名称", "レコード種別ID", "収録内容"])
            continue
        cells = trim_leading_empty(row)
        if cells:
            sections[current_key].append(cells)
    return sections


def render_data_type_list(section_map: Dict[str, List[List[str]]]) -> str:
    lines = [
        "# JV-Data Dataset Catalogue",
        "",
        "Source: `JV-Data4901.xlsx` – データ種別一覧.",
        "",
    ]
    for key, rows in section_map.items():
        lines.append(f"## {key}")
        if not rows:
            lines.append("")
            continue
        header = rows[0]
        lines.append("| " + " | ".join(escape_cell(cell) for cell in header) + " |")
        lines.append("| " + " | ".join("---" for _ in header) + " |")
        for row in rows[1:]:
            padded = row + [""] * (len(header) - len(row))
            lines.append("| " + " | ".join(escape_cell(cell) for cell in padded[: len(header)]) + " |")
        lines.append("")
    return "\n".join(lines).strip() + "\n"


def parse_delivery_schedule(rows: List[List[str]]) -> Dict[str, List[List[str]]]:
    sections: Dict[str, List[List[str]]] = {}
    current_key: Optional[str] = None
    header = [
        "名称",
        "データ種別ID",
        "曜日",
        "時間",
        "備考",
        "提供単位",
        "提供期間",
    ]

    for row in rows:
        if not any(row):
            continue
        label = row[1] if len(row) > 1 else ""
        if label.startswith("(1)"):
            current_key = "蓄積系データ"
            sections[current_key] = [header]
            continue
        if label.startswith("(2)"):
            current_key = "速報系データ"
            sections[current_key] = [header]
            continue
        if label.startswith("(3)"):
            current_key = "リアルタイム系データ"
            sections[current_key] = [header]
            continue
        if current_key is None:
            continue
        data = [
            row[1] if len(row) > 1 else "",
            row[2] if len(row) > 2 else "",
            row[3] if len(row) > 3 else "",
            row[4] if len(row) > 4 else "",
            row[5] if len(row) > 5 else "",
            row[6] if len(row) > 6 else "",
            row[7] if len(row) > 7 else "",
        ]
        # Continuation rows append to previous entry.
        if (
            data[0] == ""
            and current_key in sections
            and len(sections[current_key]) > 1
        ):
            last = sections[current_key][-1]
            merged = []
            for prev, new in zip(last, data):
                if new:
                    merged.append((prev + "\n" + new).strip() if prev else new)
                else:
                    merged.append(prev)
            sections[current_key][-1] = merged
        else:
            sections.setdefault(current_key, [header]).append(data)
    return sections


def render_delivery_schedule(sections: Dict[str, List[List[str]]]) -> str:
    lines = [
        "# Data Delivery Timing",
        "",
        "Source: `JV-Data4901.xlsx` – データ提供タイミング・提供単位.",
        "",
    ]
    for key, rows in sections.items():
        if not rows:
            continue
        lines.append(f"## {key}")
        header = rows[0]
        lines.append("| " + " | ".join(escape_cell(cell) for cell in header) + " |")
        lines.append("| " + " | ".join("---" for _ in header) + " |")
        for row in rows[1:]:
            padded = row + [""] * (len(header) - len(row))
            lines.append("| " + " | ".join(escape_cell(cell) for cell in padded[: len(header)]) + " |")
        lines.append("")
    return "\n".join(lines).strip() + "\n"


def parse_properties(html_path: Path) -> List[Dict[str, str]]:
    root = ET.parse(html_path).getroot()
    body = root.find("x:body", HTML_NS)
    elements = list(body.iter())
    properties: Dict[str, Dict[str, str]] = {}

    within = False
    for elem in elements:
        tag = elem.tag.split("}", 1)[-1]
        text = normalize("".join(elem.itertext()).strip())
        heading_text = text.replace("．", ".")
        if tag in {"h2", "h3", "h4"} and heading_text.startswith("1.プロパティ"):
            within = True
            continue
        if within and tag in {"h2", "h3"} and text and not heading_text.startswith("1.プロパティ"):
            break
        if within and tag == "table":
            rows = []
            for tr in elem.findall(".//x:tr", HTML_NS):
                row = [
                    normalize(" ".join(td.itertext()))
                    for td in tr.findall("x:th", HTML_NS) + tr.findall("x:td", HTML_NS)
                ]
                rows.append([cell for cell in row if cell])
            for row in rows[1:]:
                if not row:
                    continue
                if len(row) >= 3:
                    type_name = row[0]
                    name = row[1]
                    description = " ".join(row[2:])
                elif len(row) == 2:
                    parts = row[0].split(" ", 1)
                    if len(parts) == 2:
                        type_name, name = parts
                    else:
                        type_name, name = "", row[0]
                    description = row[1]
                else:
                    continue
                key = name
                if key in properties:
                    properties[key]["description"] = " ".join(
                        filter(None, [properties[key]["description"], description])
                    )
                else:
                    properties[key] = {
                        "type": type_name,
                        "name": name,
                        "description": description,
                    }
    return list(properties.values())


def render_properties(properties: List[Dict[str, str]]) -> str:
    lines = [
        "# JV-Link Properties",
        "",
        "Source: `JV-Link4901` specification (v4.9.0.1).",
        "",
        "| Type | Name | Description |",
        "| --- | --- | --- |",
    ]
    for item in properties:
        lines.append(
            "| {type} | {name} | {desc} |".format(
                type=escape_cell(item.get("type", "")),
                name=escape_cell(item.get("name", "")),
                desc=escape_cell(item.get("description", "")),
            )
        )
    return "\n".join(lines).strip() + "\n"


def extract_methods_from_text(text: str) -> List[str]:
    normalized = normalize(text).replace("／", "/")
    candidates = [seg.strip() for seg in normalized.split("/") if seg.strip()]
    found: List[str] = []
    for segment in candidates:
        for method in METHOD_NAMES:
            if method in segment and method not in found:
                found.append(method)
    return found


def parse_error_codes(html_path: Path) -> List[Dict[str, str]]:
    root = ET.parse(html_path).getroot()
    body = root.find("x:body", HTML_NS)
    elements = list(body.iter())
    within = False
    current_methods: List[str] = []
    entries: List[Dict[str, str]] = []
    last_entry: Optional[Dict[str, str]] = None

    for elem in elements:
        tag = elem.tag.split("}", 1)[-1]
        text = normalize("".join(elem.itertext()).strip())
        heading_text = text.replace("．", ".")
        if tag in {"h2", "h3", "h4"} and heading_text.startswith("3.コード表"):
            within = True
            continue
        if within and tag in {"h1", "h2", "h3"} and text and not heading_text.startswith("3.コード表"):
            break
        if not within:
            continue
        if tag != "table":
            methods = extract_methods_from_text(text)
            if methods:
                current_methods = methods
            continue
        rows = []
        for tr in elem.findall(".//x:tr", HTML_NS):
            row = [
                normalize(" ".join(td.itertext()))
                for td in tr.findall("x:th", HTML_NS) + tr.findall("x:td", HTML_NS)
            ]
            rows.append(row)
        if not rows:
            continue
        for raw_row in rows[1:]:
            cells = [cell for cell in raw_row if cell]
            if not cells:
                continue
            if len(cells) == 1 and last_entry:
                last_entry["notes"] = " ".join(
                    filter(None, [last_entry["notes"], cells[0]])
                ).strip()
                continue
            code_cell = cells[0]
            meaning = cells[1] if len(cells) > 1 else ""
            notes = " ".join(cells[2:]) if len(cells) > 2 else ""
            codes = [code.strip() for code in code_cell.replace("、", " ").split() if code.strip()]
            if not codes:
                if last_entry:
                    last_entry["notes"] = " ".join(
                        filter(None, [last_entry["notes"], code_cell])
                    )
                continue
            applies = current_methods or ["General"]
            for code in codes:
                entry = {
                    "methods": ", ".join(applies),
                    "code": code,
                    "meaning": meaning,
                    "notes": notes,
                }
                entries.append(entry)
                last_entry = entry
    # Deduplicate while preserving order
    seen = set()
    unique_entries = []
    for entry in entries:
        key = (
            entry["methods"],
            entry["code"],
            entry["meaning"],
            entry["notes"],
        )
        if key not in seen:
            seen.add(key)
            unique_entries.append(entry)
    return unique_entries


def render_error_codes(entries: List[Dict[str, str]]) -> str:
    def method_order(methods_str: str) -> int:
        methods = [m.strip() for m in methods_str.split(",")]
        indices = [METHOD_NAMES.index(m) for m in methods if m in METHOD_NAMES]
        return min(indices) if indices else len(METHOD_NAMES)

    def code_key(code: str) -> tuple:
        try:
            return (0, float(code.replace("－", "-")))
        except Exception:
            return (1, code)

    sorted_entries = sorted(
        entries,
        key=lambda e: (method_order(e["methods"]), code_key(e["code"])),
    )
    lines = [
        "# JV-Link Error Codes",
        "",
        "Source: `JV-Link4901` specification (v4.9.0.1).",
        "",
        "| Method(s) | Code | Meaning | Notes |",
        "| --- | --- | --- | --- |",
    ]
    for entry in sorted_entries:
        lines.append(
            "| {methods} | {code} | {meaning} | {notes} |".format(
                methods=escape_cell(entry["methods"]),
                code=escape_cell(entry["code"]),
                meaning=escape_cell(entry["meaning"]),
                notes=escape_cell(entry["notes"]),
            )
        )
    return "\n".join(lines).strip() + "\n"


def excel_serial_to_date(value: str) -> str:
    try:
        serial = int(float(value))
    except ValueError:
        return value
    from datetime import datetime, timedelta

    base = datetime(1899, 12, 30)
    return (base + timedelta(days=serial)).strftime("%Y-%m-%d")


def parse_change_history(rows: List[List[str]]) -> List[Dict[str, str]]:
    entries: List[Dict[str, str]] = []
    headers_found = False
    current = ["", "", "", "", "", ""]

    for row in rows:
        normalized = [normalize(cell) for cell in row]
        if not headers_found:
            if len(normalized) > 1 and normalized[1] == "日付ヒヅケ":
                headers_found = True
            continue
        if not any(normalized):
            continue
        for idx in range(1, 7):
            if idx < len(normalized) and normalized[idx]:
                value = normalized[idx].replace("\n", " ").strip()
                current[idx - 1] = value
        if not current[5]:
            continue
        entry = {
            "date": excel_serial_to_date(current[0]),
            "version": current[1],
            "importance": current[2],
            "item": current[3],
            "page": current[4],
            "description": current[5],
        }
        entries.append(entry.copy())
    return entries


def render_change_history(entries: List[Dict[str, str]]) -> str:
    lines = [
        "# JV-Data Change History",
        "",
        "Source: `JV-Data4901.xlsx` – 変更履歴.",
        "",
        "| Date | Version | Important Change | Item No | Page | Description |",
        "| --- | --- | --- | --- | --- | --- |",
    ]
    for entry in entries:
        lines.append(
            "| {date} | {version} | {importance} | {item} | {page} | {desc} |".format(
                date=escape_cell(entry["date"]),
                version=escape_cell(entry["version"]),
                importance=escape_cell(entry["importance"]),
                item=escape_cell(entry["item"]),
                page=escape_cell(entry["page"]),
                desc=escape_cell(entry["description"]),
            )
        )
    return "\n".join(lines).strip() + "\n"


def parse_version_info(rows: List[List[str]]) -> Dict[str, str]:
    info = {"version": "", "updated": ""}
    import re

    for row in rows:
        for cell in row:
            text = normalize(cell)
            if not text:
                continue
            if text.startswith("Ver."):
                info["version"] = text
            match = re.search(r"(\d{4})年(\d{1,2})月(\d{1,2})日", text)
            if match:
                y, m, d = match.groups()
                info["updated"] = f"{int(y):04d}-{int(m):02d}-{int(d):02d}"
    return info


def parse_event_tables(html_path: Path) -> Dict[str, List[List[str]]]:
    root = ET.parse(html_path).getroot()
    body = root.find("x:body", HTML_NS)
    events_data: Dict[str, List[List[str]]] = {"callbacks": [], "parameters": []}
    for elem in body.findall(".//x:table", HTML_NS):
        rows = []
        for tr in elem.findall(".//x:tr", HTML_NS):
            row = [
                normalize(" ".join(td.itertext()))
                for td in tr.findall("x:th", HTML_NS) + tr.findall("x:td", HTML_NS)
            ]
            rows.append(row)
        if not rows:
            continue
        header = [cell for cell in rows[0] if cell]
        if header[:3] == ["種類", "イベントメソッド名", "説明"]:
            events_data["callbacks"] = collapse_table_rows(rows)
        elif header[:3] == ["イベントメソッド名", "パラメータ", "説明"]:
            events_data["parameters"] = collapse_table_rows(
                rows, is_new_row=lambda r: bool(r and r[0].startswith("JVEvt"))
            )
    return events_data


def render_event_callbacks(data: Dict[str, List[List[str]]]) -> str:
    lines = [
        "# JV-Link Event Callbacks",
        "",
        "Extracted from the `JVWatchEvent` section of `JV-Link4901`.",
        "",
    ]
    callbacks = data.get("callbacks", [])
    if callbacks:
        lines.append("## Event Types")
        header = callbacks[0]
        lines.append("| " + " | ".join(header) + " |")
        lines.append("| " + " | ".join("---" for _ in header) + " |")
        for row in callbacks[1:]:
            padded = row + [""] * (len(header) - len(row))
            lines.append("| " + " | ".join(escape_cell(cell) for cell in padded[: len(header)]) + " |")
        lines.append("")
    parameters = data.get("parameters", [])
    if parameters:
        lines.append("## Callback Parameters")
        header = parameters[0]
        lines.append("| " + " | ".join(header) + " |")
        lines.append("| " + " | ".join("---" for _ in header) + " |")
        for row in parameters[1:]:
            padded = row + [""] * (len(header) - len(row))
            lines.append("| " + " | ".join(escape_cell(cell) for cell in padded[: len(header)]) + " |")
        lines.append("")
    return "\n".join(lines).strip() + "\n"


def render_version_info(info: Dict[str, str]) -> str:
    lines = [
        "# Specification Version",
        "",
        "Source: `JV-Data4901.xlsx` – 表紙シート.",
        "",
    ]
    if info.get("version"):
        lines.append(f"- **Version:** {info['version']}")
    if info.get("updated"):
        lines.append(f"- **Updated:** {info['updated']}")
    if len(lines) == 4:
        lines.append("- No version metadata found.")
    return "\n".join(lines).strip() + "\n"


def parse_special_notes(rows: List[List[str]]) -> List[str]:
    paragraphs: List[str] = []
    for row in rows:
        text = " ".join(cell for cell in row if cell).strip()
        if text:
            paragraphs.append(text)
    return paragraphs


def render_special_notes(paragraphs: List[str]) -> str:
    lines = [
        "# JV-Data Special Notes",
        "",
        "Source: `JV-Data4901.xlsx` – 特記事項.",
        "",
    ]
    lines.extend(paragraphs)
    return "\n".join(lines).strip() + "\n"


def write_methods_markdown(groups: List[Dict]) -> str:
    lines = [
        "# JV-Link Method Reference",
        "",
        "Source: `JV-Link4901` specification (v4.9.0.1).",
        "",
    ]
    key_map = {
        "構文": "Syntax",
        "パラメータ": "Parameters",
        "戻り値": "Return Value",
        "解説": "Explanation",
        "補足": "Notes",
        "イベント": "Event Overview",
        "イベント構文": "Event Callback Signature",
        "イベント使用方法": "Sample Usage",
    }
    for group in groups:
        lines.append(f"## {group['title']}")
        lines.append("")
        for method in group["methods"]:
            lines.append(f"- **{method['name']}** — {method['summary']}")
        lines.append("")
        for key, body in group["sections"].items():
            if not body:
                continue
            heading = key_map.get(key, key)
            lines.append(f"### {heading}")
            if heading == "Syntax" or "構文" in key:
                lines.append("```text")
                lines.extend(body.splitlines())
                lines.append("```")
            else:
                lines.extend(body.splitlines())
            lines.append("")
    return "\n".join(lines).strip() + "\n"


def ensure_output_dir() -> None:
    API_DIR.mkdir(parents=True, exist_ok=True)


def main() -> None:
    ensure_output_dir()
    method_groups = parse_methods(OFFICIAL_DIR / "JV-Link4901.html")
    properties = parse_properties(OFFICIAL_DIR / "JV-Link4901.html")
    error_codes = parse_error_codes(OFFICIAL_DIR / "JV-Link4901.html")
    event_tables = parse_event_tables(OFFICIAL_DIR / "JV-Link4901.html")
    sheets = load_workbook()

    records = parse_record_formats(sheets.get("フォーマット", []))
    code_tables = parse_generic_tables(sheets.get("コード表", []))
    data_types = parse_data_type_list(sheets.get("データ種別一覧", []))
    delivery_sections = parse_delivery_schedule(sheets.get("データ提供タイミング･提供単位", []))
    special_notes = parse_special_notes(sheets.get("特記事項", []))
    change_history = parse_change_history(sheets.get("変更履歴", []))
    version_info = parse_version_info(sheets.get("表紙", []))

    (API_DIR / "methods.md").write_text(write_methods_markdown(method_groups), encoding="utf-8")
    (API_DIR / "properties.md").write_text(render_properties(properties), encoding="utf-8")
    (API_DIR / "error_codes.md").write_text(render_error_codes(error_codes), encoding="utf-8")
    (API_DIR / "events.md").write_text(render_event_callbacks(event_tables), encoding="utf-8")
    (API_DIR / "records.md").write_text(render_record_formats(records), encoding="utf-8")
    (API_DIR / "code_tables.md").write_text(
        render_generic_tables(code_tables, "JV-Data Code Tables"),
        encoding="utf-8",
    )
    (API_DIR / "data_types.md").write_text(
        render_data_type_list(data_types), encoding="utf-8"
    )
    (API_DIR / "delivery_schedule.md").write_text(
        render_delivery_schedule(delivery_sections),
        encoding="utf-8",
    )
    (API_DIR / "special_notes.md").write_text(
        render_special_notes(special_notes),
        encoding="utf-8",
    )
    (API_DIR / "change_history.md").write_text(
        render_change_history(change_history),
        encoding="utf-8",
    )
    (API_DIR / "version.md").write_text(
        render_version_info(version_info),
        encoding="utf-8",
    )

    summary = {
        "methods": len(method_groups),
        "properties": len(properties),
        "error_codes": len(error_codes),
        "records": len(records),
        "code_tables": len(code_tables),
        "data_types_sections": len(data_types),
        "delivery_sections": len(delivery_sections),
        "special_notes_paragraphs": len(special_notes),
        "change_history_entries": len(change_history),
    }
    print(json.dumps(summary, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
