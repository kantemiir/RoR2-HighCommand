using System.Collections.Generic;
using HighCommand.Mode;
using RoR2;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HighCommand.UI
{
    /// <summary>
    /// Right side, under the objectives: what the army just picked up, as the game announces a pickup (NotificationPanel2):
    /// the icon, the name in its rarity colour, on a strip that fades out at its ends. New lines slide in from the right,
    /// old ones fade. Fills from the server's item-feed message, so it works the same on a host and on a client.
    /// </summary>
    internal sealed class LootFeed
    {
        private const int MaxRows = 6;
        private const float RowH = 40f, RowGap = 2f, Width = 330f;
        private const float IconSize = 34f;
        private const float Life = 6.5f, FadeOut = 0.8f, SlideIn = 0.18f, Slide = 40f;
        private static readonly Color Strip = new Color(0.094f, 0.094f, 0.094f, 0.55f);

        private sealed class Row
        {
            internal RectTransform root;
            internal CanvasGroup group;
            internal RawImage icon;
            internal TextMeshProUGUI label;
            internal PickupIndex pickup;
            internal int count;
            internal float born;
            internal float alpha = -1f;   // the last alpha written
            internal int slot = -1;       // the last list position written
            internal float x = float.NaN;
        }

        private readonly RectTransform container;
        private readonly List<Row> rows = new List<Row>();
        private readonly Stack<Row> pool = new Stack<Row>();
        private float shownTop = -1f;

        internal LootFeed(Transform parent)
        {
            container = UiKit.NewRect("LootFeed", parent);
            UiKit.Place(container, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-Theme.Margin, -200f),
                new Vector2(Width, MaxRows * (RowH + RowGap)));
            HcState.ItemGained += OnItemGained;
        }

        internal void Destroy()
        {
            HcState.ItemGained -= OnItemGained;
            if (container) Object.Destroy(container.gameObject);
        }

        /// <summary>Keeps the list under the objective panel (whose height changes with the objectives).</summary>
        internal void SetTop(float below)
        {
            float top = Mathf.Round(below + Theme.GroupGap);
            if (Mathf.Abs(top - shownTop) < 1f) return;
            shownTop = top;
            container.anchoredPosition = new Vector2(-Theme.Margin, -top);
        }

        private void OnItemGained(PickupIndex pickup, int count)
        {
            float now = Time.unscaledTime;
            // more of an item that is still on the list: its line counts up and stays longer
            foreach (Row r in rows)
            {
                if (r.pickup != pickup || now - r.born >= Life * 0.6f) continue;
                r.count += count;
                r.born = now - SlideIn;
                Fill(r);
                return;
            }

            Row row = pool.Count > 0 ? pool.Pop() : NewRow();
            row.root.gameObject.SetActive(true);
            row.pickup = pickup;
            row.count = count;
            row.born = now;
            row.alpha = -1f;
            row.slot = -1;
            row.x = float.NaN;
            Fill(row);
            rows.Insert(0, row);
            while (rows.Count > MaxRows)
            {
                Row old = rows[rows.Count - 1];
                rows.RemoveAt(rows.Count - 1);
                old.root.gameObject.SetActive(false);
                pool.Push(old);
            }
            UiSound.Pickup();
        }

        private Row NewRow()
        {
            var row = new Row();
            Image back = UiKit.Sliced("Row", container, HudArt.BackdropFaded, Strip);
            row.root = back.rectTransform;
            row.root.anchorMin = row.root.anchorMax = new Vector2(1f, 1f);
            row.root.pivot = new Vector2(1f, 1f);
            row.root.sizeDelta = new Vector2(Width, RowH);
            row.group = row.root.gameObject.AddComponent<CanvasGroup>();
            row.group.blocksRaycasts = false;

            row.icon = UiKit.NewRawImage("Icon", row.root, null, Color.white);
            UiKit.Place(row.icon.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(IconSize, IconSize));

            row.label = UiKit.NewText("Label", row.root, "", Theme.SizeBody, Theme.Text, TextAlignmentOptions.MidlineRight);
            UiKit.Place(row.label.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-28f - IconSize - 8f, 0f),
                new Vector2(Width - IconSize - 70f, RowH));
            row.label.enableAutoSizing = true;
            row.label.fontSizeMin = 12f;
            row.label.fontSizeMax = Theme.SizeBody;
            return row;
        }

        private static void Fill(Row r)
        {
            PickupDef def = PickupCatalog.GetPickupDef(r.pickup);
            if (def == null) { r.label.text = "?"; return; }
            bool hasIcon = PickupIcons.Apply(r.icon, def);
            r.icon.color = hasIcon ? Color.white : def.baseColor;
            string hex = ColorUtility.ToHtmlStringRGB(def.baseColor);
            string name = Language.GetString(def.nameToken);
            r.label.text = r.count > 1
                ? $"<color=#{hex}>{name}</color> <style=cStack>x{r.count}</style>"
                : $"<color=#{hex}>{name}</color>";
        }

        internal void Tick()
        {
            float now = Time.unscaledTime;
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                Row r = rows[i];
                float age = now - r.born;
                if (age > Life)
                {
                    rows.RemoveAt(i);
                    r.root.gameObject.SetActive(false);
                    pool.Push(r);
                    continue;
                }
                // in: a quick slide from the right with the alpha; out: a fade. Written only while it changes.
                float inT = Mathf.Clamp01(age / SlideIn);
                float alpha = Mathf.Min(inT, Mathf.Clamp01((Life - age) / FadeOut));
                float x = Mathf.Round(Slide * (1f - inT) * (1f - inT));
                if (Mathf.Abs(alpha - r.alpha) > 0.01f || (alpha == 1f && r.alpha != 1f))
                {
                    r.alpha = alpha;
                    r.group.alpha = alpha;
                }
                if (r.slot != i || x != r.x)
                {
                    r.slot = i;
                    r.x = x;
                    r.root.anchoredPosition = new Vector2(x, -i * (RowH + RowGap));
                }
            }
        }
    }
}
