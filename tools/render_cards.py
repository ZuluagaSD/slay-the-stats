"""Pre-render full card images by compositing art + frame + name text."""
import os
from PIL import Image, ImageDraw, ImageFont

CARDS_DIR = os.path.join(os.path.dirname(__file__), "..", "web", "public", "game-assets", "cards")
FULL_DIR = os.path.join(os.path.dirname(__file__), "..", "web", "public", "game-assets", "cards-full")

# Card dimensions (scaled for web display)
CARD_W = 180
CARD_H = 252
ART_Y = 18  # Top padding for art
ART_H = 137  # Art height
NAME_H = 26  # Name plate height
NAME_Y = ART_Y + ART_H  # Where name plate starts
DESC_Y = NAME_Y + NAME_H  # Description area
BORDER_R = 12  # Corner radius

# Card type colors
TYPES = {
    'attack': {'frame': (153, 27, 27), 'frame_dark': (100, 15, 15), 'name_bg': (120, 20, 20), 'accent': (220, 60, 60)},
    'skill': {'frame': (30, 64, 128), 'frame_dark': (18, 40, 80), 'name_bg': (25, 55, 110), 'accent': (60, 120, 220)},
    'power': {'frame': (100, 30, 160), 'frame_dark': (60, 18, 100), 'name_bg': (80, 25, 130), 'accent': (160, 80, 220)},
    'curse': {'frame': (60, 55, 50), 'frame_dark': (35, 32, 30), 'name_bg': (45, 40, 38), 'accent': (100, 90, 85)},
    'status': {'frame': (60, 55, 50), 'frame_dark': (35, 32, 30), 'name_bg': (45, 40, 38), 'accent': (100, 90, 85)},
}


def guess_card_type(name):
    n = name.lower()
    if any(w in n for w in ['strike', 'bash', 'carnage', 'slash', 'throw', 'blade', 'bite', 'claw',
                             'ricochet', 'poison', 'gamble', 'pillage', 'pummel', 'shiv', 'dagger',
                             'barrage', 'flechettes', 'skewer', 'finisher', 'eviscerate', 'predator',
                             'riddle', 'sucker', 'cleave', 'iron_wave', 'headbutt', 'anger', 'clash',
                             'clothesline', 'heavy', 'pommel', 'rampage', 'reckless', 'searing',
                             'sword', 'twin', 'uppercut', 'whirlwind', 'bludgeon', 'immolate',
                             'reaper', 'attack', 'molten', 'ashen', 'dominate', 'fight',
                             'sculpting', 'bone_shard', 'defile', 'debilitate', 'flick', 'null',
                             'setup_strike', 'pull_from', 'leading', 'echoing', 'storm',
                             'ball_lightning', 'beam', 'blizzard', 'cold_snap', 'compile',
                             'doom', 'ftl', 'go_for', 'hyperbeam', 'meteor', 'rip', 'sunder',
                             'tempest', 'thunder', 'flying', 'bowling', 'conclude', 'crush',
                             'cut', 'empty', 'flurry', 'follow', 'halt', 'just', 'reach',
                             'sanctity', 'sash', 'signature', 'tantrum', 'wallop', 'weave',
                             'wheel', 'windmill', 'wreath']):
        return 'attack'
    if any(w in n for w in ['power', 'form', 'demon', 'noxious', 'infinite', 'corruption',
                             'berserk', 'brutality', 'dark_embrace', 'evolve', 'feel_no',
                             'fire_breath', 'inflame', 'juggernaut', 'metallicize', 'rupture',
                             'accuracy', 'after_image', 'caltrops', 'envenom', 'nightmare',
                             'phantasmal', 'thousand', 'tools', 'well_laid', 'a_thousand',
                             'buffer', 'creative_ai', 'defragment', 'echo', 'electrodynamics',
                             'heatsink', 'hello_world', 'loop', 'machine', 'storm_power',
                             'static', 'devotion', 'establishment', 'fasting', 'like_water',
                             'mental_fortress', 'rushdown', 'study', 'wish', 'battle_hymn',
                             'deva', 'master_reality', 'omega', 'spirit_shield', 'vault',
                             'hellraiser', 'soul', 'grasp', 'necro', 'unleash', 'bodyguard',
                             'melancholy', 'hotfix', 'overclock', 'capacitor']):
        return 'power'
    if any(w in n for w in ['curse', 'regret', 'pain', 'decay', 'doubt', 'shame', 'parasite',
                             'normality', 'injury', 'ascenders_bane']):
        return 'curse'
    if any(w in n for w in ['wound', 'burn', 'dazed', 'void', 'slime']):
        return 'status'
    return 'skill'


def rounded_rect(draw, xy, radius, fill, outline=None):
    x0, y0, x1, y1 = xy
    draw.rounded_rectangle(xy, radius=radius, fill=fill, outline=outline)


