using System;
using System.Collections.Generic;
using System.Globalization;
using Chopper.Core;
using Chopper.Player;
using Chopper.World;
using Chopper.World.Generation;
using UnityEngine;

namespace Chopper.UI
{
    // The commands behind the developer console (F5 or the key left of 1). Information commands (help, pos, seed,
    // crypts) work for everyone; the cheats go through PlayerDevTools, so the server only runs them for the host.
    public class DevConsole
    {
        private class Command
        {
            public string Name;
            public string Usage;
            public string Help;
            public bool Cheat;
            public Action<string[]> Run;
        }

        private const int DefaultWood = 100;
        private const int DefaultItems = 10;
        private const int DefaultEnemies = 5;
        private const int DefaultWave = 6;
        private const float TeleportClearance = 0.3f;

        private readonly GameConfig _config;
        private readonly List<Command> _commands = new List<Command>();
        private readonly List<string> _output = new List<string>();

        public DevConsole(GameConfig config)
        {
            _config = config;
            Add("help", "help", "List the commands.", false, _ => Help());
            Add("clear", "clear", "Clear this window.", false, _ => { _output.Clear(); Version++; });
            Add("pos", "pos", "Where you are: position, zone and ground height.", false, _ => Position());
            Add("seed", "seed", "The world seed.", false, _ => Print($"Seed {WorldSession.Seed}"));
            Add("explore", "explore", "Reveal the whole map.", false, _ => { if (GameUi.Map != null) GameUi.Map.RevealAll(); Print("Map revealed"); });
            Add("atmosphere", "atmosphere [0-1]", "Fog and sun shafts strength, 0 off and 1 full (no number shows it).", false, a => { if (a.Length > 0 && float.TryParse(a[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)) Chopper.Core.AtmosphereRules.Strength = v; Print($"Atmosphere {Chopper.Core.AtmosphereRules.Strength:0.00}"); });
            Add("fps", "fps", "Show or hide the frame rate (average, and the slowest frames) in the top right corner.", false, _ => { GameUi.ShowFps = !GameUi.ShowFps; Print("Frame rate " + (GameUi.ShowFps ? "shown" : "hidden")); });
            Add("hud", "hud", "Hide or show the HUD, for screenshots.", false, _ => { GameUi.HudHidden = !GameUi.HudHidden; Print("HUD " + (GameUi.HudHidden ? "hidden" : "shown")); });
            Add("crypts", "crypts", "Every crypt: number, zone, tier and door position.", false, _ => Crypts());
            Add("fly", "fly", "Fly on or off. Jump rises, Crouch sinks, hold Sprint to go fast, scroll to pull the camera far back.", true, _ => Fly());
            Add("god", "god", "God mode on or off: no damage, endless stamina.", true, _ => God());
            Add("tp", "tp <x> <z> | tp hut | tp crypt <n> | tp rooms <n> | tp arena <n>", "Teleport to a spot, the hut, a crypt door, the rooms of a crypt or its arena.", true, Teleport);
            Add("heal", "heal", "Full health.", true, _ => Send(DevCommand.HealFull, 0));
            Add("wood", "wood [n]", "Carried wood (100 if no number).", true, a => Send(DevCommand.GiveWood, Count(a, DefaultWood)));
            Add("herbs", "herbs [n]", "Healing herbs.", true, a => Send(DevCommand.GiveHerbs, Count(a, DefaultItems)));
            Add("fiber", "fiber [n]", "Fiber.", true, a => Send(DevCommand.GiveFabric, Count(a, DefaultItems)));
            Add("bandages", "bandages [n]", "Bandages.", true, a => Send(DevCommand.GiveBandages, Count(a, DefaultItems)));
            Add("torches", "torches [n]", "Torches.", true, a => Send(DevCommand.GiveTorches, Count(a, DefaultItems)));
            Add("iron", "iron [n]", "Crypt iron.", true, a => Send(DevCommand.GiveIron, Count(a, DefaultItems)));
            Add("shards", "shards [n]", "Rune shards.", true, a => Send(DevCommand.GiveShards, Count(a, DefaultItems)));
            Add("axe", "axe", "A whole new axe in the hand.", true, _ => Send(DevCommand.GiveAxe, 0));
            Add("boat", "boat", "A little boat in the bag (B on the ocean deploys it, tap B by it to board or leave, hold B to pick it up).", true, _ => { if (Chopper.Player.PlayerBoat.Local != null) { Chopper.Player.PlayerBoat.Local.RequestDevBoat(); Print("Boat given"); } });
            Add("sharkrod", "sharkrod", "The shark rod (normally crafted at the workbench after landing 10 fish). Rod key in the boat at sea casts for sharks.", true, _ => { if (Chopper.Player.PlayerBoat.Local != null) { Chopper.Player.PlayerBoat.Local.RequestSharkRod(); Print("Shark rod given"); } });
            Add("weapons", "weapons", "A sword, a mace and a spear in the bag (9, 0 and - hold the sword, mace and spear).", true, _ => { if (Chopper.Player.PlayerWeapons.Local != null) { Chopper.Player.PlayerWeapons.Local.RequestDevAll(); Print("Weapons given"); } });
            Add("day", "day", "Skip to day.", true, _ => Send(DevCommand.ToDay, 0));
            Add("night", "night", "Skip to night.", true, _ => Send(DevCommand.ToNight, 0));
            Add("next", "next", "Skip to the next part of the day.", true, _ => Send(DevCommand.NextPhase, 0));
            Add("repair", "repair", "Repair the hut.", true, _ => Send(DevCommand.RepairHut, 0));
            Add("spawn", "spawn [earth|wind|fire|water|plain] [n]", "Awake tree enemies around you (5 random ones if nothing is given).", true, SpawnCommand);
            Add("killall", "killall", "Remove every tree enemy in the world.", true, _ => Send(DevCommand.KillAll, 0));
            Add("wave", "wave [n]", "A night wave rises around the hut and comes for it (6 if no number).", true, a => Send(DevCommand.Wave, Count(a, DefaultWave)));
            Add("time", "time <0-24>", "Set the clock to an hour (6 sunrise, 12 noon, 19 dusk, 22 night).", true, TimeCommand);
            Add("boss", "boss", "A grove warden rises in front of you.", true, _ => Send(DevCommand.SpawnBoss, 0));
            Add("boon", "boon", "A random boon for everyone.", true, _ => Send(DevCommand.RandomBoon, 0));
            Add("rod", "rod <rod 0-3> [lure 0-3]", "Set your fishing rod and lure.", true, SetRod);
            Add("fishkind", "fishkind <n | any>", "The next casts hook fish number n (0 is perch). any rolls again.", true, ForceFish);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Add("fishbite", "fishbite", "The fish bites now (skips the wait).", true, _ => WithFishing(f => f.DebugBiteNow(), "Cast first."));
            Add("fishsnap", "fishsnap", "Snap the line on the fish you are fighting.", true, _ => WithFishing(f => f.DebugSnap(), "Hook a fish first."));
#endif
            Add("fishres", "fishres <x>", "Reeling strain multiplier while testing (1 is normal).", true, a => { PlayerFishing.DebugResistance = a.Length > 1 && float.TryParse(a[1], out float x) ? Mathf.Clamp(x, 0.1f, 5f) : 1f; Print($"Reel strain x{PlayerFishing.DebugResistance:F2}"); });
            Add("fishdebug", "fishdebug", "Show the fight's numbers under the fishing bars.", true, _ => { PlayerFishing.DebugShowValues = !PlayerFishing.DebugShowValues; Print("Fishing numbers " + (PlayerFishing.DebugShowValues ? "on" : "off")); });
            Add("weather", "weather <sunny | cloudy | rain>", "Set the weather now (it changes again after a while).", true, SetWeather);
            Add("hotspot", "hotspot <0-1>", "Empty an active fishing hotspot, or fill a quiet one.", true, ToggleHotspot);
        }

        public IReadOnlyList<string> Output => _output;

        // Goes up whenever the output changes, so the view only redraws then.
        public int Version { get; private set; }

        public IEnumerable<string> Names
        {
            get { foreach (Command command in _commands) yield return command.Name; }
        }

        private void Add(string name, string usage, string help, bool cheat, Action<string[]> run)
        {
            _commands.Add(new Command { Name = name, Usage = usage, Help = help, Cheat = cheat, Run = run });
        }

        public void Print(string line)
        {
            _output.Add(line);
            Version++;
        }

        // Splits a typed line into lower case words.
        public static string[] Split(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return new string[0];
            return line.Trim().ToLowerInvariant().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        }

        // The number after the command, or the fallback when there is none. Never below 1.
        public static int Count(string[] args, int fallback)
        {
            if (args.Length < 2 || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)) return fallback;
            return Mathf.Max(1, value);
        }

