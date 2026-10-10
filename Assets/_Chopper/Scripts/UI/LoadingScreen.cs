using Chopper.Player;
using Chopper.World.Generation;
using UnityEngine;
using UnityEngine.UI;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace Chopper.UI
{
    // A full screen card from the moment a game is started on the start screen until the player stands in the world:
    // the world's name and seed, what is happening (connecting, shaping the land, waking up), a filling bar and a tip.
    // Lives on the NetworkManager object, which survives the scene change.
    public class LoadingScreen : MonoBehaviour
    {
        private const float FadeSeconds = 0.6f;
        private const float HoldAfterReady = 0.5f;
        private const float GiveUpSeconds = 90f;

        private static readonly string[] Tips =
        {
            "Fallen branches give wood without an axe. Pick them up on the way out of the yard.",
            "A large tree kills whoever it lands on. Sturdy gear from crypt iron lets you live through it.",
            "Every hit on a tree can knock saplings loose. At night they wake up and come for the hut.",
            "Killing an elemental enemy charges your axe with its element for 15 hits.",
            "Hold block just before a hit lands to parry it.",
            "Crypt chests hold iron and rune shards. Each player loots every chest once.",
            "The world saves itself at every dawn. Continue picks up where you left off.",
            "Red herbs and fiber from ferns make bandages anywhere. Everything else is made at the workbench."
        };

        private static LoadingScreen _instance;

        private GameObject _canvas;
        private CanvasGroup _group;
        private Text _title;
        private Text _detail;
        private Text _stage;
        private Text _tip;
        private Image _fill;
        private float _progress;
        private float _shownAt;
        private float _readyAt = -1f;
        private bool _active;
        private bool _sawWorld;

        private void Awake()
        {
            _instance = this;
            Build();
            _canvas.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // Called by the start screen right before it hosts or joins.
        public static void Show(string title, string detail)
        {
            if (_instance == null) return;
            _instance.Begin(title, detail);
        }

        public static bool Visible => _instance != null && _instance._active;

        private void Begin(string title, string detail)
        {
            _title.text = title;
            _detail.text = detail;
            _tip.text = Tips[Random.Range(0, Tips.Length)];
            _progress = 0f;
            _fill.fillAmount = 0f;
            _shownAt = Time.unscaledTime;
            _readyAt = -1f;
            _sawWorld = false;
            _active = true;
            _group.alpha = 1f;
            _canvas.SetActive(true);
        }

        private void Update()
        {
            if (!_active) return;

            bool inWorld = UnitySceneManager.GetActiveScene().name != "Boot";
            if (inWorld) _sawWorld = true;
            // Back at the start screen after the world had loaded, or the connection failed: nothing to wait for.
            bool failed = (_sawWorld && !inWorld) || Time.unscaledTime - _shownAt > GiveUpSeconds;
            Chopper.Networking.BootMenu boot = GetComponent<Chopper.Networking.BootMenu>();
            if (!inWorld && boot != null && boot.Offline && Time.unscaledTime - _shownAt > 0.5f) failed = true;
            if (failed)
            {
                Hide();
                return;
            }

            bool ready = WorldSession.Ready;
            bool standing = ready && PlayerWood.Local != null;
            float target;
            if (!inWorld)
            {
                target = 0.2f;
                _stage.text = "Connecting";
            }
            else if (!ready)
            {
                target = 0.55f;
                _stage.text = "Shaping the land";
            }
            else if (!standing)
            {
                target = 0.85f;
                _stage.text = "Growing the forests";
            }
            else
            {
                target = 1f;
                _stage.text = "Waking up";
                if (_readyAt < 0f) _readyAt = Time.unscaledTime;
            }
            _progress = Mathf.MoveTowards(_progress, target, Time.unscaledDeltaTime * 0.9f);
            _fill.fillAmount = _progress;

            if (_readyAt >= 0f && Time.unscaledTime - _readyAt > HoldAfterReady && _progress >= 0.999f)
            {
                _group.alpha = Mathf.MoveTowards(_group.alpha, 0f, Time.unscaledDeltaTime / FadeSeconds);
                if (_group.alpha <= 0f) Hide();
            }
        }

        private void Hide()
        {
            _active = false;
            _canvas.SetActive(false);
        }

        private void Build()
        {
            _canvas = new GameObject("LoadingCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            _canvas.transform.SetParent(transform, false);
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _group = _canvas.GetComponent<CanvasGroup>();

            Image back = UiKit.Box(_canvas.transform, "Back", new Color(0.03f, 0.035f, 0.03f, 1f));
            UiKit.Stretch(back.rectTransform);
            back.raycastTarget = true;
            Image glow = UiKit.Box(_canvas.transform, "Glow", new Color(1f, 0.75f, 0.4f, 0.06f), UiKit.RadialGlowSprite);
            UiKit.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1400f, 900f));

            _title = UiKit.Heading(_canvas.transform, string.Empty, 54, TextAnchor.MiddleCenter, UiKit.Accent);
            UiKit.Place(_title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(1400f, 70f));
            _detail = UiKit.Heading(_canvas.transform, string.Empty, 22, TextAnchor.MiddleCenter, UiKit.Dim);
            UiKit.Place(_detail.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 56f), new Vector2(1200f, 32f));

            UiKit.Bar bar = UiKit.MakeBar(_canvas.transform, "Progress", new Vector2(560f, 10f), UiKit.Accent, false);
            UiKit.Place(bar.Root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), bar.Root.sizeDelta);
            _fill = bar.Fill;
            _stage = UiKit.Heading(_canvas.transform, string.Empty, 20, TextAnchor.MiddleCenter, UiKit.Ink);
            UiKit.Place(_stage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -46f), new Vector2(800f, 30f));

            RectTransform rule = UiKit.Divider(_canvas.transform, 640f);
            UiKit.Place(rule, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), rule.sizeDelta);
            _tip = UiKit.Label(_canvas.transform, string.Empty, 20, TextAnchor.MiddleCenter, UiKit.Dim);
            UiKit.Place(_tip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 100f), new Vector2(1100f, 40f));
        }
    }
}
