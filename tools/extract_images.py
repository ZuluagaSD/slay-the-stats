"""Extract potion, power, and relic images from STS2 PCK as PNGs.
Handles Godot .ctex format (both raw and S3TC/BPTC compressed)."""
import struct
import os
import sys
import zlib

GAME_PCK = r"C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\SlayTheSpire2.pck"
OUTPUT_DIR = os.path.join(os.path.dirname(__file__), "..", "web", "public", "game-assets")

# Categories to extract
CATEGORIES = {
    "potions": lambda p: "potion" in p and ".ctex" in p and "outline" not in p and "atlas" not in p,
    "powers": lambda p: "power" in p and ".ctex" in p and "atlas" not in p,
}


def read_pck_files(pck_path):
    """Read all file entries from the PCK."""
    entries = []
    with open(pck_path, "rb") as f:
        f.read(4)  # GDPC
        f.read(4)  # version
        f.read(12)  # godot version
        reserved = struct.unpack("<16I", f.read(64))
        data_start = reserved[1]
        table_offset = reserved[3] | (reserved[4] << 32)
        f.read(4)  # header count

        f.seek(table_offset)
        count = struct.unpack("<I", f.read(4))[0]

        for _ in range(count):
            path_len = struct.unpack("<I", f.read(4))[0]
            path = f.read(path_len).rstrip(b"\x00").decode("utf-8", errors="replace")
            offset = struct.unpack("<q", f.read(8))[0]
            size = struct.unpack("<q", f.read(8))[0]
            f.read(16)  # md5
            flags = struct.unpack("<I", f.read(4))[0]
            entries.append((path, data_start + offset, size, flags))

    return entries


def extract_ctex_as_png(pck_path, offset, size):
    """Try to extract a .ctex file as PNG data.
    Godot ctex files start with a header then contain raw image data or webp/png data."""
    with open(pck_path, "rb") as f:
        f.seek(offset)
        data = f.read(size)

    # Check if it contains embedded PNG
    png_start = data.find(b"\x89PNG")
    if png_start >= 0:
        return data[png_start:]

    # Check if it contains embedded WebP
    webp_start = data.find(b"RIFF")
    if webp_start >= 0 and webp_start + 8 < len(data):
        riff_check = data[webp_start + 8:webp_start + 12]
        if riff_check == b"WEBP":
            return data[webp_start:]

    # If it's a raw ctex, try to parse the Godot texture format
    if data[:4] == b"GDST" or data[:4] == b"GST2":
        # Godot StreamTexture2D format
        return try_parse_godot_texture(data)

    return None


def try_parse_godot_texture(data):
    """Try to extract image data from Godot texture format."""
    # Look for embedded image data markers
    for marker, ext in [(b"\x89PNG", "png"), (b"RIFF", "webp")]:
        idx = data.find(marker)
        if idx >= 0:
            if marker == b"RIFF":
                if data[idx + 8:idx + 12] == b"WEBP":
                    return data[idx:]
            else:
                return data[idx:]
    return None


def main():
    pck_path = sys.argv[1] if len(sys.argv) > 1 else GAME_PCK

    if not os.path.exists(pck_path):
        print(f"PCK not found: {pck_path}")
        sys.exit(1)

    print(f"Reading PCK: {pck_path}")
    entries = read_pck_files(pck_path)
    print(f"Total entries: {len(entries)}")

    extracted = 0
    failed = 0

    for category, matcher in CATEGORIES.items():
        cat_dir = os.path.join(OUTPUT_DIR, category)
        os.makedirs(cat_dir, exist_ok=True)

        matching = [(p, o, s) for p, o, s, _ in entries if matcher(p.lower())]
        print(f"\n{category}: {len(matching)} files")

        for path, offset, size in matching:
            # Get clean name from the imported path
            # .godot/imported/attack_potion.png-hash.ctex -> attack_potion
            basename = os.path.basename(path)
            # Remove hash and extension
            name = basename.split(".")[0]
            # Remove _power suffix for powers since we'll add it back
            if category == "powers" and name.endswith("_power"):
                name_clean = name  # Keep as-is for matching
            else:
                name_clean = name

            img_data = extract_ctex_as_png(pck_path, offset, size)
            if img_data:
                # Determine output extension
                ext = "png" if img_data[:4] == b"\x89PNG" else "webp"
                out_path = os.path.join(cat_dir, f"{name_clean}.{ext}")
                with open(out_path, "wb") as f:
                    f.write(img_data)
                extracted += 1
                if extracted <= 30:
                    print(f"  OK: {name_clean}.{ext} ({len(img_data)} bytes)")
            else:
                failed += 1
                if failed <= 10:
                    print(f"  FAIL: {path} (no embedded PNG/WebP found, {size} bytes)")

    print(f"\nExtracted: {extracted}")
    print(f"Failed: {failed}")
    print(f"Output: {OUTPUT_DIR}")


if __name__ == "__main__":
    main()
