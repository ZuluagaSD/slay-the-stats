"""Extract all game art assets (potions, powers, relics) from STS2 PCK as PNGs/WebPs.
Handles both raw WebP and S3TC/DXT5 compressed Godot textures."""
import struct
import os
import sys
from PIL import Image
import texture2ddecoder

GAME_PCK = r"C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\SlayTheSpire2.pck"
OUTPUT_DIR = os.path.join(os.path.dirname(__file__), "..", "web", "public", "game-assets")


def read_pck(pck_path):
    entries = []
    with open(pck_path, "rb") as f:
        f.read(4)
        f.read(4)
        f.read(12)
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
            f.read(16)
            flags = struct.unpack("<I", f.read(4))[0]
            entries.append((path, data_start + offset, size))
    return entries


def read_file(pck_path, offset, size):
    with open(pck_path, "rb") as f:
        f.seek(offset)
        return f.read(size)


def decode_gst2_s3tc(data):
    """Decode a GST2 S3TC/DXT5 compressed texture to PIL Image."""
    if data[:4] != b"GST2":
        return None

    # Parse header
    version = struct.unpack_from("<I", data, 4)[0]
    width = struct.unpack_from("<I", data, 8)[0]
    height = struct.unpack_from("<I", data, 12)[0]

    # Find the actual image dimensions and data
    # GST2 header is variable, but the image data for DXT5 follows
    # For 256x256 DXT5: each 4x4 block = 16 bytes, so (256/4)*(256/4)*16 = 65536 bytes
    # Try to find where the compressed data starts by looking for known header sizes

    # Common header sizes: 32, 36, 48, 52 bytes
    for header_size in [52, 48, 44, 40, 36, 32]:
        remaining = len(data) - header_size
        expected_dxt5 = max(1, width // 4) * max(1, height // 4) * 16

        if remaining >= expected_dxt5 and remaining - expected_dxt5 < 64:
            try:
                pixels = texture2ddecoder.decode_bc3(data[header_size:header_size + expected_dxt5], width, height)
                img = Image.frombytes("RGBA", (width, height), pixels, "raw", "BGRA")
                return img
            except Exception:
                continue

    # Try DXT1 (BC1) as fallback
    for header_size in [52, 48, 44, 40, 36, 32]:
        remaining = len(data) - header_size
        expected_dxt1 = max(1, width // 4) * max(1, height // 4) * 8

        if remaining >= expected_dxt1 and remaining - expected_dxt1 < 64:
            try:
                pixels = texture2ddecoder.decode_bc1(data[header_size:header_size + expected_dxt1], width, height)
                img = Image.frombytes("RGBA", (width, height), pixels, "raw", "BGRA")
                return img
            except Exception:
                continue

    return None


def extract_image(pck_path, offset, size):
    """Extract an image from a PCK entry, handling WebP, PNG, and S3TC formats."""
    data = read_file(pck_path, offset, size)

    # Check for embedded WebP
    webp_idx = data.find(b"RIFF")
    if webp_idx >= 0 and webp_idx + 12 < len(data) and data[webp_idx + 8:webp_idx + 12] == b"WEBP":
        return data[webp_idx:], "webp"

    # Check for embedded PNG
    png_idx = data.find(b"\x89PNG")
    if png_idx >= 0:
        return data[png_idx:], "png"

    # Try S3TC decode
    if data[:4] == b"GST2":
        img = decode_gst2_s3tc(data)
        if img:
            import io
            buf = io.BytesIO()
            img.save(buf, "PNG")
            return buf.getvalue(), "png"

    return None, None


def get_asset_name(path):
    """Extract clean name from .godot/imported/xxx.png-hash.ctex."""
    basename = os.path.basename(path)
    # Remove hash: split on .png- or similar
    for ext in [".png-", ".jpg-", ".webp-", ".exr-"]:
        idx = basename.find(ext)
        if idx >= 0:
            return basename[:idx]
    return basename.split(".")[0]


def main():
    pck_path = sys.argv[1] if len(sys.argv) > 1 else GAME_PCK
    print(f"Reading PCK: {pck_path}")
    entries = read_pck(pck_path)
    print(f"Total entries: {len(entries)}\n")

    # Define what to extract
    categories = {
        "potions": lambda p: "potion" in p.lower() and ".ctex" in p and "outline" not in p.lower() and "atlas" not in p.lower() and "power" not in p.lower() and "courier" not in p.lower() and "submenu" not in p.lower() and "future" not in p.lower(),
        "powers": lambda p: "_power." in p.lower() and ".ctex" in p and "atlas" not in p.lower(),
        "relics": lambda p: ("/relics/" in p.lower() or "relic" in p.lower()) and ".ctex" in p and "atlas" not in p.lower() and "outline" not in p.lower() and "epoch" not in p.lower() and "inspect" not in p.lower() and "frame" not in p.lower() and "no_relic" not in p.lower(),
    }

    total_extracted = 0
    total_failed = 0

    for cat_name, matcher in categories.items():
        cat_dir = os.path.join(OUTPUT_DIR, cat_name)
        os.makedirs(cat_dir, exist_ok=True)

        matching = [(p, o, s) for p, o, s in entries if matcher(p)]
        print(f"{cat_name}: {len(matching)} files")

        extracted = 0
        for path, offset, size in matching:
            name = get_asset_name(path)
            img_data, ext = extract_image(pck_path, offset, size)

            if img_data:
                out_path = os.path.join(cat_dir, f"{name}.{ext}")
                with open(out_path, "wb") as f:
                    f.write(img_data)
                extracted += 1
                if extracted <= 5:
                    print(f"  OK: {name}.{ext} ({len(img_data)} bytes)")
            else:
                total_failed += 1
                if total_failed <= 3:
                    print(f"  FAIL: {name} ({size} bytes)")

        total_extracted += extracted
        print(f"  Extracted: {extracted}/{len(matching)}\n")

    print(f"\nTotal extracted: {total_extracted}")
    print(f"Total failed: {total_failed}")
    print(f"Output: {OUTPUT_DIR}")

    # List output
    for cat in categories:
        cat_dir = os.path.join(OUTPUT_DIR, cat)
        if os.path.exists(cat_dir):
            files = os.listdir(cat_dir)
            print(f"\n{cat}/: {len(files)} files")
            for f in sorted(files)[:5]:
                print(f"  {f}")
            if len(files) > 5:
                print(f"  ... and {len(files) - 5} more")


if __name__ == "__main__":
    main()
