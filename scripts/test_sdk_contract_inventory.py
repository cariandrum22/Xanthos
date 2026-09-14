"""Inventory tests, not evidence of implementation/API runtime coverage.

Run with XANTHOS_SDK_INVENTORY pointing at the generated local directory.
"""
import copy
import json
import os
import unittest
from pathlib import Path
from sdk_contract_inventory import METHODS, PROPERTIES, EVENTS, RECORD_IDS, dump, field_scale, extract_records
from sdk_inventory_validation import SCHEMAS, ENTRY, validate_shape, validate_record, validate_inventory

DIRECTORY = Path(os.environ.get('XANTHOS_SDK_INVENTORY', '.artifacts/sdk-audit/verification'))
API = json.loads((DIRECTORY / 'api-contracts.json').read_text(encoding='utf-8-sig'))
DATA = json.loads((DIRECTORY / 'record-fields.json').read_text(encoding='utf-8-sig'))
REGISTRY = {}


class InventoryEntries(unittest.TestCase):
    pass


def register(kind, name):
    test_id = f'inventory.{kind}.{name}'
    def test(self):
        if kind == 'record':
            record = next(record for record in DATA['records'] if record['id'] == name)
            validate_record(record)
        else:
            entry = next(entry for entry in API['entries'] if entry['kind'] == kind and entry['name'] == name)
            validate_shape(entry, ENTRY)
            if kind != 'property':
                self.assertEqual(list(range(1, len(entry['arguments']) + 1)), [arg['position'] for arg in entry['arguments']])
                self.assertTrue(all(arg['direction'] in ('in', 'out', 'inout') for arg in entry['arguments']))
    method = 'test_' + kind + '_' + name
    setattr(InventoryEntries, method, test)
    REGISTRY[test_id] = {'test': 'test_sdk_contract_inventory.InventoryEntries.' + method, 'scope': 'inventory', 'runtimeResult': 'not-run'}


for _kind, _names in [('method', METHODS), ('property', PROPERTIES), ('event', ['JVEvt' + e for e in EVENTS]), ('record', RECORD_IDS)]:
    for _name in _names:
        register(_kind, _name)


class InventoryIntegrity(unittest.TestCase):
    def test_implicit_child_positions_are_not_silently_dropped(self):
        from types import SimpleNamespace
        rows = []
        def row(**values):
            cells = [None] * 16
            for key, value in values.items():
                cells[int(key[1:])] = value
            rows.append([SimpleNamespace(value=v, row=len(rows) + 1) for v in cells])
        row(c1='WIN5', c5='レコード長', c7=7215)
        row(c1=1, c4='レコード種別ID', c5=1, c7=2, c10='"WF"')
        row(c1=7, c4='<Races>', c5=22, c6=5, c7=8)
        for item in 'abcd':
            row(c2=item, c4='Part', c7=2)
        fields = extract_records(rows)[0]['fields']
        self.assertEqual(['7.a', '7.b', '7.c', '7.d'], [f['id'] for f in fields[2:]])
        self.assertEqual([1, 3, 5, 7], [f['position'] for f in fields[2:]])
        row(c2='e', c4='Overflow', c7=2)
        with self.assertRaisesRegex(ValueError, 'exceeds group'):
            extract_records(rows)

    def test_decimal_templates_without_unit_prefix(self):
        self.assertEqual('0.1', field_scale('99.9秒 平地競走のみ設定'))
        self.assertEqual('0.1', field_scale('99999.9倍'))
        self.assertEqual('0.01', field_scale('99.99'))
        self.assertEqual('0.1', field_scale('単位:0.1秒'))
        self.assertEqual('1', field_scale('分＋秒（1分57秒2は1572）'))
        self.assertEqual('0.01', field_scale('99秒99で設定'))
        self.assertEqual('1', field_scale('9分99秒99で設定'))

    def test_counts_identities_and_registered_tests(self):
        validate_inventory(API, DATA, REGISTRY)

    def test_duplicate_api_is_rejected(self):
        api = copy.deepcopy(API)
        api['entries'][1] = api['entries'][0]
        with self.assertRaises(ValueError):
            validate_inventory(api, DATA, REGISTRY)

    def test_empty_target_is_rejected(self):
        api = copy.deepcopy(API)
        api['entries'][0]['publicTarget'] = ''
        with self.assertRaises(ValueError):
            validate_inventory(api, DATA, REGISTRY)

    def test_unknown_test_id_is_rejected(self):
        api = copy.deepcopy(API)
        api['entries'][0]['testId'] = 'not-registered'
        with self.assertRaises(ValueError):
            validate_inventory(api, DATA, REGISTRY)

    def test_duplicate_record_is_rejected(self):
        data = copy.deepcopy(DATA)
        data['records'][1] = data['records'][0]
        with self.assertRaises(ValueError):
            validate_inventory(API, data, REGISTRY)

    def test_shifted_byte_position_is_rejected(self):
        data = copy.deepcopy(DATA)
        next(record for record in data['records'] if record['id'] == 'RA')['fields'][3]['position'] += 1
        with self.assertRaises(ValueError):
            validate_inventory(API, data, REGISTRY)

    def test_ra_landmarks_from_original_excel(self):
        record = next(record for record in DATA['records'] if record['id'] == 'RA')
        self.assertEqual(1272, record['length'])
        fields = {field['name']: field for field in record['fields']}
        self.assertEqual((33, 60), (fields['競走名本題']['position'], fields['競走名本題']['length']))
        self.assertEqual(698, fields['距離']['position'])
        self.assertEqual(874, fields['発走時刻']['position'])


if __name__ == '__main__':
    for name, schema in SCHEMAS.items():
        dump(DIRECTORY / (name + '.schema.json'), {'$schema': 'https://json-schema.org/draft/2020-12/schema', **schema})
    dump(DIRECTORY / 'inventory-test-registry.json', REGISTRY)
    suite = unittest.defaultTestLoader.loadTestsFromModule(__import__(__name__))
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    dump(DIRECTORY / 'inventory-test-result.json', {'scope': 'inventory', 'discovered': result.testsRun,
         'passed': result.testsRun - len(result.errors) - len(result.failures) - len(result.skipped),
         'failed': len(result.errors) + len(result.failures), 'skipped': len(result.skipped),
         'result': 'pass' if result.wasSuccessful() else 'fail', 'apiImplementationResult': 'not-run'})
    raise SystemExit(0 if result.wasSuccessful() else 1)
