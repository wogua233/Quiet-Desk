"""Exercise release guards using synthetic data only, never local credentials."""
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

spec = importlib.util.spec_from_file_location('release_audit', Path(__file__).with_name('audit-release.py'))
audit_module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit_module)

class ReleaseAuditTests(unittest.TestCase):
    def check_file(self, name, content, should_block):
        with tempfile.TemporaryDirectory(prefix='QuietDesk-audit-test-') as directory:
            path = Path(directory) / name
            path.write_bytes(content)
            self.assertEqual(bool(audit_module.audit(Path(directory))['problems']), should_block)

    def test_plain_and_utf16_credentials(self):
        synthetic = 'sk' + '-' + 'a1' * 16
        for name, encoding in [('settings.json', 'utf-8'), ('settings.json', 'utf-16'), ('app.dll', 'utf-16-le')]:
            with self.subTest(name=name, encoding=encoding):
                self.check_file(name, ('"' + synthetic + '"').encode(encoding), True)

    def test_protected_config(self):
        synthetic_config = json.dumps({'ProtectedKey': 'synthetic-encrypted-value'}).encode()
        self.check_file('config.json', synthetic_config, True)
        self.check_file('config.json', b'{"ProtectedKey":""}', False)

    def test_user_data_even_without_key(self):
        self.check_file('reading.db', b'', True)
        self.check_file('state.json', b'{}', True)

    def test_chunk_boundary(self):
        synthetic = b'sk' + b'-' + b'b2' * 16
        self.check_file('app.dll', b' ' * (1024 * 1024 - 8) + synthetic, True)

    def test_clean_assets(self):
        self.check_file('source.cs', b'// API key is supplied by the user at runtime.', False)

if __name__ == '__main__':
    unittest.main()
