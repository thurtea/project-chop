using System;
using UnityEngine;

namespace Chopper.World
{
    // The practice yard's stations: what each sign says and where it stands. The key names come from the caller, so
    // the signs always show the player's own bindings (keyboard or pad). Positions are rings around the hut, kept
    // around the house and off the pond.
    public static class TutorialStations
    {
        public enum Station { Welcome, Chopping, Fighting, Gathering, Workbench, TorchAndBandage, Fishing, Crypts }

        public const int Count = 8;
        // Clear of the house and the yard torches (WorldGen.WallRadius is 14 m) with room to walk round the signs.
        public const float MinRadius = 18f;
        public const float PondMargin = 3f;

        private static readonly float[] Angles = { 0f, 50f, 100f, 150f, 200f, 250f, -1f, 300f };
        private static readonly float[] Radii = { 18f, 26f, 24f, 24f, 18f, 20f, 0f, 22f };

        public static string Title(Station station)
        {
            switch (station)
            {
                case Station.Welcome: return "The practice yard";
                case Station.Chopping: return "Chopping";
                case Station.Fighting: return "Fighting";
                case Station.Gathering: return "Herbs and ferns";
                case Station.Workbench: return "The workbench";
                case Station.TorchAndBandage: return "Torches and bandages";
                case Station.Fishing: return "Fishing";
                default: return "Crypts";
            }
        }

        // key names an action by its key (from InputHints), for example key("Attack") is "Left click" or "R2".
        public static string Body(Station station, Func<string, string> key)
        {
            switch (station)
            {
                case Station.Welcome:
                    return "This yard is for practice. You carry everything you will ever use here: the axe, a rod, torches, bandages, herbs, fiber, iron and shards.\n\n" +
                           "Walk to each sign and read it with " + key("Interact") + ", then try the thing it describes. It stays day, nothing here is saved, and you can leave from the pause menu whenever you like.";
                case Station.Chopping:
                    return "Swing at a tree with " + key("Attack") + " until it falls. Small trees give a little wood, big ones a lot.\n\n" +
                           "A tree falls away from your last blow, but on a hillside it comes down the hill, so chop it from above. Step aside when a big one cracks: it kills where it lands unless you wear Sturdy gear from the crypts.\n\n" +
                           "Felled trees leave saplings. At night they wake and come for you, so chop by day.";
                case Station.Fighting:
                    return "A training tree waits here. It hits softly and comes back when you fell it.\n\n" +
                           "Hold " + key("Block") + " to raise the axe and block. Raise it just as a blow lands to parry and stagger the tree. " +
                           key("Roll") + " rolls out of the way. Keep pressing " + key("Attack") + " for a three hit combo; the last hit lunges.\n\n" +
                           "Kill an elemental tree and your axe carries its element for fifteen hits.";
                case Station.Gathering:
                    return "Leafy plants with a yellow flower are healing herbs and ferns give fiber. Press " + key("Interact") + " to pick one, then walk over what drops.\n\n" +
                           "Fallen branches and wood piles are picked up the same way.";
                case Station.Workbench:
                    return "The workbench stands in the house: walk up and press " + key("Interact") + ". Torches are made there, the axe, armor and hut are upgraded, crypt gear is forged, " +
                           "and a worn axe or rod is mended. Everything is made at a bench except bandages, which you make anywhere from your bag (" + key("Inventory") + ").\n\n" +
                           "The chest beside the bed is shared by everyone. Press " + key("Repair") + " by the hut to repair it from its wood store.";
                case Station.TorchAndBandage:
                    return key("Torch") + " lights a torch for the night. " + key("Potion") + " wraps a bandage when you are hurt.\n\n" +
                           "Fish you catch are food: " + key("Food") + " eats the smallest one for extra stamina.";
                case Station.Fishing:
                    return "Stand on the bank and press " + key("Interact") + " to take out the rod. Hold " + key("Attack") + " to charge a cast and let go to throw.\n\n" +
                           "You can see the fish under the water. Tap " + key("Attack") + " to twitch the lure and hold it to reel slowly; splash it about too much and they flee. " +
                           "When the float goes under, strike with " + key("Attack") + ", then hold to reel and let go when the line gets tight.";
                default:
                    return "Three stone crypts hide in the forests of every world. Their doors lead down to lantern lit rooms, guardians, and chests of crypt iron and rune shards.\n\n" +
                           "Deepest of all is the arena: waves of guardians, then the crypt's boss. The map (" + key("Map") + ") marks every crypt you have found.";
            }
        }

        // Where a station's sign stands, on the ground plane (x, z) around the hut at the origin. The fishing sign stands
        // on the pond's bank, on the side facing the hut; the others keep clear of the pond.
        public static Vector2 Position(Station station, Vector2 pondCenter, float pondShore)
        {
            if (station == Station.Fishing)
            {
                Vector2 toHut = pondCenter.sqrMagnitude > 0.01f ? -pondCenter.normalized : Vector2.up;
                return pondCenter + toHut * (pondShore + 2f);
            }
            int i = (int)station;
            float angle = Angles[i];
            float radius = Radii[i];
            Vector2 p = Polar(angle, radius);
            // Turn round the hut, a few degrees at a time, until the sign is clear of the pond.
            for (int step = 0; step < 72 && Vector2.Distance(p, pondCenter) < pondShore + PondMargin; step++)
            {
                angle += 5f;
                p = Polar(angle, radius);
            }
            return p;
        }

        // The training tree stands a few meters further out than its sign.
        public static Vector2 DummyPosition(Vector2 pondCenter, float pondShore)
        {
            Vector2 sign = Position(Station.Fighting, pondCenter, pondShore);
            return sign + sign.normalized * 6f;
        }

        private static Vector2 Polar(float degrees, float radius)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * radius;
        }
    }
}
