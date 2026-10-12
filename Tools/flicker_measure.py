"""Scores shimmer in the -chopperFlicker frame sequences (ShotCapture.FlickerTour).

The camera orbits slowly at a fixed frame step, so real image change is smooth from frame to frame. Flicker is a pixel
that goes up then down (or down then up) between three frames. Per pixel: the second difference
|f(t-1) - 2 f(t) + f(t+1)| of luminance, counted only where the two first differences have opposite signs.
Reported per region: character (pixels that change when the player is hidden, mask.png against f000.png) and ground
(the lower half of the picture without the character).

Run:  python Tools/flicker_measure.py Logs/chopper_play/flicker2/flicker
Out:  one line per sequence: mean flicker (0..255 scale) and the share of pixels above 12 levels, for each region.
"""
import os
import sys

import numpy as np
from PIL import Image


def luma(path, scale):
    im = Image.open(path).convert("RGB")
    if scale != 1:
        im = im.resize((im.width // scale, im.height // scale), Image.BOX)
    a = np.asarray(im, dtype=np.float32)
    return 0.299 * a[..., 0] + 0.587 * a[..., 1] + 0.114 * a[..., 2]


def main(root, scale=1):
    rows = []
    masks = {}
    for name in sorted(os.listdir(root)):
        seq = os.path.join(root, name)
        frames = sorted(f for f in os.listdir(seq) if f.startswith("f") and f.endswith(".png"))
        if len(frames) < 3:
            continue
        place = name.split("_")[0]
        stack = np.stack([luma(os.path.join(seq, f), scale) for f in frames])
        if os.path.exists(os.path.join(seq, "mask.png")):
            hidden = luma(os.path.join(seq, "mask.png"), scale)
            masks[place] = np.abs(stack[0] - hidden) > 6
        char = masks.get(place)
        if char is None:
            continue
        h = stack.shape[1]
        ground = np.zeros_like(char)
        ground[h // 2:] = True
        ground &= ~char
        d1 = stack[1:-1] - stack[:-2]
        d2 = stack[2:] - stack[1:-1]
        flick = np.where(np.sign(d1) * np.sign(d2) < 0, np.abs(d2 - d1), 0.0)
        out = [name]
        for label, region in (("character", char), ("ground", ground)):
            v = flick[:, region]
            out.append("%s %.2f mean, %.2f%% > 12" % (label, v.mean(), 100.0 * (v > 12).mean()))
        rows.append(out)
        print("  ".join(out), flush=True)
    return rows


if __name__ == "__main__":
    main(sys.argv[1], int(sys.argv[2]) if len(sys.argv) > 2 else 1)
