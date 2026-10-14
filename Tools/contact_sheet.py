"""Tile every PNG in a shots folder into one JPG contact sheet, each picture labelled with its file name.
Usage: python contact_sheet.py <shots folder> <out.jpg>"""
import os
import sys

from PIL import Image, ImageDraw

TILE_W, TILE_H, COLS, LABEL = 640, 360, 3, 22


def main():
    folder, out = sys.argv[1], sys.argv[2]
    names = sorted(n for n in os.listdir(folder) if n.lower().endswith(".png"))
    if not names:
        print("no shots in", folder)
        return
    rows = (len(names) + COLS - 1) // COLS
    sheet = Image.new("RGB", (COLS * TILE_W, rows * (TILE_H + LABEL)), (24, 24, 24))
    draw = ImageDraw.Draw(sheet)
    for i, name in enumerate(names):
        x, y = (i % COLS) * TILE_W, (i // COLS) * (TILE_H + LABEL)
        with Image.open(os.path.join(folder, name)) as im:
            sheet.paste(im.convert("RGB").resize((TILE_W, TILE_H)), (x, y + LABEL))
        draw.text((x + 6, y + 4), name, fill=(230, 230, 230))
    sheet.save(out, quality=85)
    print("sheet:", out, len(names), "shots")


if __name__ == "__main__":
    main()
