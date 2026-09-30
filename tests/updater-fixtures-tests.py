#!/usr/bin/env python3
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

MODULE_PATH = Path(__file__).with_name('updater-fixtures.py')
SPEC = importlib.util.spec_from_file_location('updater_fixtures', MODULE_PATH)
FIXTURES = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(FIXTURES)


class FixtureSafetyTests(unittest.TestCase):
    def test_preparation_refuses_existing_files_before_running_tools(self):
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            sentinel = directory / 'existing.txt'
            sentinel.write_text('preserve')
            with patch.object(FIXTURES, 'run') as run:
                with self.assertRaisesRegex(ValueError, 'empty fixture directory'):
                    FIXTURES.prepare(directory, 'arm64', 'test identity', 8781)
                run.assert_not_called()
            self.assertEqual(sentinel.read_text(), 'preserve')

    def test_mismatched_fixture_marker_prevents_reset(self):
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            (directory / 'fixture.json').write_text(json.dumps({'directory': '/wrong-fixture'}))
            sentinel = directory / 'installed'
            sentinel.mkdir()
            with self.assertRaisesRegex(ValueError, 'does not match'):
                FIXTURES.reset(directory)
            self.assertTrue(sentinel.exists())

    def test_running_test_app_prevents_replacement(self):
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary).resolve()
            (directory / 'fixture.json').write_text(json.dumps({'directory': str(directory), 'name': 'Atlas Update Test arm64'}))
            installed = directory / 'installed'
            installed.mkdir()
            sentinel = installed / 'Atlas Update Test arm64.app'
            sentinel.mkdir()
            with patch.object(FIXTURES.subprocess, 'run') as run:
                run.return_value.returncode = 0
                with self.assertRaisesRegex(ValueError, 'Quit the isolated test app'):
                    FIXTURES.reset(directory)
            self.assertTrue(sentinel.exists())

    def test_symlinked_installation_cannot_modify_another_directory(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            directory = root / 'fixture'
            directory.mkdir()
            other = root / 'preserved'
            other.mkdir()
            sentinel = other / 'keep.txt'
            sentinel.write_text('preserve')
            (directory / 'installed').symlink_to(other, target_is_directory=True)
            (directory / 'fixture.json').write_text(json.dumps({'directory': str(directory)}))

            with self.assertRaisesRegex(ValueError, 'must not be a symlink'):
                FIXTURES.reset(directory)

            self.assertEqual(sentinel.read_text(), 'preserve')

    def test_tampered_scenario_preserves_signature_and_original_feed(self):
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary).resolve()
            (directory / 'fixture.json').write_text(json.dumps({'directory': str(directory)}))
            (directory / 'feed').mkdir()
            original = b'<sparkle:version>1.0.1</sparkle:version>SIGNATURE'
            (directory / 'base-appcast.xml').write_bytes(original)

            FIXTURES.scenario(directory, 'tampered')

            self.assertEqual((directory / 'base-appcast.xml').read_bytes(), original)
            self.assertEqual((directory / 'feed/appcast.xml').read_bytes(),
                             b'<sparkle:version>9.0.0</sparkle:version>SIGNATURE')
            self.assertEqual(json.loads((directory / 'scenario.json').read_text())['mode'], 'tampered')

    def test_restoring_valid_scenario_restores_original_signed_bytes(self):
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary).resolve()
            (directory / 'fixture.json').write_text(json.dumps({'directory': str(directory)}))
            (directory / 'feed').mkdir()
            original = b'original signed fixture'
            (directory / 'base-appcast.xml').write_bytes(original)
            (directory / 'feed/appcast.xml').write_bytes(b'altered fixture')

            FIXTURES.scenario(directory, 'valid')

            self.assertEqual((directory / 'feed/appcast.xml').read_bytes(), original)


if __name__ == '__main__':
    unittest.main()
