#!/usr/bin/env python3
import base64
import os
from pathlib import Path
import plistlib
import re
import sys
from urllib.parse import urlparse
import xml.etree.ElementTree as ET


def stable_version(value):
    if not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", value):
        raise ValueError("Direct updates require a stable three-part numeric version.")
    return tuple(int(part) for part in value.split("."))


def secure_url(value):
    parsed = urlparse(value)
    if parsed.scheme != "https" or not parsed.hostname or parsed.username or parsed.password or parsed.query or parsed.fragment:
        raise ValueError("Update URLs must use HTTPS without credentials, queries, or fragments.")
    if any(ord(character) < 33 or character in "<>\"'" for character in value):
        raise ValueError("Update URLs contain unsafe characters.")
    return value.rstrip("/")


def configuration():
    stable_version(os.environ["UPDATE_VERSION"])
    secure_url(os.environ["SPARKLE_FEED_BASE_URL"])
    secure_url(os.environ["SPARKLE_DOWNLOAD_BASE_URL"])
    if len(base64.b64decode(os.environ["SPARKLE_PUBLIC_KEY"], validate=True)) != 32:
        raise ValueError("The Sparkle public key must encode 32 bytes.")


def prepare_bundle(path, runtime):
    configuration()
    architecture = {"osx-arm64": "arm64", "osx-x64": "x64"}[runtime]
    with path.open("rb") as source:
        metadata = plistlib.load(source)
    metadata.update(
        SUFeedURL=f"{secure_url(os.environ['SPARKLE_FEED_BASE_URL'])}/{architecture}/appcast.xml",
        SUPublicEDKey=os.environ["SPARKLE_PUBLIC_KEY"],
        SURequireSignedFeed=True,
        SUVerifyUpdateBeforeExtraction=True,
        SUEnableAutomaticChecks=False,
        SUAllowsAutomaticUpdates=False,
        SUScheduledCheckInterval=86400,
        SUSendProfileInfo=False,
    )
    with path.open("wb") as destination:
        plistlib.dump(metadata, destination, sort_keys=False)


def validate_feed(path, runtime, version, previous=False):
    namespace = "{http://www.andymatuschak.org/xml-namespaces/sparkle}"
    items = ET.parse(path).findall("./channel/item")
    desired = stable_version(version)
    if not items and not previous:
        raise ValueError("The update feed is empty.")
    matching = []
    for item in items:
        enclosure = item.find("enclosure")
        if enclosure is None:
            raise ValueError("The update feed contains an item without an archive.")
        item_version = item.findtext(f"{namespace}version") or enclosure.get(f"{namespace}version", "")
        current = stable_version(item_version)
        if previous and current >= desired:
            raise ValueError("The new update version must exceed every version in the existing feed.")
        if not previous:
            if item.find(f"{namespace}channel") is not None:
                raise ValueError("Only stable updates are permitted.")
            url = secure_url(enclosure.get("url", ""))
            if enclosure.get(f"{namespace}edSignature") is None:
                raise ValueError("The update archive has no Ed25519 signature.")
            if current == desired:
                expected = f"AtlasStorageAnalyzer-{version}-{runtime}.dmg"
                if not url.endswith(f"/v{version}/{expected}") or url != f"{secure_url(os.environ['SPARKLE_DOWNLOAD_BASE_URL'])}/{expected}":
                    raise ValueError("The archive URL does not match its immutable version and architecture.")
                if item.findtext(f"{namespace}minimumSystemVersion") != "11.0":
                    raise ValueError("The update must retain the macOS 11.0 minimum.")
                architecture = {"osx-arm64": "arm64", "osx-x64": "x86_64"}[runtime]
                if item.findtext(f"{namespace}hardwareRequirements", "x86_64") != architecture:
                    raise ValueError("The update feed does not match the payload architecture.")
                matching.append(item)
    if not previous and len(matching) != 1:
        raise ValueError("The feed must contain exactly one entry for the new version.")


def main():
    operation = sys.argv[1]
    if operation == "config":
        configuration()
    elif operation == "bundle":
        prepare_bundle(Path(sys.argv[2]), sys.argv[3])
    elif operation in ("feed", "previous"):
        validate_feed(Path(sys.argv[2]), sys.argv[3], sys.argv[4], operation == "previous")
    else:
        raise ValueError("Unknown metadata operation.")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, OSError, ET.ParseError) as error:
        sys.exit(f"Update metadata error: {error}")
