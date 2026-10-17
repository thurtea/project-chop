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
        private static readonly Color Calm = new Color(0.25f, 1f, 0.35f);
        private static readonly Color Strained = new Color(1f, 0.62f, 0.08f);
        private static readonly Color Breaking = new Color(1f, 0.12f, 0.1f);
        private static readonly Color Mark = new Color(0.05f, 0.05f, 0.05f, 0.9f);

        private readonly RectTransform _root;
        private readonly RectTransform _hud;
        private static readonly Vector2 CueHome = new Vector2(0f, -260f);
        private const float BarAboveFloat = 70f;
        private const float CueAboveFloat = 120f;
        private Vector2 _lastFloat;
        private float _lastFloatAt = float.NegativeInfinity;
        private readonly UiKit.Bar _tension;
        private LineRenderer _tensionArc;
        private LineRenderer _lineArc;
        private LineRenderer _tensionBack;
        private LineRenderer _lineBack;
        private readonly Text _debug;
        private readonly Text _cue;
        private const float CueHold = 0.6f;
        private const float CueFade = 1.1f;
        private static readonly Color GoodCue = new Color(0.98f, 0.86f, 0.5f);
        private static readonly Color BadCue = new Color(0.95f, 0.55f, 0.45f);

        public FishingView(Transform hud)
        {
            _hud = hud as RectTransform;
            _root = UiKit.Rect(hud, "Fishing");
            UiKit.Place(_root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -330f), new Vector2(Width, 60f));

            _tension = UiKit.MakeBar(_root, "Tension", new Vector2(84f, 84f), UiKit.Accent, false);
            _tension.Root.gameObject.SetActive(false);
            _debug = UiKit.Label(_root, string.Empty, 14, TextAnchor.UpperLeft, UiKit.Dim, true);
            UiKit.Place(_debug.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(Width, 40f));
            _root.gameObject.SetActive(false);

            _cue = UiKit.Heading(hud, string.Empty, 50, TextAnchor.MiddleCenter, GoodCue);
            var cueOutline = _cue.gameObject.AddComponent<UnityEngine.UI.Outline>();
            cueOutline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            cueOutline.effectDistance = new Vector2(3f, -3f);
            UiKit.Place(_cue.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -260f), new Vector2(760f, 72f));
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
            bool overFloat = FloatOnScreen(fishing, out Vector2 at);
            if (!overFloat && Time.time - _lastFloatAt < CueHold + CueFade)
            {
                at = _lastFloat;
                overFloat = true;
            }
            _cue.rectTransform.anchoredPosition = !mine && overFloat ? at + Vector2.up * CueAboveFloat : CueHome;
            if (_cue.text != word) _cue.text = word;
            Color color = (mine ? mining.CueGood : fishing.CueGood) ? GoodCue : BadCue;
            color.a = 1f - Mathf.Clamp01((age - CueHold) / CueFade);
            _cue.color = color;
            // A small pop as it appears.
            _cue.rectTransform.localScale = Vector3.one * (1f + 0.15f * (1f - Mathf.Clamp01(age / 0.15f)));
        }

        // The float's spot on the HUD (centre-anchored), when it is in front of the camera.
        private bool FloatOnScreen(PlayerFishing fishing, out Vector2 position)
        {
            position = Vector2.zero;
            Camera camera = Camera.main;
            PlayerFishing.Stage stage = fishing != null ? fishing.CurrentStage : PlayerFishing.Stage.Off;
            bool floating = stage == PlayerFishing.Stage.Waiting || stage == PlayerFishing.Stage.Bite || stage == PlayerFishing.Stage.Fighting;
            if (!floating || camera == null || _hud == null) return false;
            Vector3 screen = camera.WorldToScreenPoint(fishing.BobberWorld);
            if (screen.z <= 0f) return false;
            Canvas canvas = _hud.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_hud, screen, uiCamera, out Vector2 local)) return false;
            position = local + (_hud.rect.center * -1f);
            _lastFloat = position;
            _lastFloatAt = Time.time;
            return true;
        }

        private static void AddMark(RectTransform bar, float at)
        {
            Image mark = UiKit.Box(bar, "Mark", Mark);
            mark.rectTransform.anchorMin = new Vector2(at, 0f);
            mark.rectTransform.anchorMax = new Vector2(at, 1f);
            mark.rectTransform.sizeDelta = new Vector2(3f, 4f);
            mark.rectTransform.anchoredPosition = Vector2.zero;
        }

        private static LineRenderer MakeArc(string name, float width, int queue)
        {
            var go = new GameObject(name);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = false;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.widthMultiplier = width;
            line.numCapVertices = 4;
            line.positionCount = 2;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                var mat = new Material(shader);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                if (mat.HasProperty("_Color")) mat.color = Color.white;
                mat.renderQueue = queue;
                line.material = mat;
            }
            go.SetActive(false);
            return line;
        }

        private static void DrawArc(LineRenderer line, Vector3 center, Vector3 right, Vector3 up, float radius, float amount, Color color)
        {
            const int steps = 28;
            int count = Mathf.Max(2, Mathf.RoundToInt(Mathf.Clamp01(amount) * (steps - 1)) + 1);
            line.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(steps - 1);
                float ang = Mathf.Lerp(200f, -20f, t) * Mathf.Deg2Rad;
                line.SetPosition(i, center + (right * Mathf.Cos(ang) + up * Mathf.Sin(ang)) * radius);
            }
            line.startColor = color;
            line.endColor = color;
            if (line.material != null)
            {
                if (line.material.HasProperty("_BaseColor")) line.material.SetColor("_BaseColor", color);
                if (line.material.HasProperty("_Color")) line.material.color = color;
            }
            line.gameObject.SetActive(true);
        }

        public void Refresh(PlayerFishing fishing)
        {
            RefreshCue(fishing);
            bool show = fishing != null && fishing.CurrentStage == PlayerFishing.Stage.Fighting && fishing.ResultText.Length == 0
                        && !UiState.InventoryOpen && !UiState.MapOpen;
            if (_root.gameObject.activeSelf != show) _root.gameObject.SetActive(show);
            if (!show)
            {
                if (_tensionArc != null) _tensionArc.gameObject.SetActive(false);
                if (_lineArc != null) _lineArc.gameObject.SetActive(false);
                if (_tensionBack != null) _tensionBack.gameObject.SetActive(false);
                if (_lineBack != null) _lineBack.gameObject.SetActive(false);
                return;
            }

            if (FloatOnScreen(fishing, out Vector2 above)) _root.anchoredPosition = above + Vector2.up * BarAboveFloat;
            Fight fight = fishing.CurrentFight;
            float tension = Mathf.Clamp01(fight.Tension);
            TensionZone zone = FishingRules.Zone(tension);
            Color arc = zone == TensionZone.Safe ? Calm : zone == TensionZone.Risky ? Strained : Breaking;
            if (_tensionArc == null)
            {
                _tensionBack = MakeArc("TensionBack", 0.075f, 3000);
                _lineBack = MakeArc("LineBack", 0.062f, 3000);
                _tensionArc = MakeArc("TensionArc", 0.05f, 3001);
                _lineArc = MakeArc("LineArc", 0.04f, 3001);
            }
            Camera camera = Camera.main;
            Vector3 right = camera != null ? camera.transform.right : Vector3.right;
            Vector3 bob = fishing.BobberWorld;
            float grow = camera != null ? Chopper.Core.FishShow.ArcScale(Vector3.Distance(camera.transform.position, bob)) : 1f;
            Vector3 center = bob + right * (0.32f * grow) + Vector3.up * (0.3f * grow);
            Vector3 front = center - (camera != null ? camera.transform.forward : Vector3.forward) * (0.04f * grow);
            _tensionBack.widthMultiplier = 0.075f * grow;
            _lineBack.widthMultiplier = 0.062f * grow;
            _tensionArc.widthMultiplier = 0.05f * grow;
            _lineArc.widthMultiplier = 0.04f * grow;
            bool cut = fishing.RunCutShowing;
            _tensionBack.enabled = _lineBack.enabled = _tensionArc.enabled = _lineArc.enabled = !cut;
            DrawArc(_tensionBack, center, right, Vector3.up, 0.2f * grow, 1f, new Color(0.03f, 0.03f, 0.04f, 1f));
            DrawArc(_lineBack, center, right, Vector3.up, 0.31f * grow, 1f, new Color(0.03f, 0.03f, 0.04f, 1f));
            DrawArc(_tensionArc, front, right, Vector3.up, 0.2f * grow, tension, arc);
            float line = fight.StartDistance > 0.05f ? Mathf.Clamp01(fight.Distance / fight.StartDistance) : tension;
            DrawArc(_lineArc, front, right, Vector3.up, 0.31f * grow, line, new Color(1f, 0.88f, 0.25f, 1f));
            _debug.text = PlayerFishing.DebugShowValues
                ? $"tension {fight.Tension:F2}  distance {fight.Distance:F1}/{fight.StartDistance:F1}  time {fight.Elapsed:F1}s  {fishing.Phase}"
                : string.Empty;
        }
    }
}
