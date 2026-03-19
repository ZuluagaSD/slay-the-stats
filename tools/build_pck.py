"""Generate a MegaDot v3 PCK file containing mod_manifest.json."""
import struct
import hashlib
import sys
import os

def build_pck(manifest_path: str, output_path: str) -> None:
    manifest = open(manifest_path, "rb").read()
    md5 = hashlib.md5(manifest).digest()

    path_str = "mod_manifest.json"
    path_bytes = path_str.encode("utf-8")
    padded_len = (len(path_bytes) + 3) & ~3
    path_padded = path_bytes + b"\x00" * (padded_len - len(path_bytes))

    data_start = 88  # Right after the 88-byte header
    table_offset = data_start + len(manifest)

    pck = bytearray()

    # Header: magic + pack_version + godot_version
    pck += b"GDPC"
    pck += struct.pack("<I", 3)          # MegaDot pack version
    pck += struct.pack("<III", 4, 5, 1)  # Godot 4.5.1

    # Reserved (16 x int32)
    reserved = [0] * 16
    reserved[1] = data_start             # Data area start
    reserved[3] = table_offset & 0xFFFFFFFF
    reserved[4] = (table_offset >> 32) & 0xFFFFFFFF
    for r in reserved:
        pck += struct.pack("<I", r)

    # File count at standard position (0 for v3 — real count is in the table)
    pck += struct.pack("<I", 0)
    assert len(pck) == 88

    # File data
    pck += manifest
    assert len(pck) == table_offset

    # File table
    pck += struct.pack("<I", 1)          # 1 file
    pck += struct.pack("<I", padded_len)
    pck += path_padded
    pck += struct.pack("<q", 0)          # Offset relative to data_start
    pck += struct.pack("<q", len(manifest))
    pck += md5
    pck += struct.pack("<I", 0)          # Flags

    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    with open(output_path, "wb") as f:
        f.write(pck)
    print(f"PCK written: {output_path} ({len(pck)} bytes)")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        print(f"Usage: {sys.argv[0]} <manifest.json> <output.pck>")
        sys.exit(1)
    build_pck(sys.argv[1], sys.argv[2])
