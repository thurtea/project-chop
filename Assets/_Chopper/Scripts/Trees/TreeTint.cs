using Chopper.Core;
using UnityEngine;

namespace Chopper.Trees
{
    // The pixel pack ships its pine in six leaf colors, so each zone and each enemy element simply uses the model of
    // its color (Water blue, Fire red, Earth dark and yellow, Wind light green; see MeadowBuilder). Nothing has to be
    // tinted any more: Apply and ApplyTreant only clear a leftover hit flash. Flash is the one real effect, a quick
    // bright overbright on a hit.
    public static class TreeTint
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color FlashColor = new Color(2.2f, 2.0f, 1.8f);

        // Standing trees and felled trees keep the look of the model they were built from.
        public static void Apply(GameObject tree, Element zone)
        {
        }

        // Enemy and sapling trees: removes the hit flash so the model shows its own colors again.
        public static void ApplyTreant(GameObject model, Element element)
        {
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                renderer.SetPropertyBlock(null);
            }
        }

        // A quick bright flash on a hit; ApplyTreant puts the real colors back.
        public static void Flash(GameObject model)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, FlashColor);
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
