import sys, glob, os, math, wave
import numpy as np

folder = sys.argv[1]
rows = []
for path in sorted(glob.glob(os.path.join(folder, "*.wav"))):
    with wave.open(path) as w:
        n = w.getnframes()
        rate = w.getframerate()
        ch = w.getnchannels()
        data = np.frombuffer(w.readframes(n), dtype=np.int16).astype(np.float32) / 32768.0
    if ch > 1:
        data = data.reshape(-1, ch).mean(axis=1)
    dur = len(data) / rate
    peak = float(np.max(np.abs(data))) + 1e-9
    rms = float(math.sqrt(np.mean(data ** 2))) + 1e-9
    # RMS over the loudest 50 percent of 20 ms blocks: the sound while it is actually sounding
    block = int(rate * 0.02)
    blocks = len(data) // block
    if blocks > 0:
        energy = np.sqrt(np.mean(data[:blocks * block].reshape(blocks, block) ** 2, axis=1))
        top = np.sort(energy)[-max(1, blocks // 2):]
        active = float(np.sqrt(np.mean(top ** 2))) + 1e-9
    else:
        active = rms
    spec = np.abs(np.fft.rfft(data * np.hanning(len(data))))
    freqs = np.fft.rfftfreq(len(data), 1.0 / rate)
    power = spec ** 2
    total = power.sum() + 1e-12
    centroid = float((freqs * power).sum() / total)
    low = float(power[freqs < 200].sum() / total)
    mid = float(power[(freqs >= 200) & (freqs < 2000)].sum() / total)
    high = float(power[freqs >= 2000].sum() / total)
    clipped = int(np.sum(np.abs(data) > 0.999))
    rows.append((os.path.basename(path)[:-4], dur, 20 * math.log10(peak), 20 * math.log10(rms), 20 * math.log10(active), centroid, low, mid, high, clipped))

print(f"{'sound':14s} {'sec':>5s} {'peak':>6s} {'rms':>6s} {'active':>7s} {'centroid':>9s} {'low%':>5s} {'mid%':>5s} {'high%':>5s} {'clip':>5s}")
for r in rows:
    print(f"{r[0]:14s} {r[1]:5.2f} {r[2]:6.1f} {r[3]:6.1f} {r[4]:7.1f} {r[5]:9.0f} {r[6]*100:5.0f} {r[7]*100:5.0f} {r[8]*100:5.0f} {r[9]:5d}")

# Spectrogram sheet for the ambient loops and a few sounds
if len(sys.argv) > 2:
    from PIL import Image
    names = sys.argv[2].split(",")
    tiles = []
    for name in names:
        with wave.open(os.path.join(folder, name + ".wav")) as w:
            rate = w.getframerate()
            d = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float32) / 32768.0
        win = 1024
        hop = 512
        frames = (len(d) - win) // hop
        S = np.array([np.abs(np.fft.rfft(d[i * hop:i * hop + win] * np.hanning(win)))[:256] for i in range(frames)]).T
        S = 20 * np.log10(S + 1e-6)
        S = np.clip((S - S.max() + 60) / 60, 0, 1)
        img = (S[::-1] * 255).astype(np.uint8)
        im = Image.fromarray(img).resize((min(800, img.shape[1]), 256))
        tiles.append((name, im))
    sheet = Image.new("L", (800, 270 * len(tiles)))
    for i, (name, im) in enumerate(tiles):
        sheet.paste(im, (0, i * 270))
    sheet.save(os.path.join(folder, "spectrograms.png"))
    print("spectrograms:", names)