        public void Run(string line)
        {
            string[] args = Split(line);
            if (args.Length == 0) return;
            Print("> " + line.Trim());
            Command command = _commands.Find(c => c.Name == args[0]);
            if (command == null)
            {
                Print($"Unknown command \"{args[0]}\". Type help for the list.");
                return;
            }
            if (command.Cheat)
            {
                PlayerDevTools tools = Tools();
                if (tools == null)
                {
                    Print("Start or join a world first.");
                    return;
                }
                if (!tools.IsHostPlayer)
                {
                    Print("Only the host can use that.");
                    return;
                }
            }
            command.Run(args);
        }

        // The host's own fishing (the console's cheats run on the host, which is the server).
        private void WithFishing(Action<PlayerFishing> run, string otherwise)
        {
            PlayerFishing fishing = PlayerFishing.Local;
            if (fishing == null) { Print("Start or join a world first."); return; }
            run(fishing);
            Print("Done. " + otherwise);
        }

        private void SetRod(string[] args)
        {
            PlayerFishing fishing = PlayerFishing.Local;
            if (fishing == null || args.Length < 2 || !int.TryParse(args[1], out int rod)) { Print("Usage: rod <0-3> [lure 0-3]"); return; }
            int lure = args.Length > 2 && int.TryParse(args[2], out int l) ? l : fishing.Lure;
            fishing.ServerSetGear(rod, lure);
            Print($"{FishingRules.RodName(fishing.Rod)}, {FishingRules.LureName(fishing.Lure)}");
        }

