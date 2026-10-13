using System.Collections.Generic;
using Chopper.Core;
using Chopper.Enemies;
using Chopper.Player;
using Chopper.World;
using Chopper.World.Building;
using Chopper.World.Generation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Chopper.UI
{
    // The whole in-game interface, built in code (no prefabs), laid out like Valheim: top left hotbar; top right
    // minimap and zone name; bottom left health, stamina and status effects; bottom right control hints; a small
    // crosshair in the middle with the interact prompt under it (the camera looks over the right shoulder); top
    // center clock and hut health; left side pickup messages.
    // Esc opens the pause menu, Tab the inventory, M the world map. Interact at the workbench or the storage chest
    // opens its menu; Block with the hammer out opens the piece list.
    public class GameUi : MonoBehaviour
    {
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
        private const int HotbarSlots = 6;

        // The console's hud command: hides the whole HUD, for taking clean screenshots.
        public static bool HudHidden { get; set; }
        // The console's fps command: frames per second and the slowest frame of the last second, bottom right.
        public static bool ShowFps { get; set; }
        private Text _fps;
        private int _fpsFrames;
        private float _fpsTime;
        private float _fpsWorst;
        private const float SlotSize = 68f;
        private const float SlotGap = 8f;
        private const float PadCursorRim = 4f;
        // No full screen flash on a hit any more: the character tints red instead (PlayerHitFlash).
        private const float DamageFlashAlpha = 0f;
        private const float DamageFlashFade = 1.6f;
        // Below this share of health the screen edges shade a faint, steady red (no pulsing).
        private const float LowHealthShare = 0.3f;
        private const float MaxLowHealthShade = 0.16f;
        private const int ToastSlots = 6;
        private const float FindHutDistance = 45f;

        private static readonly Element[] Elementals = { Element.Water, Element.Fire, Element.Earth, Element.Wind };

        [SerializeField] private GameConfig _config;

        private readonly MapService _map = new MapService();
        private JoinRequestView _joinRequests;
        // The local player's map, for the console's explore command.
        public static MapService Map { get; private set; }
        private MiniMapView _miniMap;
        private FullMapView _fullMap;
        private PauseMenuView _pause;
        private InventoryView _bench;
        private InventoryView _workbench;
        private ChestView _chest;
        private BuildMenuView _buildMenu;
        private Image _fedIcon;
        private Text _sleepText;
        private readonly List<Image> _wearFills = new List<Image>();
        private readonly List<RectTransform> _wearBars = new List<RectTransform>();
        private HowToPlayView _howTo;
        private DevPanelView _dev;
        private DevConsoleView _console;
        private Text _objective;

        private RectTransform _hud;
        private UiKit.Bar _health;
        private UiKit.Bar _stamina;
        private UiKit.Bar _hut;
        private Text _clockTitle;
        // "Day N" shows for a few seconds when a day begins, then fades away.
        private int _dayShown = -1;
        private float _dayShownAt;
        private const float DayTitleHoldSeconds = 4f;
        private const float DayTitleFadeSeconds = 1.5f;
        private Text _prompt;
        private Text _downText;
        private Image _flash;
        private Image _downTint;
        private Image _lowHealth;
        private readonly List<Image> _chargeIcons = new List<Image>();
        private readonly List<Text> _chargeTexts = new List<Text>();
        private Image _wetIcon;
        private readonly List<Image> _slotFrames = new List<Image>();
        private readonly List<Text> _slotCounts = new List<Text>();
        // Each slot shows an icon (axe or fists, torch, bandage) instead of a word.
        private readonly List<Image> _slotIcons = new List<Image>();
        private readonly List<Text> _toastTexts = new List<Text>();
        private PickupFeedView _pickups;
        private FishingView _fishingView;
        private int _shownFish = -1;
        // An item slot with none left: the icon stays, faded.
        private static readonly Color EmptySlot = new Color(1f, 1f, 1f, 0.28f);
        private readonly List<Text> _slotKeys = new List<Text>();
        // The pad's hotbar cursor: a bright rim around the slot D-pad up will use. Hidden on keyboard and mouse.
        private RectTransform _padCursor;
        private readonly List<Image> _lifeIcons = new List<Image>();
        private ControlHintsView _hints;
        private RectTransform _crosshair;
        private float _flashAlpha;
        // A black screen that clears after going down into a crypt or climbing out.
        private Image _travelFade;
        private float _travelAlpha;

        private void Awake()
        {
            Map = _map;
            EnsureEventSystem();
            BuildCanvas();
            UserSettings.Apply();
            PlayerHealth.LocalDamaged += OnLocalDamaged;
            PlayerCrypt.LocalTravelled += OnLocalTravelled;
            UiState.HowToPlayRequested += OpenHowTo;
            UiState.SignRequested += OpenSign;
            UiState.WorkbenchRequested += OpenWorkbench;
            UiState.ChestRequested += OpenChest;
        }

        private void OnDestroy()
        {
            PlayerHealth.LocalDamaged -= OnLocalDamaged;
            PlayerCrypt.LocalTravelled -= OnLocalTravelled;
            UiState.HowToPlayRequested -= OpenHowTo;
            UiState.SignRequested -= OpenSign;
            UiState.WorkbenchRequested -= OpenWorkbench;
            UiState.ChestRequested -= OpenChest;
            UiState.CloseAll();
        }

        internal static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        // ---------- Building ----------

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("GameUiCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _hud = UiKit.Rect(canvasGo.transform, "Hud");
            UiKit.Stretch(_hud);

            BuildFlashOverlays();
            BuildBars();
            BuildHotbar();
            BuildClock();
            BuildToasts();
            _joinRequests = new JoinRequestView(canvasGo.transform);
            _fps = UiKit.Label(canvasGo.transform, string.Empty, 18, TextAnchor.LowerRight, UiKit.Ink, true);
            UiKit.Place(_fps.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 8f), new Vector2(420f, 26f));
            _pickups = new PickupFeedView(_hud);
            _fishingView = new FishingView(_hud);
            BuildCenter();

            // The map needs terrain to exist; it is built on first refresh if the terrain is not there yet.
            _map.Build(_config);
            _miniMap = new MiniMapView(_map, _config);
            _fullMap = new FullMapView(_map, _config);
            if (_map.Ready)
            {
                _miniMap.Build(_hud);
                _fullMap.Build(canvasGo.transform);
            }

            _bench = new InventoryView(_config);
            _bench.Build(canvasGo.transform);
            _workbench = new InventoryView(_config, true);
            _workbench.Build(canvasGo.transform);
            _chest = new ChestView();
            _chest.Build(canvasGo.transform);
            _buildMenu = new BuildMenuView();
            _buildMenu.Build(canvasGo.transform, CloseScreens);
            _pause = new PauseMenuView();
            _pause.Build(canvasGo.transform, ClosePause);
            _howTo = new HowToPlayView();
            _howTo.Build(canvasGo.transform, CloseHowTo);
            _dev = new DevPanelView();
            _dev.Build(canvasGo.transform, CloseDev);
            _console = new DevConsoleView(_config);
            _console.Build(canvasGo.transform);
        }

        private void BuildFlashOverlays()
        {
            _flash = UiKit.Box(_hud, "DamageFlash", new Color(0.8f, 0.05f, 0.05f, 0f));
            UiKit.Stretch(_flash.rectTransform);
            _downTint = UiKit.Box(_hud, "DownTint", new Color(0.25f, 0f, 0f, 0f));
            UiKit.Stretch(_downTint.rectTransform);
            // A faint red at the screen edges while health is low (steady, no flashing).
            _lowHealth = UiKit.Box(_hud, "LowHealth", new Color(0.75f, 0.02f, 0.02f, 0f), EdgeVignette());
            UiKit.Stretch(_lowHealth.rectTransform);
            _travelFade = UiKit.Box(_hud, "TravelFade", new Color(0f, 0f, 0f, 0f));
            UiKit.Stretch(_travelFade.rectTransform);
            _travelFade.raycastTarget = false;
            _downText = UiKit.Label(_hud, string.Empty, 34, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.8f), true);
            UiKit.Place(_downText.rectTransform, new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 100f));
        }

        private void BuildBars()
        {
            _health = UiKit.MakeBar(_hud, "Health", new Vector2(330f, 26f), new Color(0.78f, 0.16f, 0.14f), true);
            UiKit.Place(_health.Root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(34f, 62f), _health.Root.sizeDelta);
            _stamina = UiKit.MakeBar(_hud, "Stamina", new Vector2(330f, 16f), new Color(0.93f, 0.78f, 0.25f), false);
            UiKit.Place(_stamina.Root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(34f, 38f), _stamina.Root.sizeDelta);

            // Status effects above the health bar, Valheim style: a dark round badge with an icon, packed from the left in
            // the order below, each only while it lasts (RefreshHotbar lays them out). The element charges carry their
            // hit count; Wet and Fed need no words.
            for (int i = 0; i < Elementals.Length; i++)
            {
                RectTransform badge = StatusBadge("Charge_" + Elementals[i], HotbarIcons.ElementIcon(Elementals[i]), ZonePalette.Label(Elementals[i]));
                Text count = UiKit.Label(badge, string.Empty, 17, TextAnchor.LowerRight, Color.white, true);
                UiKit.Place(count.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(2f, -4f), new Vector2(40f, 22f));
                _chargeIcons.Add(badge.GetComponent<Image>());
                _chargeTexts.Add(count);
            }
            // Wet (after swimming or in the rain): a campfire dries it off. Fed: a meal of fish, more stamina for a while.
            _wetIcon = StatusBadge("Wet", HotbarIcons.Wet, new Color(0.55f, 0.75f, 1f)).GetComponent<Image>();
            _fedIcon = StatusBadge("Fed", HotbarIcons.Fed, new Color(1f, 0.78f, 0.45f)).GetComponent<Image>();

            // Lives left this night, to the right of the health bar.
            for (int i = 0; i < 5; i++)
            {
                Image life = UiKit.Box(_hud, "Life" + i, new Color(0.85f, 0.2f, 0.2f), UiKit.CircleSprite);
                UiKit.Place(life.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(376f + i * 26f, 64f), new Vector2(20f, 20f));
                _lifeIcons.Add(life);
            }

            _hints = new ControlHintsView();
            _hints.Build(_hud);
        }

        private void BuildHotbar()
        {
            Sprite[] icons = { HotbarIcons.Axe, HotbarIcons.Torch, HotbarIcons.Bandage, HotbarIcons.Rod, HotbarIcons.Fish, HotbarIcons.Hammer };
            // Made first so it sits behind the slots and shows only as a rim around the chosen one.
            _padCursor = UiKit.Box(_hud, "PadCursor", new Color(1f, 0.86f, 0.45f, 1f)).rectTransform;
            _padCursor.GetComponent<Image>().raycastTarget = false;
            // Along the top left corner, like Valheim's hotbar.
            for (int i = 0; i < HotbarSlots; i++)
            {
                Image frame = UiKit.Box(_hud, "Slot" + (i + 1), new Color(0.55f, 0.43f, 0.22f, 0.9f));
                UiKit.Place(frame.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(34f + i * (SlotSize + SlotGap), -30f), new Vector2(SlotSize, SlotSize));
                Image inner = UiKit.Box(frame.transform, "Inner", new Color(0.08f, 0.08f, 0.09f, 0.85f));
                UiKit.Stretch(inner.rectTransform, 3f);

                Text key = UiKit.Label(frame.transform, (i + 1).ToString(), 14, TextAnchor.UpperLeft, UiKit.Dim);
                UiKit.Place(key.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f, -3f), new Vector2(34f, 18f));
                _slotKeys.Add(key);
                Image icon = UiKit.Box(frame.transform, "Icon", Color.white, icons[i]);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                UiKit.Stretch(icon.rectTransform, 10f);
                // The slot number stays on top of the icon.
                key.transform.SetAsLastSibling();
                Text count = UiKit.Label(frame.transform, string.Empty, 17, TextAnchor.LowerRight, UiKit.Accent, true);
                UiKit.Place(count.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-6f, 4f), new Vector2(60f, 22f));

                _slotFrames.Add(frame);
                _slotIcons.Add(icon);
                _slotCounts.Add(count);

                // A thin wear bar along the bottom of the tool slots (the axe and the rod), no numbers.
                RectTransform bar = UiKit.Rect(frame.transform, "Wear");
                UiKit.Place(bar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 5f), new Vector2(SlotSize - 14f, 5f));
                Image back = UiKit.Box(bar, "Back", new Color(0f, 0f, 0f, 0.6f));
                UiKit.Stretch(back.rectTransform);
                Image fill = UiKit.Box(bar, "Fill", Color.white);
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                UiKit.Stretch(fill.rectTransform);
                bar.gameObject.SetActive(i == 0 || i == 3);
                _wearBars.Add(bar);
                _wearFills.Add(fill);
            }
        }

        private void BuildClock()
        {
            _clockTitle = UiKit.Heading(_hud, string.Empty, 40, TextAnchor.MiddleCenter, UiKit.Ink);
            UiKit.Place(_clockTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(700f, 50f));

            _hut = UiKit.MakeBar(_hud, "HutBar", new Vector2(360f, 20f), new Color(0.85f, 0.65f, 0.2f), true);
            UiKit.Place(_hut.Root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -86f), _hut.Root.sizeDelta);

            _objective = UiKit.Label(_hud, string.Empty, 20, TextAnchor.UpperLeft, UiKit.Accent, true);
            _objective.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.Place(_objective.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -120f), new Vector2(520f, 70f));

        }

        private void BuildToasts()
        {
            for (int i = 0; i < ToastSlots; i++)
            {
                Text toast = UiKit.Label(_hud, string.Empty, 20, TextAnchor.MiddleLeft, UiKit.Ink, true);
                UiKit.Place(toast.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, -i * 28f), new Vector2(400f, 26f));
                _toastTexts.Add(toast);
            }
        }

        private void BuildCenter()
        {
            // A small crosshair in the middle of the screen: four short strokes around an open center.
            _crosshair = UiKit.Rect(_hud, "Crosshair");
            UiKit.Place(_crosshair, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 24f));
            for (int i = 0; i < 4; i++)
            {
                bool horizontal = i < 2;
                float side = i % 2 == 0 ? -1f : 1f;
                Image stroke = UiKit.Box(_crosshair, "Stroke" + i, new Color(1f, 1f, 1f, 0.8f));
                stroke.raycastTarget = false;
                UiKit.Place(stroke.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    horizontal ? new Vector2(side * 7f, 0f) : new Vector2(0f, side * 7f), horizontal ? new Vector2(7f, 2f) : new Vector2(2f, 7f));
            }

            // The interact prompt, Valheim's way: just right of and below the crosshair, what it is on the first line and
            // the key (in yellow) with what it does under it. No boxes, no bars.
            _prompt = UiKit.Label(_hud, string.Empty, 20, TextAnchor.UpperLeft, UiKit.Ink, true);
            _prompt.supportRichText = true;
            _prompt.lineSpacing = 1.05f;
            UiKit.Place(_prompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(22f, -6f), new Vector2(420f, 90f));

            // In bed: how many are sleeping, low in the middle of the darkened screen.
            _sleepText = UiKit.Heading(_hud, string.Empty, 26, TextAnchor.MiddleCenter, UiKit.Ink);
            UiKit.Place(_sleepText.rectTransform, new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 40f));
        }

        // ---------- Per frame ----------

        private void Update()
        {
            InputHints.Tick();
            RefreshFps();
            HandleKeys();
            _joinRequests?.Refresh();
            _pause?.Refresh();
            PlayerWood local = PlayerWood.Local;
            bool showHud = local != null && !HudHidden;
            if (_hud.gameObject.activeSelf != showHud) _hud.gameObject.SetActive(showHud);
            if (local == null) return;

            if (!_map.Ready && _map.Build(_config))
            {
                _miniMap.Build(_hud);
                _fullMap.Build(_hud.parent);
            }
            _map.Tick(local.transform.position);

            RefreshBars();
            RefreshHotbar(local);
            RefreshClock();
            RefreshPrompt(local);
            RefreshToasts();
            _pickups?.Refresh();
            _fishingView.Refresh(local.GetComponent<PlayerFishing>());
            RefreshOverlays();

            Element zone = ZoneMap.ElementAt(local.transform.position, _config.HubRadius);
            if (_map.Ready)
            {
                _miniMap.Refresh(local.transform, zone);
                _miniMap.Visible = !UiState.InventoryOpen && !UiState.MapOpen;
                _fullMap.Refresh(local.transform);
            }
            _bench.Refresh();
            _workbench.Refresh();
            _chest.Refresh();
            _buildMenu.Refresh();
            // Walking away from the bench or the chest closes its menu; the hammer asks for its list with Block.
            Vector3 here = local.transform.position;
            if (UiState.WorkbenchOpen && !Workbench.AnyInReach(here)) CloseScreens();
            if (UiState.ChestOpen && !StorageChest.AnyInReach(here)) CloseScreens();
            if (UiState.BuildMenuOpen && !_buildMenu.Visible)
            {
                CloseOthersForBuildMenu();
                _buildMenu.Visible = true;
            }
            _console.Refresh();
        }

        private void HandleKeys()
        {
            if (PlayerWood.Local == null || SettingsView.RebindRecent) return;
            Keyboard keyboard = Keyboard.current;
            Gamepad pad = Gamepad.current;

            bool pause = (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
            bool inventory = Keybinds.WasPressed(KeyAction.Inventory);
            bool map = Keybinds.WasPressed(KeyAction.Map);
            // B backs out of any open menu (while playing, B is roll).
            bool cancel = pad != null && pad.buttonEast.wasPressedThisFrame && UiState.InputBlocked;
            bool dev = keyboard != null && keyboard.f1Key.wasPressedThisFrame;
            bool console = keyboard != null && (keyboard.f5Key.wasPressedThisFrame || keyboard.backquoteKey.wasPressedThisFrame);

            // While the console is open every other key is typing, so only its own keys and Esc do anything.
            if (UiState.ConsoleOpen)
            {
                if (console || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)) CloseConsole();
                return;
            }
            if (console && !UiState.PauseOpen)
            {
                OpenConsole();
                return;
            }

            if (pause || cancel)
            {
                if (UiState.DevOpen) CloseDev();
                else if (UiState.HowToPlayOpen) CloseHowTo();
                else if (UiState.InventoryOpen || UiState.MapOpen || UiState.WorkbenchOpen || UiState.ChestOpen || UiState.BuildMenuOpen) CloseScreens();
                else if (UiState.PauseOpen)
                {
                    // B (or Esc) in the settings goes back to the pause menu; Start always closes it.
                    if (_pause.InSettings && !(pad != null && pad.startButton.wasPressedThisFrame)) _pause.Back();
                    else ClosePause();
                }
                else if (pause) OpenPause();
            }
            else if (dev && !UiState.PauseOpen)
            {
                if (UiState.DevOpen) CloseDev();
                else OpenDev();
            }
            else if (inventory && !UiState.PauseOpen)
            {
                bool open = !UiState.InventoryOpen && !UiState.WorkbenchOpen && !UiState.ChestOpen && !UiState.BuildMenuOpen;
                CloseScreens();
                UiState.InventoryOpen = open;
                _bench.Visible = open;
                if (open) Focus(_bench.Root);
            }
            else if (map && !UiState.PauseOpen)
            {
                bool open = !UiState.MapOpen;
                CloseScreens();
                UiState.MapOpen = open;
                _fullMap.Visible = open && _map.Ready;
            }
        }

        // Puts the gamepad selection on the first button of a screen so the stick and D-pad can navigate it.
        private static void Focus(Transform root)
        {
            if (EventSystem.current == null || root == null) return;
            Selectable first = root.GetComponentInChildren<Selectable>();
            EventSystem.current.SetSelectedGameObject(first != null ? first.gameObject : null);
        }

        // Development and screenshots: one page of the inventory (or of the workbench menu when it is open).
        public void DebugInventoryPage(int page)
        {
            if (UiState.WorkbenchOpen) _workbench.DebugShow(page);
            else _bench.DebugShow(page);
        }

        private void OpenWorkbench() => OpenWorkbench(false);

        // force: screenshots, away from any bench.
        private void OpenWorkbench(bool force)
        {
            if (PlayerWood.Local == null || (!force && UiState.InputBlocked)) return;
            CloseScreens();
            UiState.WorkbenchOpen = true;
            _workbench.Visible = true;
            Focus(_workbench.Root);
        }

        private void OpenChest() => OpenChest(false);

        private void OpenChest(bool force)
        {
            if (PlayerWood.Local == null || (!force && UiState.InputBlocked)) return;
            CloseScreens();
            UiState.ChestOpen = true;
            _chest.Visible = true;
            _chest.Focus();
        }

        // The piece list opens over nothing else.
        private void CloseOthersForBuildMenu()
        {
            UiState.InventoryOpen = false;
            UiState.MapOpen = false;
            UiState.WorkbenchOpen = false;
            UiState.ChestOpen = false;
            _bench.Visible = false;
            _workbench.Visible = false;
            _chest.Visible = false;
            _fullMap.Visible = false;
        }

        // Development and screenshots: 0 closes everything, 1 pause, 2 inventory, 3 map, 4 to 7 settings tabs,
        // 8 the workbench menu, 9 the chest, 10 the piece list.
        public void DebugShow(int screen)
        {
            CloseConsole();
            CloseHowTo();
            CloseDev();
            ClosePause();
            CloseScreens();
            if (screen == 1) OpenPause();
            else if (screen == 2) { UiState.InventoryOpen = true; _bench.Visible = true; }
            else if (screen == 3) { UiState.MapOpen = true; _fullMap.Visible = _map.Ready; }
            else if (screen == 8) OpenWorkbench(true);
            else if (screen == 9) OpenChest(true);
            else if (screen == 10) { UiState.BuildMenuOpen = true; _buildMenu.Visible = true; }
            else if (screen >= 4 && screen <= 7)
            {
                OpenPause();
                _pause.DebugShowSettings((SettingsView.Tab)(screen - 4));
            }
        }

        private void OpenDev()
        {
            CloseScreens();
            UiState.DevOpen = true;
            _dev.Visible = true;
            Focus(_dev.Root);
        }

        private void CloseDev()
        {
            UiState.DevOpen = false;
            _dev.Visible = false;
        }

        // Development and screenshots: opens the console and runs a line in it.
        public void DebugConsole(string line)
        {
            OpenConsole();
            _console.Run(line);
        }

        private void OpenConsole()
        {
            CloseDev();
            CloseHowTo();
            CloseScreens();
            UiState.ConsoleOpen = true;
            _console.Visible = true;
        }

        private void CloseConsole()
        {
            UiState.ConsoleOpen = false;
            _console.Visible = false;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private void OpenHowTo()
        {
            _howTo.SetSign(null, null);
            ShowReader();
        }

        // A sign's text in the same reading panel.
        private void OpenSign(string title, string body)
        {
            _howTo.SetSign(title, body);
            ShowReader();
        }

        private void ShowReader()
        {
            if (PlayerWood.Local == null) return;
            ClosePause();
            CloseScreens();
            UiState.HowToPlayOpen = true;
            _howTo.Visible = true;
            Focus(_howTo.Root);
        }

        private void CloseHowTo()
        {
            UiState.HowToPlayOpen = false;
            _howTo.Visible = false;
        }

        private void OpenPause()
        {
            UiState.PauseOpen = true;
            _pause.Visible = true;
            Focus(_pause.Root);
        }

        private void ClosePause()
        {
            UiState.PauseOpen = false;
            _pause.Visible = false;
        }

        private void CloseScreens()
        {
            UiState.InventoryOpen = false;
            UiState.MapOpen = false;
            UiState.WorkbenchOpen = false;
            UiState.ChestOpen = false;
            UiState.BuildMenuOpen = false;
            _bench.Visible = false;
            _workbench.Visible = false;
            _chest.Visible = false;
            _buildMenu.Visible = false;
            _fullMap.Visible = false;
        }

        private void RefreshBars()
        {
            PlayerHealth health = PlayerHealth.Local;
            if (health != null)
            {
                _health.Fill.fillAmount = health.MaxHealth > 0 ? (float)health.Health / health.MaxHealth : 0f;
            }

            PlayerStamina stamina = PlayerStamina.Local;
            if (stamina != null)
            {
                _stamina.Fill.fillAmount = stamina.Fraction;
                _stamina.Fill.color = stamina.Exhausted ? new Color(0.75f, 0.35f, 0.25f) : new Color(0.93f, 0.78f, 0.25f);
            }

            Hut hut = Hut.Instance;
            DayNightCycle cycle = DayNightCycle.Instance;
            bool showHut = hut != null && (hut.Health < hut.MaxHealth || (cycle != null && cycle.Phase == DayPhase.Night));
            _hut.Root.gameObject.SetActive(showHut);
            if (showHut)
            {
                _hut.Fill.fillAmount = hut.MaxHealth > 0 ? (float)hut.Health / hut.MaxHealth : 0f;
            }
        }

        private void RefreshHotbar(PlayerWood local)
        {
            PlayerInventory inventory = PlayerInventory.Local;
            PlayerBuffs buffs = local.GetComponent<PlayerBuffs>();
            PlayerUpgrades upgrades = local.GetComponent<PlayerUpgrades>();

            for (int i = 0; i < Elementals.Length; i++)
            {
                int charges = buffs != null ? buffs.Charges(Elementals[i]) : 0;
                _chargeIcons[i].gameObject.SetActive(charges > 0);
                _chargeTexts[i].text = charges.ToString();
            }
            PlayerMotor motor = local.GetComponent<PlayerMotor>();
            _wetIcon.gameObject.SetActive(motor != null && motor.IsWet);

            Element charged = buffs != null ? buffs.StrongestCharged : Element.Neutral;
            Color normal = new Color(0.55f, 0.43f, 0.22f, 0.9f);
            _slotFrames[0].color = charged == Element.Neutral ? normal : ZonePalette.Label(charged);
            _slotCounts[0].text = upgrades != null && upgrades.AxeLevel > 0 ? $"L{upgrades.AxeLevel}" : string.Empty;
            PlayerAxe ownAxe = local.GetComponent<PlayerAxe>();
            // A broken axe leaves fists; the empty wear bar says why.
            _slotIcons[0].sprite = ownAxe != null && !ownAxe.HasAxe ? HotbarIcons.Fists : HotbarIcons.Axe;
            if (ownAxe != null) ShowWear(0, ownAxe.Durability, ownAxe.MaxDurability);
            // Holstered: the axe is faded and the frame plain; drawn: lit like the rod when it is out.
            bool holstered = ownAxe != null && ownAxe.HasAxe && !ownAxe.IsDrawn;
            _slotIcons[0].color = holstered ? EmptySlot : Color.white;
            if (charged == Element.Neutral && ownAxe != null && ownAxe.IsDrawn) _slotFrames[0].color = new Color(0.45f, 0.7f, 0.9f);

            bool lit = inventory != null && inventory.TorchLit;
            _slotFrames[1].color = lit ? new Color(1f, 0.7f, 0.25f) : normal;
            _slotCounts[1].text = inventory == null || (!lit && inventory.Torches == 0) ? string.Empty : (lit ? $"{inventory.TorchSecondsLeft}s" : inventory.Torches.ToString());
            _slotIcons[1].color = inventory != null && (lit || inventory.Torches > 0) ? Color.white : EmptySlot;
            // A lit torch already shows by its glowing frame and the seconds left.
            _slotCounts[2].text = inventory == null || inventory.Salves == 0 ? string.Empty : inventory.Salves.ToString();
            _slotIcons[2].color = inventory != null && inventory.Salves > 0 ? Color.white : EmptySlot;
            // The rod: lit while it is out, faded until the player has one. Fish: how many are carried.
            PlayerFishing fishing = local.GetComponent<PlayerFishing>();
            bool hasRod = fishing != null && fishing.Rod > 0;
            _slotFrames[3].color = fishing != null && fishing.Busy ? new Color(0.45f, 0.7f, 0.9f) : normal;
            _slotIcons[3].enabled = hasRod;
            _slotIcons[3].color = fishing != null && fishing.RodBroken ? EmptySlot : Color.white;
            if (fishing != null) ShowWear(3, fishing.RodDurability, fishing.RodMaxDurability);
            _fedIcon.gameObject.SetActive(fishing != null && fishing.FedBonus > 0);
            LayoutBadges();
            // The hammer: lit while building.
            _slotFrames[5].color = PlayerBuilder.LocalBuilding ? new Color(0.45f, 0.7f, 0.9f) : normal;
            _slotCounts[3].text = string.Empty;
            int fishTotal = fishing != null ? fishing.FishTotal : 0;
            _slotIcons[4].enabled = fishTotal > 0;
            if (fishTotal != _shownFish)
            {
                _shownFish = fishTotal;
                _slotCounts[4].text = fishTotal > 0 ? fishTotal.ToString() : string.Empty;
            }
        }

        private const float BadgeSize = 50f;
        private const float BadgeGap = 8f;

        // A dark round badge with a thin warm rim and a tinted icon in the middle, hidden until needed.
        private RectTransform StatusBadge(string name, Sprite icon, Color tint)
        {
            Image rim = UiKit.Box(_hud, name, new Color(0.72f, 0.56f, 0.30f, 0.85f), UiKit.CircleSprite);
            rim.raycastTarget = false;
            RectTransform badge = rim.rectTransform;
            UiKit.Place(badge, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(34f, 100f), new Vector2(BadgeSize, BadgeSize));
            Image fill = UiKit.Box(badge, "Fill", new Color(0.06f, 0.05f, 0.045f, 0.92f), UiKit.CircleSprite);
            UiKit.Stretch(fill.rectTransform, 2f);
            fill.raycastTarget = false;
            Image symbol = UiKit.Box(badge, "Icon", tint, icon);
            symbol.preserveAspect = true;
            symbol.raycastTarget = false;
            UiKit.Stretch(symbol.rectTransform, 10f);
            badge.gameObject.SetActive(false);
            return badge;
        }

        // Shows the badges that are on, packed from the left.
        private void LayoutBadges()
        {
            float x = 34f;
            foreach (Image badge in _chargeIcons) Place(badge);
            Place(_wetIcon);
            Place(_fedIcon);

            void Place(Image badge)
            {
                if (!badge.gameObject.activeSelf) return;
                badge.rectTransform.anchoredPosition = new Vector2(x, 100f);
                x += BadgeSize + BadgeGap;
            }
        }

        private void RefreshFps()
        {
            if (!ShowFps)
            {
                if (_fps.text.Length > 0) _fps.text = string.Empty;
                return;
            }
            float dt = Time.unscaledDeltaTime;
            _fpsFrames++;
            _fpsTime += dt;
            _fpsWorst = Mathf.Max(_fpsWorst, dt);
            if (_fpsTime < 1f) return;
            _fps.text = $"{Mathf.RoundToInt(_fpsFrames / _fpsTime)} fps   slowest {_fpsWorst * 1000f:F0} ms";
            _fpsFrames = 0;
            _fpsTime = 0f;
            _fpsWorst = 0f;
        }

        private void ShowWear(int slot, int durability, int max)
        {
            float share = ToolRules.Fraction(durability, max);
            _wearFills[slot].fillAmount = share;
            _wearFills[slot].color = ToolRules.BarColor(share);
        }

        private void RefreshClock()
        {
            DayNightCycle cycle = DayNightCycle.Instance;
            if (cycle == null) return;

            // Only "Day N", fading after a few seconds. No night or dawn words or countdown: the sun going down and
            // coming up says it (the user's call, 2026-10-06).
            if (cycle.Day != _dayShown)
            {
                _dayShown = cycle.Day;
                _dayShownAt = Time.time;
                _clockTitle.text = $"Day {cycle.Day}";
            }
            float shown = Time.time - _dayShownAt;
            Color titleColor = UiKit.Ink;
            titleColor.a = 1f - Mathf.Clamp01((shown - DayTitleHoldSeconds) / DayTitleFadeSeconds);
            _clockTitle.color = titleColor;
        }

        // Where the local player aims: the camera's heading (strikes go where the camera looks, like chops).
        private static Vector3 LocalAim(PlayerWood local)
        {
            if (PlayerCamera.Local != null) return Quaternion.Euler(0f, PlayerCamera.Local.Yaw, 0f) * Vector3.forward;
            return local.transform.forward;
        }

        private void RefreshPrompt(PlayerWood local)
        {
            Hut hut = Hut.Instance;
            bool near = false;
            if (hut != null)
            {
                Vector3 offset = hut.transform.position - local.transform.position;
                offset.y = 0f;
                near = offset.magnitude <= _config.HutInteractRadius;
            }

            string prompt = string.Empty;
            if (hut != null && !UiState.InputBlocked)
            {
                Vector3 position = local.transform.position;
                Sign sign = Sign.Nearest(position);
                PlayerWood.Station station = PlayerWood.StationHere(position);
                PlayerBuilder builder = PlayerBuilder.Local;
                PlayerRest rest = PlayerRest.Local;
                // Next to the hut with wood to put away, interact deposits; otherwise it reads the sign.
                bool signHere = sign != null && (local.Carried == 0 || !near);
                bool herbHere = HerbManager.Instance != null && HerbManager.Instance.HerbInReach(position);
                bool branchHere = BranchManager.Instance != null && BranchManager.Instance.BranchInReach(position);
                WoodPile pile = local.Carried < local.Cap ? WoodPile.Nearest(position) : null;
                PlayerFishing fishing = local.GetComponent<PlayerFishing>();
                int door = CryptWorld.DoorInReach(position);
                int chest = CryptWorld.ChestInReach(position);
                // With the hammer out: what is under the crosshair can be taken down.
                if (builder != null && builder.Active) prompt = BuildPrompt(builder);
                else if (rest != null && rest.InBed) prompt = string.Empty;
                else if (door >= 0) prompt = InputHints.CryptDoorPrompt;
                else if (CryptWorld.ExitInReach(position) >= 0) prompt = InputHints.CryptExitPrompt;
                else if (CryptWorld.ArenaDoorInReach(position) >= 0) prompt = InputHints.ArenaDoorPrompt;
                else if (CryptWorld.ArenaExitInReach(position) >= 0) prompt = InputHints.ArenaExitPrompt;
                else if (chest >= 0) prompt = CryptWorld.OpenedHere(chest) ? InputHints.EmptyChestPrompt : InputHints.ChestPrompt;
                else if (MineWorld.DoorInReach(position) is int mineDoor && mineDoor >= 0) prompt = InputHints.MineDoorPrompt(MineWorld.Plans[mineDoor].Name);
                else if (MineWorld.ExitInReach(position) >= 0) prompt = InputHints.MineExitPrompt;
                else if (MineWorld.VeinInReach(position, LocalAim(local)) is int vein && vein >= 0)
                    prompt = MineWorld.SpentHere(vein) ? InputHints.WorkedVeinPrompt : InputHints.VeinPrompt;
                else if (GroundItem.GearInReach(position, local.gameObject) is GroundItem gear)
                    prompt = gear.Kind == GroundItemKind.Axe ? InputHints.AxeOnGroundPrompt : InputHints.OutfitOnGroundPrompt;
                // Fishing has no prompts: the rod in hand at the water says it all.
                else if (fishing != null && (fishing.Busy || (fishing.Rod > 0 && PlayerFishing.AtWater(position)))) prompt = string.Empty;
                else if (herbHere) prompt = InputHints.HerbPrompt(HerbManager.Instance.FabricInReach(position));
                else if (pile != null) prompt = InputHints.WoodPilePrompt;
                else if (branchHere) prompt = InputHints.BranchPrompt;
                else if (station == PlayerWood.Station.Workbench) prompt = InputHints.WorkbenchPrompt;
                else if (station == PlayerWood.Station.Chest) prompt = InputHints.StoragePrompt;
                else if (station == PlayerWood.Station.Bed) prompt = InputHints.BedPrompt(DayNightCycle.Instance != null && SleepRules.CanSleep(DayNightCycle.Instance.Phase));
                else if (signHere) prompt = InputHints.SignPrompt;
                else if (near && !hut.IsRepaired) prompt = InputHints.RepairHutPrompt(hut.RepairWoodHave, hut.RepairWoodNeeded, local.Carried);
                else if (near) prompt = InputHints.HutPrompt(local.Carried);
            }
            _prompt.text = prompt;
            RefreshObjective(local, hut);
            RefreshSleep();
            _hints.Refresh();
            _crosshair.gameObject.SetActive(!UiState.InputBlocked);
            for (int i = 0; i < _slotKeys.Count; i++) _slotKeys[i].text = InputHints.Hotbar(i);
            _padCursor.gameObject.SetActive(InputHints.UsingGamepad);
            UiKit.Place(_padCursor, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(34f - PadCursorRim + PadHotbar.Selected * (SlotSize + SlotGap), -30f + PadCursorRim), new Vector2(SlotSize + 2f * PadCursorRim, SlotSize + 2f * PadCursorRim));

            // Announcements (hut repaired, night falls, bosses) are no longer shown as big text in the middle of the
            // screen; the objective line and the clock carry that information.
        }

        private static string BuildPrompt(PlayerBuilder builder)
        {
            BuildManager manager = BuildManager.Instance;
            if (manager == null) return string.Empty;
            if (builder.AimedPiece != 0)
            {
                int index = manager.IndexOf(builder.AimedPiece);
                PieceDefinition piece = index >= 0 ? manager.Catalog.Get(manager.Pieces[index].Kind) : null;
                if (piece != null) return InputHints.Prompt(piece.DisplayName, KeyAction.Interact, "Take down");
            }
            PieceDefinition chosen = manager.Catalog != null ? manager.Catalog.Get(builder.Kind) : null;
            if (chosen == null) return string.Empty;
            // Why a red piece cannot go there, in a word or two.
            switch (builder.AimResult)
            {
                case PlaceResult.NoWorkbench: return chosen.DisplayName + "\nNeeds a workbench nearby";
                case PlaceResult.CannotAfford: return chosen.DisplayName + "\nNot enough wood";
                case PlaceResult.Unstable: return chosen.DisplayName + "\nToo far from the ground";
                case PlaceResult.HubProtected: return chosen.DisplayName + "\nThe hub stays as it is: build in the forests";
                default: return chosen.DisplayName;
            }
        }

        private void RefreshSleep()
        {
            PlayerRest rest = PlayerRest.Local;
            bool inBed = rest != null && rest.InBed;
            _sleepText.text = inBed ? $"In bed  {PlayerRest.InBedCount}/{PlayerRest.PlayerCount}" : string.Empty;
        }

        // One line that says what to do next, following the order of the game: fix the hut, chop, defend.
        private void RefreshObjective(PlayerWood local, Hut hut)
        {
            DayNightCycle cycle = DayNightCycle.Instance;
            string text = string.Empty;
            if (hut != null && !hut.IsRepaired)
            {
                Vector3 toHut = hut.transform.position - local.transform.position;
                toHut.y = 0f;
                text = toHut.magnitude > FindHutDistance ? "Find the hut" : $"Repair the hut  {hut.RepairWoodHave}/{hut.RepairWoodNeeded}";
            }
            else if (local.GetComponent<PlayerAxe>() is PlayerAxe axe && axe.IsBroken)
            {
                text = "Repair the axe at the workbench";
            }

            // Near an awake grove warden the goal is the fight: its guards first, while they shield it.
            Treant warden = NearbyWarden(local.transform.position);
            if (warden != null) text = warden.IsShielded ? "Cut down the warden's guards" : "Fell the grove warden";

            // In a crypt arena the line follows the fight instead ("Wave 2 of 3", the boss rising).
            int arena = CryptWorld.ArenaAt(local.transform.position);
            if (arena >= 0)
            {
                string status = ArenaManager.StatusOf(arena);
                text = status;
            }
            // Down in a crypt the surface objectives do not apply.
            else if (CryptWorld.IsUnderground(local.transform.position)) text = string.Empty;
            // Nothing is written on the HUD: the goal is shown at the top of the Tab screen instead.
            CurrentGoal = text;
            _objective.text = string.Empty;
        }

        private const float WardenGoalDistance = 60f;

        private static Treant NearbyWarden(Vector3 position)
        {
            foreach (Treant t in Treant.ClientAll)
            {
                if (t == null || !t.IsWarden || !t.IsActive) continue;
                Vector3 offset = t.transform.position - position;
                offset.y = 0f;
                if (offset.magnitude <= WardenGoalDistance) return t;
            }
            return null;
        }

        // What to do next ("Repair the hut  4/10", "Wave 2/3"), for the Tab screen. Empty when there is nothing to say.
        public static string CurrentGoal { get; private set; } = string.Empty;

        private void RefreshToasts()
        {
            Toasts.Prune();
            IReadOnlyList<Toasts.Item> items = Toasts.Items;
            for (int i = 0; i < _toastTexts.Count; i++)
            {
                if (i < items.Count)
                {
                    Toasts.Item item = items[items.Count - 1 - i];
                    _toastTexts[i].text = item.Text;
                    Color color = item.Color;
                    color.a = Mathf.Clamp01(item.Until - Time.time);
                    _toastTexts[i].color = color;
                }
                else
                {
                    _toastTexts[i].text = string.Empty;
                }
            }
        }

        private void OnLocalDamaged()
        {
            _flashAlpha = DamageFlashAlpha;
        }

        // Clear in the middle, solid toward the edges and corners.
        private static Sprite _edgeVignette;

        private static Sprite EdgeVignette()
        {
            if (_edgeVignette != null) return _edgeVignette;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(u * u + v * v);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0.6f, 1.35f, d)));
                }
            }
            texture.Apply();
            _edgeVignette = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _edgeVignette;
        }

        // Low health: the screen edges shade a faint, steady dark red that eases in and out. No flare on hits and no
        // heartbeat pulse (both were jarring); the character's red tint (PlayerHitFlash) already shows a hit.
        private void RefreshLowHealth(PlayerHealth health)
        {
            float low = 0f;
            if (health != null && !health.IsDown && health.MaxHealth > 0)
            {
                float share = health.Health / (float)health.MaxHealth;
                low = Mathf.Clamp01((LowHealthShare - share) / LowHealthShare);
            }
            Color c = _lowHealth.color;
            c.a = Mathf.MoveTowards(c.a, low * MaxLowHealthShade, 0.25f * Time.deltaTime);
            _lowHealth.color = c;
        }

        private void OnLocalTravelled()
        {
            _travelAlpha = 1f;
        }

        private void RefreshOverlays()
        {
            _flashAlpha = Mathf.MoveTowards(_flashAlpha, 0f, DamageFlashFade * Time.deltaTime);
            var flash = _flash.color;
            flash.a = _flashAlpha;
            _flash.color = flash;
            _travelAlpha = Mathf.MoveTowards(_travelAlpha, 0f, 1.6f * Time.deltaTime);
            _travelFade.color = new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, _travelAlpha));

            PlayerHealth health = PlayerHealth.Local;
            RefreshLowHealth(health);
            bool down = health != null && health.IsDown;
            bool asleep = PlayerRest.Local != null && PlayerRest.Local.InBed;
            var tint = _downTint.color;
            tint.r = asleep && !down ? 0f : 0.25f;
            tint.a = Mathf.MoveTowards(tint.a, down ? 0.55f : asleep ? 0.7f : 0f, 2f * Time.deltaTime);
            _downTint.color = tint;
            if (!down) _downText.text = string.Empty;
            else if (health.IsOut) _downText.text = "No lives left. Hold on until dawn.";
            else if (health.FiniteLives) _downText.text = $"You died!\n{health.Lives} {(health.Lives == 1 ? "life" : "lives")} left";
            else _downText.text = "You died!";

            for (int i = 0; i < _lifeIcons.Count; i++)
            {
                _lifeIcons[i].gameObject.SetActive(health != null && health.FiniteLives && i < health.MaxLives);
                _lifeIcons[i].color = health != null && i < health.Lives ? new Color(0.85f, 0.2f, 0.2f) : new Color(0.25f, 0.1f, 0.1f, 0.8f);
            }
        }
    }
}
