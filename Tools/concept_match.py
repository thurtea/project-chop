import json
import os
import sys
from datetime import datetime

from PIL import Image, ImageDraw

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
REGIONS = os.path.join(REPO, "Chopper", "Art", "concept_regions.json")
OUT = os.path.join(os.path.expanduser("~"), "chopper-shots", "concept_match")


def srgb_to_lab(rgb):
    def lin(c):
        c = c / 255.0
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    r, g, b = (lin(v) for v in rgb)
    x = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047
    y = 0.2126 * r + 0.7152 * g + 0.0722 * b
    z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883

    def f(t):
        return t ** (1 / 3) if t > 0.008856 else 7.787 * t + 16 / 116
    fx, fy, fz = f(x), f(y), f(z)
    return (116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz))


def delta_e(a, b):
    return sum((a[i] - b[i]) ** 2 for i in range(3)) ** 0.5


def box_pixels(img, box):
    w, h = img.size
    x0, y0, x1, y1 = (int(round(box[0] * w / 100)), int(round(box[1] * h / 100)), int(round(box[2] * w / 100)), int(round(box[3] * h / 100)))
    crop = img.crop((x0, y0, max(x1, x0 + 1), max(y1, y0 + 1)))
    return list(crop.getdata()), (x0, y0, x1, y1)


def background(img):
    w, h = img.size
    corners = [img.getpixel((2, 2)), img.getpixel((w - 3, 2)), img.getpixel((2, h - 3)), img.getpixel((w - 3, h - 3))]
    return srgb_to_lab(tuple(sum(c[i] for c in corners) / 4 for i in range(3)))


def tones(pixels, bg):
    labs = [srgb_to_lab(p) for p in pixels]
    labs = [l for l in labs if delta_e(l, bg) > 6] or labs
    labs.sort(key=lambda l: l[0])
    n = len(labs)

    def mean(part):
        return tuple(sum(l[i] for l in part) / len(part) for i in range(3))
    dark = mean(labs[: max(1, n // 5)])
    mid = mean(labs[int(n * 0.4): max(int(n * 0.4) + 1, int(n * 0.6))])
    light = mean(labs[n - max(1, n // 5):])
    return dark, mid, light, len(labs)


def lab_to_srgb(lab):
    L, a, b = lab
    fy = (L + 16) / 116
    fx, fz = fy + a / 500, fy - b / 200

    def finv(t):
        return t ** 3 if t ** 3 > 0.008856 else (t - 16 / 116) / 7.787
    x, y, z = finv(fx) * 0.95047, finv(fy), finv(fz) * 1.08883
    r = 3.2406 * x - 1.5372 * y - 0.4986 * z
    g = -0.9689 * x + 1.8758 * y + 0.0415 * z
    bl = 0.0557 * x - 0.2040 * y + 1.0570 * z

    def gam(c):
        c = max(0.0, min(1.0, c))
        return int(round(255 * (12.92 * c if c <= 0.0031308 else 1.055 * c ** (1 / 2.4) - 0.055)))
    return gam(r), gam(g), gam(bl)


def score_model(name, spec):
    render = Image.open(os.path.join(REPO, spec["render"])).convert("RGB")
    concept = Image.open(os.path.join(REPO, spec["concept"])).convert("RGB")
    rbg, cbg = background(render), background(concept)
    rows = []
    rdraw_img, cdraw_img = render.copy(), concept.copy()
    rd, cd = ImageDraw.Draw(rdraw_img), ImageDraw.Draw(cdraw_img)
    for part, boxes in spec["parts"].items():
        rp, rbox = box_pixels(render, boxes["render"])
        cp, cbox = box_pixels(concept, boxes["concept"])
        rt, ct = tones(rp, rbg)[:3], tones(cp, cbg)[:3]
        des = [delta_e(rt[i], ct[i]) for i in range(3)]
        rows.append((part, des, rt, ct))
        for d, b in ((rd, rbox), (cd, cbox)):
            d.rectangle(b, outline=(255, 255, 0), width=3)
            d.text((b[0] + 3, b[1] + 3), part, fill=(255, 255, 0))
    return rows, rdraw_img, cdraw_img


def sheet(name, rows, rimg, cimg):
    H = 700
    r = rimg.resize((int(rimg.width * H / rimg.height), H))
    c = cimg.resize((int(cimg.width * H / cimg.height), H))
    sw = 34
    table_h = 30 + 28 * len(rows)
    out = Image.new("RGB", (r.width + c.width, H + table_h), (24, 24, 28))
    out.paste(r, (0, 0))
    out.paste(c, (r.width, 0))
    d = ImageDraw.Draw(out)
    d.text((10, H + 8), f"{name}: part   dE dark / mid / light   swatches render (top) vs concept (bottom)", fill=(230, 230, 230))
    for i, (part, des, rt, ct) in enumerate(rows):
        y = H + 30 + i * 28
        d.text((10, y + 6), f"{part:<10} {des[0]:5.1f} {des[1]:5.1f} {des[2]:5.1f}   mean {sum(des) / 3:5.1f}", fill=(230, 230, 230))
        for k in range(3):
            x = 330 + k * (sw * 2 + 8)
            d.rectangle((x, y, x + sw, y + 12), fill=lab_to_srgb(rt[k]))
            d.rectangle((x, y + 13, x + sw, y + 25), fill=lab_to_srgb(ct[k]))
            d.rectangle((x + sw, y, x + 2 * sw, y + 25), fill=lab_to_srgb(ct[k]))
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    data = json.load(open(REGIONS, encoding="utf8"))
    only = sys.argv[1:]
    stamp = datetime.now().strftime("%m%d-%H%M")
    lines = []
    for name, spec in data.items():
        if name.startswith("_") or (only and name not in only):
            continue
        rows, rimg, cimg = score_model(name, spec)
        model = sum(sum(d) / 3 for _, d, _, _ in rows) / len(rows)
        worst = max(rows, key=lambda r: sum(r[1]))
        path = os.path.join(OUT, f"{name}-{stamp}.jpg")
        sheet(name, rows, rimg, cimg).save(path, quality=88)
        parts = ", ".join(f"{p} {sum(d) / 3:.1f}" for p, d, _, _ in rows)
        line = f"[ConceptMatch] {name}: mean dE {model:.1f}; worst {worst[0]} {sum(worst[1]) / 3:.1f}; parts {parts}; sheet {path}"
        print(line)
        lines.append(line)
    with open(os.path.join(OUT, f"scores-{stamp}.txt"), "w", encoding="utf8") as f:
        f.write("\n".join(lines) + "\n")


if __name__ == "__main__":
    main()
