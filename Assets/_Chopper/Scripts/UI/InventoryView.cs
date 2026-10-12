using System.Collections.Generic;
using System.Text;
using Chopper.Core;
using Chopper.Player;
using Chopper.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Chopper.UI
{
    // One window with pages along the top, paged with LB and RB (Q and E on the keyboard) or a click, the way Valheim
    // keeps its inventory, crafting and skills a bumper apart. It comes in two kinds:
    // - The Tab screen: Character (the sheet), Bag (a grid of what you carry; pick one to see it and use it), Craft (only
    //   what needs no station: the bandage) and Fishing (gear, level and the fish of the water you are at).
    // - The workbench menu (interact at a workbench): Craft (every recipe), Upgrades and Forge (by what pays for them)
    //   and Repair (the axe and the rod). It closes when the player walks away from the bench.
    public class InventoryView
    {
        public enum Page { Character, Bag, Craft, Upgrades, Forge, Repair, Fishing, Mining }

        private static readonly string[] AllPageNames = { "Character", "Bag", "Craft", "Upgrades", "Forge", "Repair", "Fishing", "Mining" };
        private static readonly Page[] TabPages = { Page.Character, Page.Bag, Page.Craft, Page.Fishing, Page.Mining };
        private static readonly Page[] BenchPages = { Page.Craft, Page.Upgrades, Page.Forge, Page.Repair };
        private const float Width = 1440f;
        private const float Height = 840f;
        private const float PageTop = -118f;
        private const float RowHeight = 76f;
        private const int GridColumns = 7;
        private const int GridRows = 4;
        private const float SlotSize = 104f;
        private const float SlotGap = 10f;

        private sealed class Row
        {
            public Text Title;
            public Text Detail;
            public Button Button;
            public RectTransform Root;
            public System.Func<string> Name;
            public System.Func<string> Describe;
            public System.Func<string> ButtonText;
            public System.Func<bool> Enabled;
        }

        private sealed class Slot
        {
            public Button Button;
            public Image Icon;
            public Text Count;
            public Text Name;
        }

        private struct Item
        {
            public string Name;
            public string Description;
            public Sprite Icon;
            public Color Tint;
            public string Count;
            public string Action;
            public System.Action Use;
        }

        private readonly GameConfig _config;
        private readonly bool _bench;
        private readonly Page[] _shown;
        private RectTransform _root;
        private readonly Button[] _tabs;
        private readonly RectTransform[] _pages;
        private Page _page;
        private Text _leftKey;
        private Text _rightKey;
        private readonly List<Row> _rows = new List<Row>();

        // Character page.
        private Text _sheetLeft;
        private Text _sheetRight;

        // Bag page.
        private readonly List<Slot> _slots = new List<Slot>();
        private readonly List<Item> _items = new List<Item>();
        private int _selected;
        private Image _detailIcon;
        private Text _detailName;
        private Text _detailText;
        private Button _detailButton;
        private Text _bagEmpty;

        // Mining page.
        private Text _mineAbout;
        private Text _mineList;

        // Fishing page.
        private Text _fishGear;
        private UiKit.Bar _fishXp;
        private Text _fishXpLabel;
        private Text _fishList;
        private Text _fishWorld;

        // bench: the workbench menu instead of the Tab screen.
        public InventoryView(GameConfig config, bool bench = false)
        {
            _config = config;
            _bench = bench;
            _shown = bench ? BenchPages : TabPages;
            _tabs = new Button[_shown.Length];
            _pages = new RectTransform[_shown.Length];
            _page = _shown[0];
        }

        public bool IsBench => _bench;

        private int SlotOf(Page page) => System.Array.IndexOf(_shown, page);

        public RectTransform Root => _root;

        public bool Visible
        {
            get => _root != null && _root.gameObject.activeSelf;
            set
            {
                if (_root == null) return;
                _root.gameObject.SetActive(value);
                if (value) Show(_page);
            }
        }

        // ---------- Build ----------

        public void Build(Transform parent)
        {
            _root = UiKit.Rect(parent, "Inventory");
            UiKit.Stretch(_root);
            Image dim = UiKit.Box(_root, "Dim", new Color(0f, 0f, 0f, 0.35f));
            UiKit.Stretch(dim.rectTransform);

            RectTransform window = UiKit.Panel(_root, "Window", new Vector2(Width, Height));
            UiKit.Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, window.sizeDelta);
            if (_bench)
            {
                // The workbench menu says what it is above its pages, in the Cinzel heading face.
                Text title = UiKit.Heading(_root, "Workbench", 34, TextAnchor.MiddleCenter, UiKit.Accent);
                UiKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0f, Height * 0.5f + 6f), new Vector2(600f, 46f));
            }

            float tabWidth = 200f;
            float tabsLeft = -(_shown.Length - 1) * tabWidth * 0.5f;
            for (int i = 0; i < _shown.Length; i++)
            {
                Page target = _shown[i];
                Button tab = UiKit.MakeTab(window, AllPageNames[(int)target], new Vector2(tabWidth - 8f, 52f), () => Show(target));
                UiKit.Place(tab.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(tabsLeft + i * tabWidth, -22f), new Vector2(tabWidth - 8f, 52f));
                _tabs[i] = tab;
            }
            // The keys that page left and right, at either end of the tabs.
            _leftKey = UiKit.Label(window, "Q", 20, TextAnchor.MiddleCenter, UiKit.Dim, true);
            UiKit.Place(_leftKey.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(tabsLeft - tabWidth * 0.5f - 36f, -22f), new Vector2(60f, 52f));
            _rightKey = UiKit.Label(window, "E", 20, TextAnchor.MiddleCenter, UiKit.Dim, true);
            UiKit.Place(_rightKey.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-tabsLeft + tabWidth * 0.5f + 36f, -22f), new Vector2(60f, 52f));
            RectTransform rule = UiKit.Divider(window, Width - 80f);
            UiKit.Place(rule, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), rule.sizeDelta);

            for (int i = 0; i < _shown.Length; i++)
            {
                RectTransform page = UiKit.Rect(window, "Page_" + AllPageNames[(int)_shown[i]]);
                UiKit.Place(page, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, PageTop), new Vector2(Width - 100f, Height + PageTop - 30f));
                _pages[i] = page;
                switch (_shown[i])
                {
                    case Page.Character: BuildCharacter(page); break;
                    case Page.Bag: BuildBag(page); break;
                    case Page.Craft: BuildCraft(page); break;
                    case Page.Upgrades: BuildUpgrades(page); break;
                    case Page.Forge: BuildForge(page); break;
                    case Page.Repair: BuildRepair(page); break;
                    case Page.Fishing: BuildFishing(page); break;
                    case Page.Mining: BuildMining(page); break;
                }
            }
            _root.gameObject.SetActive(false);
        }

        private static Text Column(RectTransform page, float x, float width, int size)
        {
            Text text = UiKit.Label(page, string.Empty, size, TextAnchor.UpperLeft, UiKit.Ink);
            UiKit.Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, 0f), new Vector2(width, Height + PageTop - 60f));
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.lineSpacing = 1.12f;
            text.supportRichText = true;
            return text;
        }

        private void BuildCharacter(RectTransform page)
        {
            _sheetLeft = Column(page, 40f, 620f, 20);
            _sheetRight = Column(page, 700f, 620f, 20);
        }

        private void BuildBag(RectTransform page)
        {
            float gridWidth = GridColumns * SlotSize + (GridColumns - 1) * SlotGap;
            for (int i = 0; i < GridColumns * GridRows; i++)
            {
                int index = i;
                int column = i % GridColumns;
                int row = i / GridColumns;
                var slotRoot = new GameObject("Slot" + i, typeof(RectTransform), typeof(Image), typeof(Button));
                slotRoot.transform.SetParent(page, false);
                var rect = slotRoot.GetComponent<RectTransform>();
                UiKit.Place(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f + column * (SlotSize + SlotGap), -10f - row * (SlotSize + SlotGap)), new Vector2(SlotSize, SlotSize));
                var frame = slotRoot.GetComponent<Image>();
                frame.sprite = UiKit.WhiteSprite;
                frame.color = new Color(0.55f, 0.43f, 0.22f, 0.9f);
                var button = slotRoot.GetComponent<Button>();
                ColorBlock colors = button.colors;
                colors.highlightedColor = new Color(1.25f, 1.15f, 0.9f);
                colors.selectedColor = new Color(1.35f, 1.2f, 0.85f);
                button.colors = colors;
                button.onClick.AddListener(() => _selected = index);
                Image inner = UiKit.Box(slotRoot.transform, "Inner", new Color(0.08f, 0.08f, 0.09f, 0.92f));
                UiKit.Stretch(inner.rectTransform, 3f);
                inner.raycastTarget = false;
                Image icon = UiKit.Box(slotRoot.transform, "Icon", Color.white);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                UiKit.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(64f, 64f));
                Text count = UiKit.Label(slotRoot.transform, string.Empty, 17, TextAnchor.UpperRight, UiKit.Accent, true);
                UiKit.Place(count.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-7f, -5f), new Vector2(80f, 22f));
                Text name = UiKit.Label(slotRoot.transform, string.Empty, 13, TextAnchor.LowerCenter, UiKit.Dim);
                UiKit.Place(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 5f), new Vector2(SlotSize - 6f, 20f));
                _slots.Add(new Slot { Button = button, Icon = icon, Count = count, Name = name });
            }
            _bagEmpty = UiKit.Label(page, "Nothing carried yet", 20, TextAnchor.MiddleCenter, UiKit.Dim);
            UiKit.Place(_bagEmpty.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -200f), new Vector2(gridWidth, 40f));

            // The item picked in the grid: what it is, and what it is for.
            float x = 40f + gridWidth + 50f;
            RectTransform detail = UiKit.Rect(page, "Detail");
            UiKit.Place(detail, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -10f), new Vector2(Width - 100f - x - 30f, 460f));
            Image back = UiKit.Box(detail, "Back", new Color(1f, 1f, 1f, 0.04f));
            UiKit.Stretch(back.rectTransform);
            _detailIcon = UiKit.Box(detail, "Icon", Color.white);
            _detailIcon.preserveAspect = true;
            UiKit.Place(_detailIcon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(110f, 110f));
            _detailName = UiKit.Heading(detail, string.Empty, 26, TextAnchor.MiddleCenter, UiKit.Accent);
            UiKit.Place(_detailName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -146f), new Vector2(440f, 36f));
            _detailText = UiKit.Label(detail, string.Empty, 17, TextAnchor.UpperCenter, UiKit.Ink);
            _detailText.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.Place(_detailText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(420f, 170f));
            _detailButton = UiKit.MakeFieldButton(detail, "Use", new Vector2(200f, 46f), UseSelected, 19);
            UiKit.Place(_detailButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(200f, 46f));
        }

        // Every recipe this place can make (RecipeBook): the workbench makes them all, the Tab screen only what needs no
        // station (the bandage).
        private void BuildCraft(RectTransform page)
        {
            for (int id = 0; id < RecipeBook.Count; id++)
            {
                int recipe = id;
                if (!RecipeBook.StationAllows(recipe, _bench)) continue;
                AddRow(page, () => RecipeBook.Get(recipe, _config).Name, () => RecipeText(recipe), () => "Craft", () => CanCraft(recipe), () => Craft(recipe));
            }
        }

        private void BuildRepair(RectTransform page)
        {
            AddRow(page, () => "Axe", () => RepairText(ToolKind.Axe), () => "Repair", () => CanRepair(ToolKind.Axe), () => Repair(ToolKind.Axe));
            AddRow(page, () => LocalFishing != null ? FishingRules.RodName(Mathf.Max(1, LocalFishing.Rod)) : "Fishing rod", () => RepairText(ToolKind.Rod),
                () => "Repair", () => CanRepair(ToolKind.Rod), () => Repair(ToolKind.Rod));
        }

        private void BuildUpgrades(RectTransform page)
        {
            AddRow(page, () => "Axe", () => UpgradeText(PlayerUpgrades.KindAxe), () => "Upgrade", () => CanUpgrade(PlayerUpgrades.KindAxe), () => Upgrade(PlayerUpgrades.KindAxe));
            AddRow(page, () => "Armor", () => UpgradeText(PlayerUpgrades.KindArmor), () => "Upgrade", () => CanUpgrade(PlayerUpgrades.KindArmor), () => Upgrade(PlayerUpgrades.KindArmor));
            AddRow(page, () => "Hut", () => UpgradeText(PlayerUpgrades.KindHut), () => "Upgrade", () => CanUpgrade(PlayerUpgrades.KindHut), () => Upgrade(PlayerUpgrades.KindHut));
        }

        private const int CryptSlots = 3;

        private void BuildForge(RectTransform page)
        {
            AddRow(page, () => "Sturdy gear", () => UpgradeText(PlayerUpgrades.KindSturdy), () => "Forge", () => CanUpgrade(PlayerUpgrades.KindSturdy), () => Upgrade(PlayerUpgrades.KindSturdy));
            AddRow(page, () => "Rune axe", () => UpgradeText(PlayerUpgrades.KindRune), () => "Carve", () => CanUpgrade(PlayerUpgrades.KindRune), () => Upgrade(PlayerUpgrades.KindRune));
            // One ward and one stone per crypt of this world (their arenas give the essence).
            for (int i = 0; i < CryptSlots; i++)
            {
                int slot = i;
                AddRow(page, () => CryptElement(slot) + " ward", () => WardText(slot), () => "Forge", () => CanWard(slot), () => Ward(slot), () => CryptElement(slot) != Element.Neutral);
            }
            for (int i = 0; i < CryptSlots; i++)
            {
                int slot = i;
                AddRow(page, () => CryptElement(slot) + " stone", () => StoneText(slot), () => "Forge", () => CanStone(slot), () => Stone(slot), () => CryptElement(slot) != Element.Neutral);
            }
        }

        private void BuildFishing(RectTransform page)
        {
            _fishGear = Column(page, 40f, 600f, 20);
            _fishXp = UiKit.MakeBar(page, "FishingXp", new Vector2(560f, 14f), UiKit.Accent, false);
            UiKit.Place(_fishXp.Root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -170f), _fishXp.Root.sizeDelta);
            _fishXpLabel = UiKit.Label(page, string.Empty, 16, TextAnchor.MiddleLeft, UiKit.Dim);
            UiKit.Place(_fishXpLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -190f), new Vector2(560f, 24f));
            _fishWorld = Column(page, 40f, 600f, 18);
            _fishWorld.rectTransform.anchoredPosition = new Vector2(40f, -240f);
            _fishList = Column(page, 700f, 620f, 19);
        }

        // A workbench row: name, what it does and costs, and its button. visible hides rows this world has no use for.
        private void AddRow(RectTransform page, System.Func<string> name, System.Func<string> detail, System.Func<string> buttonText, System.Func<bool> enabled,
            System.Action onClick, System.Func<bool> visible = null)
        {
            int index = page.childCount;
            RectTransform row = UiKit.Rect(page, "Row");
            UiKit.Place(row, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f - index * RowHeight), new Vector2(1180f, RowHeight - 10f));
            Image back = UiKit.Box(row, "Back", new Color(1f, 1f, 1f, 0.05f));
            UiKit.Stretch(back.rectTransform);
            Text title = UiKit.Heading(row, string.Empty, 21, TextAnchor.UpperLeft, UiKit.Ink);
            UiKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -7f), new Vector2(500f, 28f));
            Text info = UiKit.Label(row, string.Empty, 16, TextAnchor.UpperLeft, UiKit.Dim);
            info.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.Place(info.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -36f), new Vector2(960f, 30f));
            Button button = UiKit.MakeFieldButton(row, string.Empty, new Vector2(150f, 44f), onClick, 18);
            UiKit.Place(button.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(150f, 44f));
            _rows.Add(new Row { Root = row, Title = title, Detail = info, Button = button, Name = name, Describe = detail, ButtonText = buttonText, Enabled = enabled });
            if (visible != null) _rowVisible[_rows[_rows.Count - 1]] = visible;
        }

        private readonly Dictionary<Row, System.Func<bool>> _rowVisible = new Dictionary<Row, System.Func<bool>>();

        // ---------- Pages ----------

        private void Show(Page page)
        {
            if (SlotOf(page) < 0) page = _shown[0];
            _page = page;
            int slot = SlotOf(page);
            for (int i = 0; i < _pages.Length; i++)
            {
                _pages[i].gameObject.SetActive(i == slot);
                UiKit.SetTabActive(_tabs[i], i == slot);
            }
            RefreshContent();
            // With a pad, the first thing on the new page takes the focus.
            if (EventSystem.current == null) return;
            Selectable first = _pages[slot].GetComponentInChildren<Selectable>();
            EventSystem.current.SetSelectedGameObject(first != null ? first.gameObject : _tabs[slot].gameObject);
        }

        // Development and screenshots: show a page by its place along the top.
        public void DebugShow(int page) => Show(_shown[Mathf.Clamp(page, 0, _shown.Length - 1)]);

        private void Step(int delta)
        {
            int next = (SlotOf(_page) + delta + _shown.Length) % _shown.Length;
            Show(_shown[next]);
        }

        // ---------- Refresh ----------

        // Once a frame while the screen is open: the page keys, then the page itself. The keys are read only here, never
        // from RefreshContent: Show refreshes the new page, and reading a key press there again in the same frame paged
        // on and on until the stack overflowed (the crash when paging with Q or E).
        public void Refresh()
        {
            if (!Visible) return;
            if (_pagedFrame != Time.frameCount)
            {
                _pagedFrame = Time.frameCount;
                Gamepad pad = Gamepad.current;
                Keyboard keyboard = Keyboard.current;
                if ((pad != null && pad.leftShoulder.wasPressedThisFrame) || (keyboard != null && keyboard.qKey.wasPressedThisFrame)) { Step(-1); return; }
                if ((pad != null && pad.rightShoulder.wasPressedThisFrame) || (keyboard != null && keyboard.eKey.wasPressedThisFrame)) { Step(1); return; }
            }
            RefreshContent();
        }

        private int _pagedFrame = -1;

        private void RefreshContent()
        {
            if (!Visible) return;
            _leftKey.text = InputHints.UsingGamepad ? Keybinds.PadDisplayName("<Gamepad>/leftShoulder") : "Q";
            _rightKey.text = InputHints.UsingGamepad ? Keybinds.PadDisplayName("<Gamepad>/rightShoulder") : "E";

            foreach (Row row in _rows)
            {
                // Only the rows of the page on show.
                if (!row.Root.parent.gameObject.activeSelf) continue;
                bool shown = !_rowVisible.TryGetValue(row, out System.Func<bool> visible) || visible();
                if (row.Root.gameObject.activeSelf != shown) row.Root.gameObject.SetActive(shown);
                if (!shown) continue;
                row.Title.text = row.Name();
                row.Detail.text = row.Describe();
                UiKit.SetButtonText(row.Button, row.ButtonText());
                row.Button.interactable = row.Enabled();
            }

            switch (_page)
            {
                case Page.Character: RefreshCharacter(); break;
                case Page.Bag: RefreshBag(); break;
                case Page.Fishing: RefreshFishing(); break;
                case Page.Mining: RefreshMining(); break;
            }
        }

        private static string Gold(string text) => "<color=#EDB84D>" + text + "</color>";

        private void RefreshCharacter()
        {
            PlayerWood wood = PlayerWood.Local;
            if (wood == null) return;
            PlayerHealth health = PlayerHealth.Local;
            PlayerStamina stamina = PlayerStamina.Local;
            PlayerUpgrades upgrades = wood.GetComponent<PlayerUpgrades>();
            PlayerBuffs buffs = wood.GetComponent<PlayerBuffs>();
            PlayerBoons boons = wood.GetComponent<PlayerBoons>();
            Hut hut = Hut.Instance;

            var left = new StringBuilder();
            left.AppendLine(Gold("Chopper"));
            if (health != null) left.AppendLine($"Health  {health.Health} / {health.MaxHealth}");
            if (stamina != null) left.AppendLine($"Stamina  {Mathf.CeilToInt(stamina.Current)} / {Mathf.CeilToInt(stamina.Max)}");
            PlayerFishing meal = wood.GetComponent<PlayerFishing>();
            if (meal != null && meal.FedBonus > 0) left.AppendLine($"Fed  +{meal.FedBonus} stamina, {Mathf.CeilToInt(meal.FedSecondsLeft / 60f)} min left");
            if (upgrades != null)
            {
                left.AppendLine();
                left.AppendLine(Gold("Gear"));
                PlayerAxe ownAxe = wood.GetComponent<PlayerAxe>();
                left.AppendLine($"Axe  level {upgrades.AxeLevel}  (damage x{upgrades.AxeDamageMultiplier:0.00}), {Condition(ownAxe != null ? ownAxe.Durability : 0, ownAxe != null ? ownAxe.MaxDurability : 1)}");
                if (upgrades.RuneLevel > 0) left.AppendLine($"Rune axe  level {upgrades.RuneLevel}");
                var stones = new List<string>();
                foreach (Element element in EssenceRules.Elements) if (upgrades.HasStone(element)) stones.Add(element.ToString());
                if (stones.Count > 0) left.AppendLine("Stones  " + string.Join(", ", stones));
                left.AppendLine($"Armor  level {upgrades.ArmorLevel}  ({Mathf.RoundToInt(upgrades.ArmorReduction * 100f)}% less damage)");
                left.AppendLine(upgrades.Armoured ? "Outfit  armoured" : "Outfit  unarmoured (a boss tree drops an armour kit)");
                left.AppendLine($"Sturdy gear  level {upgrades.SturdyLevel}  (a falling tree {CrushRules.Describe(upgrades.SturdyLevel)})");
                foreach (Element element in EssenceRules.Elements)
                {
                    if (upgrades.WardLevel(element) > 0) left.AppendLine($"{element} ward  level {upgrades.WardLevel(element)}");
                }
            }
            string boonText = boons != null ? boons.Describe() : string.Empty;
            if (boonText.Length > 0)
            {
                left.AppendLine();
                left.AppendLine(Gold("Boons"));
                left.Append(boonText);
            }
            _sheetLeft.text = left.ToString();

            var right = new StringBuilder();
            if (GameUi.CurrentGoal.Length > 0)
            {
                right.AppendLine(Gold("Goal"));
                right.AppendLine(GameUi.CurrentGoal);
                right.AppendLine();
            }
            if (buffs != null)
            {
                right.AppendLine(Gold("Axe charges"));
                bool any = false;
                foreach (Element element in EssenceRules.Elements)
                {
                    int charges = buffs.Charges(element);
                    if (charges <= 0) continue;
                    right.AppendLine($"{element}  {charges} hits");
                    any = true;
                }
                if (!any) right.AppendLine("None");
                right.AppendLine();
            }
            if (hut != null)
            {
                right.AppendLine(Gold("Hut"));
                right.AppendLine($"Level {hut.Level}    Health {hut.Health} / {hut.MaxHealth}");
                right.AppendLine($"Wood store  {hut.WoodStore}");
            }
            _sheetRight.text = right.ToString();
        }

        // ---------- Bag ----------

        private void CollectItems()
        {
            _items.Clear();
            PlayerWood wood = PlayerWood.Local;
            PlayerInventory inventory = PlayerInventory.Local;
            if (wood == null || inventory == null) return;
            PlayerAxe axe = wood.GetComponent<PlayerAxe>();
            PlayerUpgrades upgrades = wood.GetComponent<PlayerUpgrades>();
            PlayerFishing fishing = wood.GetComponent<PlayerFishing>();

            if (axe != null)
                _items.Add(new Item { Name = axe.IsBroken ? "Broken axe" : "Axe", Icon = HotbarIcons.Axe, Tint = axe.IsBroken ? EmptyTint : Color.white,
                    Count = upgrades != null && upgrades.AxeLevel > 0 ? "L" + upgrades.AxeLevel : string.Empty,
                    Description = axe.IsBroken ? "Broken. Repair it at a workbench." : $"For trees and for anything that walks out of the woods at night. {Condition(axe.Durability, axe.MaxDurability)}." });
            if (fishing != null && fishing.Rod > 0)
                _items.Add(new Item { Name = FishingRules.RodName(fishing.Rod), Icon = HotbarIcons.Rod, Tint = fishing.RodBroken ? EmptyTint : Color.white, Count = string.Empty,
                    Description = fishing.RodBroken ? "Broken. Repair it at a workbench."
                        : $"With a {FishingRules.LureName(fishing.Lure).ToLowerInvariant()}. Fish from a shore: the pond, a lake, the marsh or the sea. {Condition(fishing.RodDurability, fishing.RodMaxDurability)}." });
            Add("Wood", PickupFeed.Kind.Wood, wood.Carried, "Carried wood. Store it at the hut or in the chest, or spend it at a workbench.", null, null);
            if (inventory.Torches > 0)
                Add("Torch", PickupFeed.Kind.Torch, inventory.Torches, inventory.TorchLit ? $"Burning, {inventory.TorchSecondsLeft} s left." : "Lights the night around you.",
                    inventory.TorchLit ? "Put out" : "Light", inventory.RequestToggleTorch);
            Add("Bandage", PickupFeed.Kind.Bandage, inventory.Salves, $"Restores {_config.SalveHeal} health.", "Use", inventory.RequestSalve);
            Add("Healing herb", PickupFeed.Kind.Herb, inventory.Herbs, $"{_config.BandageHerbCost} and {_config.BandageFabricCost} fiber make a bandage, anywhere.",
                "Make bandage", () => Craft(RecipeBook.Bandage));
            Add("Fiber", PickupFeed.Kind.Fabric, inventory.Fabric, "From ferns. Bandages are made with it, and rods are mended with it.", "Make bandage", () => Craft(RecipeBook.Bandage));
            Add("Crypt iron", PickupFeed.Kind.Iron, inventory.Iron, "From crypt chests and arena bosses. Forges Sturdy gear.", null, null);
            Add("Rune shard", PickupFeed.Kind.Shard, inventory.Shards, "Carves the Rune axe and sets element stones.", null, null);
            if (upgrades != null && upgrades.ArmourKits > 0)
                _items.Add(new Item { Name = "Armour kit", Icon = HotbarIcons.Get("upgrade"), Tint = Color.white, Count = upgrades.ArmourKits.ToString(),
                    Description = upgrades.Armoured ? "You already wear the armour."
                        : $"From a felled boss tree. Puts on the helmet, chainmail, bracers and shield for good: {Mathf.RoundToInt(PlayerUpgrades.ArmouredReduction * 100f)}% less damage.",
                    Action = upgrades.Armoured ? null : "Put on", Use = upgrades.Armoured ? null : upgrades.RequestUseArmourKit });
            foreach (Element element in EssenceRules.Elements)
            {
                int essence = inventory.Essence(element);
                if (essence <= 0) continue;
                _items.Add(new Item { Name = element + " essence", Icon = PickupFeedView.IconFor(PickupFeed.Kind.Essence), Tint = Color.Lerp(Color.white, ZonePalette.Label(element), 0.7f),
                    Count = essence.ToString(), Description = $"From the {Chopper.World.Generation.CryptPlan.NameFor(element)} arena. Forges {element} wards and stones." });
            }
            if (fishing != null)
            {
                for (int k = 0; k < FishingRules.Kinds; k++)
                {
                    var kind = (FishKind)k;
                    int count = fishing.FishCount(kind);
                    if (count <= 0) continue;
                    FishSpecies species = fishing.SpeciesOf(kind);
                    _items.Add(new Item { Name = species.Name, Icon = HotbarIcons.Fish, Tint = FishTint(species.Rarity), Count = count.ToString(),
                        Description = $"{FishingRules.RarityName(species.Rarity)}, from the {FishingRules.WaterName(species.Home).ToLowerInvariant()}. A meal: +{FoodRules.StaminaBonus(species.Heal, _config.FishStaminaPerPoint)} stamina for {Mathf.RoundToInt(_config.FedSeconds / 60f)} minutes.", Action = "Eat", Use = () => fishing.RequestEat(kind) });
                }
            }
            PlayerMining mining = wood.GetComponent<PlayerMining>();
            if (mining != null)
            {
                for (int k = 0; k < MiningRules.Kinds; k++)
                {
                    int count = mining.FindCount((MineFindKind)k);
                    if (count <= 0) continue;
                    MineFind find = MiningRules.Finds[k];
                    float best = mining.Record((MineFindKind)k);
                    _items.Add(new Item { Name = find.Name, Icon = HotbarIcons.Get(find.Icon), Tint = ItemLook.RarityTint(find.Rarity), Count = count.ToString(),
                        Description = $"{FishingRules.RarityName(find.Rarity)}. {find.About}{(best > 0f ? $" Your biggest: {MiningRules.Size(best)}." : string.Empty)}" });
                }
            }
        }

        private void Add(string name, PickupFeed.Kind kind, int count, string description, string action, System.Action use)
        {
            if (count <= 0) return;
            _items.Add(new Item { Name = name, Icon = PickupFeedView.IconFor(kind), Tint = Color.white, Count = count.ToString(), Description = description, Action = action, Use = use });
        }

        private static readonly Color EmptyTint = new Color(1f, 1f, 1f, 0.35f);

        // Tool condition in words (the hotbar bar shows it without numbers).
        private static string Condition(int durability, int max)
        {
            float share = ToolRules.Fraction(durability, max);
            if (share <= 0f) return "Broken";
            if (share < 0.25f) return "Nearly broken";
            if (share < 0.6f) return "Worn";
            if (share < 0.999f) return "Used";
            return "Like new";
        }

        private static Color FishTint(int rarity)
        {
            switch (rarity)
            {
                case 1: return new Color(0.75f, 0.95f, 0.8f);
                case 2: return new Color(1f, 0.85f, 0.4f);
                case 3: return new Color(0.75f, 0.85f, 0.6f);
                default: return Color.white;
            }
        }

        private void RefreshBag()
        {
            CollectItems();
            // A pad moves the selection by moving the focus.
            GameObject focused = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (focused == _slots[i].Button.gameObject && i < _items.Count) _selected = i;
            }
            if (_selected >= _items.Count) _selected = Mathf.Max(0, _items.Count - 1);
            for (int i = 0; i < _slots.Count; i++)
            {
                Slot slot = _slots[i];
                bool filled = i < _items.Count;
                slot.Icon.enabled = filled;
                slot.Button.interactable = filled;
                if (filled)
                {
                    slot.Icon.sprite = _items[i].Icon;
                    slot.Icon.color = _items[i].Tint;
                    slot.Count.text = _items[i].Count;
                    slot.Name.text = _items[i].Name;
                }
                else
                {
                    slot.Count.text = string.Empty;
                    slot.Name.text = string.Empty;
                }
            }
            bool any = _items.Count > 0;
            _bagEmpty.gameObject.SetActive(!any);
            _detailIcon.enabled = any;
            _detailName.text = any ? _items[_selected].Name : string.Empty;
            _detailText.text = any ? _items[_selected].Description : string.Empty;
            if (any)
            {
                _detailIcon.sprite = _items[_selected].Icon;
                _detailIcon.color = _items[_selected].Tint;
            }
            bool usable = any && _items[_selected].Use != null;
            _detailButton.gameObject.SetActive(usable);
            if (usable) UiKit.SetButtonText(_detailButton, _items[_selected].Action);
        }

        private void UseSelected()
        {
            if (_selected < _items.Count) _items[_selected].Use?.Invoke();
        }

        // ---------- Mining ----------

        private void BuildMining(RectTransform page)
        {
            _mineAbout = Column(page, 40f, 600f, 20);
            _mineList = Column(page, 700f, 620f, 18);
        }

        // How mining works, what is carried, and every find by mine with the count carried and the biggest yet.
        private void RefreshMining()
        {
            PlayerMining mining = PlayerMining.Local;
            if (mining == null) return;
            var about = new StringBuilder();
            about.AppendLine(Gold("Mining"));
            about.AppendLine("Every forest has a mine cut into the mountain at its edge.");
            about.AppendLine("Swing at a vein when its crack glows: at the peak is clean, near it is good, in the dark is rough and can crack the find.");
            about.AppendLine("Each vein gives you one find a day; they fill again at dawn.");
            about.AppendLine();
            about.AppendLine(Gold("With fishing"));
            about.AppendLine($"Glowworms carried: {mining.FindCount(MineFindKind.Glowworm)}. A cast takes one as bait, and rarer fish come to it.");
            about.AppendLine(mining.HasPickHead ? "You carry an old pick head: the glow is easier to catch." : "A fishing line sometimes brings up a geode, or a lost pick head that makes the glow easier to catch.");
            _mineAbout.text = about.ToString();

            var list = new StringBuilder();
            foreach (MineZone zone in new[] { MineZone.Any, MineZone.Water, MineZone.Fire, MineZone.Earth, MineZone.Wind, MineZone.FishingOnly })
            {
                list.AppendLine(Gold(zone == MineZone.Any ? "Every mine" : zone == MineZone.FishingOnly ? "From the water" : MiningRules.MineName(zone)));
                for (int k = 0; k < MiningRules.Kinds; k++)
                {
                    MineFind find = MiningRules.Finds[k];
                    if (find.Home != zone) continue;
                    float best = mining.Record((MineFindKind)k);
                    string record = best > 0f ? $"biggest {MiningRules.Size(best)}" : "not found yet";
                    list.AppendLine($"  {find.Name}  <color=#9A968C>{FishingRules.RarityName(find.Rarity)}, carried {mining.FindCount((MineFindKind)k)}, {record}</color>");
                }
                list.AppendLine();
            }
            _mineList.text = list.ToString();
        }

        // ---------- Fishing ----------

        private void RefreshFishing()
        {
            PlayerFishing fishing = PlayerFishing.Local;
            if (fishing == null) return;
            var gear = new StringBuilder();
            gear.AppendLine(Gold("Gear"));
            gear.AppendLine($"{FishingRules.RodName(Mathf.Max(1, fishing.Rod))}, {FishingRules.LureName(fishing.Lure)}, {Condition(fishing.RodDurability, fishing.RodMaxDurability).ToLowerInvariant()}");
            gear.AppendLine("Better rods and lures come from the crypt arena bosses.");
            gear.AppendLine();
            gear.AppendLine(Gold($"Fishing level {fishing.Level}"));
            _fishGear.text = gear.ToString();

            int[] levels = _config.FishingLevelXp;
            int level = fishing.Level;
            bool top = levels == null || level >= levels.Length - 1;
            int from = levels != null && levels.Length > 0 ? levels[Mathf.Min(level, levels.Length - 1)] : 0;
            int to = top ? from : levels[level + 1];
            _fishXp.Fill.fillAmount = top ? 1f : Mathf.Clamp01((fishing.Xp - from) / (float)Mathf.Max(1, to - from));
            _fishXpLabel.text = top ? $"{fishing.Xp} xp, the highest level" : $"{fishing.Xp} / {to} xp to level {level + 1}";

            PlayerWood local = PlayerWood.Local;
            FishWater here = FishWater.Pond;
            Chopper.World.Generation.WorldGen gen = Chopper.World.Generation.WorldSession.Gen;
            if (local != null && gen != null && gen.TryFishingSpot(new Vector2(local.transform.position.x, local.transform.position.z), out FishWater spot, out _, out _))
                here = spot;
            WeatherKind weatherKind = WeatherSystem.Instance != null ? WeatherSystem.Instance.Kind : WeatherKind.Sunny;
            DayPhase phase = DayNightCycle.Instance != null ? DayNightCycle.Instance.Phase : DayPhase.Day;

            var world = new StringBuilder();
            world.AppendLine(Gold(FishingRules.WaterName(here)));
            if (here == FishWater.Pond)
            {
                FishingHotspots hotspots = FishingHotspots.Instance;
                if (hotspots != null)
                {
                    int active = 0;
                    for (int i = 0; i < FishingHotspots.SpotCount; i++) if (hotspots.State(i) == FishingHotspots.SpotState.Active) active++;
                    world.AppendLine(active > 0 ? $"Hotspots bubbling  {active} of {FishingHotspots.SpotCount}" : "The hotspots are quiet");
                }
            }
            world.AppendLine(weatherKind == WeatherKind.Rain ? "Rain: bites come sooner, and rain fish are out" : "Weather: " + WeatherRules.Name(weatherKind));
            world.AppendLine(phase == DayPhase.Night ? "Night fish are out" : phase == DayPhase.Dawn || phase == DayPhase.Dusk ? "Dawn and dusk fish are out" : "Daytime");
            _fishWorld.text = world.ToString();

            var list = new StringBuilder();
            list.AppendLine(Gold("Fish here"));
            for (int k = 0; k < FishingRules.Kinds; k++)
            {
                FishSpecies species = fishing.SpeciesOf((FishKind)k);
                if (species.Home != here) continue;
                bool locked = fishing.Level < species.MinLevel;
                bool open = FishingRules.WindowOpen(species.Window, weatherKind, phase);
                list.AppendLine();
                list.AppendLine($"{species.Name}  <color=#9A968C>{FishingRules.RarityName(species.Rarity)}, {FishingRules.WindowName(species.Window)}</color>");
                if (locked) list.AppendLine($"  Needs fishing level {species.MinLevel}");
                else if (!open) list.AppendLine("  Not biting in this weather or hour");
                else list.AppendLine($"  Biting now. Carried {fishing.FishCount((FishKind)k)}, +{FoodRules.StaminaBonus(species.Heal, _config.FishStaminaPerPoint)} stamina");
                float best = fishing.Record((FishKind)k);
                list.AppendLine(best > 0f ? $"  <color=#9A968C>Biggest {best:F2} kg, up to {LureRules.MaxKg(species):F1} kg</color>" : $"  <color=#9A968C>Up to {LureRules.MaxKg(species):F1} kg</color>");
            }
            _fishList.text = list.ToString();
        }

        // ---------- Workbench rules ----------

        // Only the workbench menu sells upgrades and the forge, and only while the player stands at a bench.
        private bool NearBench()
        {
            PlayerWood wood = PlayerWood.Local;
            return _bench && wood != null && Workbench.AnyInReach(wood.transform.position);
        }

        private static PlayerUpgrades LocalUpgrades => PlayerWood.Local != null ? PlayerWood.Local.GetComponent<PlayerUpgrades>() : null;
        private static PlayerFishing LocalFishing => PlayerWood.Local != null ? PlayerWood.Local.GetComponent<PlayerFishing>() : null;

        private static string Have(int have) => $"(you have {have})";

        private static Element CryptElement(int slot)
        {
            var plans = Chopper.World.Generation.CryptWorld.Plans;
            return slot < plans.Count ? plans[slot].Site.Zone : Element.Neutral;
        }

        private string WardText(int slot)
        {
            Element element = CryptElement(slot);
            PlayerUpgrades upgrades = LocalUpgrades;
            PlayerInventory inventory = PlayerInventory.Local;
            if (element == Element.Neutral || upgrades == null) return string.Empty;
            int level = upgrades.WardLevel(element);
            string effect = $"Level {level}: {element} enemies do {Mathf.RoundToInt(upgrades.WardReduction(element) * 100f)}% less damage.";
            return upgrades.NextWardCost(element, out int cost)
                ? $"{effect} Next {cost} {element} essence {Have(inventory != null ? inventory.Essence(element) : 0)}."
                : $"{effect} The highest level.";
        }

        private bool CanWard(int slot)
        {
            Element element = CryptElement(slot);
            PlayerUpgrades upgrades = LocalUpgrades;
            PlayerInventory inventory = PlayerInventory.Local;
            return NearBench() && element != Element.Neutral && upgrades != null && inventory != null
                   && upgrades.NextWardCost(element, out int cost) && inventory.Essence(element) >= cost;
        }

        private static void Ward(int slot)
        {
            PlayerUpgrades upgrades = LocalUpgrades;
            if (upgrades != null) upgrades.RequestWard(CryptElement(slot));
        }

        private string StoneText(int slot)
        {
            Element element = CryptElement(slot);
            PlayerUpgrades upgrades = LocalUpgrades;
            PlayerInventory inventory = PlayerInventory.Local;
            if (element == Element.Neutral || upgrades == null) return string.Empty;
            if (upgrades.HasStone(element)) return $"Set in your axe: it is enchanted with {element} for good.";
            return $"Enchants the axe with {element} for good. {_config.StoneEssenceCost} {element} essence {Have(inventory != null ? inventory.Essence(element) : 0)} "
                   + $"and {_config.StoneShardCost} rune shards {Have(inventory != null ? inventory.Shards : 0)}.";
        }

        private bool CanStone(int slot)
        {
            Element element = CryptElement(slot);
            PlayerUpgrades upgrades = LocalUpgrades;
            PlayerInventory inventory = PlayerInventory.Local;
            return NearBench() && element != Element.Neutral && upgrades != null && inventory != null && !upgrades.HasStone(element)
                   && inventory.Essence(element) >= _config.StoneEssenceCost && inventory.Shards >= _config.StoneShardCost;
        }

        private static void Stone(int slot)
        {
            PlayerUpgrades upgrades = LocalUpgrades;
            if (upgrades != null) upgrades.RequestStone(CryptElement(slot));
        }

        private string RecipeText(int recipe)
        {
            Recipe r = RecipeBook.Get(recipe, _config);
            var text = new StringBuilder();
            text.Append(recipe == RecipeBook.Torch
                ? $"Lights the night around you for {Mathf.RoundToInt(_config.TorchBurnSeconds / 60f)} minutes. "
                : $"Restores {_config.SalveHeal} health. ");
            for (int i = 0; i < r.Inputs.Length; i++)
            {
                Ingredient input = r.Inputs[i];
                if (i > 0) text.Append(i == r.Inputs.Length - 1 ? " and " : ", ");
                text.Append($"{input.Amount} {ItemName(input.Kind)} {Have(PlayerItems.Get(LocalPlayer, (int)input.Kind))}");
            }
            text.Append('.');
            return text.ToString();
        }

        private static string ItemName(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Wood: return "wood";
                case ItemKind.Herb: return "healing herbs";
                case ItemKind.Fiber: return "fiber";
                default: return kind.ToString().ToLowerInvariant();
            }
        }

        private static GameObject LocalPlayer => PlayerWood.Local != null ? PlayerWood.Local.gameObject : null;

        private bool CanCraft(int recipe)
        {
            GameObject player = LocalPlayer;
            if (player == null) return false;
            bool atBench = Workbench.AnyInReach(player.transform.position);
            if (!RecipeBook.StationAllows(recipe, atBench)) return false;
            Recipe r = RecipeBook.Get(recipe, _config);
            return RecipeBook.CanAfford(r, kind => PlayerItems.Get(player, (int)kind), PlayerItems.Get(player, (int)r.Output), PlayerItems.Cap(_config, (int)r.Output));
        }

        private static void Craft(int recipe)
        {
            if (PlayerInventory.Local != null) PlayerInventory.Local.RequestCraft(recipe);
        }

        private string RepairText(ToolKind tool)
        {
            PlayerUpgrades upgrades = LocalUpgrades;
            GameObject player = LocalPlayer;
            if (upgrades == null || player == null) return string.Empty;
            int durability, max;
            if (tool == ToolKind.Axe)
            {
                PlayerAxe axe = player.GetComponent<PlayerAxe>();
                durability = axe != null ? axe.Durability : 0;
                max = axe != null ? axe.MaxDurability : 1;
            }
            else
            {
                PlayerFishing fishing = player.GetComponent<PlayerFishing>();
                durability = fishing != null ? fishing.RodDurability : 0;
                max = fishing != null ? fishing.RodMaxDurability : 1;
            }
            string state = Condition(durability, max);
            if (!upgrades.NeedsRepair(tool)) return state + ". Nothing to mend.";
            int wood = upgrades.RepairWood(tool);
            int fabric = upgrades.RepairFabric(tool);
            string cost = $"{wood} wood {Have(PlayerItems.Get(player, (int)ItemKind.Wood))}";
            if (fabric > 0) cost += $" and {fabric} fiber {Have(PlayerItems.Get(player, (int)ItemKind.Fiber))}";
            return $"{state}. Back to new for {cost}.";
        }

        private bool CanRepair(ToolKind tool)
        {
            PlayerUpgrades upgrades = LocalUpgrades;
            GameObject player = LocalPlayer;
            return NearBench() && upgrades != null && player != null && upgrades.NeedsRepair(tool)
                   && PlayerItems.Get(player, (int)ItemKind.Wood) >= upgrades.RepairWood(tool)
                   && PlayerItems.Get(player, (int)ItemKind.Fiber) >= upgrades.RepairFabric(tool);
        }

        private static void Repair(ToolKind tool)
        {
            PlayerUpgrades upgrades = LocalUpgrades;
            if (upgrades != null) upgrades.RequestRepair(tool);
        }

        private string UpgradeText(int kind)
        {
            PlayerUpgrades upgrades = LocalUpgrades;
            Hut hut = Hut.Instance;
            PlayerInventory inventory = PlayerInventory.Local;
            if (upgrades == null || hut == null) return string.Empty;
            int cost;
            switch (kind)
            {
                case PlayerUpgrades.KindAxe:
                    return upgrades.NextAxeCost(out cost)
                        ? $"Level {upgrades.AxeLevel}: faster and stronger swings. Next {cost} wood from the hut store ({hut.WoodStore} stored)."
                        : $"Level {upgrades.AxeLevel}, the highest.";
                case PlayerUpgrades.KindArmor:
                    return upgrades.NextArmorCost(out cost)
                        ? $"Level {upgrades.ArmorLevel}: {Mathf.RoundToInt(upgrades.ArmorReduction * 100f)}% less damage. Next {cost} wood from the hut store ({hut.WoodStore} stored)."
                        : $"Level {upgrades.ArmorLevel}, the highest.";
                case PlayerUpgrades.KindSturdy:
                    return upgrades.NextSturdyCost(out cost)
                        ? $"Level {upgrades.SturdyLevel}: a falling tree {CrushRules.Describe(upgrades.SturdyLevel)}. Next {cost} crypt iron {Have(inventory != null ? inventory.Iron : 0)}."
                        : $"Level {upgrades.SturdyLevel}, the highest.";
                case PlayerUpgrades.KindRune:
                    return upgrades.NextRuneCost(out cost)
                        ? $"Level {upgrades.RuneLevel}: +{Mathf.RoundToInt(upgrades.RuneBonus * 100f)}% axe damage. Next {cost} rune shards {Have(inventory != null ? inventory.Shards : 0)}."
                        : $"Level {upgrades.RuneLevel}, the highest.";
                default:
                    return hut.NextUpgradeCost(out cost)
                        ? $"Level {hut.Level}: more hut health. Next {cost} wood from the hut store ({hut.WoodStore} stored)."
                        : $"Level {hut.Level}, the highest.";
            }
        }

        private bool CanUpgrade(int kind)
        {
            PlayerUpgrades upgrades = LocalUpgrades;
            Hut hut = Hut.Instance;
            if (!NearBench() || upgrades == null || hut == null) return false;
            int cost;
            PlayerInventory inventory = PlayerInventory.Local;
            if (kind == PlayerUpgrades.KindSturdy) return inventory != null && upgrades.NextSturdyCost(out cost) && inventory.Iron >= cost;
            if (kind == PlayerUpgrades.KindRune) return inventory != null && upgrades.NextRuneCost(out cost) && inventory.Shards >= cost;
            bool available = kind == PlayerUpgrades.KindAxe ? upgrades.NextAxeCost(out cost)
                : kind == PlayerUpgrades.KindArmor ? upgrades.NextArmorCost(out cost)
                : hut.NextUpgradeCost(out cost);
            return available && hut.WoodStore >= cost;
        }

        private static void Upgrade(int kind)
        {
            PlayerUpgrades upgrades = LocalUpgrades;
            if (upgrades != null) upgrades.RequestUpgrade(kind);
        }
    }
}
