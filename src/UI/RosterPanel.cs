using System.Collections.Generic;
using HighCommand.Army;
using HighCommand.Client;
using HighCommand.Mode;
using HighCommand.Orders;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

namespace HighCommand.UI
{
    /// <summary>
    /// The army down the left side, one card per hero, where the stock HUD lists its allies: the game's own ally card
    /// (portrait, name, the stock health bar with its hurt trail, shield, low-health flash and the red cross of a fallen
    /// ally) plus what a commander needs: level and rank, the current order, the control group, the selection. A click
    /// selects (Shift adds), a double click flies the camera there; a fallen hero's card shows the revival price and a
    /// click buys it.
    /// </summary>
    internal sealed class RosterPanel
    {
        private const string AllyCardKey = "RoR2/Base/UI/AllyCard.prefab";
        private const float Width = 244f;
        private const float MaxCardH = 48f, MinCardH = 30f, CardGap = 4f;
        private const float BottomRoom = 300f;   // the minimap under the list
        private const float DoubleClick = 0.4f;

        private static readonly Color BackNormal = Theme.AllyCard;
        private static readonly Color BackSelected = new Color(Theme.Button.r, Theme.Button.g, Theme.Button.b, 0.72f);
        private static readonly Color PortraitDead = new Color(0.4f, 0.4f, 0.4f, 1f);

        private sealed class Card
        {
            internal RectTransform root;
            internal Image back;
            internal RawImage portrait;
            internal RectTransform bars;
            internal TextMeshProUGUI name;
            internal HealthBar health;
            internal TextMeshProUGUI level;
            internal Image order;
            internal TextMeshProUGUI group;
            internal TextMeshProUGUI revive;
            internal Image selectFrame;
            internal HoverOutline hover;

            // what is shown now: a quiet frame touches nothing (a written text or rect rebuilds the canvas)
            internal Hero hero;
            internal int index = -1;
            internal float height = -1f;
            internal HealthComponent source;
            internal int alive = -1;
            internal int levelKey = -1;
            internal int orderKey = -1;
            internal int groupKey = -2;
            internal int selected = -1;
            internal int reviveKey = -1;
        }

        private static readonly IReadOnlyList<Hero> NoHeroes = new List<Hero>();

        private readonly RectTransform root;
        private readonly List<Card> cards = new List<Card>();
        private readonly List<Hero> sortedFallback = new List<Hero>();
        private readonly GameObject cardPrefab;
        private float lastClickTime;
        private Hero lastClickHero;
        private Hero tipHero;
        private int tipKey = int.MinValue;
        private string tipBody;

        internal RosterPanel(Transform parent)
        {
            root = UiKit.NewRect("Roster", parent);
            UiKit.Place(root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Theme.Margin, -(PurseCluster.Bottom + Theme.GroupGap)),
                new Vector2(Width, 600f));
            try { cardPrefab = Addressables.LoadAssetAsync<GameObject>(AllyCardKey).WaitForCompletion(); }
            catch (System.Exception e) { Plugin.Log.LogWarning("RosterPanel: no stock ally card (" + e.Message + "); plain cards are used."); }
            ClientInput.RegisterUiHitTest(Contains);
            root.gameObject.SetActive(false);
        }

        internal bool Contains(Vector2 screenPoint)
        {
            if (!root.gameObject.activeSelf) return false;
            for (int i = 0; i < cards.Count; i++)
                if (cards[i].root.gameObject.activeSelf && UiKit.Contains(cards[i].root, screenPoint)) return true;
            return false;
        }

        internal void Destroy()
        {
            ClientInput.UnregisterUiHitTest(Contains);
            if (root) Object.Destroy(root.gameObject);
        }

        // ---- building a card ------------------------------------------------------------------------------------

