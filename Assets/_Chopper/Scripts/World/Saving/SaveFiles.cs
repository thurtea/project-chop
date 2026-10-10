using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Chopper.World.Saving
{
    // World save files on disk: one JSON file per world under the game's persistent data folder (Saves). Writes go
    // to a temporary file first and then replace the old one, so a crash mid write never loses a world.
    public static class SaveFiles
    {
        public const string Extension = ".world.json";

        // Test runs (-chopperTestSaves) use a scratch folder, so they never see or change the player's own worlds.
        public static string Folder => Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperTestSaves") >= 0
            ? Path.Combine(Application.temporaryCachePath, "TestSaves")
            : Path.Combine(Application.persistentDataPath, "Saves");

        public static List<SaveSummary> List()
        {
            var list = new List<SaveSummary>();
            if (!Directory.Exists(Folder)) return list;
            foreach (string path in Directory.GetFiles(Folder, "*" + Extension))
            {
                WorldSave save = Read(path);
                if (save == null) continue;
                list.Add(new SaveSummary
                {
                    Path = path,
                    Name = save.Name,
                    Seed = save.Seed,
                    Day = save.Day,
                    SavedAt = save.SavedAt,
                    Written = File.GetLastWriteTime(path)
                });
            }
            // Most recently played first.
            list.Sort((a, b) => b.Written.CompareTo(a.Written));
            return list;
        }

        public static bool Any() => Directory.Exists(Folder) && Directory.GetFiles(Folder, "*" + Extension).Length > 0;

        public static WorldSave Read(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                WorldSave save = JsonUtility.FromJson<WorldSave>(File.ReadAllText(path));
                return save != null && save.Version >= 1 ? save : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] Could not read {path}: {e.Message}");
                return null;
            }
        }

        public static bool Write(WorldSave save, string path)
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                string temp = path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(save, true));
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] Could not write {path}: {e.Message}");
                return false;
            }
        }

        public static void Delete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] Could not delete {path}: {e.Message}");
            }
        }

        // A file name for a new world: its name made safe for the file system, numbered if that name is taken.
        public static string NewPath(string worldName)
        {
            var builder = new System.Text.StringBuilder();
            foreach (char c in (worldName ?? string.Empty).Trim())
            {
                if (char.IsLetterOrDigit(c)) builder.Append(c);
                else if (c == ' ' || c == '-' || c == '_') builder.Append('_');
            }
            string safe = builder.ToString();
            if (safe.Length == 0) safe = "World";
            string path = Path.Combine(Folder, safe + Extension);
            for (int n = 2; File.Exists(path); n++) path = Path.Combine(Folder, $"{safe}_{n}{Extension}");
            return path;
        }

        // "World 3" when two worlds exist already.
        public static string SuggestName()
        {
            int count = Directory.Exists(Folder) ? Directory.GetFiles(Folder, "*" + Extension).Length : 0;
            return $"World {count + 1}";
        }
    }
}
