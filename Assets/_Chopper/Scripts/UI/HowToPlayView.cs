using System;
using UnityEngine;
using UnityEngine.UI;

namespace Chopper.UI
{
    // The reading panel. Shows the instructions (from the guide signpost by the hut and from the Esc menu) or the text
    // of any other sign.
    public class HowToPlayView
    {
        private const float PanelWidth = 1000f;
        private const float PanelHeight = 900f;

        // Built when the screen opens, so the control names follow the player's own bindings and the pad they are using.
        public static string Instructions()
        {
            string attack = InputHints.Name(KeyAction.Attack);
            string interact = InputHints.Name(KeyAction.Interact);
            string bag = InputHints.Name(KeyAction.Inventory);
            string repair = InputHints.Name(KeyAction.Repair);
            string build = InputHints.Name(KeyAction.Build);
            string block = InputHints.Name(KeyAction.Block);
            string food = InputHints.Name(KeyAction.Food);
            // On a pad the hammer and the fish are hotbar slots, picked with the D-pad.
            bool pad = InputHints.UsingGamepad;
            string takeHammer = pad ? "Pick the hammer on the hotbar (D-pad left or right, then up) to take it out." : build + " takes out the hammer.";
            string eat = pad ? "pick the fish on the hotbar and press D-pad up to eat" : food + " eats";
            return
                "<b>Go out and chop.</b> Head into one or all of the four forests: Water to the north, Earth to the east, " +
                "Fire to the south and Wind to the west. Hold " + attack + " at a tree and cut down as many as you can.\n\n" +
                "<b>Bring the wood home.</b> Press " + interact + " at the hut to store it, or put it in the storage chest in the house, which every player shares. " +
                "Everything is made at the workbench in the house (walk up and press " + interact + "): torches, upgrades, crypt gear, and repairs for the axe and the rod, which wear out with use. " +
                "Bandages are the one thing you make anywhere: pick the small red healing herbs and the ferns (fiber) with " + interact + ", then make one from your bag (" + bag + "). " +
                "Press " + repair + " to repair the hut.\n\n" +
                "<b>Forest trees plant saplings.</b> A small tree plants 1 to 2 saplings, a medium one 2 to 3, a large one 3 to 4. Trees in the hub plant none, so a day spent at home " +
                "(fishing, building, talking) brings a quiet night. In each forest some saplings are elemental (colored, glowing) and some are plain brown. " +
                "At night they wake as tree enemies and go for you first, then anything you built in their way, then the hut.\n\n" +
                "<b>Sleep.</b> From dusk on, press " + interact + " at the bed. When every player is in bed the night passes and it is morning.\n\n" +
                "<b>Build.</b> " + takeHammer + " " + block + " picks a piece, " + attack + " places it, " + repair + " (or the wheel) turns it, and " + interact +
                " on a piece takes it down with its wood back. Pieces need a workbench nearby, and a workbench can be built anywhere.\n\n" +
                "<b>Eat.</b> Fish are food: " + eat + " the smallest for extra stamina for a while.\n\n" +
                "<b>Enchant your axe.</b> Kill any elemental tree, say a Water one, and your axe is enchanted with Water for 15 hits: it hits everything harder, and Fire enemies (which Water beats) die much faster. " +
                "Carry that enchantment to the next elemental enemy: Water beats Fire, Fire beats Wind, Wind beats Earth, Earth beats Water.\n\n" +
                "<b>Every night is different.</b> Some nights bring mostly elemental trees and are hard. Some bring mostly plain " +
                "ones, or you keep your enchantment all night and cut through them. Survive until dawn, then do it again.\n\n" +
                "<b>Grove wardens.</b> From day 3, fell too many trees in one part of a forest and its warden rises among the stumps. " +
                "It fights in three stages: hurt it and roots burst from the ground under your feet and guards come to shield its bark, " +
                "so cut the guards down first; near the end it is enraged. It never leaves its grove, and heals if everyone walks away. " +
                "Beat it for a permanent boon for everyone: a better axe, outfit, torch, bandage or stamina.";
        }

        private Text _body;
        private Text _heading;
        private string _signTitle;
        private string _signBody;

        // Shows a sign's text the next time the panel opens; null title goes back to the instructions.
        public void SetSign(string title, string body)
        {
            _signTitle = title;
            _signBody = body;
        }

        private RectTransform _dim;
        private Action _onClose;

        public RectTransform Root => _dim;

        public bool Visible
        {
            get => _dim != null && _dim.gameObject.activeSelf;
            set
            {
                if (_dim == null) return;
                if (value && _body != null)
                {
                    bool sign = _signTitle != null;
                    _body.text = sign ? _signBody : Instructions();
                    if (_heading != null) _heading.text = sign ? _signTitle : "How to play";
                }
                _dim.gameObject.SetActive(value);
            }
        }

        public void Build(Transform parent, Action onClose)
        {
            _onClose = onClose;

            Image dim = UiKit.Box(parent, "HowToPlayDim", new Color(0f, 0f, 0f, 0.65f));
            dim.raycastTarget = true;
            UiKit.Stretch(dim.rectTransform);
            _dim = dim.rectTransform;

            RectTransform panel = UiKit.Panel(_dim, "How to play", new Vector2(PanelWidth, PanelHeight));
            UiKit.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, panel.sizeDelta);

            Text heading = UiKit.Heading(panel, "How to play", 38, TextAnchor.MiddleCenter, UiKit.Accent);
            _heading = heading;
            UiKit.Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(PanelWidth, 48f));
            RectTransform rule = UiKit.Divider(panel, PanelWidth - 80f);
            UiKit.Place(rule, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -76f), rule.sizeDelta);

            Text body = UiKit.Label(panel, Instructions(), 19, TextAnchor.UpperLeft, UiKit.Ink);
            _body = body;
            body.supportRichText = true;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            body.lineSpacing = 1.02f;
            UiKit.Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(PanelWidth - 90f, PanelHeight - 200f));

            RectTransform close = UiKit.MakeButton(panel, "Close", new Vector2(300f, 52f), () => _onClose?.Invoke(), 22).GetComponent<RectTransform>();
            UiKit.Place(close, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), close.sizeDelta);

            _dim.gameObject.SetActive(false);
        }
    }
}
