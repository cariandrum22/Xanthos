"""Extract local SDK contracts. Proprietary documents and generated inventories stay local.

Requires pypdf and openpyxl in the invoking Python environment. The repository's
F# parser definitions are deliberately not an input to this specification oracle.
"""
import argparse
import hashlib
import json
import re
import unicodedata
from pathlib import Path

API_FILE = 'JV-Linkインターフェース仕様書_4.9.0.1(Win).pdf'
DATA_FILE = 'JV-Data仕様書_4.9.0.1.xlsx'
HASHES = {
    API_FILE: 'dfd1c425a62304bb464f15c25106e030ffccbf99c7c777972d6bb6b6d27ef1d7',
    DATA_FILE: '23bafd375f704acbdd696b5032ac1619f17d47e882587d6e7954b610527a8234',
    'JV-Data仕様書_4.9.0.1.pdf': 'b6c21aae4ccbba6a71c5e8065609c4fbb1ccee826c16e7d99ca6ecf7a4101522',
    '蓄積系提供データ一覧.xls': '6658f662f6eefe51c3eb7b755ca92a0405f222ac01c4cfe45e1969ca0a51299d',
}
# Planned public names are tracked separately from implementation verification.
METHODS = dict(zip(
    'JVInit JVSetUIProperties JVSetServiceKey JVSetSaveFlag JVSetSavePath JVOpen JVRTOpen JVStatus JVRead JVGets JVSkip JVCancel JVClose JVFiledelete JVFukuFile JVFuku JVMVCheck JVMVCheckWithType JVMVPlay JVMVPlayWithType JVMVOpen JVMVRead JVCourseFile JVCourseFile2 JVWatchEvent JVWatchEventClose'.split(),
    'init configureUi setServiceKey setSaveFlag setSavePath openData openRealtime status read gets skip cancel closeData deleteFile silksFile silksBinary movieCheck movieCheckWithType moviePlay moviePlayWithType movieOpen movieRead courseFile courseFile2 watchEvent watchEventClose'.split(),
))
PROPERTIES = {
    'm_saveflag': ('Integer', 'getSaveFlag'), 'm_savepath': ('String', 'getSavePath'),
    'm_servicekey': ('String', 'getServiceKey'), 'm_JVLinkVersion': ('String', 'getVersion'),
    'm_TotalReadFilesize': ('Long', 'getTotalReadFileSize'),
    'm_CurrentReadFilesize': ('Long', 'getCurrentReadFileSize'),
    'm_CurrentFileTimestamp': ('String', 'getCurrentFileTimestamp'),
    'ParentHWnd': ('Long', 'setParentWindowHandle'), 'm_payflag': ('Integer', 'getPayFlag'),
}
EVENTS = ['Pay', 'JockeyChange', 'Weather', 'CourseChange', 'Avoid', 'TimeChange', 'Weight']
RECORD_IDS = 'TK RA SE HR H1 H6 O1 O2 O3 O4 O5 O6 UM KS CH BR BN HN SK CK RC HC HS HY YS BT CS DM TM WF JG WC WH WE AV JC TC CC'.split()
OUT_ARGS = {'JVOpen': {'readcount', 'downloadcount', 'lastfiletimestamp'},
            'JVRead': {'filename'}, 'JVGets': {'buff', 'filename'}, 'JVFuku': {'buff'},
            'JVCourseFile': {'filepath', 'explanation'}}
INOUT_ARGS = {'JVRead': {'buff'}, 'JVMVRead': {'buff'}}


