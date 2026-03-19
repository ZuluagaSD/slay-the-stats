"""Extract card, relic, and potion images from SlayTheSpire2.pck (MegaDot v3)."""
import struct
import os
import sys

GAME_PCK = r"C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\SlayTheSpire2.pck"
OUTPUT_DIR = os.path.join(os.path.dirname(__file__), "..", "web", "public", "game-assets")

# Paths we want to extract (lowercase for matching)
EXTRACT_PREFIXES = [
    "images/powers/",
    "images/relics/",
    "images/potions/",
    "images/packed/cards/",
    "images/packed/run_history/",
    "images/orbs/",
    "images/ui/top_panel/",
    "images/enchantments/",
]


def read_pck(pck_path):
    """Read MegaDot v3 PCK file table and yield (path, offset, size) tuples."""
    with open(pck_path, "rb") as f:
        magic = f.read(4)
        if magic != b"GDPC":
            raise ValueError(f"Not a PCK file: {magic}")

        pack_version = struct.unpack("<I", f.read(4))[0]
        print(f"Pack version: {pack_version}")

        # Godot version
        godot_major, godot_minor, godot_patch = struct.unpack("<III", f.read(12))
        print(f"Godot version: {godot_major}.{godot_minor}.{godot_patch}")

        # Reserved (16 x int32)
        reserved = struct.unpack("<16I", f.read(64))
        data_start = reserved[1]
        table_offset_lo = reserved[3]
        table_offset_hi = reserved[4]
        table_offset = table_offset_lo | (table_offset_hi << 32)

        print(f"Data start: {data_start}")
        print(f"Table offset: {table_offset}")

        # File count at standard position
        file_count_header = struct.unpack("<I", f.read(4))[0]
        print(f"File count (header): {file_count_header}")

        # Seek to file table at end
        f.seek(table_offset)
        file_count = struct.unpack("<I", f.read(4))[0]
        print(f"File count (table): {file_count}")

        files = []
        for i in range(file_count):
            # Path string (4-byte aligned)
            path_len = struct.unpack("<I", f.read(4))[0]
            path_bytes = f.read(path_len)
            path = path_bytes.rstrip(b"\x00").decode("utf-8", errors="replace")

            # Offset (relative to data_start), size, md5, flags
            offset = struct.unpack("<q", f.read(8))[0]
            size = struct.unpack("<q", f.read(8))[0]
            md5 = f.read(16)
            flags = struct.unpack("<I", f.read(4))[0]

            abs_offset = data_start + offset
            files.append((path, abs_offset, size, flags))

            if i < 5 or i % 10000 == 0:
                print(f"  [{i}] {path} (offset={abs_offset}, size={size}, flags={flags})")

        print(f"\nTotal files in PCK: {file_count}")
        return files, f.name


def extract_matching(pck_path, files, output_dir):
    """Extract files matching our prefixes."""
    os.makedirs(output_dir, exist_ok=True)

    extracted = 0
    skipped = 0

    with open(pck_path, "rb") as f:
        for path, offset, size, flags in files:
            # Check if this file matches any prefix we want
            path_lower = path.lower()
            match = False
            for prefix in EXTRACT_PREFIXES:
                if prefix in path_lower:
                    match = True
                    break

            if not match:
                continue

            if flags != 0:
                print(f"  SKIP (compressed, flags={flags}): {path}")
                skipped += 1
                continue

            # Read the file data
            f.seek(offset)
            data = f.read(size)

            # Write to output
            out_path = os.path.join(output_dir, path.replace("res://", ""))
            os.makedirs(os.path.dirname(out_path), exist_ok=True)

            with open(out_path, "wb") as out:
                out.write(data)

            extracted += 1
            if extracted <= 20 or extracted % 100 == 0:
                print(f"  Extracted: {path} ({size} bytes)")

    print(f"\nExtracted: {extracted} files")
    print(f"Skipped (compressed): {skipped} files")
    print(f"Output: {output_dir}")


def main():
    pck_path = sys.argv[1] if len(sys.argv) > 1 else GAME_PCK

    if not os.path.exists(pck_path):
        print(f"PCK file not found: {pck_path}")
        sys.exit(1)

    print(f"Reading PCK: {pck_path}")
    print(f"Size: {os.path.getsize(pck_path) / 1024 / 1024:.1f} MB\n")

    files, _ = read_pck(pck_path)

    # Show what we found matching our prefixes
    matching = [f for f in files if any(p in f[0].lower() for p in EXTRACT_PREFIXES)]
    print(f"\nMatching files: {len(matching)}")
    for path, _, size, flags in matching[:30]:
        print(f"  {path} ({size} bytes, flags={flags})")
    if len(matching) > 30:
        print(f"  ... and {len(matching) - 30} more")

    if matching:
        print(f"\nExtracting to: {OUTPUT_DIR}")
        extract_matching(pck_path, files, OUTPUT_DIR)
    else:
        # Show all unique path prefixes to find where images are
        prefixes = set()
        for path, _, _, _ in files:
            parts = path.split("/")
            if len(parts) >= 2:
                prefixes.add("/".join(parts[:2]))
        print("\nAll path prefixes in PCK:")
        for p in sorted(prefixes):
            count = sum(1 for f in files if f[0].startswith(p))
            print(f"  {p}/ ({count} files)")


if __name__ == "__main__":
    main()
