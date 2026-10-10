using Chopper.Core;
using Chopper.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Chopper.UI
{
    // The fishing minigame's HUD: almost no words. One word pops up at the moments that matter (Hooked!, Caught!,
    // Snapped, Got away, Missed) and fades within two seconds, as New World does. Everything else is in the world: the landing ring on the water
    // while a cast is charged, the float dipping as fish nose at it and going right under on a bite, the rod bending
    // and the line pulling taut, the fish's shadow and its splashes. The only overlay is an unlabeled tension bar while
    // a fish is on (green, orange, red, with marks where it turns risky and critical), and the catch shows as a pickup
    // card like any other item.
    public class FishingView
    {
        private const float Width = 300f;
        private static readonly Color Calm = new Color(0.45f, 0.78f, 0.38f);
        private static readonly Color Strained = new Color(0.95f, 0.62f, 0.2f);
        private static readonly Color Breaking = new Color(0.92f, 0.22f, 0.16f);
        private static readonly Color Mark = new Color(0.05f, 0.05f, 0.05f, 0.9f);

        private readonly RectTransform _root;
        private readonly UiKit.Bar _tension;
        private readonly Text _debug;
        private readonly Text _cue;
        private const float CueHold = 0.6f;
        private const float CueFade = 1.1f;
        private static readonly Color GoodCue = new Color(0.98f, 0.86f, 0.5f);
        private static readonly Color BadCue = new Color(0.95f, 0.55f, 0.45f);

        public FishingView(Transform hud)
        {
            _root = UiKit.Rect(hud, "Fishing");
            UiKit.Place(_root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -330f), new Vector2(Width, 60f));

            _tension = UiKit.MakeBar(_root, "Tension", new Vector2(Width, 12f), UiKit.Accent, false);
            UiKit.Place(_tension.Root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), _tension.Root.sizeDelta);
            // Marks where the line turns risky and critical, so the zones read without color.
            AddMark(_tension.Root, FishingRules.RiskyTension);
            AddMark(_tension.Root, FishingRules.CriticalTension);

            // Development only (DebugShowValues): the fight's raw numbers for tuning.
            _debug = UiKit.Label(_root, string.Empty, 14, TextAnchor.UpperLeft, UiKit.Dim, true);
            UiKit.Place(_debug.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(Width, 40f));
            _root.gameObject.SetActive(false);

            _cue = UiKit.Heading(hud, string.Empty, 34, TextAnchor.MiddleCenter, GoodCue);
            UiKit.Place(_cue.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -260f), new Vector2(500f, 50f));
        }

        // The newest of fishing's and mining's words (they share the spot under the crosshair).
        private void RefreshCue(PlayerFishing fishing)
        {
            PlayerMining mining = PlayerMining.Local;
            float fishAt = fishing != null ? fishing.CueAt : float.NegativeInfinity;
            float mineAt = mining != null ? mining.CueAt : float.NegativeInfinity;
            bool mine = mineAt > fishAt;
            float age = Time.time - (mine ? mineAt : fishAt);
            bool show = age < CueHold + CueFade && !UiState.InputBlocked;
            if (!show)
            {
                if (_cue.text.Length > 0) _cue.text = string.Empty;
                return;
            }
            string word = mine ? mining.CueWord : fishing.CueWord;
            if (_cue.text != word) _cue.text = word;
            Color color = (mine ? mining.CueGood : fishing.CueGood) ? GoodCue : BadCue;
            color.a = 1f - Mathf.Clamp01((age - CueHold) / CueFade);
            _cue.color = color;
            // A small pop as it appears.
            _cue.rectTransform.localScale = Vector3.one * (1f + 0.15f * (1f - Mathf.Clamp01(age / 0.15f)));
        }

        private static void AddMark(RectTransform bar, float at)
        {
            Image mark = UiKit.Box(bar, "Mark", Mark);
            mark.rectTransform.anchorMin = new Vector2(at, 0f);
            mark.rectTransform.anchorMax = new Vector2(at, 1f);
            mark.rectTransform.sizeDelta = new Vector2(3f, 4f);
            mark.rectTransform.anchoredPosition = Vector2.zero;
        }

        public void Refresh(PlayerFishing fishing)
        {
            RefreshCue(fishing);
            bool show = fishing != null && fishing.CurrentStage == PlayerFishing.Stage.Fighting && fishing.ResultText.Length == 0
                        && !UiState.InventoryOpen && !UiState.MapOpen;
            if (_root.gameObject.activeSelf != show) _root.gameObject.SetActive(show);
            if (!show) return;

            Fight fight = fishing.CurrentFight;
            float tension = Mathf.Clamp01(fight.Tension);
            TensionZone zone = FishingRules.Zone(tension);
            _tension.Fill.fillAmount = tension;
            _tension.Fill.color = zone == TensionZone.Safe ? Calm : zone == TensionZone.Risky ? Strained : Breaking;
            _debug.text = PlayerFishing.DebugShowValues
                ? $"tension {fight.Tension:F2}  distance {fight.Distance:F1}/{fight.StartDistance:F1}  time {fight.Elapsed:F1}s  {fishing.Phase}"
                : string.Empty;
        }
    }
}
