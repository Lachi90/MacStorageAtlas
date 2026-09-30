import base64
import importlib.util
import os
from pathlib import Path
import plistlib
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
SPEC = importlib.util.spec_from_file_location("update_metadata", ROOT / "packaging/update-metadata.py")
METADATA = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(METADATA)
NAMESPACE = "{http://www.andymatuschak.org/xml-namespaces/sparkle}"


class UpdateMetadataTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.path = Path(self.directory.name)
        self.environment = patch.dict(os.environ, {
            "UPDATE_VERSION": "1.2.3",
            "SPARKLE_FEED_BASE_URL": "https://example.com/updates",
            "SPARKLE_DOWNLOAD_BASE_URL": "https://github.com/Example/App/releases/download/v1.2.3",
            "SPARKLE_PUBLIC_KEY": base64.b64encode(bytes(32)).decode(),
        })
        self.environment.start()
        self.addCleanup(self.environment.stop)

    def test_metadata_separates_architectures_and_enforces_signed_feed_policy(self):
        for runtime, architecture in (("osx-arm64", "arm64"), ("osx-x64", "x64")):
            with self.subTest(runtime=runtime):
                path = self.path / "Info.plist"
                with path.open("wb") as destination:
                    plistlib.dump({"CFBundleVersion": "1.2.3"}, destination)

                METADATA.prepare_bundle(path, runtime)

                with path.open("rb") as source:
                    result = plistlib.load(source)
                self.assertEqual(result["SUFeedURL"], f"https://example.com/updates/{architecture}/appcast.xml")
                self.assertTrue(result["SURequireSignedFeed"])
                self.assertTrue(result["SUVerifyUpdateBeforeExtraction"])
                self.assertFalse(result["SUEnableAutomaticChecks"])
                self.assertFalse(result["SUAllowsAutomaticUpdates"])
                self.assertFalse(result["SUSendProfileInfo"])
                self.assertEqual(result["SUScheduledCheckInterval"], 86400)

    def test_configuration_rejects_invalid_keys_urls_and_prerelease_versions(self):
        for name, value in (
            ("SPARKLE_PUBLIC_KEY", "bad"),
            ("SPARKLE_PUBLIC_KEY", base64.b64encode(bytes(31)).decode()),
            ("SPARKLE_FEED_BASE_URL", "http://example.com"),
            ("SPARKLE_FEED_BASE_URL", "https://user:password@example.com"),
            ("SPARKLE_DOWNLOAD_BASE_URL", "https://example.com/?secret=token"),
            ("UPDATE_VERSION", "1.2.3-beta"),
        ):
            with self.subTest(name=name, value=value), patch.dict(os.environ, {name: value}):
                with self.assertRaises(ValueError):
                    METADATA.configuration()

    def write_feed(self, runtime="osx-arm64", version="1.2.3", architecture="arm64"):
        root = ET.Element("rss")
        item = ET.SubElement(ET.SubElement(root, "channel"), "item")
        ET.SubElement(item, NAMESPACE + "version").text = version
        ET.SubElement(item, NAMESPACE + "minimumSystemVersion").text = "11.0"
        if architecture:
            ET.SubElement(item, NAMESPACE + "hardwareRequirements").text = architecture
        ET.SubElement(item, "enclosure", {
            "url": f"https://github.com/Example/App/releases/download/v{version}/AtlasStorageAnalyzer-{version}-{runtime}.dmg",
            NAMESPACE + "edSignature": base64.b64encode(bytes(64)).decode(),
        })
        path = self.path / "appcast.xml"
        ET.ElementTree(root).write(path)
        return path

    def test_arm64_and_default_x64_architecture_feeds_are_accepted(self):
        METADATA.validate_feed(self.write_feed(), "osx-arm64", "1.2.3")
        METADATA.validate_feed(self.write_feed("osx-x64", architecture=None), "osx-x64", "1.2.3")

    def test_wrong_architecture_missing_signature_and_mutable_url_are_rejected(self):
        for failure in ("architecture", "signature", "url", "os", "channel"):
            with self.subTest(failure=failure):
                path = self.write_feed()
                tree = ET.parse(path)
                item = tree.find("./channel/item")
                if failure == "architecture":
                    item.find(NAMESPACE + "hardwareRequirements").text = "x86_64"
                if failure == "signature":
                    item.find("enclosure").attrib.pop(NAMESPACE + "edSignature")
                if failure == "url":
                    item.find("enclosure").set("url", "https://example.com/latest.dmg")
                if failure == "os":
                    item.find(NAMESPACE + "minimumSystemVersion").text = "12.0"
                if failure == "channel":
                    ET.SubElement(item, NAMESPACE + "channel").text = "beta"
                tree.write(path)

                with self.assertRaises(ValueError):
                    METADATA.validate_feed(path, "osx-arm64", "1.2.3")

    def test_existing_feed_prevents_equal_or_older_release(self):
        path = self.write_feed(version="1.2.3")
        for version in ("1.2.3", "1.2.2"):
            with self.subTest(version=version), self.assertRaises(ValueError):
                METADATA.validate_feed(path, "osx-arm64", version, previous=True)
        METADATA.validate_feed(path, "osx-arm64", "1.2.4", previous=True)


if __name__ == "__main__":
    unittest.main()
