using System.Collections.Generic;
using UnityEngine;

namespace Chopper.Audio
{
    // Real recordings dropped into Assets/_Chopper/Resources/Audio replace the synthesized placeholders by name, with
    // no code change: "tree-fall.wav" is the tree fall, and "tree-fall-1.wav" to "tree-fall-12.wav" are variations
    // picked at random each time. requirements.md lists every name. Each effect is measured once and brought to the
    // same loudness as the sound it replaces, so a file that is too loud or too quiet still sits right in the mix.
    public static class SoundBank
    {
        private const int MaxVariants = 12;
        private const float MinGain = 0.25f;
        private const float MaxGain = 4f;

        private static readonly Dictionary<string, AudioClip[]> _clips = new Dictionary<string, AudioClip[]>();
        private static readonly Dictionary<AudioClip, float> _gains = new Dictionary<AudioClip, float>();

        // Every recording found for a name and its numbered variations; empty when there are none.
        public static AudioClip[] All(string name)
        {
            if (_clips.TryGetValue(name, out AudioClip[] found)) return found;
            var list = new List<AudioClip>();
            AudioClip plain = Resources.Load<AudioClip>("Audio/" + name);
            if (plain != null) list.Add(plain);
            for (int i = 1; i <= MaxVariants; i++)
            {
                AudioClip variant = Resources.Load<AudioClip>($"Audio/{name}-{i}");
                if (variant != null) list.Add(variant);
            }
            found = list.ToArray();
            _clips[name] = found;
            return found;
        }

        public static bool Has(string name) => All(name).Length > 0;

        // A random recording for the name, or null when there is none (the caller then uses its synthesized sound).
        // targetDb is the loudness the synthesized version is made at; the recording is matched to it.
        public static AudioClip Pick(string name, float targetDb)
        {
            AudioClip[] all = All(name);
            if (all.Length == 0) return null;
            AudioClip clip = all.Length == 1 ? all[0] : all[Random.Range(0, all.Length)];
            if (!_gains.ContainsKey(clip)) _gains[clip] = Measure(clip, targetDb);
            return clip;
        }

        // The volume multiplier that brings a recording to its target loudness; 1 for synthesized clips.
        public static float GainOf(AudioClip clip)
        {
            if (clip == null) return 1f;
            return _gains.TryGetValue(clip, out float gain) ? gain : 1f;
        }

        // Loudness as the RMS of the loudest half of the clip in 20 ms blocks (the same measure the synthesized sounds
        // are normalized with). Streamed or compressed-in-memory clips cannot be read back, so they keep gain 1.
        private static float Measure(AudioClip clip, float targetDb)
        {
            if (clip.loadType != AudioClipLoadType.DecompressOnLoad) return 1f;
            if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
            if (clip.loadState != AudioDataLoadState.Loaded) return 1f;
            var data = new float[clip.samples * clip.channels];
            if (data.Length == 0 || !clip.GetData(data, 0)) return 1f;

            int block = Mathf.Max(1, clip.frequency * clip.channels / 50);
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
            System.Array.Sort(energy);
            double top = 0.0;
            int from = blocks / 2;
            for (int b = from; b < blocks; b++) top += energy[b];
            float rms = Mathf.Sqrt((float)(top / Mathf.Max(1, blocks - from))) + 1e-6f;
            return Mathf.Clamp(Mathf.Pow(10f, targetDb / 20f) / rms, MinGain, MaxGain);
        }
    }
}
