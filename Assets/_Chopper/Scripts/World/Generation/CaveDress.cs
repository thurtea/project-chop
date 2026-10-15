using System.Collections.Generic;
using UnityEngine;

namespace Chopper.World.Generation
{
    // One kit mesh placed in a crypt or a mine. Position is world space. The block shell stays;
    // these pieces only dress it. Solid pieces take a box collider on the prefab.
    public struct CavePiece
    {
        public string Name;
        public Vector3 Position;
        public float Yaw;
        public bool Solid;
        // Scorch panels: a small point light at the ember, in the piece's local space.
        public bool Ember;
        public Vector3 Scale;
    }

    // Slot tables from Chopper/Art/Scripts/cave_kit_manifest.json. Every kept slot is already
    // outside the walking cross. Floor tiles and the rail are allowed on it.
    public static class CaveDress
    {
        public const string Lantern = "prop_mine_lantern_voxel";
        public const string Rail = "prop_mine_rail_voxel";
        public static readonly Vector3 LanternFlame = new Vector3(0.063f, 0.313f, 0.438f);
        public static readonly Vector3 ScorchFlame = new Vector3(0f, 1.75f, 0.65f);

        public static readonly string[] CryptPanels =
        {
            "prop_crypt_wall_panel_voxel", "prop_crypt_wall_moss_voxel",
            "prop_crypt_wall_crack_voxel", "prop_crypt_wall_scorch_voxel"
        };
        public static readonly string[] CryptFloors = { "prop_crypt_floor_cracked_voxel", "prop_crypt_floor_moss_voxel" };
        public static readonly string[] CryptOverhead =
        {
            "prop_crypt_chain_voxel", "prop_crypt_cobweb_voxel", "prop_crypt_dripstone_voxel"
        };
        public static readonly string[] CryptSolids =
        {
            "prop_crypt_pillar_voxel", "prop_crypt_pillar_broken_voxel", "prop_crypt_sarcophagus_voxel",
            "prop_crypt_urn_voxel", "prop_crypt_bone_pile_voxel", "prop_crypt_roots_voxel",
            "prop_crypt_rubble_voxel", "prop_crypt_flowstone_voxel"
        };
        public const string CryptArch = "prop_crypt_arch_voxel";
        public const string CryptTrim = "prop_crypt_trim_voxel";
        public const string ArenaPillar = "prop_crypt_pillar_arena_voxel";
        public const string CryptRubble = "prop_crypt_rubble_voxel";

        public const string MineFrame = "prop_mine_gallery_frame_voxel";
        public const string MineTimber = "prop_mine_timber_frame_voxel";
        public const string MineOre = "prop_mine_ore_voxel";
        public const string MineCrystal = "prop_mine_crystal_voxel";
        public const string MineCart = "prop_mine_cart_voxel";
        public static readonly string[] MineClutter =
        {
            "prop_mine_barrel_voxel", "prop_mine_crate_voxel", "prop_mine_crystal_voxel", "prop_mine_rubble_voxel"
        };

        public static void Add(List<CavePiece> list, string name, Vector3 position, float yaw, bool solid, bool ember = false, Vector3 scale = default)
        {
            if (scale == Vector3.zero) scale = Vector3.one;
            list.Add(new CavePiece { Name = name, Position = position, Yaw = yaw, Solid = solid, Ember = ember, Scale = scale });
        }

        // face is the distance from the room centre to the piece origin, along the wall normal (out of the room).
        public static void AddWall(List<CavePiece> list, string name, Vector3 origin, Vector3 normal, float face, float along, float y, bool solid, bool ember = false, Vector3 scale = default)
        {
            Vector3 tangent = new Vector3(normal.z, 0f, -normal.x);
            float yaw = Mathf.Atan2(-normal.x, -normal.z) * Mathf.Rad2Deg;
            Add(list, name, origin + normal * face + tangent * along + Vector3.up * y, yaw, solid, ember, scale);
        }