        private void ForceFish(string[] args)
        {
            PlayerFishing fishing = PlayerFishing.Local;
            if (fishing == null || args.Length < 2) { Print("Usage: fishkind <n | any>"); return; }
            int kind = int.TryParse(args[1], out int k) ? k : -1;
            fishing.ServerForceKind(kind);
            Print(kind >= 0 && kind < FishingRules.Kinds ? "Next fish: " + fishing.SpeciesOf((FishKind)kind).Name : "Fish roll normally again.");
        }

        private void SetWeather(string[] args)
        {
            WeatherSystem weather = WeatherSystem.Instance;
            if (weather == null || args.Length < 2) { Print("Usage: weather <sunny | cloudy | rain | snow>"); return; }
            WeatherKind kind = args[1] == "rain" ? WeatherKind.Rain : args[1] == "cloudy" ? WeatherKind.Cloudy : args[1] == "snow" ? WeatherKind.Snow : WeatherKind.Sunny;
            weather.ServerSet(kind);
            Print("Weather: " + WeatherRules.Name(kind));
        }

        private void ToggleHotspot(string[] args)
        {
            FishingHotspots hotspots = FishingHotspots.Instance;
            if (hotspots == null || args.Length < 2 || !int.TryParse(args[1], out int spot)) { Print("Usage: hotspot <0-1>"); return; }
            hotspots.ServerToggle(spot);
            Print($"Hotspot {spot}: {hotspots.State(spot)}");
        }

        private static PlayerDevTools Tools()
        {
            PlayerWood local = PlayerWood.Local;
            return local != null ? local.GetComponent<PlayerDevTools>() : null;
        }

        private void Send(DevCommand command, int value)
        {
            Tools().Request(command, value);
            Print("Done.");
        }

        private void SpawnCommand(string[] args)
        {
            if (args.Length >= 2 && TryParseElement(args[1], out Element element))
            {
                int count = Count(new[] { args[0], args.Length >= 3 ? args[2] : string.Empty }, DefaultEnemies);
                Send(DevCommand.SpawnElement, EncodeSpawn(element, count));
                return;
            }
            Send(DevCommand.SpawnEnemies, Count(args, DefaultEnemies));
        }

        private void TimeCommand(string[] args)
        {
            if (args.Length < 2 || !TryParseHour(args[1], out float hour)) { Print("Usage: time <0-24>"); return; }
            Send(DevCommand.SetTime, Mathf.RoundToInt(hour * 100f));
        }

        public static bool TryParseElement(string word, out Element element)
        {
            switch ((word ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "earth": element = Element.Earth; return true;
                case "wind": element = Element.Wind; return true;
                case "fire": element = Element.Fire; return true;
                case "water": element = Element.Water; return true;
                case "plain": case "neutral": case "none": element = Element.Neutral; return true;
            }
            element = Element.Neutral;
            return false;
        }

        public static bool TryParseHour(string text, out float hour)
        {
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out hour) || hour < 0f || hour > 24f) { hour = 0f; return false; }
            return true;
        }

        public static int EncodeSpawn(Element element, int count) => (int)element * 1000 + Mathf.Clamp(count, 1, 999);

        public static void DecodeSpawn(int value, out Element element, out int count)
        {
            element = (Element)Mathf.Clamp(value / 1000, 0, 4);
            count = Mathf.Clamp(value % 1000, 1, 999);
        }

