"""Extract individual card art from STS2 sprite atlases."""
import struct
import os
import re
import io
from PIL import Image
import texture2ddecoder

GAME_PCK = r"C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\SlayTheSpire2.pck"
OUTPUT_DIR = os.path.join(os.path.dirname(__file__), "..", "web", "public", "game-assets", "cards")


def read_pck(pck_path):
    entries = {}
    with open(pck_path, "rb") as f:
        f.read(4); f.read(4); f.read(12)
        reserved = struct.unpack("<16I", f.read(64))
        data_start = reserved[1]
        table_offset = reserved[3] | (reserved[4] << 32)
        f.read(4)
        f.seek(table_offset)
        count = struct.unpack("<I", f.read(4))[0]
        for _ in range(count):
            path_len = struct.unpack("<I", f.read(4))[0]
            path = f.read(path_len).rstrip(b"\x00").decode("utf-8", errors="replace")
            offset = struct.unpack("<q", f.read(8))[0]
            size = struct.unpack("<q", f.read(8))[0]
            f.read(16); f.read(4)
            entries[path] = (data_start + offset, size)
    return entries


def read_file(pck_path, offset, size):
    with open(pck_path, "rb") as f:
        f.seek(offset)
        return f.read(size)


def decode_bptc_atlas(data):
    """Decode a BPTC (BC7) compressed atlas texture."""
    if data[:4] != b"GST2":
        return None

    # Parse GST2 header
    version = struct.unpack_from("<I", data, 4)[0]
    width = struct.unpack_from("<I", data, 8)[0]
    height = struct.unpack_from("<I", data, 12)[0]
    print(f"  Atlas dimensions: {width}x{height}")

    # BC7 block size: each 4x4 block = 16 bytes
    blocks_w = max(1, (width + 3) // 4)
    blocks_h = max(1, (height + 3) // 4)
    expected_size = blocks_w * blocks_h * 16

    # Try different header sizes
    for header_size in [52, 48, 44, 40, 36, 32]:
        remaining = len(data) - header_size
        if remaining >= expected_size:
            try:
                pixels = texture2ddecoder.decode_bc7(data[header_size:header_size + expected_size], width, height)
                img = Image.frombytes("RGBA", (width, height), pixels, "raw", "BGRA")
                print(f"  Decoded BC7 with header_size={header_size}")
                return img
            except Exception as e:
                print(f"  BC7 failed at header_size={header_size}: {e}")
                continue

    # Try BC3 (DXT5) as fallback
    expected_bc3 = blocks_w * blocks_h * 16
    for header_size in [52, 48, 44, 40, 36, 32]:
        remaining = len(data) - header_size
        if remaining >= expected_bc3:
            try:
                pixels = texture2ddecoder.decode_bc3(data[header_size:header_size + expected_bc3], width, height)
                img = Image.frombytes("RGBA", (width, height), pixels, "raw", "BGRA")
                print(f"  Decoded BC3 with header_size={header_size}")
                return img
            except Exception:
                continue

    return None


def parse_tres(content):
    """Parse a .tres file to get atlas index and region rect."""
    # Find which atlas: card_atlas_0.png, card_atlas_1.png, card_atlas_2.png
    atlas_match = re.search(r'card_atlas_(\d+)\.png', content)
    if not atlas_match:
        return None
    atlas_idx = int(atlas_match.group(1))

    # Find region rect
    rect_match = re.search(r'region\s*=\s*Rect2\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)', content)
    if not rect_match:
        return None

    x, y, w, h = int(rect_match.group(1)), int(rect_match.group(2)), int(rect_match.group(3)), int(rect_match.group(4))
    return atlas_idx, x, y, w, h


def main():
    print(f"Reading PCK: {GAME_PCK}")
    entries = read_pck(GAME_PCK)
    print(f"Total entries: {len(entries)}")

    # Step 1: Decode the 3 atlas textures
    atlas_images = {}
    for i in range(3):
        # Find the atlas ctex file
        atlas_key = None
        for path in entries:
            if f"card_atlas_{i}.png" in path and ".ctex" in path:
                atlas_key = path
                break

        if not atlas_key:
            print(f"Atlas {i} not found!")
            continue

        offset, size = entries[atlas_key]
        print(f"\nDecoding atlas {i}: {atlas_key} ({size / 1024 / 1024:.1f} MB)")
        data = read_file(GAME_PCK, offset, size)
        img = decode_bptc_atlas(data)
        if img:
            atlas_images[i] = img
            print(f"  OK: {img.size}")
        else:
            print(f"  FAILED to decode atlas {i}")

    if not atlas_images:
        print("No atlases decoded!")
        return

    # Step 2: Find all card .tres files and extract sprites
    os.makedirs(OUTPUT_DIR, exist_ok=True)

    tres_files = [(p, *entries[p]) for p in entries if "card_atlas.sprites" in p and p.endswith(".tres") and "/beta/" not in p]
    print(f"\nCard sprite definitions: {len(tres_files)}")

    extracted = 0
    failed = 0

    for path, offset, size in tres_files:
        content = read_file(GAME_PCK, offset, size).decode("utf-8", errors="replace")
        result = parse_tres(content)
        if not result:
            failed += 1
            continue

        atlas_idx, x, y, w, h = result
        if atlas_idx not in atlas_images:
            failed += 1
            continue

        # Get card name from path: .../silent/strike_silent.tres -> strike_silent
        card_name = os.path.basename(path).replace(".tres", "")

        # Crop the sprite from the atlas
        atlas = atlas_images[atlas_idx]
        try:
            sprite = atlas.crop((x, y, x + w, y + h))
            out_path = os.path.join(OUTPUT_DIR, f"{card_name}.png")
            sprite.save(out_path, "PNG")
            extracted += 1
            if extracted <= 10 or extracted % 100 == 0:
                print(f"  [{extracted}] {card_name}.png ({w}x{h})")
        except Exception as e:
            failed += 1
            if failed <= 5:
                print(f"  FAIL: {card_name} at ({x},{y},{w},{h}) in atlas {atlas_idx}: {e}")

    print(f"\nExtracted: {extracted} card sprites")
    print(f"Failed: {failed}")
    print(f"Output: {OUTPUT_DIR}")


if __name__ == "__main__":
    main()