        public static Vector3 Corner(string name, int index)
        {
            index = ((index % 4) + 4) % 4;
            switch (name)
            {
                case "prop_crypt_pillar_voxel": return Quad(index, 6.6f, 6.6f, 6.6f, -6.6f, -6.6f, 6.6f, -6.6f, -6.6f, 0f);
                case "prop_crypt_pillar_broken_voxel": return Quad(index, 6.35f, 6.35f, 6.35f, -6.35f, -6.35f, 6.35f, -6.35f, -6.35f, 0f);
                case "prop_crypt_sarcophagus_voxel": return Quad(index, 6.1f, 6.85f, 6.1f, -6.85f, -6.1f, 6.85f, -6.1f, -6.85f, 0f);
                case "prop_crypt_urn_voxel": return Quad(index, 7.1f, 7.1f, 7.1f, -7.1f, -7.1f, 7.1f, -7.1f, -7.1f, 0f);
                case "prop_crypt_bone_pile_voxel": return Quad(index, 6.6f, 6.85f, 6.6f, -6.725f, -6.725f, 6.85f, -6.725f, -6.725f, 0f);
                case "prop_crypt_roots_voxel": return Quad(index, 6.6f, 6.1f, 6.6f, -7.1f, -6.6f, 6.1f, -6.6f, -7.1f, 0f);
                case "prop_crypt_rubble_voxel": return Quad(index, 6.35f, 6.6f, 6.35f, -6.6f, -6.6f, 6.6f, -6.6f, -6.6f, 0f);
                case "prop_crypt_flowstone_voxel": return Quad(index, 7.1f, 7.1f, 7.1f, -7.1f, -7.1f, 7.1f, -7.1f, -7.1f, 0f);
                case "prop_crypt_chain_voxel": return Quad(index, 7.1f, 7.35f, 7.1f, -7.1f, -7.35f, 7.35f, -7.35f, -7.1f, 6f);
                case "prop_crypt_cobweb_voxel": return Quad(index, 7.225f, 7.35f, 7.225f, -5.85f, -5.975f, 7.35f, -5.975f, -5.85f, 6f);
                case "prop_crypt_dripstone_voxel": return Quad(index, 7.1f, 7.225f, 7.1f, -7.1f, -7.225f, 7.225f, -7.225f, -7.1f, 6f);
                case "prop_crypt_floor_cracked_voxel":
                case "prop_crypt_floor_moss_voxel": return Quad(index, 6.35f, 6.35f, 6.35f, -6.35f, -6.35f, 6.35f, -6.35f, -6.35f, 0.02f);
                case "prop_crypt_pillar_arena_voxel": return Quad(index, 12.1f, 12.1f, 12.1f, -12.1f, -12.1f, 12.1f, -12.1f, -12.1f, 0f);
                case "prop_mine_cart_voxel": return Quad(index, 7.35f, 7.05f, 7.35f, -7.05f, -7.35f, 7.05f, -7.35f, -7.05f, 0f);
                case "prop_mine_barrel_voxel":
                case "prop_mine_crate_voxel": return Quad(index, 7.475f, 7.475f, 7.475f, -7.475f, -7.475f, 7.475f, -7.475f, -7.475f, 0f);
                case "prop_mine_rubble_voxel": return Quad(index, 6.6f, 7.1f, 6.6f, -7.1f, -7.1f, 7.1f, -7.1f, -7.1f, 0f);
                case "prop_mine_crystal_voxel": return Quad(index, 7.35f, 7.725f, 7.35f, -7.6f, -7.6f, 7.725f, -7.6f, -7.6f, 0f);
                default: return Quad(index, 6.6f, 6.6f, 6.6f, -6.6f, -6.6f, 6.6f, -6.6f, -6.6f, 0f);
            }
        }

        // index 0 is the floor-centre tile. 1..4 are the corner slots.
        public static Vector3 FloorSlot(string name, int slot)
        {
            if (slot <= 0) return new Vector3(0f, 0.02f, 0f);
            return Corner(name, slot - 1);
        }

        // True when a solid standing here would cut the door-to-door cross.
        public static bool BlocksWalkingCross(Vector3 local, float clearHalf)
        {
            return Mathf.Abs(local.x) < clearHalf && Mathf.Abs(local.z) < clearHalf && local.y < 2.2f;
        }

        static Vector3 Quad(int index, float x0, float z0, float x1, float z1, float x2, float z2, float x3, float z3, float y)
        {
            switch (index)
            {
                case 1: return new Vector3(x1, y, z1);
                case 2: return new Vector3(x2, y, z2);
                case 3: return new Vector3(x3, y, z3);
                default: return new Vector3(x0, y, z0);
            }
        }
    }
}
