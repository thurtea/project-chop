using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chopper.Audio
{
    // Placeholder sounds synthesized in code so every action has audio feedback before real CC0 samples are added.
    // Deterministic: the noise generators use fixed seeds. A recording with the right name in Resources/Audio takes
    // over automatically (SoundBank).
    public static class ProceduralSfx
    {
        private const int SampleRate = 44100;
        private const int NoiseSeed = 4242;
        private const int FootstepVariants = 3;

        private static AudioClip _chop, _fell, _swing, _pickup, _deposit, _upgrade, _hurt, _horn, _hutHit, _block, _parry, _splash, _click, _hover;
        private static readonly AudioClip[] _footsteps = new AudioClip[FootstepVariants];
        private static readonly AudioClip[] _dirtSteps = new AudioClip[FootstepVariants];

        // A recording in Resources/Audio with this name (or name-1, name-2 ...) replaces the synthesized sound; see
        // SoundBank and the audio list in requirements.md.
        private static AudioClip RealOr(string file, string synthName, ref AudioClip cache, Func<AudioClip> build)
        {
            AudioClip real = SoundBank.Pick(file, LevelFor(synthName));
            if (real != null) return real;
            if (cache == null) cache = build();
            return cache;
        }

        // Axe hitting a tree or an enemy (the real axe recording ships with the game).
        public static AudioClip Chop => RealOr("axe-impact", "Chop", ref _chop, () => Tone("Chop", 0.16f, 170f, 70f, 0.35f, 38f, 0.9f));
        public static AudioClip Fell => RealOr("tree-fall", "Fell", ref _fell, () => Tone("Fell", 1.4f, 90f, 35f, 0.5f, 4f, 1f));
        public static AudioClip HutHit => RealOr("hut-hit", "HutHit", ref _hutHit, () => Tone("HutHit", 0.35f, 95f, 45f, 0.45f, 10f, 1f));
        public static AudioClip Swing => RealOr("axe-slash", "Swing", ref _swing, BuildSwing);
        public static AudioClip Pickup => RealOr("wood-pickup", "Pickup", ref _pickup, () => Tone("Pickup", 0.12f, 520f, 780f, 0.05f, 22f, 0.6f));
        public static AudioClip Deposit => RealOr("wood-deposit", "Deposit", ref _deposit, BuildDeposit);
        public static AudioClip Upgrade => RealOr("upgrade", "Upgrade", ref _upgrade, BuildUpgrade);
        public static AudioClip Hurt => RealOr("player-hurt", "Hurt", ref _hurt, () => Tone("Hurt", 0.3f, 210f, 90f, 0.3f, 9f, 0.9f));
        public static AudioClip Horn => RealOr("night-horn", "Horn", ref _horn, BuildHorn);

        public static AudioClip Block => RealOr("shield-block", "Block", ref _block, () => Tone("Block", 0.24f, 330f, 150f, 0.45f, 13f, 0.95f));
        public static AudioClip Parry => RealOr("shield-parry", "Parry", ref _parry, BuildParry);
        public static AudioClip Splash => RealOr("splash", "Splash", ref _splash, BuildSplash);

        // Menu buttons: a soft wooden tick on click, a fainter one on hover.
        public static AudioClip UiClick => RealOr("ui-click", "UiClick", ref _click, () => BuildTick("UiClick", 1900f, 0.05f));
        public static AudioClip UiHover => RealOr("ui-hover", "UiHover", ref _hover, () => BuildTick("UiHover", 2600f, 0.03f));

        private static AudioClip BuildTick(string name, float hz, float seconds)
        {
            var random = new System.Random(NoiseSeed + 31);
            float low = 0f;
            return Synth(name, seconds, (t, progress) =>
            {
                low += ((float)random.NextDouble() * 2f - 1f - low) * 0.35f;
                float body = Mathf.Sin(2f * Mathf.PI * hz * t) * 0.6f + low * 0.5f;
                return body * Mathf.Exp(-120f * t) * Mathf.Clamp01(t * 3000f);
            });
        }

        // A bright ringing clang: two high partials on a short noise click.
        private static AudioClip BuildParry()
        {
            var random = new System.Random(NoiseSeed + 21);
            return Synth("Parry", 0.55f, (t, progress) =>
            {
                float click = ((float)random.NextDouble() * 2f - 1f) * Mathf.Exp(-90f * t) * 0.6f;
                float ring = (Mathf.Sin(2f * Mathf.PI * 1180f * t) + 0.7f * Mathf.Sin(2f * Mathf.PI * 1770f * t) + 0.4f * Mathf.Sin(2f * Mathf.PI * 2630f * t)) * Mathf.Exp(-9f * t) * 0.3f;
                return click + ring;
            });
        }

        // Water hitting water: filtered noise that swells and trails off with a few bubbly pops.
        private static AudioClip BuildSplash()
        {
            var random = new System.Random(NoiseSeed + 23);
            float low = 0f;
            return Synth("Splash", 0.55f, (t, progress) =>
            {
                low += ((float)random.NextDouble() * 2f - 1f - low) * Mathf.Lerp(0.42f, 0.12f, progress);
                float envelope = Mathf.Clamp01(t * 60f) * Mathf.Exp(-6f * t);
                float pops = Mathf.Sin(2f * Mathf.PI * (320f + 600f * Mathf.Sin(t * 40f)) * t) * Mathf.Exp(-14f * Mathf.Repeat(t, 0.13f)) * 0.12f * Mathf.Exp(-3f * t);
                return low * envelope * 1.4f + pops;
            });
        }

        public static AudioClip Footstep(int variant) => FootstepOn(GroundSurface.Kind.Grass, variant);

        // A footstep for the ground underfoot: a recording if one is there (footstep-grass, footstep-dirt,
        // footstep-water), otherwise the synthesized thud (dirt is a firmer, crunchier version of it).
        public static AudioClip FootstepOn(GroundSurface.Kind surface, int variant)
        {
            string file = surface == GroundSurface.Kind.Water ? "footstep-water"
                : surface == GroundSurface.Kind.Grass ? "footstep-grass" : "footstep-dirt";
            AudioClip real = SoundBank.Pick(file, surface == GroundSurface.Kind.Water ? -28f : FootstepLevel);
            if (real != null) return real;
            int index = Mathf.Abs(variant) % FootstepVariants;
            if (surface == GroundSurface.Kind.Grass || surface == GroundSurface.Kind.Water)
            {
                if (_footsteps[index] == null) _footsteps[index] = BuildFootstep(index, false);
                return _footsteps[index];
            }
            if (_dirtSteps[index] == null) _dirtSteps[index] = BuildFootstep(index, true);
            return _dirtSteps[index];
        }

        // A falling tone mixed with low passed noise and a fast decay: thumps, grunts, knocks.
        private static AudioClip Tone(string name, float seconds, float startHz, float endHz, float noiseMix, float decay, float gain)
        {
            var random = new System.Random(NoiseSeed);
            float phase = 0f, low = 0f;
            return Synth(name, seconds, (t, progress) =>
            {
                phase += 2f * Mathf.PI * Mathf.Lerp(startHz, endHz, progress) / SampleRate;
                low += ((float)random.NextDouble() * 2f - 1f - low) * 0.25f;
                float envelope = Mathf.Exp(-decay * t) * Mathf.Clamp01(t * 800f);
                return (Mathf.Sin(phase) * (1f - noiseMix) + low * noiseMix * 2f) * envelope * gain;
            });
        }

        // A soft footfall on grass: a short low thud with a little crunch of mid noise on top.
        private static AudioClip BuildFootstep(int variant, bool dirt)
        {
            var random = new System.Random(NoiseSeed + 11 * (variant + 1) + (dirt ? 5 : 0));
            float low = 0f, phase = 0f, crunchLow = 0f, crunchHigh = 0f;
            float pitch = 70f + variant * 11f + (dirt ? 18f : 0f);
            float crunchMix = dirt ? 0.75f : 0.35f;
            return Synth((dirt ? "FootstepDirt" : "Footstep") + variant, 0.16f, (t, progress) =>
            {
                phase += 2f * Mathf.PI * pitch / SampleRate;
                float white = (float)random.NextDouble() * 2f - 1f;
                low += (white - low) * (0.08f + 0.02f * variant);
                crunchLow += (white - crunchLow) * 0.16f;
                crunchHigh += (crunchLow - crunchHigh) * 0.03f;
                float thud = (low * 1.2f + Mathf.Sin(phase) * 0.5f) * Mathf.Exp(-38f * t) * Mathf.Clamp01(t * 1200f);
                float crunch = (crunchLow - crunchHigh) * 1.6f * Mathf.Exp(-30f * t) * Mathf.Clamp01(t * 800f);
                return thud * 0.9f + crunch * crunchMix;
            });
        }

        private static AudioClip BuildSwing()
        {
            var random = new System.Random(NoiseSeed + 3);
            float low = 0f;
            return Synth("Swing", 0.28f, (t, progress) =>
            {
                float coefficient = Mathf.Lerp(0.05f, 0.5f, Mathf.Sin(progress * Mathf.PI));
                low += ((float)random.NextDouble() * 2f - 1f - low) * coefficient;
                float envelope = Mathf.Pow(Mathf.Sin(progress * Mathf.PI), 2f);
                return low * envelope * 0.9f;
            });
        }

        private static AudioClip BuildDeposit()
        {
            AudioClip a = Tone("knockA", 0.16f, 150f, 80f, 0.3f, 30f, 0.9f);
            return Mix("Deposit", 0.4f, (a, 0f, 1f), (a, 0.14f, 0.8f), (a, 0.26f, 0.6f));
        }

        private static AudioClip BuildUpgrade()
        {
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            return Synth("Upgrade", 0.8f, (t, progress) =>
            {
                float sum = 0f;
                for (int i = 0; i < notes.Length; i++)
                {
                    float local = t - i * 0.11f;
                    if (local < 0f) continue;
                    sum += Mathf.Sin(2f * Mathf.PI * notes[i] * local) * Mathf.Exp(-5f * local) * 0.35f;
                }
                return sum;
            });
        }

        private static AudioClip BuildHorn()
        {
            return Synth("Horn", 1.8f, (t, progress) =>
            {
                float envelope = Mathf.Clamp01(t * 3f) * Mathf.Exp(-1.6f * Mathf.Max(0f, t - 0.6f));
                float a = Mathf.Sin(2f * Mathf.PI * 110f * t) + 0.4f * Mathf.Sin(2f * Mathf.PI * 220f * t);
                float b = Mathf.Sin(2f * Mathf.PI * 164.8f * t) * (t > 0.5f ? 1f : 0f);
                return (a * 0.5f + b * 0.4f) * envelope * 0.7f;
            });
        }

        // ---------- helpers ----------

        private static AudioClip Synth(string name, float seconds, Func<float, float, float> sample)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            for (int i = 0; i < count; i++) data[i] = sample(i / (float)SampleRate, i / (float)count);
            return ToClip(name, data);
        }

        private static AudioClip Mix(string name, float seconds, params (AudioClip clip, float delay, float gain)[] parts)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            foreach (var part in parts)
            {
                var source = new float[part.clip.samples];
                part.clip.GetData(source, 0);
                int offset = Mathf.RoundToInt(part.delay * SampleRate);
                for (int i = 0; i < source.Length && offset + i < count; i++) data[offset + i] += source[i] * part.gain;
            }
            return ToClip(name, data);
        }

        // Target loudness of each sound, as the RMS of its loudest half in dBFS. The synthesized sounds used to be 5 to
        // 10 dB louder than the real axe recordings (about -22 dB), which made the mix harsh and the wind clip; every
        // sound is now brought to a level that fits next to them. Ambience sits well under the effects.
        private static readonly Dictionary<string, float> Levels = new Dictionary<string, float>
        {
            { "Chop", -22f }, { "Fell", -17f }, { "HutHit", -18f }, { "Swing", -24f }, { "Pickup", -25f }, { "Deposit", -23f },
            { "Upgrade", -24f }, { "Hurt", -21f }, { "Horn", -22f },
            { "Block", -21f }, { "Parry", -23f }, { "Splash", -26f },
            { "UiClick", -30f }, { "UiHover", -38f }
        };
        private const float DefaultLevel = -22f;
        private const float FootstepLevel = -31f;

        private static float LevelFor(string name)
        {
            if (name.StartsWith("Footstep")) return FootstepLevel;
            return Levels.TryGetValue(name, out float level) ? level : DefaultLevel;
        }

        // Scales the samples so the loudest half (in 20 ms blocks) has the target RMS, then rounds any peak off softly
        // instead of letting it clip.
        private static void Normalize(float[] data, float targetDb)
        {
            int block = Mathf.Max(1, SampleRate / 50);
            int blocks = Mathf.Max(1, data.Length / block);
            var energy = new float[blocks];
            for (int b = 0; b < blocks; b++)
            {
                double sum = 0.0;
                int start = b * block;
                int end = Mathf.Min(data.Length, start + block);
                for (int i = start; i < end; i++) sum += data[i] * data[i];
                energy[b] = (float)(sum / Mathf.Max(1, end - start));
            }
            Array.Sort(energy);
            int from = blocks / 2;
            double top = 0.0;
            for (int b = from; b < blocks; b++) top += energy[b];
            float active = Mathf.Sqrt((float)(top / Mathf.Max(1, blocks - from))) + 1e-6f;
            float gain = Mathf.Pow(10f, targetDb / 20f) / active;
            for (int i = 0; i < data.Length; i++) data[i] = SoftLimit(data[i] * gain);
        }

        private static float SoftLimit(float x)
        {
            const float knee = 0.7f;
            float magnitude = Mathf.Abs(x);
            if (magnitude <= knee) return x;
            float limited = knee + (1f - knee) * (float)Math.Tanh((magnitude - knee) / (1f - knee));
            return Mathf.Sign(x) * limited;
        }

        private static AudioClip ToClip(string name, float[] data)
        {
            Normalize(data, LevelFor(name));
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
