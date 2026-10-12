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

        // Batch: Chopper.Editor.VoxelChecks.CheckBlockShader. Lists Chopper/Block compile errors for this platform. A
        // shader with errors draws nothing in a player, which hid the whole block ground.
        public static void CheckBlockShader()
        {
            Shader shader = Shader.Find("Chopper/Block");
            bool pass = shader != null;
            if (shader != null)
            {
                for (int i = 0; i < shader.passCount; i++)
                    ShaderUtil.CompilePass(new Material(shader), i, true);
                foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
                {
                    Debug.Log($"[Voxel] shader {message.severity} line {message.line}: {message.message}");
                    if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error) pass = false;
                }
                bool hasError = ShaderUtil.ShaderHasError(shader);
                Debug.Log($"[Voxel] shader passes {shader.passCount}, has error {hasError}");
                if (hasError) pass = false;
            }
            Debug.Log("[Voxel] SHADER " + (pass ? "PASS" : "FAIL"));
            EditorApplication.Exit(pass ? 0 : 1);
        }
    }
}
