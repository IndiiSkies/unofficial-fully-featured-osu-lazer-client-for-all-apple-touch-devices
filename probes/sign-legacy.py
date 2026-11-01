#!/usr/bin/env python3
"""Replace a reserved signature with an early-format ad-hoc SHA-1 signature.

Only for our thin ARMv6 test executables. Requires LC_CODE_SIGNATURE storage
reserved by ldid2 first. No certificates, entitlements, or device changes.
"""
import hashlib
import math
import struct
import sys
from pathlib import Path


def sign(path):
    data = bytearray(path.read_bytes())
    magic, cpu, subtype, kind, count, command_size, flags = struct.unpack_from("<7I", data)
    if magic != 0xFEEDFACE or cpu != 12 or subtype & 0xFFFFFF != 6 or kind != 2:
        raise ValueError("Expected a thin ARMv6 Mach-O executable")
    cursor = 28
    signature = None
    for _ in range(count):
        command, size = struct.unpack_from("<2I", data, cursor)
        if size < 8 or cursor + size > 28 + command_size:
            raise ValueError("Invalid Mach-O command")
        if command == 0x1D:
            signature = struct.unpack_from("<2I", data, cursor + 8)
        cursor += size
    if signature is None:
        raise ValueError("First reserve signature space with ldid2 -Hsha1 -Cadhoc -S")
    offset, capacity = signature
    if offset + capacity != len(data):
        raise ValueError("Expected signature at end of executable")
    bundle = path.parent
    special = b""
    identifier = b"org.osu.armv6-runtime-probe\0"
    if bundle.suffix == ".app":
        import plistlib
        info = (bundle / "Info.plist").read_bytes()
        resources = (bundle / "_CodeSignature" / "CodeResources").read_bytes()
        identifier = plistlib.loads(info)["CFBundleIdentifier"].encode() + b"\0"
        # Slots -3 (resource envelope), -2 (no requirements), -1 (Info.plist).
        special = hashlib.sha1(resources).digest() + bytes(20) + hashlib.sha1(info).digest()
    pages = math.ceil(offset / 4096)
    hash_offset = 44 + len(identifier) + len(special)
    cd_length = hash_offset + 20 * pages
    # Version 0x20001, CS_ADHOC, SHA-1, 4 KiB pages.
    directory = struct.pack(
        ">9I4BI", 0xFADE0C02, cd_length, 0x20001, 2, hash_offset,
        44, len(special) // 20, pages, offset, 20, 1, 0, 12, 0
    ) + identifier + special
    for page in range(pages):
        directory += hashlib.sha1(data[page * 4096:min((page + 1) * 4096, offset)]).digest()
    blob = struct.pack(">5I", 0xFADE0CC0, 20 + len(directory), 1, 0, 20) + directory
    if len(blob) > capacity:
        raise ValueError("Insufficient reserved signature space")
    data[offset:] = blob + bytes(capacity - len(blob))
    path.write_bytes(data)
    print("Signed %s: CodeDirectory 0x20001, SHA-1, %d pages" % (path, pages))


if __name__ == "__main__":
    sign(Path(sys.argv[1]))
