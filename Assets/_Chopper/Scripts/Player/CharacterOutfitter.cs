using System.Collections.Generic;
using UnityEngine;

namespace Chopper.Player
{
    // Dresses the skeleton in a character creator look: the outfit's own cut of the body, the outfit, a hairstyle and
    // a beard, tinted by the chosen skin, hair and shirt colors. The meshes bind to the matching bones on the existing
    // skeleton, so its animations drive them.
    public class CharacterOutfitter : MonoBehaviour
    {
        private const string RootBoneName = "root";
        private const string HeadBoneName = "Head";
        private const string SpineBoneName = "spine_01";
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        [SerializeField] private CharacterCatalog _catalog;

        private Transform _container;
        private Dictionary<string, Transform> _bones;
        private bool _hiddenOriginals;
        private MaterialPropertyBlock _block;
        private bool _applied;

        // The draugr wear plain village clothes instead of Chopper's plaid shirt and jeans.
        public bool WearChopperClothes { get; set; } = true;

        public CharacterCatalog Catalog
        {
            get => _catalog;
            set => _catalog = value;
        }

        // The editor dresses the prefab too, but tints are property blocks, which a prefab does not keep. So every copy
        // is dressed again when it comes to life, unless something (PlayerAppearance, a draugr) already did.
        private void Start()
        {
            if (!_applied) Apply(AppearanceData.Default);
        }

        // Chopper's default look (the draugr and the editor use it).
        public void ApplyChopper() => Apply(AppearanceData.Default);

        public void Apply(AppearanceData wanted)
        {
            if (_catalog == null) return;
            _applied = true;
            AppearanceData data = wanted.Clamped(_catalog);
            BuildBoneMap();
            HideOriginals();

            // Throw away any outfit baked into the prefab, so the parts are not worn twice.
            if (_container == null) _container = transform.Find("Outfit");
            if (_container != null) Remove(_container.gameObject);
            // Old prefabs may still carry code-built suspenders or a hat.
            if (_bones.TryGetValue(SpineBoneName, out Transform spineBone)) RemoveChild(spineBone, "Suspenders");
            if (_bones.TryGetValue(HeadBoneName, out Transform headBone)) RemoveChild(headBone, "Hat");
            _container = new GameObject("Outfit").transform;
            _container.SetParent(transform, false);

            OutfitSet outfit = Pick(_catalog.Outfits, data.Outfit);
            AddParts(_catalog.Head, data);
            if (outfit != null)
            {
                AddParts(outfit.Body, data);
                AddParts(outfit.Parts, data);
            }
            if (outfit == null || !outfit.HidesHair)
            {
                OutfitSet hair = Pick(_catalog.Hairs, data.Hair);
                if (hair != null) AddParts(hair.Parts, data);
            }
            OutfitSet beard = Pick(_catalog.Beards, data.Beard);
            if (beard != null) AddParts(beard.Parts, data);
        }

        private static void RemoveChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) Remove(child.gameObject);
        }

        // Destroy waits for the end of the frame, so the old part is renamed first and never found again.
        private static void Remove(GameObject go)
        {
            go.name += "_old";
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        private static OutfitSet Pick(OutfitSet[] sets, int index)
        {
            return sets != null && index >= 0 && index < sets.Length ? sets[index] : null;
        }

        private void BuildBoneMap()
        {
            if (_bones != null) return;
            _bones = new Dictionary<string, Transform>();
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (!_bones.ContainsKey(t.name)) _bones.Add(t.name, t);
            }
        }

        private void HideOriginals()
        {
            if (_hiddenOriginals) return;
            _hiddenOriginals = true;
            foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.enabled = false;
        }

        private void AddParts(OutfitPart[] parts, AppearanceData data)
        {
            if (parts == null) return;
            foreach (OutfitPart part in parts)
            {
                if (part == null || part.Mesh == null) continue;
                AddPart(part, data);
            }
        }

        private void AddPart(OutfitPart part, AppearanceData data)
        {
            var go = new GameObject(part.Name);
            go.transform.SetParent(_container, false);
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = part.Mesh;

            _bones.TryGetValue(RootBoneName, out Transform root);
            var bones = new Transform[part.BoneNames.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                if (!_bones.TryGetValue(part.BoneNames[i], out bones[i])) bones[i] = root;
            }
            smr.bones = bones;
            smr.rootBone = root;
            smr.localBounds = new Bounds(new Vector3(0f, 0.95f, 0f), new Vector3(1.8f, 2.1f, 1.8f));
            smr.sharedMaterials = ClothesFor(part.Materials);

            if (_block == null) _block = new MaterialPropertyBlock();
            for (int i = 0; i < part.Roles.Length; i++)
            {
                if (part.Roles[i] == PartMaterialRole.Fixed) continue;
                _block.Clear();
                _block.SetColor(BaseColorId, TintFor(part.Roles[i], data));
                // Dyed trousers: the brown cloth texture would swallow the color, so it is replaced by plain white.
                if (part.Roles[i] == PartMaterialRole.Pants) _block.SetTexture(BaseMapId, Texture2D.whiteTexture);
                smr.SetPropertyBlock(_block, i);
            }
        }

        private Material[] ClothesFor(Material[] materials)
        {
            if (WearChopperClothes || _catalog.ChopperCloth == null || _catalog.PlainCloth == null) return materials;
            var swapped = (Material[])materials.Clone();
            for (int i = 0; i < swapped.Length; i++)
            {
                if (swapped[i] == _catalog.ChopperCloth || swapped[i] == _catalog.ChopperLegs) swapped[i] = _catalog.PlainCloth;
                else if (swapped[i] == _catalog.ChopperSleeves && _catalog.PlainSleeves != null) swapped[i] = _catalog.PlainSleeves;
            }
            return swapped;
        }

        private Color TintFor(PartMaterialRole role, AppearanceData data)
        {
            switch (role)
            {
                case PartMaterialRole.Skin: return ColorAt(_catalog.SkinTones, data.Skin);
                case PartMaterialRole.Hair: return ColorAt(_catalog.HairColors, data.HairColor);
                case PartMaterialRole.Cloth: return ColorAt(_catalog.ClothTints, data.Cloth);
                case PartMaterialRole.Pants: return _catalog.PantsColor;
                default: return Color.white;
            }
        }

        private static Color ColorAt(NamedColor[] colors, int index)
        {
            return colors != null && index >= 0 && index < colors.Length ? colors[index].Color : Color.white;
        }
    }
}
