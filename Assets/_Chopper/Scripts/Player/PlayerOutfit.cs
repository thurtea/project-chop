using System.Collections.Generic;
using UnityEngine;

namespace Chopper.Player
{
    // The looks of Chopper (model sheet, user 2026-10-13): dressed is hair, plaid, vest, belt, chainmail skirt, bracers,
    // trousers and boots; with an armour kit used (PlayerUpgrades.Armoured) the helmet and the shield on the back show,
    // and the hair hides under the helmet. Every client runs this for every player it sees. Nothing here is networked.
    public class PlayerOutfit : MonoBehaviour
    {
        private static readonly string[] ArmourParts = { "chopper_helmet" };
        private const string Shield = "chopper_shield";
        private const string Hair = "chopper_hair";
        // The everyday outfit (W9-01): shown once it is picked up on the shore (PlayerUpgrades.Dressed). Without it
        // Chopper wears his base layer (undershirt and braies): the shirt and body with the base-layer atlas.
        private static readonly string[] OutfitParts = { "chopper_vest", "chopper_belt", "chopper_chainmail", "chopper_bracers" };
        private static readonly string[] BaseLayerParts = { "chopper_shirt", "chopper_body", "chopper_boots" };
        private const string BaseLayerAtlas = "Character/chopper_atlas_under";
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int UnderId = Shader.PropertyToID("_Under");
        private static Texture2D _baseLayer;

        private readonly List<Renderer> _armour = new List<Renderer>();
        private readonly List<Renderer> _shield = new List<Renderer>();
        private readonly List<Renderer> _hair = new List<Renderer>();
        private readonly List<Renderer> _outfit = new List<Renderer>();
        private readonly List<Renderer> _baseLayerRenderers = new List<Renderer>();
        private MaterialPropertyBlock _block;
        private PlayerUpgrades _upgrades;

        public bool ShowsArmour { get; private set; }

        private void Awake()
        {
            _upgrades = GetComponent<PlayerUpgrades>();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (IsArmour(renderer.transform)) _armour.Add(renderer);
                else if (IsNamed(renderer.transform, Shield)) _shield.Add(renderer);
                else if (renderer.gameObject.name == Hair) _hair.Add(renderer);
                else if (System.Array.IndexOf(OutfitParts, renderer.gameObject.name) >= 0) _outfit.Add(renderer);
                else if (System.Array.IndexOf(BaseLayerParts, renderer.gameObject.name) >= 0) _baseLayerRenderers.Add(renderer);
            }
        }

        private void OnEnable()
        {
            if (_upgrades != null) _upgrades.OutfitChanged += Apply;
            Apply();
        }

        private void OnDisable()
        {
            if (_upgrades != null) _upgrades.OutfitChanged -= Apply;
        }

        private void Apply()
        {
            ShowsArmour = _upgrades != null && _upgrades.Armoured;
            bool showShield = _upgrades != null && (_upgrades.HasShield || _upgrades.Armoured);
            foreach (Renderer renderer in _armour)
                if (renderer != null) renderer.enabled = ShowsArmour;
            foreach (Renderer renderer in _shield)
                if (renderer != null) renderer.enabled = showShield;
            foreach (Renderer renderer in _hair) if (renderer != null) renderer.enabled = !ShowsArmour;
            bool dressed = _upgrades == null || _upgrades.Dressed;
            foreach (Renderer renderer in _outfit)
                if (renderer != null) renderer.enabled = dressed;
            if (_block == null) _block = new MaterialPropertyBlock();
            if (_baseLayer == null) _baseLayer = Resources.Load<Texture2D>(BaseLayerAtlas);
            foreach (Renderer renderer in _baseLayerRenderers)
            {
                if (renderer == null) continue;
                renderer.enabled = true;
                // Per slot, so PlayerHitFlash's slot blocks keep the texture.
                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                {
                    Material material = renderer.sharedMaterials[i];
                    Texture own = material != null && material.HasProperty(BaseMapId) ? material.GetTexture(BaseMapId) : null;
                    Texture wanted = dressed || _baseLayer == null ? own : _baseLayer;
                    renderer.GetPropertyBlock(_block, i);
                    _block.SetFloat(UnderId, dressed ? 0f : 1f);
                    if (wanted != null) _block.SetTexture(BaseMapId, wanted);
                    renderer.SetPropertyBlock(_block, i);
                }
            }
        }


        // A mounted prop (the shield) keeps its mesh on a child, so the part's name may be on a parent.
        private bool IsArmour(Transform t)
        {
            for (; t != null && t != transform; t = t.parent)
                if (System.Array.IndexOf(ArmourParts, t.name) >= 0) return true;
            return false;
        }

        private bool IsNamed(Transform t, string part)
        {
            for (; t != null && t != transform; t = t.parent)
                if (t.name == part) return true;
            return false;
        }
    }
}
