using UnityEngine;

namespace Chopper.World.Generation
{
    // Everything WorldBuilder needs to dress a generated world: ground layers, water, rock and tree models, and the
    // grass material. Filled by MeadowBuilder (Rebuild Network Scenes) and referenced from the World scene, so all of
    // it is included in builds.
    [CreateAssetMenu(fileName = "SO_WorldAssets", menuName = "Chopper/World Assets")]
    public class WorldAssets : ScriptableObject
    {
        [Tooltip("Hub, Water, Earth, Fire, Wind, Dirt, Rock: the order WorldGen paints them in.")]
        public TerrainLayer[] TerrainLayers;
        public Material TerrainMaterial;

        public Material PondWater;
        public Material MarshWater;
        public Material SeaWater;
        public Material SeaFloor;

        public GameObject[] RockModels;

        public GameObject[] TreeModels;
        [Tooltip("Natural height of each tree model, measured when the asset is built.")]
        public float[] TreeModelHeights;
        [Tooltip("Model indices per zone, in Element order: hub, water, fire, earth, wind.")]
        public IntList[] Species;

        public Material GrassMaterial;

        [Tooltip("The campfire placed round the hut and by the pond.")]
        public GameObject Campfire;

        [Header("Crypts")]
        public Material CryptWall;
        public Material CryptFloor;
        public Material CryptCeiling;
        public Material CryptVoid;
        public GameObject ChestModel;
        public GameObject GrandChestModel;
        public GameObject LanternModel;

        [Header("Mines")]
        public Material MineWall;
        public Material MineFloor;
        public Material MineCeiling;
        public Material MineTimber;
        [Tooltip("The vein rock: emission on (black at rest), so its crack can glow.")]
        public Material MineVein;
        public GameObject VeinModel;
        public GameObject RailModel;
        public GameObject CartModel;
        public GameObject BarrelModel;
        public GameObject LadderModel;

        [System.Serializable]
        public class IntList
        {
            public int[] Values;
        }

        public int[][] SpeciesArrays()
        {
            var result = new int[Species != null ? Species.Length : 0][];
            for (int i = 0; i < result.Length; i++) result[i] = Species[i] != null ? Species[i].Values : null;
            return result;
        }
    }
}