def render_card(card_name, art_path, card_type, upgrade_level=0):
    colors = TYPES.get(card_type, TYPES['skill'])
    is_upgraded = upgrade_level > 0

    # Create card canvas
    img = Image.new('RGBA', (CARD_W, CARD_H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Card background
    rounded_rect(draw, (0, 0, CARD_W - 1, CARD_H - 1), BORDER_R,
                 fill=colors['frame_dark'],
                 outline=(*colors['frame'], 255) if not is_upgraded else (212, 168, 67, 255))

    # Inner background (slightly inset)
    rounded_rect(draw, (3, 3, CARD_W - 4, CARD_H - 4), BORDER_R - 2,
                 fill=(12, 10, 20, 255))

    # Load and paste card art
    try:
        art = Image.open(art_path).convert('RGBA')
        art = art.resize((CARD_W - 12, ART_H), Image.LANCZOS)
        img.paste(art, (6, ART_Y), art)
    except Exception:
        # Fallback: dark rectangle
        draw.rectangle((6, ART_Y, CARD_W - 7, ART_Y + ART_H), fill=(20, 18, 30))

    # Art border line at bottom
    draw.line([(4, ART_Y + ART_H), (CARD_W - 5, ART_Y + ART_H)],
              fill=(*colors['frame'], 200), width=1)

    # Name plate background
    draw.rectangle((4, NAME_Y + 1, CARD_W - 5, NAME_Y + NAME_H),
                   fill=colors['name_bg'])

    # Card name text
    display_name = card_name.replace('_', ' ').title()
    if len(display_name) > 18:
        display_name = display_name[:17] + '…'

    try:
        font = ImageFont.truetype("arial.ttf", 13)
        font_small = ImageFont.truetype("arial.ttf", 9)
    except Exception:
        font = ImageFont.load_default()
        font_small = font

    text_color = (240, 208, 120) if is_upgraded else (230, 230, 230)
    bbox = draw.textbbox((0, 0), display_name, font=font)
    text_w = bbox[2] - bbox[0]
    text_x = (CARD_W - text_w) // 2
    draw.text((text_x, NAME_Y + 5), display_name, fill=text_color, font=font)

    # Name plate bottom line
    draw.line([(4, NAME_Y + NAME_H), (CARD_W - 5, NAME_Y + NAME_H)],
              fill=(*colors['frame'], 150), width=1)

    # Description area (dark fill)
    draw.rectangle((4, DESC_Y + 1, CARD_W - 5, CARD_H - 5 - 18),
                   fill=(15, 13, 25, 255))

    # Card type label at very bottom
    type_label = card_type.upper()
    bbox2 = draw.textbbox((0, 0), type_label, font=font_small)
    tw2 = bbox2[2] - bbox2[0]
    draw.text(((CARD_W - tw2) // 2, CARD_H - 18), type_label,
              fill=(150, 140, 160), font=font_small)

    # Upgrade badge
    if is_upgraded:
        badge_text = f"+{upgrade_level}"
        bx, by = CARD_W - 26, 4
        draw.ellipse((bx, by, bx + 22, by + 22), fill=(212, 168, 67), outline=(255, 220, 120))
        bbx = draw.textbbox((0, 0), badge_text, font=font_small)
        btw = bbx[2] - bbx[0]
        draw.text((bx + (22 - btw) // 2, by + 5), badge_text, fill=(20, 10, 0), font=font_small)

    # Upgraded gold glow border
    if is_upgraded:
        rounded_rect(draw, (0, 0, CARD_W - 1, CARD_H - 1), BORDER_R,
                     fill=None, outline=(212, 168, 67, 255))

    return img


def main():
    os.makedirs(FULL_DIR, exist_ok=True)

    # Get all card art files
    art_files = [f for f in os.listdir(CARDS_DIR) if f.endswith('.png')]
    print(f"Found {len(art_files)} card art files")

    rendered = 0
    for filename in sorted(art_files):
        card_name = filename.replace('.png', '')
        art_path = os.path.join(CARDS_DIR, filename)
        card_type = guess_card_type(card_name)

        # Render normal version
        img = render_card(card_name, art_path, card_type, 0)
        img.save(os.path.join(FULL_DIR, f"{card_name}.png"))

        # Render upgraded version
        img_up = render_card(card_name, art_path, card_type, 1)
        img_up.save(os.path.join(FULL_DIR, f"{card_name}_upgraded.png"))

        rendered += 1
        if rendered <= 5 or rendered % 100 == 0:
            print(f"  [{rendered}] {card_name} ({card_type})")

    print(f"\nRendered {rendered} cards (x2 for upgraded = {rendered * 2} images)")
    print(f"Output: {FULL_DIR}")


if __name__ == "__main__":
    main()