def dump(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def normalized(text):
    return unicodedata.normalize('NFKC', text)


def source(page):
    return {'document': API_FILE, 'page': page}


def extract_api(pages):
    entries = []
    for number, original in enumerate(pages, 1):
        if '【構文】' not in original:
            continue
        syntax = original.split('【構文】', 1)[1].split('【パラメータ】')[0].split('【戻り値】')[0]
        for match in re.finditer(r'(Long|void)\s*(JV\w+)\s*\((.*?)\)\s*;', normalized(syntax), re.S):
            return_type, name, parameters = match.groups()
            if name not in METHODS:
                raise ValueError(f'Unexpected SDK method: {name}')
            args = []
            for index, parameter in enumerate(filter(str.strip, parameters.split(',')), 1):
                parsed = re.fullmatch(r'\s*(String|Long|Byte\s+Array)\s*型\s*(\w+)\s*', parameter)
                if not parsed:
                    raise ValueError(f'Unparsed signature: {name}: {parameter}')
                kind, arg_name = parsed.groups()
                direction = ('out' if arg_name in OUT_ARGS.get(name, set()) else
                             'inout' if arg_name in INOUT_ARGS.get(name, set()) else 'in')
                args.append({'name': arg_name, 'position': index, 'sdkType': ' '.join(kind.split()), 'direction': direction})
            section_end = next((n for n in range(number, len(pages)) if '【構文】' in pages[n]), len(pages))
            section = '\n'.join(pages[number - 1:section_end])
            returns = section.split('【戻り値】', 1)[1].split('【解説】')[0].strip() if '【戻り値】' in section else 'void'
            entries.append({'kind': 'method', 'name': name, 'source': source(number),
                            'signature': ' '.join(match.group(0).split()), 'arguments': args,
                            'returnType': return_type, 'returnRules': returns,
                            'preconditionsSource': {'document': API_FILE, 'pages': list(range(number, section_end + 1))},
                            'publicTarget': 'JvLink.' + METHODS[name], 'targetStatus': 'planned',
                            'testId': 'inventory.method.' + name, 'implementationResult': 'not-run'})
    for name, (kind, target) in PROPERTIES.items():
        page = 8 if name in ('ParentHWnd', 'm_payflag') else 7
        text = pages[page - 1]
        start = text.index(name)
        following = [text.find(other, start + len(name)) for other in PROPERTIES if other != name]
        stop = min([pos for pos in following if pos > start] or [len(text)])
        entries.append({'kind': 'property', 'name': name, 'source': source(page),
                        'sdkType': kind, 'description': text[start:stop].strip(),
                        'accessVerification': 'not-run', 'accessSource': 'Verify against installed 5.0 type library in T01/T04',
                        'publicTarget': 'JvLink.' + target, 'targetStatus': 'planned',
                        'testId': 'inventory.property.' + name, 'implementationResult': 'not-run'})
    for event in EVENTS:
        name = 'JVEvt' + event
        if name not in pages[49]:
            raise ValueError(f'Event absent from original page 50: {name}')
        prefix = {'JockeyChange': 'JC', 'Weather': 'WE', 'CourseChange': 'CC', 'Avoid': 'AV', 'TimeChange': 'TC'}.get(event, '')
        entries.append({'kind': 'event', 'name': name, 'source': source(50),
                        'arguments': [{'name': 'bstr', 'position': 1, 'sdkType': 'String', 'direction': 'in'}],
                        'returnType': 'Void', 'keyPattern': prefix + 'YYYYMMDDJJRR' + ('yyyyMMddHHmmss' if prefix else ''),
                        'publicTarget': 'JvLink.subscribe/' + event, 'targetStatus': 'planned',
                        'testId': 'inventory.event.' + name, 'implementationResult': 'not-run'})
    return entries


def field_scale(description):
    """Recognize explicit multipliers and decimal templates even without the word unit."""
    text = normalized(description)
    multiplier = re.search(r'単位\s*[:：]?\s*(0\.\d+)', text)
    fixed_decimal = re.search(r'(?<!\d)9+\.(9+)(?!\d)', text)
    seconds_decimal = re.search(r'(?<!\d)9+秒(9+)(?!\d)', text) if '分' not in text else None
    if multiplier:
        return multiplier.group(1)
    if fixed_decimal:
        return '0.' + '0' * (len(fixed_decimal.group(1)) - 1) + '1'
    if seconds_decimal:
        return '0.' + '0' * (len(seconds_decimal.group(1)) - 1) + '1'
    return '1'


def extract_records(sheet):
    records = []
    current = None
    group = None
    header = None
    categories = []
    for cells in sheet:
        row = [cell.value for cell in cells]
        number = cells[0].row
        if row[5] == 'レコード長':
            header = {'name': row[1], 'length': row[7], 'headerRow': number}
        if row[1] == '項番':
            categories = row[11:]
        if row[4] == 'レコード種別ID':
            code = re.search(r'"([A-Z][A-Z0-9])"', str(row[10])).group(1)
            current = {**header, 'id': code, 'source': f'{DATA_FILE}#フォーマット!E{number}',
                       'publicTarget': 'Records.' + code, 'targetStatus': 'planned',
                       'testId': 'inventory.record.' + code, 'implementationResult': 'not-run', 'fields': []}
            records.append(current)
            group = None
        if current is None or row[4] is None or row[7] is None or row[5] == 'レコード長':
            continue
        if not isinstance(row[7], (int, float)):
            continue
        raw_position = str(row[5])
        # CK's lettered child rows omit parentheses but are still group-relative.
        relative = raw_position.strip().startswith('(') or (row[1] is None and row[2] is not None)
        if relative and group is None:
            raise ValueError(f'Relative field without group at row {number}')
        if row[5] is None:
            if not relative:
                raise ValueError(f'Missing absolute position at row {number}')
            # WF 7.a–d have lengths but no position cells. Their group is packed
            # in the listed order; calculate offsets and check the group boundary.
            siblings = [f for f in current['fields'] if f['parent'] == group['id']]
            position = max((f['position'] + f['length'] * f['repeat'] for f in siblings), default=1)
            if position + int(row[7]) * int(row[6] or 1) - 1 > group['length']:
                raise ValueError(f'Implicit field exceeds group at row {number}')
        else:
            position = int(re.sub(r'[()\s]', '', raw_position))
        item = str(row[1]) if row[1] is not None else str(row[2])
        if item == 'None':
            # WH's five child fields have no item letters in the original workbook.
            if current['id'] == 'WH' and relative:
                item = 'p' + str(position)
            else:
                raise ValueError(f'Unnumbered sized field at row {number}')
        field_id = (group['id'] + '.' + item) if relative else item
        # The source uses item 98 for both the owner group and producer code.
        if current['id'] == 'CK' and item == '98' and position == 6597:
            field_id = '98_producer'
        description = str(row[10] or '')
        scale = field_scale(description)
        field = {'id': field_id, 'sourceItem': item, 'name': str(row[4]).strip(), 'sourceCell': f'フォーマット!E{number}',
                 'position': position, 'positionKind': 'relative' if relative else 'absolute',
                 'parent': group['id'] if relative else None,
                 'length': int(row[7]), 'repeat': int(row[6] or 1),
                 'initialValue': row[9], 'reserved': '予備' in str(row[4]) or '予約' in str(row[4]),
                 'encoding': 'Shift-JIS/JIS8', 'scale': scale,
                 'scaleSource': description, 'description': description,
                 'dataCategories': {str(k): v for k, v in zip(categories, row[11:]) if k is not None and v is not None},
                 'publicTarget': f'Records.{current["id"]}.Field{field_id.replace(".", "_")}',
                 'targetStatus': 'planned', 'implementationResult': 'not-run'}
        current['fields'].append(field)
        if not relative:
            group = field if field['repeat'] > 1 and str(row[4]).strip().startswith('<') else None
    # A few cells inherit the unit from the preceding paired field.
    inherited_units = {('SE', '27'): '26', ('SE', '59'): '58'}
    for record in records:
        by_id = {field['id']: field for field in record['fields']}
        for field in record['fields']:
            source = inherited_units.get((record['id'], field['id']))
            if source:
                field['scale'] = by_id[source]['scale']
    return records


def build(documents, output):
    from pypdf import PdfReader
    import openpyxl
    output.mkdir(parents=True, exist_ok=True)
    hashes = []
    for name, expected in HASHES.items():
        actual = hashlib.sha256((documents / name).read_bytes()).hexdigest()
        hashes.append({'file': name, 'expected': expected, 'actual': actual, 'result': 'pass' if actual == expected else 'fail'})
    dump(output / 'source-hashes.json', hashes)
    if any(item['result'] != 'pass' for item in hashes):
        raise ValueError('SDK differs from audited version; review required')
    pages = [page.extract_text(extraction_mode='layout') for page in PdfReader(documents / API_FILE).pages]
    apis = extract_api(pages)
    book = openpyxl.load_workbook(documents / DATA_FILE, data_only=True)
    records = extract_records(book['フォーマット'])
    dump(output / 'api-contracts.json', {'schemaVersion': 1, 'entries': apis})
    dump(output / 'record-fields.json', {'schemaVersion': 1, 'records': records})
    print(json.dumps({'methods': sum(a['kind'] == 'method' for a in apis), 'properties': sum(a['kind'] == 'property' for a in apis),
                      'events': sum(a['kind'] == 'event' for a in apis), 'records': len(records), 'fields': sum(len(r['fields']) for r in records)}))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--documents', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    options = parser.parse_args()
    build(options.documents, options.output)