        public static void HourToClock(float hour, out DayPhase phase, out float fraction)
        {
            hour = ((hour % 24f) + 24f) % 24f;
            if (hour >= 6f && hour < 18f) { phase = DayPhase.Day; fraction = (hour - 6f) / 12f; }
            else if (hour >= 18f && hour < 20f) { phase = DayPhase.Dusk; fraction = (hour - 18f) / 2f; }
            else if (hour >= 4f && hour < 6f) { phase = DayPhase.Dawn; fraction = (hour - 4f) / 2f; }
            else { phase = DayPhase.Night; fraction = ((hour >= 20f ? hour - 20f : hour + 4f)) / 8f; }
        }

        private void Help()
        {
            Print("Commands (the starred ones are host only):");
            foreach (Command command in _commands) Print($"  {(command.Cheat ? "*" : " ")} {command.Usage}  {command.Help}");
        }

        private void Fly()
        {
            bool next = !Tools().Flying;
            Tools().Request(DevCommand.Fly, next ? 1 : 0);
            Print(next ? "Flying. Jump rises, Crouch sinks, hold Sprint to go fast." : "Flying off.");
        }

        private void God()
        {
            PlayerCombat combat = PlayerWood.Local.GetComponent<PlayerCombat>();
            bool next = combat != null && !combat.God;
            Tools().Request(DevCommand.GodMode, next ? 1 : 0);
            Print(next ? "God mode on." : "God mode off.");
        }

        private void Position()
        {
            PlayerWood local = PlayerWood.Local;
            if (local == null)
            {
                Print("Start or join a world first.");
                return;
            }
            Vector3 p = local.transform.position;
            string where = CryptWorld.IsUnderground(p) ? "underground" : ZoneMap.ElementAt(p, _config != null ? _config.HubRadius : 40f) + " zone";
            bool hasGround = Chopper.World.WorldGround.Exists;
            string ground = hasGround ? $", ground {Chopper.World.WorldGround.HeightAt(p):F1}" : string.Empty;
            Print($"x {p.x:F1}  y {p.y:F1}  z {p.z:F1}  ({where}{ground})");
        }

        private void Crypts()
        {
            IReadOnlyList<CryptPlan> plans = CryptWorld.Plans;
            if (plans == null || plans.Count == 0)
            {
                Print("No crypts in this world yet.");
                return;
            }
            for (int i = 0; i < plans.Count; i++)
            {
                CryptPlan plan = plans[i];
                Print($"  {i + 1}. {plan.Name}, {plan.Site.Zone}, tier {plan.Site.Tier}, {plan.Rooms.Count} rooms, door at x {plan.DoorSpot.x:F0} z {plan.DoorSpot.z:F0}");
            }
        }

        private void Teleport(string[] args)
        {
            if (args.Length >= 3 && TryFloat(args[1], out float x) && TryFloat(args[2], out float z))
            {
                Go(OnGround(new Vector3(x, 0f, z)), PlayerCamera.Local != null ? PlayerCamera.Local.Yaw : 0f, $"x {x:F0} z {z:F0}");
                return;
            }
            string place = args.Length >= 2 ? args[1] : string.Empty;
            if (place == "hut" && Hut.Instance != null)
            {
                Go(OnGround(Hut.Instance.transform.position + new Vector3(0f, 0f, 7.5f)), 180f, "the hut");
                return;
            }
            if (place == "crypt" || place == "rooms" || place == "arena")
            {
                IReadOnlyList<CryptPlan> plans = CryptWorld.Plans;
                int index = Count(args.Length >= 3 ? new[] { args[0], args[2] } : args, 1) - 1;
                if (plans == null || index < 0 || index >= plans.Count)
                {
                    Print($"No crypt {index + 1}. Type crypts for the list.");
                    return;
                }
                CryptPlan plan = plans[index];
                if (place == "crypt") Go(plan.ReturnSpot, plan.ReturnYaw, plan.Name + " door");
                else if (place == "rooms") Go(plan.ArrivalSpot, plan.ArrivalYaw, plan.Name + " rooms");
                else Go(plan.ArenaEntry, plan.ArenaEntryYaw, plan.Name + " arena");
                return;
            }
            Print("Usage: tp <x> <z>, tp hut, tp crypt <n>, tp rooms <n> or tp arena <n>.");
        }

        private void Go(Vector3 position, float yaw, string label)
        {
            Tools().RequestTeleport(position, yaw);
            Print("Teleported to " + label + ".");
        }

        private static Vector3 OnGround(Vector3 position)
        {
            bool hasGround = Chopper.World.WorldGround.Exists;
            if (hasGround) position.y = Chopper.World.WorldGround.HeightAt(position) + TeleportClearance;
            return position;
        }

        private static bool TryFloat(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
