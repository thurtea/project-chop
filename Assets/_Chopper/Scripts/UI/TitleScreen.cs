using System.Collections.Generic;
using Chopper.Networking;
using Chopper.World.Saving;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Chopper.UI
{
    // The start screen: Continue and Load Game (when worlds are saved), New Game, Join Game, Settings, Exit. Shown in
    // the Boot scene while offline. New Game goes to the world name and seed, and Join Game goes to the friends list.
    // Settings holds the controls too (keyboard and gamepad tabs). Built in code like the rest of the UI.
    public class TitleScreen : MonoBehaviour
    {
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
        private const float PanelWidth = 520f;
        private const float MenuX = 0.21f;
        private static readonly Vector2 MenuButtonSize = new Vector2(360f, 58f);
        private static readonly Vector2 ButtonSize = new Vector2(340f, 56f);
        private static readonly Vector2 FooterButtonSize = new Vector2(230f, 56f);

        private enum Flow { NewGame, Join }

        private BootMenu _boot;
        private GameObject _canvas;
        private RectTransform _main;
        private SettingsView _settings;
        private readonly List<GameObject> _branding = new List<GameObject>();
        private RectTransform _join;
        private RectTransform _newWorld;
        private InputField _seed;
        private InputField _worldName;
        private RectTransform _load;
        private RectTransform _loadList;
        private RectTransform _continueButton;
        private RectTransform _loadButton;
        private readonly List<RectTransform> _mainRows = new List<RectTransform>();
        private RectTransform _mainBottomRule;
        private RectTransform _newWorldStart;
        private RectTransform _joinButton;
        private Text _status;
        private bool _shown;
        private AudioSource _music;

        private void Start()
        {
            _boot = GetComponent<BootMenu>();
            GameUi.EnsureEventSystem();
            UserSettings.Apply();
            Build();
            Refresh(true);
        }

        private void Update()
        {
            if (_boot == null) return;
            bool visible = _boot.InBootScene && _boot.Offline;
            if (visible != _shown) Refresh(visible);
            UpdateMusic();
            if (_shown && _status != null) _status.text = _boot.Status;
            if (!_shown) return;
            HandlePadAndBack();
            if (_join != null && _join.gameObject.activeSelf && Time.unscaledTime >= _nextFriendRefresh) RefreshFriends();
        }

        // B or Esc steps back to the main menu. The gamepad also takes the selection back if a mouse click cleared it,
        // so the D-pad never moves nothing.
        private void HandlePadAndBack()
        {
            if (SettingsView.RebindRecent) return;
            Gamepad pad = Gamepad.current;
            Keyboard keyboard = Keyboard.current;
            bool back = (pad != null && pad.buttonEast.wasPressedThisFrame) || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame);
            if (back && _main != null && !_main.gameObject.activeSelf)
            {
                bool editing = (_seed != null && _seed.isFocused) || (_worldName != null && _worldName.isFocused);
                if (!editing)
                {
                    GoBack();
                    return;
                }
            }

            EventSystem system = EventSystem.current;
            if (pad == null || system == null || system.currentSelectedGameObject != null) return;
            bool padInput = pad.leftStick.ReadValue().magnitude > 0.5f || pad.dpad.up.wasPressedThisFrame || pad.dpad.down.wasPressedThisFrame
                            || pad.dpad.left.wasPressedThisFrame || pad.dpad.right.wasPressedThisFrame || pad.buttonSouth.wasPressedThisFrame;
            if (!padInput) return;
            RectTransform active = ActivePanel();
            Selectable first = active != null ? active.GetComponentInChildren<Selectable>() : null;
            if (first != null) system.SetSelectedGameObject(first.gameObject);
        }

        private void GoBack() => ShowMain();

        private RectTransform ActivePanel()
        {
            if (_main.gameObject.activeSelf) return _main;
            if (_settings.Root.gameObject.activeSelf) return _settings.Root;
            if (_join.gameObject.activeSelf) return _join;
            if (_newWorld.gameObject.activeSelf) return _newWorld;
            if (_load.gameObject.activeSelf) return _load;
            if (_credits != null && _credits.gameObject.activeSelf) return _credits;
            return null;
        }

        // Title music, only when a music-title recording is present in Resources/Audio.
        private void UpdateMusic()
        {
            if (_music == null)
            {
                AudioClip[] clips = Chopper.Audio.SoundBank.All("music-title");
                if (clips.Length == 0) return;
                _music = gameObject.AddComponent<AudioSource>();
                _music.clip = clips[0];
                _music.loop = true;
                _music.spatialBlend = 0f;
                _music.volume = 0f;
                _music.Play();
            }
            float target = _shown ? UserSettings.MusicVolume * 0.5f : 0f;
            _music.volume = Mathf.MoveTowards(_music.volume, target, Time.unscaledDeltaTime * 0.5f);
        }

        private void Refresh(bool visible)
        {
            _shown = visible;
            _canvas.SetActive(visible);
            if (!visible) return;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ShowMain();
        }

        private void Build()
        {
            _canvas = new GameObject("TitleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas.transform.SetParent(transform, false);
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // A picture of the game, darkened on the left where the menu sits.
            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter)).GetComponent<RawImage>();
            backdrop.rectTransform.SetParent(_canvas.transform, false);
            UiKit.Stretch(backdrop.rectTransform);
            var shot = Resources.Load<Texture2D>("Branding/title");
            backdrop.texture = shot != null ? shot : BackdropTexture();
            backdrop.raycastTarget = false;
            var fitter = backdrop.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = backdrop.texture.width / (float)backdrop.texture.height;

            var shade = new GameObject("Shade", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            shade.rectTransform.SetParent(_canvas.transform, false);
            UiKit.Stretch(shade.rectTransform);
            shade.texture = ShadeTexture();
            shade.raycastTarget = false;

            Text title = UiKit.Heading(_canvas.transform, "CHOPPER", 120, TextAnchor.MiddleCenter, UiKit.Ink);
            UiKit.Place(title.rectTransform, new Vector2(MenuX, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(800f, 160f));
            var outline = title.gameObject.AddComponent<Shadow>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.65f);
            outline.effectDistance = new Vector2(3f, -4f);
            _branding.Add(title.gameObject);

            // The build version in a corner, so testers can tell which build they are running.
            Text version = UiKit.Label(_canvas.transform, "v" + Application.version, 16, TextAnchor.LowerRight, UiKit.Dim);
            UiKit.Place(version.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 12f), new Vector2(300f, 24f));

            _status = UiKit.Label(_canvas.transform, string.Empty, 22, TextAnchor.MiddleCenter, UiKit.Ink);
            UiKit.Place(_status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(900f, 36f));

            BuildMain();
            BuildSettings();
            BuildJoin();
            BuildNewWorld();
            BuildLoad();
            BuildCredits();
        }

        // ---------- Credits ----------

        private RectTransform _credits;

        // Everything the game is made with.
        private void BuildCredits()
        {
            _credits = NewPage("Credits");
            StepHeader(_credits, "Credits", "Music, sound and art");
            RectTransform panel = UiKit.Panel(_credits, "CreditsPanel", new Vector2(980f, 600f));
            UiKit.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), panel.sizeDelta);
            string text =
                "<b>Music</b>\n" +
                "\"Echoes of Valhalla\", \"Northern Lights\", \"Saga of the Sea Wolves\", \"Frostbound Horizons\",\n" +
                "\"Odin's Whisper\" and \"Throne of the Fjords\" by AlkaKrab\n\n" +
                "<b>Sound</b>\n" +
                "Kenney (kenney.nl): Impact Sounds, RPG Audio, Interface Sounds, UI Audio. CC0\n" +
                "Freesound contributors: kyles, Evcy, JesterWhoo, qubodup, velcronator, Danjocross, nicholasdaryl, ecfike, Kinoton,\n" +
                "ozymandias37, EvilOldScratch, elynch0901, micahlg, MrFossy, nanoPlink, Robo9418, samuelcable,\n" +
                "833510117, nathanaelj83. CC0\n\n" +
                "<b>Art</b>\n" +
                "Quaternius: characters, outfits, animations and weapons. CC0\n" +
                "Kevin Iglesias: run and sprint (Human Basic Motions)\n" +
                "100+ Pixelated Lowpoly Assets 3D by Markus Darius: trees, rocks, crypt stone, chests and lanterns. CC0\n" +
                "Poly Haven: ground textures. CC0\n" +
                "Woodcutting axe by Price, OpenGameArt. CC0\n" +
                "Icons by Lorc, Delapouite, Sbed, Carl Olsen, Faithtoken, Cathelineau, John Redman and Caro Asercion, game-icons.net. CC BY 3.0\n\n" +
                "<b>Type and tools</b>\n" +
                "Cinzel by the Cinzel Project Authors, SIL Open Font License\n" +
                "Unity, FishNet, FishySteamworks (BSD 2-Clause) and Steamworks.NET";
            Text body = UiKit.Label(panel, text, 17, TextAnchor.UpperLeft, UiKit.Ink);
            body.supportRichText = true;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            UiKit.Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(900f, 540f));
            RectTransform back = UiKit.MakeButton(_credits, "Back", FooterButtonSize, ShowMain, 24).GetComponent<RectTransform>();
            UiKit.Place(back, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), FooterButtonSize);
        }

        private static Texture2D ShadeTexture()
        {
            const int width = 64;
            var texture = new Texture2D(width, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)(width - 1);
                float alpha = 0.88f * (1f - Mathf.SmoothStep(0f, 1f, u / 0.6f));
                texture.SetPixel(x, 0, new Color(0.03f, 0.03f, 0.02f, alpha));
            }
            texture.Apply();
            return texture;
        }

        private static Texture2D BackdropTexture()
        {
            const int width = 960;
            const int height = 540;
            var pixels = new Color[width * height];
            var skyTop = new Color(0.55f, 0.72f, 0.86f);
            var skyLow = new Color(0.96f, 0.78f, 0.52f);
            var mist = new Color(0.78f, 0.82f, 0.84f);
            var farPeak = new Color(0.72f, 0.78f, 0.82f);
            var snow = new Color(0.93f, 0.95f, 0.96f);
            var nearPine = new Color(0.16f, 0.28f, 0.14f);
            var water = new Color(0.28f, 0.48f, 0.62f);
            float[] peaks = PeakLine(width);
            float[] pines = TreeLine(width, 0.16f, 0.20f, 29, 1.15f);
            for (int y = 0; y < height; y++)
            {
                float v = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)(width - 1);
                    Color c = Color.Lerp(skyLow, skyTop, Mathf.SmoothStep(0.2f, 1f, v));
                    c = Color.Lerp(c, mist, Mathf.Clamp01(1f - Mathf.Abs(v - 0.42f) * 5f) * 0.45f);
                    if (v < peaks[x])
                    {
                        float snowLine = peaks[x] - 0.06f;
                        c = v > snowLine ? snow : farPeak;
                        c = Color.Lerp(c, mist, 0.25f);
                    }
                    if (v < 0.14f) c = Color.Lerp(water, skyLow, v / 0.14f);
                    if (v < pines[x] && v > 0.05f) c = nearPine;
                    float sun = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(0.72f, 0.62f)) * 7f);
                    c = Color.Lerp(c, new Color(1f, 0.92f, 0.7f), sun * 0.55f);
                    c.a = 1f;
                    pixels[y * width + x] = c;
                }
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        // Stepped mountain tops across the horizon (0 to 1 of the screen).
        private static float[] PeakLine(int width)
        {
            var line = new float[width];
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)width;
                float n = Mathf.PerlinNoise(u * 2.2f, 4.2f);
                float stepped = Mathf.Round(n * 7f) / 7f;
                float peak = 0.34f + stepped * 0.16f;
                if (Mathf.Abs(u - 0.28f) < 0.08f) peak += 0.08f;
                if (Mathf.Abs(u - 0.62f) < 0.1f) peak += 0.12f;
                line[x] = peak;
            }
            return line;
        }

        // Height (0 to 1 of the screen) of a ridge covered in pointed pine crowns at each column.
        private static float[] TreeLine(int width, float baseHeight, float treeHeight, int seed, float scale)
        {
            var random = new System.Random(seed);
            var line = new float[width];
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)width;
                line[x] = baseHeight + (Mathf.PerlinNoise(u * 3f, seed) - 0.5f) * 0.08f;
            }
            int trees = (int)(90 * scale);
            for (int t = 0; t < trees; t++)
            {
                float center = (float)random.NextDouble() * width;
                float h = treeHeight * (0.45f + (float)random.NextDouble() * 0.55f);
                float halfWidth = h * width * 0.11f;
                float foot = line[Mathf.Clamp((int)center, 0, width - 1)];
                for (int x = Mathf.Max(0, (int)(center - halfWidth)); x < Mathf.Min(width, (int)(center + halfWidth)); x++)
                {
                    float k = 1f - Mathf.Abs(x - center) / halfWidth;
                    // Little steps along the slope read as boughs.
                    float bough = Mathf.Repeat(k * 5f, 1f) * 0.12f;
                    float top = foot + h * Mathf.Clamp01(k - bough * (1f - k));
                    line[x] = Mathf.Max(line[x], top);
                }
            }
            return line;
        }

        // A screen title in the display font with a small step line under it ("Step 2 of 2").
        private static Text StepHeader(Transform parent, string title, string step)
        {
            Text heading = UiKit.Heading(parent, title, 46, TextAnchor.MiddleCenter, UiKit.Accent);
            UiKit.Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(1200f, 64f));
            Text stepText = UiKit.Heading(parent, step, 20, TextAnchor.MiddleCenter, UiKit.Dim);
            UiKit.Place(stepText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(600f, 30f));
            RectTransform rule = UiKit.Divider(parent, 520f);
            UiKit.Place(rule, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -164f), rule.sizeDelta);
            return stepText;
        }

        // A full screen page (no box): the step screens draw their own panels inside it.
        private RectTransform NewPage(string name)
        {
            RectTransform page = UiKit.Rect(_canvas.transform, name);
            UiKit.Stretch(page);
            return page;
        }

        private static RectTransform ButtonRow(Transform parent, string text, System.Action onClick)
        {
            return UiKit.MakeButton(parent, text, ButtonSize, onClick, 24).GetComponent<RectTransform>();
        }

        // Back on the left, the main action on the right, along the bottom of a step screen.
        private static RectTransform Footer(Transform parent, string backText, System.Action back, string nextText, System.Action next,
            string middleText = null, System.Action middle = null)
        {
            RectTransform footer = UiKit.Rect(parent, "Footer");
            UiKit.Place(footer, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(1100f, 60f));
            RectTransform rule = UiKit.Divider(footer, 1000f);
            UiKit.Place(rule, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), rule.sizeDelta);

            RectTransform backButton = UiKit.MakeButton(footer, backText, FooterButtonSize, back, 24).GetComponent<RectTransform>();
            UiKit.Place(backButton, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), FooterButtonSize);
            if (middleText != null)
            {
                RectTransform middleButton = UiKit.MakeButton(footer, middleText, FooterButtonSize, middle, 24).GetComponent<RectTransform>();
                UiKit.Place(middleButton, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, FooterButtonSize);
            }
            Button nextButton = UiKit.MakeFieldButton(footer, nextText, FooterButtonSize, next, 26);
            RectTransform nextRect = nextButton.GetComponent<RectTransform>();
            UiKit.Place(nextRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40f, 0f), FooterButtonSize);
            return nextRect;
        }

        // ---------- Main menu ----------

        private void BuildMain()
        {
            // No box: the logo above, then a clean column of text buttons between two faint rules.
            _main = UiKit.Rect(_canvas.transform, "Menu");
            UiKit.Place(_main, new Vector2(MenuX, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 150f), new Vector2(PanelWidth, 460f));
            RectTransform top = UiKit.Divider(_main, 420f);
            UiKit.Place(top, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), top.sizeDelta);
            _continueButton = MenuButton("Continue", ContinueLatest);
            _mainRows.Add(MenuButton("New Game", () => StartFlow(Flow.NewGame)));
            _mainRows.Add(MenuButton("Tutorial", StartTutorial));
            _loadButton = MenuButton("Load Game", ShowLoad);
            _mainRows.Add(MenuButton("Join Game", () => StartFlow(Flow.Join)));
            _mainRows.Add(MenuButton("Settings", () => ShowSettings(SettingsView.Tab.General)));
            _mainRows.Add(MenuButton("Credits", () => Show(_credits)));
            _mainRows.Add(MenuButton("Exit", Exit));
            _mainBottomRule = UiKit.Divider(_main, 420f);
        }

        private RectTransform MenuButton(string text, System.Action onClick)
        {
            return UiKit.MakeButton(_main, text, MenuButtonSize, onClick, 28).GetComponent<RectTransform>();
        }

        // Continue and Load Game only show when a world has been saved; the column is stacked again to fit.
        private void ShowMain()
        {
            bool saves = SaveFiles.Any();
            _continueButton.gameObject.SetActive(saves);
            _loadButton.gameObject.SetActive(saves);
            var rows = new List<RectTransform>();
            if (saves) rows.Add(_continueButton);
            rows.Add(_mainRows[0]);
            if (saves) rows.Add(_loadButton);
            for (int i = 1; i < _mainRows.Count; i++) rows.Add(_mainRows[i]);
            UiKit.Stack(_main, rows, 12f, 26f);
            float bottom = 26f + rows.Count * MenuButtonSize.y + (rows.Count - 1) * 12f + 26f;
            UiKit.Place(_mainBottomRule, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -bottom), _mainBottomRule.sizeDelta);
            Show(_main);
        }

        // The practice yard: a fixed world with everything in hand, never saved (TutorialSession, TutorialYard).
        private void StartTutorial()
        {
            SaveSystem.Clear();
            Chopper.World.TutorialSession.Begin();
            LoadingScreen.Show("Tutorial", "The practice yard");
            _boot.StartHost(Chopper.World.TutorialSession.Seed);
        }

        // ---------- Loading saved worlds ----------

        private void ContinueLatest()
        {
            List<SaveSummary> saves = SaveFiles.List();
            if (saves.Count > 0) LoadWorld(saves[0].Path);
        }

        private void LoadWorld(string path)
        {
            WorldSave save = SaveFiles.Read(path);
            if (save == null)
            {
                _boot.SetStatus("That world could not be read.");
                return;
            }
            SaveSystem.PrepareLoad(path, save);
            LoadingScreen.Show(save.Name, $"Day {save.Day}    Seed {save.Seed}");
            _boot.StartHost(save.Seed);
        }

        private void BuildLoad()
        {
            _load = NewPage("Load Game");
            StepHeader(_load, "Load a World", "Your saved worlds, most recent first");
            _loadList = UiKit.Panel(_load, "Worlds", new Vector2(900f, 560f));
            UiKit.Place(_loadList, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), _loadList.sizeDelta);
            RectTransform back = UiKit.MakeButton(_load, "Back", FooterButtonSize, ShowMain, 24).GetComponent<RectTransform>();
            UiKit.Place(back, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), FooterButtonSize);
        }

        private const int MaxListedWorlds = 6;

        private void ShowLoad()
        {
            for (int i = _loadList.childCount - 1; i >= 0; i--)
            {
                Transform child = _loadList.GetChild(i);
                if (child.name.StartsWith("World_")) Destroy(child.gameObject);
            }
            List<SaveSummary> saves = SaveFiles.List();
            var rows = new List<RectTransform>();
            for (int i = 0; i < saves.Count && i < MaxListedWorlds; i++) rows.Add(WorldRow(saves[i]));
            UiKit.Stack(_loadList, rows, 10f, 30f);
            Show(_load);
            if (saves.Count == 0) ShowMain();
        }

        private const float DeleteHoldSeconds = 1.2f;

        // One saved world: its name, day, seed and when it was last played, then Load and Delete (held down to delete).
        private RectTransform WorldRow(SaveSummary summary)
        {
            RectTransform row = UiKit.Rect(_loadList, "World_" + summary.Name);
            row.sizeDelta = new Vector2(820f, 74f);
            Text name = UiKit.Heading(row, summary.Name, 26, TextAnchor.MiddleLeft, UiKit.Ink);
            UiKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -6f), new Vector2(480f, 34f));
            Text detail = UiKit.Label(row, $"Day {summary.Day}    Seed {summary.Seed}    {summary.SavedAt}", 17, TextAnchor.MiddleLeft, UiKit.Dim);
            UiKit.Place(detail.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 8f), new Vector2(480f, 26f));

            string path = summary.Path;
            Button load = UiKit.MakeFieldButton(row, "Load", new Vector2(150f, 50f), () => LoadWorld(path), 22);
            UiKit.Place(load.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-170f, 0f), new Vector2(150f, 50f));
            // Deleting cannot be undone, so it is a hold: a red bar fills behind the label while Delete is held.
            Button delete = UiKit.MakeButton(row, "Delete", new Vector2(140f, 50f), null, 20);
            HoldButton.Attach(delete, DeleteHoldSeconds, new Color(0.65f, 0.12f, 0.08f, 0.85f), () =>
            {
                SaveFiles.Delete(path);
                ShowLoad();
            });
            UiKit.Place(delete.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(140f, 50f));
            UiKit.HighlightRow(row, load, name);
            return row;
        }

        // New Game goes straight to the world name and seed (everyone plays as Chopper, so there is no creator);
        // Join Game goes to the friends list.
        private void StartFlow(Flow flow)
        {
            if (flow == Flow.Join)
            {
                Show(_join);
                RefreshFriends();
                return;
            }
            _seed.text = Chopper.World.Generation.WorldSession.RandomSeed().ToString();
            _worldName.text = SaveFiles.SuggestName();
            Show(_newWorld);
        }

        // The same tabbed settings the pause menu uses: general, graphics, keyboard and mouse, gamepad.
        private void BuildSettings()
        {
            _settings = SettingsView.Build(_canvas.transform, ShowMain);
            RectTransform root = _settings.Root;
            UiKit.Place(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), root.sizeDelta);
            root.gameObject.SetActive(false);
        }

        private void ShowSettings(SettingsView.Tab tab)
        {
            Show(_settings.Root);
            _settings.ShowTab(tab);
        }

        // ---------- Step 2: a text field panel (seed or address) ----------

        private InputField MakeField(Transform parent, string label, out RectTransform labelRect, out RectTransform fieldRect)
        {
            Text text = UiKit.Heading(parent, label, 22, TextAnchor.MiddleLeft, UiKit.Ink);
            text.rectTransform.sizeDelta = new Vector2(420f, 30f);
            labelRect = text.rectTransform;

            Image field = UiKit.Box(parent, label + "Field", new Color(1f, 0.9f, 0.75f, 0.07f), UiKit.SoftPanelSprite);
            field.type = Image.Type.Sliced;
            field.raycastTarget = true;
            field.rectTransform.sizeDelta = new Vector2(420f, 54f);
            fieldRect = field.rectTransform;
            Text value = UiKit.Label(field.transform, string.Empty, 24, TextAnchor.MiddleCenter, UiKit.Ink);
            UiKit.Stretch(value.rectTransform, 12f);
            var input = field.gameObject.AddComponent<InputField>();
            input.targetGraphic = field;
            input.textComponent = value;
            input.characterLimit = 40;
            // With a controller the field must not grab the stick the moment it is highlighted: it only starts
            // editing when A (or a click) is pressed on it, so the D-pad can move on to the buttons.
            input.shouldActivateOnSelect = false;
            ColorBlock colors = input.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(2.6f, 2f, 1.2f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.colorMultiplier = 1f;
            input.colors = colors;
            return input;
        }

        private RectTransform CenterPanel(RectTransform page, float height)
        {
            RectTransform panel = UiKit.Panel(page, "Panel", new Vector2(PanelWidth + 80f, height));
            UiKit.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), panel.sizeDelta);
            return panel;
        }

        // Friends are found through Steam, never by address: the list shows Steam friends who are in a Chopper world.
        private void BuildJoin()
        {
            _join = NewPage("Join Game");
            StepHeader(_join, "Join a Friend", "Join Game");

            // The host's join code (on their pause menu): type it and join. The host then lets you in.
            RectTransform codeRow = UiKit.Rect(_join, "CodeRow");
            UiKit.Place(codeRow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(900f, 60f));
            _code = MakeField(codeRow, "Join code", out RectTransform codeLabel, out RectTransform codeField);
            _code.characterLimit = 8;
            UiKit.Place(codeLabel, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(180f, 54f));
            UiKit.Place(codeField, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(220f, 0f), new Vector2(360f, 54f));
            Button codeJoin = UiKit.MakeFieldButton(codeRow, "Join", new Vector2(200f, 54f), JoinByCode, 22);
            UiKit.Place(codeJoin.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(620f, 0f), new Vector2(200f, 54f));
            _code.onEndEdit.AddListener(_ => { if (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)) JoinByCode(); });

            _friendList = UiKit.Panel(_join, "Friends", new Vector2(900f, 420f));
            UiKit.Place(_friendList, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), _friendList.sizeDelta);
            _friendHint = UiKit.Label(_friendList, string.Empty, 19, TextAnchor.MiddleCenter, UiKit.Dim);
            UiKit.Place(_friendHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(820f, 60f));
            _friendHint.horizontalOverflow = HorizontalWrapMode.Wrap;
            _joinButton = Footer(_join, "Back", ShowMain, "Refresh", RefreshFriends);
        }

        private RectTransform _friendList;
        private InputField _code;

        private void JoinByCode()
        {
            if (_code == null || string.IsNullOrWhiteSpace(_code.text)) return;
            // A joining player plays in the host's world; only the host saves it.
            SaveSystem.Clear();
            _boot.SetStatus("Looking for that world...");
            Chopper.Networking.SteamLobby.JoinByCode(_code.text);
        }
        private Text _friendHint;
        private float _nextFriendRefresh;
        private const int MaxListedFriends = 6;

        // Rebuilds the list of friends in a world. Called when the page opens, on Refresh, and every few seconds.
        private void RefreshFriends()
        {
            _nextFriendRefresh = Time.unscaledTime + 3f;
            for (int i = _friendList.childCount - 1; i >= 0; i--)
            {
                Transform child = _friendList.GetChild(i);
                if (child.name.StartsWith("Friend_")) Destroy(child.gameObject);
            }
            if (!Chopper.Networking.NetworkBootstrap.UsingSteam)
            {
                string problem = Chopper.Networking.SteamPlatform.Problem;
                _friendHint.text = string.IsNullOrEmpty(problem) ? "Playing together needs Steam. Start Steam, then restart Chopper." : problem;
                return;
            }
            var friends = Chopper.Networking.SteamLobby.FriendsInWorlds();
            var rows = new List<RectTransform>();
            for (int i = 0; i < friends.Count && i < MaxListedFriends; i++) rows.Add(FriendRow(friends[i]));
            UiKit.Stack(_friendList, rows, 10f, 30f);
            _friendHint.text = friends.Count > 0
                ? "Or join a friend from this list. Nobody sees anyone's IP address: everything goes through Steam."
                : "Ask the host for the join code on their pause menu, or wait here: friends in a world show up by themselves.";
        }

        private RectTransform FriendRow(Chopper.Networking.SteamLobby.FriendWorld friend)
        {
            RectTransform row = UiKit.Rect(_friendList, "Friend_" + friend.Friend);
            row.sizeDelta = new Vector2(820f, 74f);
            Text name = UiKit.Heading(row, friend.FriendName, 26, TextAnchor.MiddleLeft, UiKit.Ink);
            UiKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -6f), new Vector2(560f, 34f));
            Text detail = UiKit.Label(row, "Playing in " + friend.WorldName, 17, TextAnchor.MiddleLeft, UiKit.Dim);
            UiKit.Place(detail.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 8f), new Vector2(560f, 26f));
            ulong lobby = friend.Lobby;
            string who = friend.FriendName;
            Button join = UiKit.MakeFieldButton(row, "Join", new Vector2(160f, 50f), () =>
            {
                // A joining player plays in the host's world; only the host saves it.
                SaveSystem.Clear();
                LoadingScreen.Show("Joining " + who, "Through Steam");
                Chopper.Networking.SteamLobby.Join(lobby);
            }, 22);
            UiKit.Place(join.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(160f, 50f));
            UiKit.HighlightRow(row, join, name);
            return row;
        }

        // Every new game is a new world made from a seed, like other survival games. The field starts with a random
        // seed; typing a number (or any word) gives that world again, so friends can share good ones.
        private void BuildNewWorld()
        {
            _newWorld = NewPage("New World");
            StepHeader(_newWorld, "Choose Your World", "New Game");
            RectTransform panel = CenterPanel(_newWorld, 500f);
            _worldName = MakeField(panel, "World name", out RectTransform nameLabel, out RectTransform nameField);
            _worldName.characterLimit = 28;
            _seed = MakeField(panel, "World seed", out RectTransform label, out RectTransform field);
            _seed.characterLimit = 24;
            Text hint = UiKit.Label(panel, "The same seed always makes the same land. Share a good one with friends.", 17, TextAnchor.MiddleCenter, UiKit.Dim);
            hint.rectTransform.sizeDelta = new Vector2(480f, 50f);
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            RectTransform reroll = ButtonRow(panel, "New random seed", () => _seed.text = Chopper.World.Generation.WorldSession.RandomSeed().ToString());
            UiKit.Stack(panel, new[] { nameLabel, nameField, label, field, hint.rectTransform, reroll }, 12f, 40f);
            _newWorldStart = Footer(_newWorld, "Back", ShowMain, "Start", StartNewWorld);
        }

        private void StartNewWorld()
        {
            int seed = Chopper.World.Generation.WorldSession.ParseSeed(_seed.text);
            SaveSystem.PrepareNew(_worldName.text);
            LoadingScreen.Show(SaveSystem.WorldName, $"New world    Seed {seed}");
            _boot.StartHost(seed);
        }


        private void Show(RectTransform panel)
        {
            _main.gameObject.SetActive(panel == _main);
            _settings.Root.gameObject.SetActive(panel == _settings.Root);
            // Only the main menu shows the logo; the other screens have their own titles.
            foreach (GameObject branding in _branding) branding.SetActive(panel == _main);
            _join.gameObject.SetActive(panel == _join);
            _newWorld.gameObject.SetActive(panel == _newWorld);
            if (_load != null) _load.gameObject.SetActive(panel == _load);
            if (_credits != null) _credits.gameObject.SetActive(panel == _credits);

            // Puts the gamepad selection on the first control so the stick and D-pad work here too.
            if (EventSystem.current == null) return;
            Selectable first;
            if (panel == _newWorld && _newWorldStart != null) first = _newWorldStart.GetComponent<Selectable>();
            else if (panel == _join && _joinButton != null) first = _joinButton.GetComponent<Selectable>();
            else first = panel.GetComponentInChildren<Selectable>();
            EventSystem.current.SetSelectedGameObject(first != null ? first.gameObject : null);
        }

        private static void Exit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
