using System;
using System.Collections.Generic;
using System.IO;
using Chopper.Audio;
using UnityEditor;
using UnityEngine;

namespace Chopper.Editor
{
    // Development helper: writes every sound the game plays (the synthesized ones and the real recordings) to 16 bit
    // WAV files, so loudness and spectrum can be measured and compared outside Unity.
    // Menu: Chopper > Setup > Export Sounds. Batch: -executeMethod Chopper.Editor.AudioExport.Export -audioOutput <folder>
    public static class AudioExport
    {
        [MenuItem("Chopper/Setup/Export Sounds")]
        public static void Export()
        {
            string folder = Path.Combine(Path.GetTempPath(), "ChopperSounds");
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-audioOutput") folder = args[i + 1];
            Directory.CreateDirectory(folder);

            var clips = new List<AudioClip>
            {
                ProceduralSfx.Chop, ProceduralSfx.Fell, ProceduralSfx.HutHit, ProceduralSfx.Swing, ProceduralSfx.Pickup,
                ProceduralSfx.Deposit, ProceduralSfx.Upgrade, ProceduralSfx.Hurt,
                ProceduralSfx.Horn,
                ProceduralSfx.Block, ProceduralSfx.Parry, ProceduralSfx.Splash,
                ProceduralSfx.Footstep(0), ProceduralSfx.Footstep(1), ProceduralSfx.Footstep(2)
            };
            foreach (AudioClip clip in clips) Write(clip, folder);
            Debug.Log($"[AudioExport] Wrote {clips.Count} sounds to {folder}");
        }

        private static void Write(AudioClip clip, string folder)
        {
            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            string path = Path.Combine(folder, clip.name + ".wav");
            using (var stream = new FileStream(path, FileMode.Create))
            using (var writer = new BinaryWriter(stream))
            {
                int dataBytes = samples.Length * 2;
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataBytes);
                writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)clip.channels);
                writer.Write(clip.frequency);
                writer.Write(clip.frequency * clip.channels * 2);
                writer.Write((short)(clip.channels * 2));
                writer.Write((short)16);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataBytes);
                foreach (float sample in samples) writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample, -1f, 1f) * 32767f));
            }
        }
    }
}
