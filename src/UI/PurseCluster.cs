using HighCommand.Army;
using HighCommand.Client;
using HighCommand.Mode;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HighCommand.UI
{
    /// <summary>
    /// Top left, where the stock HUD keeps the money: the army's purse on the game's money strip, and under it the army
    /// (living / all) with the price of the next recruit and the barracks key. A click opens the barracks.
    /// </summary>
    internal sealed class PurseCluster
    {
        private const float Width = 244f;
        private const float MoneyH = 38f;
        private const float ArmyH = 26f;
        private const float Height = Theme.Inset * 3f + MoneyH + ArmyH;

        private readonly RectTransform root;
        private readonly TextMeshProUGUI gold;
        private readonly TextMeshProUGUI army;
        private readonly Image armyIcon;
        private readonly TextMeshProUGUI hire;
        private readonly KeyLabel key;
        private readonly HoverOutline hover;

        // the counter catches up with the purse in steps, as the stock MoneyText does
        private int shownGold = -1, targetGold;
        private float stepTimer;

        private int armyKey = int.MinValue;
        private int tipKey = int.MinValue;
        private string tipBody;

        internal PurseCluster(Transform parent)
        {
            Image frame = UiKit.Sliced("Purse", parent, HudArt.ClusterFrame, Theme.ClusterFrame);
            root = frame.rectTransform;
            UiKit.Place(root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Theme.Margin, -Theme.Margin), new Vector2(Width, Height));

            // the money row: the stock ramp behind a big "$" and the sum on the right
            RawImage ramp = UiKit.NewRawImage("MoneyRamp", root, HudArt.MoneyRamp, Theme.MoneyStrip);
            if (!ramp.texture) ramp.texture = Texture2D.whiteTexture;
            UiKit.Place(ramp.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Theme.Inset, -Theme.Inset),
                new Vector2(Width - 2f * Theme.Inset, MoneyH));
            TextMeshProUGUI dollar = UiKit.NewText("Dollar", ramp.rectTransform, "$", 32f, Theme.Money, TextAlignmentOptions.MidlineLeft);
            UiKit.Stretch(dollar.rectTransform, 8f, 0f, 8f, 0f);
            gold = UiKit.NewText("Gold", ramp.rectTransform, "", Theme.SizeMoney, Theme.Money, TextAlignmentOptions.MidlineRight);
            UiKit.Stretch(gold.rectTransform, 36f, 0f, 10f, 0f);

            // the army row
            RectTransform row = UiKit.NewRect("ArmyRow", root);
            UiKit.Place(row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Theme.Inset, -(2f * Theme.Inset + MoneyH)),
                new Vector2(Width - 2f * Theme.Inset, ArmyH));
            armyIcon = UiKit.NewImage("Icon", row, HudArt.Glyph(Glyph.Person), Theme.TextHud);
            armyIcon.preserveAspect = true;
            UiKit.Place(armyIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(22f, 22f));
            army = UiKit.NewText("Army", row, "", 20f, Theme.TextHud, TextAlignmentOptions.MidlineLeft);
            UiKit.Place(army.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(32f, 0f), new Vector2(70f, ArmyH));

            key = new KeyLabel(row);
            key.Root.anchorMin = key.Root.anchorMax = new Vector2(1f, 0.5f);
            key.Root.pivot = new Vector2(1f, 0.5f);
            key.Root.anchoredPosition = new Vector2(-2f, 0f);
            hire = UiKit.NewText("Hire", row, "", Theme.SizeKey, Theme.TextSub, TextAlignmentOptions.MidlineRight);
            UiKit.Place(hire.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 0f), new Vector2(130f, ArmyH));
            hire.enableAutoSizing = true;
            hire.fontSizeMin = 11f;
            hire.fontSizeMax = Theme.SizeKey;

            hover = HoverOutline.ForButton(root);
        }

        internal bool Contains(Vector2 screenPoint) => root.gameObject.activeSelf && UiKit.Contains(root, screenPoint);

        internal void SetVisible(bool visible)
        {
            if (root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
        }

        /// <summary>The bottom of the cluster in canvas units below the top edge (the roster starts under it).</summary>
        internal static float Bottom => Theme.Margin + Height;

        internal void Tick(float dt)
        {
            HcState state = HcState.Instance;
            if (state == null || !root.gameObject.activeSelf) return;

            TickGold((int)Mathf.Min(state.Gold, int.MaxValue), dt);

            var heroes = state.Heroes;
            int alive = 0;
            for (int i = 0; i < heroes.Count; i++) if (heroes[i].IsAlive) alive++;
            int price = state.NextHeroPrice();
            bool full = heroes.Count >= state.ArmyLimit;
            bool affordable = state.Gold >= (uint)price;
            int k = ((alive * 64 + heroes.Count) * 64 + state.ArmyLimit) * 4 + (full ? 2 : 0) + (affordable ? 1 : 0);
            k = k * 31 + price;
            if (k != armyKey)
            {
                armyKey = k;
                army.text = alive < heroes.Count
                    ? $"<color={Theme.HexDeath}>{alive}</color>/{heroes.Count}"
                    : $"{alive}/{heroes.Count}";
                hire.text = full
                    ? Loc.Get("HIGHCOMMAND_PURSE_FULL")
                    : Loc.Format("HIGHCOMMAND_PURSE_HIRE", affordable ? Theme.HexMoney : Theme.HexDeath, price);
                key.Set(UiKit.KeyText(Settings.KeyBarracks.Value));
            }

            bool over = Contains(ClientInput.Mouse) && !ClientInput.MouseBlocked;
            hover.Tick(over);
            if (over) ShowTooltip(state, price, heroes.Count, state.ArmyLimit);
        }

        private void TickGold(int target, float dt)
        {
            if (shownGold < 0) { shownGold = target; gold.text = target.ToString(); }
            targetGold = target;
            stepTimer -= dt;
            if (stepTimer > 0f || shownGold == targetGold) return;

            int diff = targetGold - shownGold;
            int step = diff / 3;
            if (step == 0) step = diff > 0 ? 1 : -1;
            shownGold += step;
            stepTimer = Mathf.Min(0.5f / Mathf.Abs(step), 0.25f);
            gold.text = shownGold.ToString();
            if (step > 0) UiSound.Coin();
        }

        private void ShowTooltip(HcState state, int price, int heroes, int limit)
        {
            int income = Mathf.RoundToInt(Economy.IncomePerMinute());
            int k = (price * 31 + income) * 31 + heroes * 64 + limit;
            if (k != tipKey || tipBody == null)
            {
                tipKey = k;
                tipBody = Loc.Format("HIGHCOMMAND_PURSE_TIP", income, price, heroes, limit, state.RevivePrice())
                          + "\n\n" + Tooltip.KeyHint(UiKit.KeyText(Settings.KeyBarracks.Value)) + " " + Loc.Get("HIGHCOMMAND_HELP_BARRACKS");
            }
            Tooltip.Show(Loc.Get("HIGHCOMMAND_PURSE_TITLE"), tipBody);
        }

        internal void Destroy()
        {
            if (root) Object.Destroy(root.gameObject);
        }
    }
}
