"""Generates the Goth Mermaid boss sprite sheet.

The sprite is authored as ASCII art (one pixel per character) plus a palette,
so it can be tweaked by hand without a pixel editor. Animation frames are
derived from the single drawing by swaying the tail with a sine wave, which
keeps the head and torso pixel-stable while the tail swishes.

Usage:  python tools/generate_goth_mermaid.py
Output: assets/bosses/goth_mermaid.png  (8 frames of 48x56, laid out in a row)
"""

import math
import os

FRAME_W, FRAME_H = 48, 56
FRAME_COUNT = 8

# Rows below this sway with the tail; the head and torso stay put.
TAIL_BEND_START_Y = 31
TAIL_BEND_AMPLITUDE = 3.0

PALETTE = {
    ".": (0, 0, 0, 0),
    "o": (255, 255, 255, 255),   # outline, matches the SpearFishing pack
    "h": (37, 23, 52, 255),      # hair, dark
    "H": (74, 45, 99, 255),      # hair, highlight
    "s": (242, 226, 235, 255),   # skin, pale
    "S": (208, 180, 198, 255),   # skin, shaded
    "e": (18, 12, 26, 255),      # eye / eyeliner
    "l": (170, 30, 70, 255),     # lipstick
    "t": (22, 19, 30, 255),      # corset, black
    "T": (46, 39, 62, 255),      # corset, highlight
    "c": (200, 32, 63, 255),     # choker + laces, blood red
    "f": (59, 29, 82, 255),      # tail, dark
    "F": (92, 44, 122, 255),     # tail, mid
    "G": (139, 76, 176, 255),    # tail, highlight
    "n": (176, 120, 214, 255),   # fin membrane
}

# 48 wide, 56 tall. Drawn upright and facing the camera; the game flips her
# horizontally when she swims left.
SPRITE = [
    "",
    "",
    "...................oooooooooo",
    "...............oohhhhhhhhhhhhhhoo",
    "............oohhhhhhhhhhhhhhhhhhhhoo",
    "..........oohhhhhhhhhhHHhhhhhhhhhhhhoo",
    ".........ohhhhhhhhhHHhhhhhhhhhhhhhhhho",
    "........ohhhhhhhhhsssssssssshhhhhhhhhho",
    "........ohhhhhhhhssssssssssssshhhhhhhho",
    "........ohhhhhhhsssssssssssssshhhhhhhho",
    "........ohhhhhhhsseeesssseeesshhhhhhhho",
    "........ohhhhhhhsseoesssseoesshhhhhhhho",
    "........ohhhhhhhssssssssssssshhhhhhhhho",
    "........ohhhhhhhssssssSsssssshhhhhhhhho",
    "........ohhhhhhhhsssslllssssshhhhhhhhho",
    "........ohhhhhhhhhsssllsssssshhhhhhhhho",
    "........ohhhhhhhhhsssssssssshhhhhhhhhho",
    "........ohhhhhhhhhhhsssssshhhhhhhhhhhho",
    "........ohhhhhhhhhhhcccccchhhhhhhhhhhho",
    ".......ohhhhhhhhhssstTTTTTTtssshhhhhhhhho",
    ".......ohhhhhhhssstTTTcccTTTtssshhhhhhhho",
    "......ohhhhhhhssstTTTTcccTTTTtssshhhhhhhho",
    "......ohhhhhhhsstTTTTTcccTTTTTtsshhhhhhhho",
    "......ohhhhhhhhstTTTTTcccTTTTTtshhhhhhhhho",
    "......ohhhhhhhhhstTTTTcccTTTTtshhhhhhhhhho",
    ".......ohhhhhhhhhstTTTTTTTTTtshhhhhhhhhho",
    ".......ohhhhhhhhhhstTTTTTTTTshhhhhhhhhho",
    "........ohhhhhhhhhhssssssssshhhhhhhhhho",
    "........ohhhhhhhhhhhssssssshhhhhhhhhhho",
    ".........ohhhhhhhhhhssssssshhhhhhhhhho",
    ".........ohhhhhhhhhsssssssssshhhhhhhhho",
    ".........ohhhhhhhhffFFFFFFFFFFfhhhhhhhho",
    ".........ohhhhhhhffFFFFGGGGFFFFfhhhhhhho",
    "..........ohhhhhhfFFFFGGGGGGFFFfhhhhhho",
    "...........ohhhhhfFFFGGGGGGGGFFfhhhhho",
    "............ohhhhfFFFGGGGGGGGFFf.ohhho",
    ".............ohhhfFFFGGGGGGGGFFf.ohho",
    "..............oo.ffFFGGGGGGGGFFf.oo",
    "...............offFFGGGGGGGGFFffo",
    "................offFGGGGGGGGFffo",
    "................offFGGGGGGGGFffo",
    ".................offFGGGGGGFffo",
    ".................offFGGGGGGFffo",
    "..................offGGGGGGffo",
    "................onnffGGGGGGffnno",
    "..............onnnnffGGGGGGffnnnno",
    "............onnnnnnffGGGGGGffnnnnnno",
    "...........onnnnnnnnffGGGGffnnnnnnnno",
    "..........onnnnnnnnnffGGGGffnnnnnnnnno",
    "..........onnnnnnnnnffGGGGffnnnnnnnnno",
    "...........onnnnnnnnffGGGGffnnnnnnnno",
    ".............onnnnnnnffGGffnnnnnnno",
    "...............onnnnnffGGffnnnnno",
    "..................onnnffffnnno",
    ".....................onffno",
    "",
]


def build_frame(sprite_rows, phase):
    """Return a frame as a list of (x, y, char), tail rows swayed by `phase`."""
    pixels = []
    span = max(1, FRAME_H - TAIL_BEND_START_Y)
    for y, row in enumerate(sprite_rows):
        if y > TAIL_BEND_START_Y:
            # The sway grows towards the tip of the tail.
            reach = (y - TAIL_BEND_START_Y) / span
            offset = round(math.sin(phase) * TAIL_BEND_AMPLITUDE * reach)
        else:
            offset = 0

        for x, char in enumerate(row):
            if char == ".":
                continue
            x_out = x + offset
            if 0 <= x_out < FRAME_W:
                pixels.append((x_out, y, char))
    return pixels


def main():
    from PIL import Image

    if len(SPRITE) != FRAME_H or any(len(r) > FRAME_W for r in SPRITE):
        raise SystemExit(f"SPRITE must be {FRAME_H} rows of at most {FRAME_W} characters")

    # Rows may be written short; trailing transparent pixels are implied.
    rows = [row.ljust(FRAME_W, ".") for row in SPRITE]

    sheet = Image.new("RGBA", (FRAME_W * FRAME_COUNT, FRAME_H), (0, 0, 0, 0))
    for frame_index in range(FRAME_COUNT):
        phase = (frame_index / FRAME_COUNT) * math.tau
        for x, y, char in build_frame(rows, phase):
            sheet.putpixel((frame_index * FRAME_W + x, y), PALETTE[char])

    out_dir = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "assets", "bosses")
    os.makedirs(out_dir, exist_ok=True)
    out_path = os.path.join(out_dir, "goth_mermaid.png")
    sheet.save(out_path)
    print(f"wrote {out_path} ({sheet.width}x{sheet.height}, {FRAME_COUNT} frames)")


if __name__ == "__main__":
    main()