        private Card NewCard()
        {
            var c = new Card();
            GameObject go = null;
            if (cardPrefab)
            {
                try
                {
                    go = Object.Instantiate(cardPrefab, root, false);
                    var stock = go.GetComponent<AllyCardController>();
                    if (stock)
                    {
                        c.health = stock.healthBar;
                        UseAllyBarStyle(c.health);
                        c.name = stock.nameLabel;
                        c.portrait = stock.portraitIconImage;
                        // the card follows a master by itself, rebuilding names every frame: the roster drives it instead
                        Object.DestroyImmediate(stock);
                    }
                    var rowLayout = go.GetComponent<HorizontalLayoutGroup>();
                    if (rowLayout) rowLayout.enabled = false;
                    if (c.portrait)
                    {
                        var fitter = c.portrait.GetComponent<AspectRatioFitter>();
                        if (fitter) fitter.enabled = false;
                    }
                    c.bars = c.name ? c.name.transform.parent as RectTransform : null;
                    // the name and the bar are placed by the roster: the level and the order sit beside the name
                    var column = c.bars ? c.bars.GetComponent<VerticalLayoutGroup>() : null;
                    if (column) column.enabled = false;
                    c.back = go.GetComponent<Image>();
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogWarning("RosterPanel: the stock ally card could not be used: " + e.Message);
                    if (go) Object.Destroy(go);
                    go = null;
                }
            }
            if (!go || !c.name || !c.portrait || !c.bars) BuildPlain(c, go);
            else c.root = (RectTransform)go.transform;

            c.root.name = "Card";
            c.root.anchorMin = c.root.anchorMax = new Vector2(0f, 1f);
            c.root.pivot = new Vector2(0f, 1f);
            c.root.localRotation = Quaternion.identity;
            c.root.localScale = Vector3.one;
            c.name.enableAutoSizing = true;
            c.name.fontSizeMin = 10f;
            c.name.fontSizeMax = 18f;

            c.level = UiKit.NewText("Level", c.bars, "", Theme.SizeSmall, Theme.TextHud, TextAlignmentOptions.TopRight);
            IgnoreLayout(c.level.gameObject);
            UiKit.Place(c.level.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-4f, -3f), new Vector2(80f, 16f));
            c.order = UiKit.NewImage("Order", c.bars, null, Theme.TextHud);
            c.order.preserveAspect = true;
            IgnoreLayout(c.order.gameObject);
            UiKit.Place(c.order.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-4f, -18f), new Vector2(16f, 16f));
            c.revive = UiKit.NewText("Revive", c.bars, "", Theme.SizeSmall, Theme.TextSub, TextAlignmentOptions.BottomLeft);
            IgnoreLayout(c.revive.gameObject);

            c.group = UiKit.NewText("Group", c.portrait.rectTransform, "", Theme.SizeSmall, Theme.Text, TextAlignmentOptions.TopLeft);
            UiKit.Stretch(c.group.rectTransform, 3f, 0f, 0f, 1f);

            c.selectFrame = UiKit.Sliced("Selected", c.root, HudArt.HighlightThin, Theme.Highlight);
            UiKit.Stretch(c.selectFrame.rectTransform, -1f, -1f, -1f, -1f);
            c.selectFrame.enabled = false;
            c.hover = HoverOutline.ForButton(c.root);
            return c;
        }

        private static readonly System.Reflection.FieldInfo HealthBarStyleField =
            typeof(HealthBar).GetField("_style", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        // The card's bar is a few pixels tall here; the stock card's own style (the big HUD bar's) draws a barrier as a
        // tall box round it. Set before the bar's Start, which builds its pieces from the style.
        private static void UseAllyBarStyle(HealthBar bar)
        {
            HealthBarStyle style = HudArt.AllyBarStyle;
            if (!bar || !style || HealthBarStyleField == null) return;
            try { HealthBarStyleField.SetValue(bar, style); }
            catch (System.Exception e) { Plugin.Log.LogWarning("RosterPanel: the ally bar style was not applied: " + e.Message); }
        }

        private static void IgnoreLayout(GameObject go)
        {
            var e = go.AddComponent<LayoutElement>();
            e.ignoreLayout = true;
        }

        // without the stock prefab: the same parts, plain (no health bar of the game's)
        private void BuildPlain(Card c, GameObject failed)
        {
            if (failed) Object.Destroy(failed);
            c.back = UiKit.Sliced("Card", root, HudArt.CleanButton, BackNormal);
            c.root = c.back.rectTransform;
            c.portrait = UiKit.NewRawImage("Portrait", c.root, null, Color.white);
            c.bars = UiKit.NewRect("Bars", c.root);
            c.name = UiKit.NewText("NameLabel", c.bars, "", 18f, Theme.Text, TextAlignmentOptions.BottomLeft);
            UiKit.Stretch(c.name.rectTransform, 6f, 10f, 6f, 4f);
            c.health = null;
        }

        private static void Layout(Card c, int index, float height)
        {
            c.index = index;
            c.height = height;
            c.root.sizeDelta = new Vector2(Width, height);
            c.root.anchoredPosition = new Vector2(0f, -index * (height + CardGap));
            float p = height - 2f;
            RectTransform pr = c.portrait.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0f, 0.5f);
            pr.pivot = new Vector2(0f, 0.5f);
            pr.anchoredPosition = new Vector2(1f, 0f);
            pr.sizeDelta = new Vector2(p, p);
            c.bars.anchorMin = Vector2.zero;
            c.bars.anchorMax = Vector2.one;
            c.bars.pivot = new Vector2(0.5f, 0.5f);
            c.bars.offsetMin = new Vector2(p + 2f, 0f);
            c.bars.offsetMax = Vector2.zero;

            // the name above, the stock bar along the bottom (as the stock card: 6 high, 6 from the edges)
            PlaceName(c, c.alive != 0);
            if (c.health) Fill((RectTransform)c.health.transform, BarPad, BarPad, BarPad, height - BarPad - BarH);
            Fill(c.revive.rectTransform, BarPad, RevivePad, BarPad, height - RevivePad - ReviveH);
            bool roomy = height >= 40f;
            c.order.rectTransform.anchoredPosition = new Vector2(roomy ? -4f : -60f, roomy ? -18f : -3f);
        }

        // A fallen hero's price line is taller than the bar it replaces: the name moves up to stay clear of it.
        private static void PlaceName(Card c, bool alive) =>
            Fill(c.name.rectTransform, BarPad, alive ? BarPad + BarH + 1f : RevivePad + ReviveH, NameRoom, 2f);

        private const float BarPad = 6f, BarH = 6f;
        private const float RevivePad = 2f, ReviveH = Theme.SizeSmall + 3f;
        private const float NameRoom = 52f;   // right of the name: the level and the order

        private static void Fill(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        // ---- per frame ----------------------------------------------------------------------------------------

        // The army keeps its heroes in join order (the list a client rebuilds follows the same order); a copy is
        // sorted only in the unexpected case that the order is broken.
        private IReadOnlyList<Hero> InJoinOrder(IReadOnlyList<Hero> heroes)
        {
            for (int i = 1; i < heroes.Count; i++)
            {
                if (heroes[i].JoinOrder >= heroes[i - 1].JoinOrder) continue;
                sortedFallback.Clear();
                for (int k = 0; k < heroes.Count; k++) sortedFallback.Add(heroes[k]);
                sortedFallback.Sort((a, b) => a.JoinOrder.CompareTo(b.JoinOrder));
                return sortedFallback;
            }
            return heroes;
        }

        internal void Tick(Selection selection, OrderController orders, bool visible)
        {
            HcState state = HcState.Instance;
            IReadOnlyList<Hero> heroes = state != null ? InJoinOrder(state.Heroes) : NoHeroes;
            bool show = visible && heroes.Count > 0;
            if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
            if (!show) return;

            // the cards shrink so that the whole army fits above the minimap
            var canvasRect = root.parent as RectTransform;
            float room = (canvasRect ? canvasRect.rect.height : 1080f) - PurseCluster.Bottom - Theme.GroupGap - BottomRoom;
            float height = Mathf.Floor(Mathf.Clamp(room / heroes.Count - CardGap, MinCardH, MaxCardH));

            Vector2 mouse = ClientInput.Mouse;
            bool click = ClientInput.MouseDown(0);
            int revivePrice = state.RevivePrice();
            bool affordable = state.Gold >= (uint)revivePrice;
            Hero hovered = null;

            for (int i = 0; i < heroes.Count; i++)
            {
                Hero hero = heroes[i];
                while (cards.Count <= i) cards.Add(NewCard());
                Card c = cards[i];
                if (!c.root.gameObject.activeSelf) c.root.gameObject.SetActive(true);
                if (c.index != i || c.height != height) Layout(c, i, height);
                if (c.hero != hero) Assign(c, hero);
                Refresh(c, hero, selection, revivePrice, affordable, height >= 40f);

                bool over = UiKit.Contains(c.root, mouse) && !ClientInput.MouseBlocked;
                c.hover.Tick(over);
                if (!over) continue;
                hovered = hero;
                if (click) OnClick(hero, hero.IsAlive, selection, orders);
            }
            for (int i = heroes.Count; i < cards.Count; i++)
            {
                if (!cards[i].root.gameObject.activeSelf) continue;
                cards[i].root.gameObject.SetActive(false);
                cards[i].hero = null;
                if (cards[i].health) cards[i].health.source = null;
            }
            if (hovered != null) ShowTooltip(hovered, revivePrice);
        }

        private static void Assign(Card c, Hero hero)
        {
            c.hero = hero;
            c.name.text = hero.DisplayName;
            c.portrait.texture = PortraitOf(hero);
            c.source = null;
            c.alive = c.levelKey = c.orderKey = c.selected = c.reviveKey = -1;
            c.groupKey = -2;
            if (c.health) c.health.source = null;
        }

        private static Texture PortraitOf(Hero hero)
        {
            CharacterBody body = hero.Body;
            if (body && body.portraitIcon) return body.portraitIcon;
            CharacterBody prefab = hero.Survivor && hero.Survivor.bodyPrefab ? hero.Survivor.bodyPrefab.GetComponent<CharacterBody>() : null;
            return prefab ? prefab.portraitIcon : null;
        }

        private void Refresh(Card c, Hero hero, Selection selection, int revivePrice, bool affordable, bool roomy)
        {
            CharacterBody body = hero.Body;
            bool alive = hero.IsAlive;
            HealthComponent hc = alive && body ? body.healthComponent : null;
            if (c.health && hc != c.source)
            {
                c.source = hc;
                c.health.source = hc;
            }
            if (c.alive != (alive ? 1 : 0))
            {
                c.alive = alive ? 1 : 0;
                c.portrait.color = alive ? Color.white : PortraitDead;
                if (!c.portrait.texture) c.portrait.texture = PortraitOf(hero);
                c.level.gameObject.SetActive(alive);
                c.revive.gameObject.SetActive(!alive);
                PlaceName(c, alive);
                // a fallen hero's bar gives way to the revival price; the bar keeps running (it draws the red cross)
                if (c.health)
                {
                    var barCanvas = c.health.GetComponent<Canvas>();
                    if (barCanvas) barCanvas.enabled = alive;
                }
                c.levelKey = c.orderKey = c.reviveKey = -1;
            }

            if (alive)
            {
                int level = body ? (int)body.level : 0;
                int levelKey = level * 8 + hero.Rank;
                if (levelKey != c.levelKey)
                {
                    c.levelKey = levelKey;
                    string text = Loc.Format("HIGHCOMMAND_LEVEL_SHORT", level);
                    if (hero.Rank > 0) text += " <color=" + Theme.HexDamage + ">" + new string('★', hero.Rank) + "</color>";
                    c.level.text = text;
                }
                int orderKey = hero.NetOrderKind * 2 + (roomy ? 1 : 0);
                if (orderKey != c.orderKey)
                {
                    c.orderKey = orderKey;
                    bool has = OrderLooks.Glyph((OrderKind)hero.NetOrderKind, out Glyph glyph);
                    c.order.enabled = has;
                    if (has) c.order.sprite = HudArt.Glyph(glyph);
                }
            }
            else
            {
                int reviveKey = revivePrice * 2 + (affordable ? 1 : 0);
                if (reviveKey != c.reviveKey)
                {
                    c.reviveKey = reviveKey;
                    c.order.enabled = false;
                    c.revive.text = Loc.Format("HIGHCOMMAND_ROSTER_REVIVE", affordable ? Theme.HexMoney : Theme.HexDeath, revivePrice);
                }
            }

            int group = GroupOf(hero, selection);
            if (group != c.groupKey)
            {
                c.groupKey = group;
                c.group.text = group >= 0 ? ((group + 1) % 10).ToString() : "";
            }

            int selected = selection.Contains(hero) ? 1 : 0;
            if (selected != c.selected)
            {
                c.selected = selected;
                c.selectFrame.enabled = selected == 1;
                if (c.back) c.back.color = selected == 1 ? BackSelected : BackNormal;
            }
        }

        /// <summary>The first control group the hero is in (-1: none).</summary>
        private static int GroupOf(Hero hero, Selection selection)
        {
            for (int g = 0; g < Selection.GroupCount; g++)
            {
                IReadOnlyList<Hero> members = selection.Group(g);
                for (int i = 0; i < members.Count; i++) if (members[i] == hero) return g;
            }
            return -1;
        }

        private void OnClick(Hero hero, bool alive, Selection selection, OrderController orders)
        {
            if (!alive)
            {
                orders.Revive(hero);
                return;
            }

            float now = Time.unscaledTime;
            bool doubleClick = lastClickHero == hero && now - lastClickTime < DoubleClick;
            lastClickHero = hero;
            lastClickTime = now;
            UiSound.Click();

            if (ClientInput.Shift) selection.Toggle(hero);
            else selection.SetSingle(hero);

            if (doubleClick && ClientRoot.Instance && ClientRoot.Instance.Camera)
                ClientRoot.Instance.Camera.FocusOn(hero.Position);
        }

        private void ShowTooltip(Hero hero, int revivePrice)
        {
            bool alive = hero.IsAlive;
            int key = alive
                ? (((hero.NetStance * 32 + hero.NetOrderKind) * 16 + hero.NetQueued) * 8 + hero.Rank) * 4096 + hero.Kills
                : -1 - revivePrice;
            if (hero != tipHero || key != tipKey || tipBody == null)
            {
                tipHero = hero;
                tipKey = key;
                string role = Loc.Get(HeroRoles.RoleToken(hero.Survivor ? hero.Survivor.cachedName : ""));
                if (!alive) tipBody = role + "\n\n" + Loc.Format("HIGHCOMMAND_ROSTER_FALLEN", revivePrice);
                else
                {
                    var sb = new System.Text.StringBuilder(role);
                    sb.Append("\n<style=cSub>").Append(Loc.Get("HIGHCOMMAND_ROSTER_STANCE")).Append("</style> ")
                        .Append(OrderLooks.StanceName((Stance)hero.NetStance));
                    string order = OrderLooks.Name((OrderKind)hero.NetOrderKind);
                    if (order.Length > 0)
                    {
                        sb.Append("\n<style=cSub>").Append(Loc.Get("HIGHCOMMAND_ROSTER_ORDER")).Append("</style> ").Append(order);
                        if (hero.NetQueued > 0) sb.Append(" <style=cStack>(+").Append(hero.NetQueued).Append(")</style>");
                    }
                    string rank = hero.Rank < ArmyUpgrades.RankKills.Length
                        ? Loc.Format("HIGHCOMMAND_RANK_INFO", hero.Rank, hero.Kills, ArmyUpgrades.RankKills[hero.Rank])
                        : Loc.Format("HIGHCOMMAND_RANK_INFO_MAX", hero.Rank, hero.Kills);
                    sb.Append("\n<style=cSub>").Append(rank).Append("</style>");
                    sb.Append("\n\n<style=cSub>").Append(Loc.Get("HIGHCOMMAND_ROSTER_HINT")).Append("</style>");
                    tipBody = sb.ToString();
                }
            }
            Tooltip.Show(hero.DisplayName, tipBody, Tooltip.SurvivorBar(hero.Body ? hero.Body : BodyPrefab(hero)));
        }

        private static CharacterBody BodyPrefab(Hero hero) =>
            hero.Survivor && hero.Survivor.bodyPrefab ? hero.Survivor.bodyPrefab.GetComponent<CharacterBody>() : null;
    }
}
