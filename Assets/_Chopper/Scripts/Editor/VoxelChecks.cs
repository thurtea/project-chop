using Chopper.World.Generation;
using Chopper.World.Voxel;
using UnityEditor;
using UnityEngine;

namespace Chopper.Editor
{
    // Batch: Chopper.Editor.VoxelChecks.RunColumns
    public static class VoxelChecks
    {
        public static void RunColumns()
        {
            BlockPalette.Ensure();
            var first = VoxelColumns.Build(new WorldGen(4242, 110f));
            var second = VoxelColumns.Build(new WorldGen(4242, 110f));
            var third = VoxelColumns.Build(new WorldGen(4242, 110f));
            bool match = first.Crc == second.Crc && second.Crc == third.Crc;
            int triangles = VoxelWorld.CountTriangles(first);
            bool pass = match && triangles > 100000 && BlockPalette.LoadedFromFile;
            Debug.Log($"[Voxel] CRC {first.Crc:X8} three runs match {match}. First build {first.BuildMilliseconds} ms. Triangles {triangles}. Palette file {BlockPalette.LoadedFromFile}.");
            Debug.Log("[Voxel] RESULT " + (pass ? "PASS" : "FAIL"));
            if (!pass) EditorApplication.Exit(1);
        }
    }
}
