# Source Documents

This directory is for placing official documents provided by JRA-VAN Data Lab.

## Required Documents

The following documents are not included in this repository due to copyright restrictions.
To develop, please obtain them from JRA-VAN Data Lab. and place them in this directory.

### JV-Link Interface Specification

- **Source**: [JRA-VAN Data Lab.](https://jra-van.jp/dlb/)
- **Format**: PDF
- **Conversion steps**:
  1. Export to HTML format using Adobe Acrobat
  2. Output filename: `JV-Link<version>.html` (e.g., `JV-Link4901.html`)
  3. Image files will be placed in `JV-Link<version>_files/` directory

### JV-Data Specification (Excel Version)

- **Source**: [JRA-VAN Data Lab.](https://jra-van.jp/dlb/)
- **Format**: Excel (.xlsx)
- **Filename**: `JV-Data<version>.xlsx` (e.g., `JV-Data4901.xlsx`)

> **Note**: `<version>` is the version number (e.g., Ver.4.9.0.1 → `4901`).
> When using the latest version, please substitute the filenames accordingly.

## Directory Structure

After placing the documents, the structure will be as follows (example for version 4.9.0.1):

```
source-docs/
├── README.md
├── JV-Link4901.html          # HTML converted from PDF
├── JV-Link4901.txt           # Text extracted by scripts/
├── JV-Link4901_files/        # Image files in HTML
│   └── Image_*.png/jpg
└── JV-Data4901.xlsx          # Excel specification
```

## Conversion Scripts

The `scripts/` directory contains Python scripts for parsing these documents and generating Markdown files in `design/specs/`.
