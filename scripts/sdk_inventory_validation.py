"""Strict validation of the inventory schema subset defined in this module."""
from collections import Counter

TEXT = {'type': 'string', 'minLength': 1}
POSITIVE = {'type': 'integer', 'minimum': 1}
RESULT = {'type': 'string', 'enum': ['pass', 'fail', 'blocked', 'not-run']}


def object_schema(properties):
    return {'type': 'object', 'required': list(properties), 'properties': properties}


def array_schema(items, minimum=1):
    return {'type': 'array', 'minItems': minimum, 'items': items}


ENTRY = object_schema({'name': TEXT, 'kind': {'enum': ['method', 'property', 'event']},
                       'source': object_schema({'document': TEXT, 'page': POSITIVE}),
                       'publicTarget': TEXT, 'targetStatus': {'enum': ['planned', 'implemented']},
                       'testId': TEXT, 'implementationResult': RESULT})
FIELD = object_schema({'id': TEXT, 'name': TEXT, 'sourceCell': TEXT, 'position': POSITIVE,
                       'positionKind': {'enum': ['absolute', 'relative']},
                       'length': POSITIVE, 'repeat': POSITIVE, 'reserved': {'type': 'boolean'},
                       'encoding': TEXT, 'scale': TEXT, 'publicTarget': TEXT,
                       'targetStatus': {'enum': ['planned', 'implemented']}, 'implementationResult': RESULT})
RECORD = object_schema({'id': TEXT, 'name': TEXT, 'length': POSITIVE, 'source': TEXT,
                        'publicTarget': TEXT, 'testId': TEXT, 'implementationResult': RESULT,
                        'fields': array_schema(FIELD)})
SCHEMAS = {
    'api-contracts': object_schema({'schemaVersion': {'const': 1}, 'entries': array_schema(ENTRY)}),
    'record-fields': object_schema({'schemaVersion': {'const': 1}, 'records': array_schema(RECORD)}),
}


def validate_shape(value, schema, path='$'):
    supported = {'type', 'required', 'properties', 'items', 'minItems', 'minLength', 'minimum', 'enum', 'const', '$schema', 'title'}
    unknown = set(schema) - supported
    if unknown:
        raise ValueError(f'Unsupported schema keywords: {unknown}')
    types = {'object': dict, 'array': list, 'string': str, 'integer': int, 'boolean': bool}
    if 'type' in schema and type(value) is not types[schema['type']]:
        raise ValueError(f'{path}: expected {schema["type"]}')
    if 'enum' in schema and value not in schema['enum']:
        raise ValueError(f'{path}: invalid enum')
    if 'const' in schema and value != schema['const']:
        raise ValueError(f'{path}: wrong constant')
    for keyword in ('minItems', 'minLength'):
        if keyword in schema and len(value) < schema[keyword]:
            raise ValueError(f'{path}: below {keyword}')
    if 'minimum' in schema and value < schema['minimum']:
        raise ValueError(f'{path}: below minimum')
    for name in schema.get('required', []):
        if name not in value:
            raise ValueError(f'{path}: missing {name}')
    for name, child in schema.get('properties', {}).items():
        if name in value:
            validate_shape(value[name], child, f'{path}.{name}')
    if 'items' in schema:
        for index, child in enumerate(value):
            validate_shape(child, schema['items'], f'{path}[{index}]')


def unique(values, context):
    duplicates = [name for name, count in Counter(values).items() if count != 1]
    if duplicates:
        raise ValueError(f'{context}: duplicate identities: {duplicates}')


def validate_record(record):
    validate_shape(record, RECORD)
    fields = record['fields']
    unique([field['id'] for field in fields], record['id'])
    by_id = {field['id']: field for field in fields}
    intervals = []
    for field in fields:
        end = field['position'] - 1 + field['length'] * field['repeat']
        if field['positionKind'] == 'absolute':
            if field.get('parent') is not None or end > record['length']:
                raise ValueError(f'{record["id"]}/{field["id"]}: outside record')
            intervals.append((field['position'], end))
        else:
            parent = by_id.get(field.get('parent'))
            if parent is None or parent['repeat'] <= 1 or end > parent['length']:
                raise ValueError(f'{record["id"]}/{field["id"]}: outside repeating group')
    # Every byte, including reserved areas and CRLF, must belong to a top-level field.
    cursor = 1
    for start, end in sorted(intervals):
        if start != cursor:
            raise ValueError(f'{record["id"]}: hole/overlap at {cursor}, next={start}')
        cursor = end + 1
    if cursor != record['length'] + 1:
        raise ValueError(f'{record["id"]}: incomplete record coverage')
    parents = {field['parent'] for field in fields if field['positionKind'] == 'relative'}
    for parent_id in parents:
        children = [field for field in fields if field.get('parent') == parent_id]
        cursor = 1
        for child in sorted(children, key=lambda field: field['position']):
            if child['position'] != cursor:
                raise ValueError(f'{record["id"]}/{parent_id}: child hole/overlap at {cursor}')
            cursor += child['length'] * child['repeat']
        if cursor != by_id[parent_id]['length'] + 1:
            raise ValueError(f'{record["id"]}/{parent_id}: incomplete group coverage')


def validate_inventory(api, data, registered_tests):
    validate_shape(api, SCHEMAS['api-contracts'])
    validate_shape(data, SCHEMAS['record-fields'])
    entries = api['entries']
    records = data['records']
    if Counter(entry['kind'] for entry in entries) != {'method': 26, 'property': 9, 'event': 7} or len(records) != 38:
        raise ValueError('Expected 26 methods, 9 properties, 7 events and 38 records')
    unique([entry['name'] for entry in entries], 'API')
    unique([record['id'] for record in records], 'records')
    test_ids = [entry['testId'] for entry in entries + records]
    unique(test_ids, 'test IDs')
    for test_id in test_ids:
        if test_id not in registered_tests:
            raise ValueError(f'Unregistered inventory test: {test_id}')
    for record in records:
        validate_record(record)
