using System.Collections.Generic;
using UnityEngine;

namespace Chopper.Player
{
    // The two looks of Chopper (user, 2026-10-12): everyone starts unarmoured (hair, plaid, vest, trousers, boots); with
    // an armour kit used (PlayerUpgrades.Armoured) the helmet, chainmail, bracers and the shield on the back show, and the
    // hair hides under the helmet. Every client runs this for every player it sees. Nothing here is networked.
    public class PlayerOutfit : MonoBehaviour
    {
        private static readonly string[] ArmourParts = { "chopper_helmet", "chopper_chainmail", "chopper_bracers", "chopper_shield" };
        private const string Hair = "chopper_hair";

        private readonly List<Renderer> _armour = new List<Renderer>();
        private readonly List<Renderer> _hair = new List<Renderer>();
        private PlayerUpgrades _upgrades;

        public bool ShowsArmour { get; private set; }

        private void Awake()
        {
            _upgrades = GetComponent<PlayerUpgrades>();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (IsArmour(renderer.transform)) _armour.Add(renderer);
                else if (renderer.gameObject.name == Hair) _hair.Add(renderer);
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
            foreach (Renderer renderer in _armour) if (renderer != null) renderer.enabled = ShowsArmour;
            foreach (Renderer renderer in _hair) if (renderer != null) renderer.enabled = !ShowsArmour;
        }

        // A mounted prop (the shield) keeps its mesh on a child, so the part's name may be on a parent.
        private bool IsArmour(Transform t)
        {
            for (; t != null && t != transform; t = t.parent)
                if (System.Array.IndexOf(ArmourParts, t.name) >= 0) return true;
            return false;
        }
    }
}
