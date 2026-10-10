using System;
using UnityEngine;

namespace Chopper.Player
{
    // What a material slot of an outfit part is tinted by: nothing, the skin tone, the hair color, the shirt color
    // or the trouser color. Trousers are a flat dyed color (their texture is replaced), the rest multiply the texture.
    public enum PartMaterialRole
    {
        Fixed = 0,
        Skin = 1,
        Hair = 2,
        Cloth = 3,
        Pants = 4
    }

    // One skinned mesh of the character (a shirt, a pair of boots, a hairstyle). The bone names say which skeleton
    // bones the mesh was skinned to, so it can be bound to any skeleton with the same bone names.
    [Serializable]
    public class OutfitPart
    {
        public string Name;
        public Mesh Mesh;
        public Material[] Materials;
        public PartMaterialRole[] Roles;
        public string[] BoneNames;
    }

    // A collection of skinned parts, such as an outfit, a hairstyle or a beard.
    [Serializable]
    public class OutfitSet
    {
        public string Name;
        public OutfitPart[] Parts;
        [Tooltip("An outfit's own body: the whole skin with only what these clothes cover taken out, so no gap shows.")]
        public OutfitPart[] Body;
        [Tooltip("A hood or helmet: hair is not shown with it.")]
        public bool HidesHair;
        [Tooltip("The hairstyle (an index into Hairs) this body always wears; -1 lets the player choose.")]
        public int HairStyle = -1;
    }

    [Serializable]
    public class NamedColor
    {
        public string Name;
        public Color Color = Color.white;
    }

    // Everything the character creator offers: outfits (each with its own cut of the body), hairstyles, beards and
    // colors. Built by the editor script OutfitSetup from the Quaternius Universal Base Characters and Modular
    // Character Outfits packs (both CC0).
    [CreateAssetMenu(menuName = "Chopper/Character Catalog")]
    public class CharacterCatalog : ScriptableObject
    {
        [Tooltip("Eyes and eyebrows: always shown. The body comes with each outfit.")]
        public OutfitPart[] Head;

        public OutfitSet[] Outfits;
        public OutfitSet[] Hairs;
        public OutfitSet[] Beards;
        public NamedColor[] HairColors;
        public NamedColor[] ClothTints;
        public NamedColor[] SkinTones;
        [Tooltip("The trousers' dyed color.")]
        public Color PantsColor = new Color(0.16f, 0.24f, 0.42f);
        [Tooltip("Chopper's own clothes (plaid shirt, belt, jeans), baked by ClothesBaker.")]
        public Material ChopperCloth;
        [Tooltip("Chopper's jeans and belt, baked on their own copy of the peasant atlas.")]
        public Material ChopperLegs;
        [Tooltip("The plain village clothes worn instead of ChopperCloth and ChopperLegs by the draugr.")]
        public Material PlainCloth;
        [Tooltip("Chopper's long plaid sleeves, baked by ClothesBaker on the ranger sleeve atlas.")]
        public Material ChopperSleeves;
        [Tooltip("The plain ranger sleeves worn instead of ChopperSleeves by the draugr.")]
        public Material PlainSleeves;
        [Tooltip("Plain brown for the fishing rod and the suspenders (gear made in code).")]
        [UnityEngine.Serialization.FormerlySerializedAs("HatMaterial")]
        public Material GearMaterial;

        [Tooltip("The animated character the start screen's creator shows.")]
        public GameObject PreviewPrefab;

        public static int Clamp(int value, int count) => count <= 0 ? 0 : Mathf.Clamp(value, 0, count - 1);
    }
}
