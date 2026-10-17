import sys
import numpy as np
from PIL import Image

def load(p, size=None):
    im = Image.open(p).convert("RGB")
    if size and im.size != size:
        im = im.resize(size, Image.LANCZOS)
    return im

def diff(a, b, thr=6):
    x = np.asarray(a, dtype=np.int16)
    y = np.asarray(b, dtype=np.int16)
    d = np.abs(x - y).max(axis=2)
    return int((d > thr).sum()), d.size, float(d.mean())

def main():
    before, after, out = sys.argv[1], sys.argv[2], sys.argv[3]
    sheet = sys.argv[4] if len(sys.argv) > 4 else None
    a = load(before)
    b = load(after, a.size)
    n, total, mean = diff(a, b)
    print(f"DIFF {n} of {total} px ({100.0 * n / total:.2f}%) mean {mean:.2f}")
    h = 720
    tiles = [im.resize((int(im.width * h / im.height), h), Image.LANCZOS) for im in [a, b] + ([load(sheet)] if sheet else [])]
    w = sum(t.width for t in tiles)
    canvas = Image.new("RGB", (w, h))
    x = 0
    for t in tiles:
        canvas.paste(t, (x, 0))
        x += t.width
    canvas.save(out)

main()