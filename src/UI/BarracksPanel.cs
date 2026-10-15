using System.Collections.Generic;
using HighCommand.Army;
using HighCommand.Client;
using HighCommand.Mode;
using RoR2;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HighCommand.UI
{
    /// <summary>
    /// The barracks: every survivor the game knows as a card of the stock character select (the bubbly backdrop, the
    /// portrait in a slate frame, the name on a dark strip), what it costs or why it cannot be bought; under them the
    /// army upgrades as stock menu buttons; at the bottom the description of what the pointer rests on.
    /// </summary>
    internal sealed class BarracksPanel
    {
        private const int Columns = 7;
        private const float CardSize = 136f, Gap = 8f;
        private const float NameH = 44f;
        private const float UpgradeH = 52f, UpgradeHeadH = 30f;
        private const float DetailH = 52f;
        private const float StatusEvery = 0.25f;   // the purse and unlocks are looked at this often, not every frame

        private static readonly char[] LineBreaks = { '\n', '\r' };
        private static readonly Color PortraitClosed = new Color(0.2f, 0.2f, 0.22f, 1f);
        private static readonly Color PortraitOwned = new Color(0.55f, 0.55f, 0.58f, 1f);
        private static readonly Color FrameColor = new Color(Theme.Button.r, Theme.Button.g, Theme.Button.b, 0.87f);

        private sealed class Card
        {
            internal SurvivorDef def;
            internal RectTransform root;
            internal RawImage portrait;
            internal TextMeshProUGUI name;
            internal TextMeshProUGUI line;
            internal HoverOutline hover;
            internal RecruitStatus status = (RecruitStatus)255;
            internal int price = -1;
            internal bool wasHover;
        }

        private readonly Window window;
        private readonly RectTransform grid;
        private readonly TextMeshProUGUI upgradeHead;
        private readonly MenuButton[] upgrades = new MenuButton[ArmyUpgrades.Count];
        private readonly int[] upgradeKeys = new int[ArmyUpgrades.Count];
        private readonly TextMeshProUGUI detail;
        private readonly List<Card> cards = new List<Card>();
        private static readonly IReadOnlyList<Hero> NoHeroes = new List<Hero>();

        private float statusAt = -10f;
        private int noteKey = int.MinValue;
        private object detailFor;

        internal bool Visible => window.Visible;

        internal BarracksPanel(Transform parent)
        {
            float bodyW = Columns * CardSize + (Columns - 1) * Gap;
            window = new Window(parent, "Barracks", new Vector2(bodyW + 2f * (Window.Pad + 3f), 700f));

            grid = UiKit.NewRect("Grid", window.Body);
            grid.anchorMin = grid.anchorMax = new Vector2(0f, 1f);
            grid.pivot = new Vector2(0f, 1f);

            upgradeHead = UiKit.NewText("UpgradeHead", window.Body, "", Theme.SizeBody, Theme.TextSub, TextAlignmentOptions.BottomLeft);
            float buttonW = (bodyW - (ArmyUpgrades.Count - 1) * Gap) / ArmyUpgrades.Count;
            for (int i = 0; i < upgrades.Length; i++)
            {
                upgrades[i] = new MenuButton(window.Body, "Upgrade_" + i, new Vector2(buttonW, UpgradeH), 20f);
                upgrades[i].Label.lineSpacing = -12f;
                upgradeKeys[i] = int.MinValue;
            }

            detail = UiKit.NewText("Detail", window.Body, "", 16f, Theme.TextBody, TextAlignmentOptions.TopLeft);
            detail.enableWordWrapping = true;
            detail.enableAutoSizing = true;
            detail.fontSizeMin = 12f;
            detail.fontSizeMax = 16f;
            detail.overflowMode = TextOverflowModes.Ellipsis;

            ClientInput.RegisterUiHitTest(Contains);
        }

        internal bool Contains(Vector2 screenPoint) => window.Contains(screenPoint);

        internal void Toggle()
        {
            if (window.Visible) Hide();
            else Show();
        }

        internal void Show()
        {
            if (window.Visible) return;
            Build();
            window.Show();
            UiSound.Click();
        }

        internal void Hide() => window.Hide();

        internal void Destroy()
        {
            ClientInput.UnregisterUiHitTest(Contains);
            window.Destroy();
        }

        // ---- building ---------------------------------------------------------------------------------------

        private void Build()
        {
            foreach (Card c in cards) if (c.root) Object.Destroy(c.root.gameObject);
            cards.Clear();
            statusAt = -10f;
            noteKey = int.MinValue;
            detailFor = null;
            for (int i = 0; i < upgradeKeys.Length; i++) upgradeKeys[i] = int.MinValue;

            int index = 0;
            foreach (SurvivorDef def in ArmyService.Candidates())
            {
                int col = index % Columns, row = index / Columns;
                Card c = NewCard(def);
                c.root.anchoredPosition = new Vector2(col * (CardSize + Gap), -row * (CardSize + Gap));
                cards.Add(c);
                index++;
            }

            float bodyW = window.BodyWidth;
            int rows = Mathf.Max(1, (index + Columns - 1) / Columns);
            float gridH = rows * CardSize + (rows - 1) * Gap;
            grid.sizeDelta = new Vector2(bodyW, gridH);
            grid.anchoredPosition = Vector2.zero;

            float y = gridH + Theme.GroupGap;
            Place(upgradeHead.rectTransform, y, UpgradeHeadH, bodyW);
            upgradeHead.text = Loc.Get("HIGHCOMMAND_UPGRADE_HEADER");
            y += UpgradeHeadH + 4f;
            float buttonW = (bodyW - (ArmyUpgrades.Count - 1) * Gap) / ArmyUpgrades.Count;
            for (int i = 0; i < upgrades.Length; i++) upgrades[i].SetPosition(i * (buttonW + Gap), y);
            y += UpgradeH + Theme.GroupGap;
            Place(detail.rectTransform, y, DetailH, bodyW);
            y += DetailH;

            window.SetSize(new Vector2(window.Panel.sizeDelta.x, Window.HeightFor(y)));
            window.Title.text = Loc.Get("HIGHCOMMAND_BARRACKS_NAME");
            window.FitToScreen();
        }

        private static void Place(RectTransform rt, float y, float h, float w)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(0f, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private Card NewCard(SurvivorDef def)
        {
            var c = new Card { def = def };
            Image face = UiKit.Sliced("Card_" + def.cachedName, grid, HudArt.CleanButton, Theme.CardFace);
            c.root = face.rectTransform;
            c.root.anchorMin = c.root.anchorMax = new Vector2(0f, 1f);
            c.root.pivot = new Vector2(0f, 1f);
            c.root.sizeDelta = new Vector2(CardSize, CardSize);

            RawImage bubbles = UiKit.NewRawImage("Backdrop", c.root, HudArt.SurvivorBackground, Color.white);
            UiKit.Stretch(bubbles.rectTransform, 2f, 2f, 2f, 2f);
            if (!bubbles.texture) bubbles.enabled = false;
            Image frame = UiKit.Sliced("Frame", c.root, HudArt.CleanButton, FrameColor);
            UiKit.Stretch(frame.rectTransform);
            frame.fillCenter = false;

            c.portrait = UiKit.NewRawImage("Portrait", c.root, null, Color.white);
            UiKit.Stretch(c.portrait.rectTransform, 8f, 8f, 8f, 8f);
            CharacterBody body = def.bodyPrefab ? def.bodyPrefab.GetComponent<CharacterBody>() : null;
            if (body) c.portrait.texture = body.portraitIcon;

            Image strip = UiKit.NewImage("NameStrip", c.root, UiKit.White, Theme.CardName);
            UiKit.Place(strip.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 6f), new Vector2(CardSize, NameH));
            strip.rectTransform.anchorMax = new Vector2(1f, 0f);
            strip.rectTransform.sizeDelta = new Vector2(0f, NameH);
            c.name = UiKit.NewText("Name", strip.rectTransform, HeroName(def), 19f, Theme.Text, TextAlignmentOptions.TopLeft);
            UiKit.Stretch(c.name.rectTransform, 6f, 18f, 4f, 2f);
            c.name.enableAutoSizing = true;
            c.name.fontSizeMin = 12f;
            c.name.fontSizeMax = 19f;
            c.line = UiKit.NewText("Line", strip.rectTransform, "", 14f, Theme.TextSub, TextAlignmentOptions.BottomLeft);
            UiKit.Stretch(c.line.rectTransform, 6f, 3f, 4f, 22f);
            c.line.enableAutoSizing = true;
            c.line.fontSizeMin = 10f;
            c.line.fontSizeMax = 14f;

            c.hover = HoverOutline.ForIcon(c.root);
            return c;
        }

        private static string HeroName(SurvivorDef def)
        {
            string token = def.displayNameToken;
            if (string.IsNullOrEmpty(token))
            {
                CharacterBody b = def.bodyPrefab ? def.bodyPrefab.GetComponent<CharacterBody>() : null;
                token = b ? b.baseNameToken : "";
            }
            return string.IsNullOrEmpty(token) ? def.cachedName : Hero.Plain(Language.GetString(token));
        }

        // ---- per frame --------------------------------------------------------------------------------------

        internal void Tick()
        {
            window.Tick();
            if (!window.Visible) return;

            HcState state = HcState.Instance;
            IReadOnlyList<Hero> heroes = state != null ? state.Heroes : NoHeroes;
            Vector2 mouse = ClientInput.MouseBlocked ? new Vector2(-1f, -1f) : ClientInput.Mouse;
            bool click = ClientInput.MouseDown(0);
            int price = state != null ? state.NextHeroPrice() : 0;
            uint gold = state != null ? state.Gold : 0u;

            int note = price * 31 + (int)Mathf.Min(gold, int.MaxValue / 64);
            if (note != noteKey)
            {
                noteKey = note;
                window.Note.text = Loc.Format("HIGHCOMMAND_BARRACKS_NOTE", price, gold, gold >= (uint)price ? Theme.HexMoney : Theme.HexDeath);
            }

            bool recheck = Time.unscaledTime - statusAt >= StatusEvery;
            if (recheck) statusAt = Time.unscaledTime;
            object hovered = null;

            foreach (Card c in cards)
            {
                if (!c.root) continue;
                if (recheck)
                {
                    RecruitStatus status = ArmyService.Check(c.def, heroes, state);
                    if (status != c.status || price != c.price) Show(c, status, price);
                }
                bool over = UiKit.Contains(c.root, mouse);
                c.hover.Tick(over);
                if (over && !c.wasHover) UiSound.Hover();
                c.wasHover = over;
                if (!over) continue;
                hovered = c.def;
                if (click) Recruit(c, state);
            }

            for (int i = 0; i < upgrades.Length; i++)
            {
                if (TickUpgrade(i, mouse, click, gold)) hovered = upgrades[i];
            }

            ShowDetail(hovered);
        }

        private static void Show(Card c, RecruitStatus status, int price)
        {
            c.status = status;
            c.price = price;
            string role = Loc.Get(HeroRoles.RoleToken(c.def.cachedName));
            switch (status)
            {
                case RecruitStatus.Available:
                    c.line.text = role + "  <color=" + Theme.HexMoney + ">$" + price + "</color>";
                    break;
                case RecruitStatus.NeedGold:
                    c.line.text = role + "  <color=" + Theme.HexDeath + ">$" + price + "</color>";
                    break;
                case RecruitStatus.AlreadyOwned:
                    c.line.text = "<color=" + Theme.HexHealing + ">" + Loc.Get("HIGHCOMMAND_BARRACKS_OWNED") + "</color>";
                    break;
                case RecruitStatus.ArmyFull:
                    c.line.text = role + "  <style=cStack>" + Loc.Get("HIGHCOMMAND_BARRACKS_FULL") + "</style>";
                    break;
                default:
                    c.line.text = "<style=cStack>" + ArmyService.StatusText(status, null) + "</style>";
                    break;
            }
            bool closed = status == RecruitStatus.Locked || status == RecruitStatus.NotAllowed;
            c.portrait.color = closed ? PortraitClosed : status == RecruitStatus.AlreadyOwned ? PortraitOwned : Color.white;
            c.name.color = closed ? Theme.TextDisabled : Theme.Text;
        }

        private static void Recruit(Card c, HcState state)
        {
            RecruitStatus status = ArmyService.Check(c.def, state != null ? state.Heroes : NoHeroes, state);
            if (status != RecruitStatus.Available)
            {
                Toast.Error(ArmyService.StatusText(status, state));
                return;
            }
            // host: recruited right here; a client asks the server, which answers with a toast
            string error = ArmyService.TryRecruit(c.def, ArmyService.LoadoutFor(c.def));
            if (error == Net.CommandChannel.Relayed) return;
            if (error != null) Toast.Error(error);
            else
            {
                Toast.Show(Loc.Format("HIGHCOMMAND_RECRUITED", HeroName(c.def)));
                UiSound.Click();
            }
        }

        /// <summary>Returns true while the pointer is on the button.</summary>
        private bool TickUpgrade(int i, Vector2 mouse, bool click, uint gold)
        {
            MenuButton b = upgrades[i];
            int price = ArmyUpgrades.Price(i);
            int level = ArmyUpgrades.Levels[i];
            int max = ArmyUpgrades.MaxLevel[i];
            bool affordable = price > 0 && gold >= (uint)price;
            int key = (price * 16 + level) * 2 + (affordable ? 1 : 0);
            if (key != upgradeKeys[i])
            {
                upgradeKeys[i] = key;
                string name = Loc.Get(ArmyUpgrades.NameToken(i));
                string sub = price < 0
                    ? Loc.Format("HIGHCOMMAND_UPGRADE_LEVEL", max, max) + "  " + Loc.Get("HIGHCOMMAND_UPGRADE_MAXSHORT")
                    : Loc.Format("HIGHCOMMAND_UPGRADE_LEVEL", level, max) + "  <color=" + (affordable ? Theme.HexMoney : Theme.HexDeath) + ">$" + price + "</color>";
                b.Label.text = name + "\n<size=72%><style=cSub>" + sub + "</style></size>";
            }
            bool over = b.Refresh(mouse, price > 0);
            if (!b.Clicked(click)) return over;

            string error = ArmyUpgrades.Buy(i);
            if (error != null) Toast.Error(error);
            else Toast.Show(Loc.Format("HIGHCOMMAND_UPGRADE_DONE", Loc.Get(ArmyUpgrades.NameToken(i)), ArmyUpgrades.Levels[i]));
            return over;
        }

        private void ShowDetail(object hovered)
        {
            if (ReferenceEquals(hovered, detailFor) && detail.text.Length > 0) return;
            detailFor = hovered;
            if (hovered is SurvivorDef def)
            {
                string desc = def.descriptionToken != null ? Language.GetString(def.descriptionToken).TrimStart() : "";
                int cut = desc.IndexOfAny(LineBreaks);
                if (cut > 0) desc = desc.Substring(0, cut);   // the first paragraph: who it is, not the whole guide
                detail.text = "<color=#FFFFFF>" + HeroName(def) + "</color>  " + desc;
                return;
            }
            for (int i = 0; i < upgrades.Length; i++)
            {
                if (!ReferenceEquals(hovered, upgrades[i])) continue;
                detail.text = "<color=#FFFFFF>" + Loc.Get(ArmyUpgrades.NameToken(i)) + "</color>  " + Loc.Get(ArmyUpgrades.DescToken(i));
                return;
            }
            detail.text = "<style=cSub>" + Loc.Get("HIGHCOMMAND_BARRACKS_HINT") + "</style>";
        }
    }
}
