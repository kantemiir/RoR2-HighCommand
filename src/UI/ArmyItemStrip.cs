using System.Collections.Generic;
using HighCommand.Army;
using HighCommand.Client;
using RoR2;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HighCommand.UI
{
    /// <summary>
    /// Top centre, where the stock HUD shows the player's items: the army's items as the stock item display draws them
    /// (bare icons in rows from the middle, the stack as "x3" in the corner, smaller icons when the rows fill up). The
    /// stock display itself needs a HUD above it; this one follows the army's pool through its change event. A click on
    /// an item opens the game's item card.
    /// </summary>
    internal sealed class ArmyItemStrip
    {
        private const float SideRoom = 330f;   // the purse on the left, the run panel on the right
        private const float Top = 6f;
        private const float MaxIcon = 46f, MinIcon = 26f, IconGap = 2f;
        private const int MaxRows = 2;

        private sealed class Cell
        {
            internal RectTransform root;
            internal RawImage icon;
            internal TextMeshProUGUI stack;
            internal ItemIndex item = ItemIndex.None;
        }

        private readonly RectTransform root;
        private readonly List<Cell> cells = new List<Cell>();
        private readonly List<ItemIndex> shown = new List<ItemIndex>();
        private readonly HoverOutline hover;
        private readonly RectTransform hoverHost;
        private Inventory subscribed;
        private bool dirty = true;
        private float laidOutWidth = -1f;
        private int used;

        internal ArmyItemStrip(Transform parent)
        {
            root = UiKit.NewRect("ArmyItems", parent);
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.offsetMin = new Vector2(SideRoom, -Top - MaxRows * (MaxIcon + IconGap));
            root.offsetMax = new Vector2(-SideRoom, -Top);

            hoverHost = UiKit.NewRect("Hover", root);
            hover = HoverOutline.ForIcon(hoverHost, 1f);
        }

        internal void SetVisible(bool visible)
        {
            if (root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
        }

        /// <summary>Over one of the item icons.</summary>
        internal bool Contains(Vector2 screenPoint) => HoveredCell(screenPoint) != null;

        /// <summary>The item under the point (a click there opens its card), or none.</summary>
        internal ItemIndex ItemAt(Vector2 screenPoint)
        {
            Cell c = HoveredCell(screenPoint);
            return c != null ? c.item : ItemIndex.None;
        }

        internal void Destroy()
        {
            Subscribe(null);
            if (root) Object.Destroy(root.gameObject);
        }

        private void Subscribe(Inventory pool)
        {
            if (subscribed) subscribed.onInventoryChanged -= OnChanged;
            subscribed = pool;
            if (subscribed) subscribed.onInventoryChanged += OnChanged;
            dirty = true;
        }

        private void OnChanged() => dirty = true;

        internal void Tick()
        {
            if (!root.gameObject.activeInHierarchy) return;
            Inventory pool = LootService.Pool;
            if (pool != subscribed) Subscribe(pool);
            if (Mathf.Abs(root.rect.width - laidOutWidth) > 0.5f) dirty = true;   // a new resolution or interface scale
            if (dirty) Rebuild(pool);

            Cell over = ClientInput.MouseBlocked ? null : HoveredCell(ClientInput.Mouse);
            if (over != null)
            {
                if (hoverHost.parent != over.root)
                {
                    hoverHost.SetParent(over.root, false);
                    UiKit.Stretch(hoverHost);
                }
                ShowTooltip(over.item, pool);
            }
            hover.Tick(over != null);
        }

        private void Rebuild(Inventory pool)
        {
            dirty = false;
            laidOutWidth = root.rect.width;
            shown.Clear();
            if (pool)
            {
                var order = pool.itemAcquisitionOrder;
                for (int i = 0; i < order.Count; i++)
                {
                    ItemDef def = ItemCatalog.GetItemDef(order[i]);
                    if (def == null || def.hidden || pool.GetItemCountPermanent(order[i]) <= 0) continue;
                    shown.Add(order[i]);
                }
            }

            // the biggest icon at which every item fits in the rows (as the stock display shrinks its icons)
            float width = Mathf.Max(MaxIcon, laidOutWidth);
            float size = MaxIcon;
            while (size > MinIcon && Mathf.CeilToInt(shown.Count / Mathf.Floor(width / (size + IconGap))) > MaxRows) size -= 2f;
            int perRow = Mathf.Max(1, Mathf.FloorToInt(width / (size + IconGap)));
            int capacity = Mathf.Min(shown.Count, perRow * MaxRows);

            used = capacity;
            while (cells.Count < capacity) cells.Add(NewCell());
            for (int i = 0; i < cells.Count; i++)
            {
                Cell c = cells[i];
                bool on = i < capacity;
                if (c.root.gameObject.activeSelf != on) c.root.gameObject.SetActive(on);
                if (!on) { c.item = ItemIndex.None; continue; }

                int row = i / perRow, col = i % perRow;
                int inRow = Mathf.Min(perRow, capacity - row * perRow);
                float rowW = inRow * (size + IconGap) - IconGap;
                c.root.sizeDelta = new Vector2(size, size);
                c.root.anchoredPosition = new Vector2(-rowW * 0.5f + col * (size + IconGap), -row * (size + IconGap));

                ItemIndex item = shown[i];
                if (c.item != item)
                {
                    c.item = item;
                    PickupIcons.Apply(c.icon, PickupIcons.DefOf(item));
                }
                int count = pool.GetItemCountPermanent(item);
                c.stack.text = count > 1 ? "x" + count : "";
                c.stack.fontSize = Mathf.Round(size * 0.38f);
            }
        }

        private Cell NewCell()
        {
            var c = new Cell { root = UiKit.NewRect("Item", root) };
            c.root.anchorMin = c.root.anchorMax = new Vector2(0.5f, 1f);
            c.root.pivot = new Vector2(0f, 1f);
            c.icon = UiKit.NewRawImage("Icon", c.root, null, Color.white);
            UiKit.Stretch(c.icon.rectTransform);
            // the stock stack count: bold, top right
            c.stack = UiKit.NewText("Stack", c.root, "", 18f, Theme.Text, TextAlignmentOptions.TopRight);
            c.stack.fontStyle = FontStyles.Bold;
            UiKit.Stretch(c.stack.rectTransform, 0f, 0f, -2f, -2f);
            return c;
        }

        private Cell HoveredCell(Vector2 screenPoint)
        {
            if (!root.gameObject.activeInHierarchy || used == 0) return null;
            for (int i = 0; i < used; i++)
                if (UiKit.Contains(cells[i].root, screenPoint)) return cells[i];
            return null;
        }

        // the texts are built when the item or its count changes, not every frame the pointer rests on it
        private ItemIndex tipItem = ItemIndex.None;
        private int tipCount = -1;
        private string tipTitle, tipBody;
        private Color? tipBar;

        private void ShowTooltip(ItemIndex item, Inventory pool)
        {
            int count = pool ? pool.GetItemCountPermanent(item) : 0;
            if (item != tipItem || count != tipCount || tipTitle == null)
            {
                ItemDef def = ItemCatalog.GetItemDef(item);
                if (def == null) return;
                PickupDef pickup = PickupIcons.DefOf(item);
                tipItem = item;
                tipCount = count;
                tipTitle = Language.GetString(def.nameToken);
                tipBody = Language.GetString(def.pickupToken);
                if (count > 1) tipBody += "\n\n<style=cSub>" + Loc.Format("HIGHCOMMAND_ITEMS_ARMY_COUNT", count) + "</style>";
                tipBody += "\n" + Tooltip.KeyHint(Loc.Get("HIGHCOMMAND_KEY_CLICK")) + " <style=cSub>" + Loc.Get("HIGHCOMMAND_ITEM_CARD") + "</style>  "
                           + Tooltip.KeyHint(UiKit.KeyText(Settings.KeyInventory.Value)) + " <style=cSub>" + Loc.Get("HIGHCOMMAND_ITEMS_OPEN") + "</style>";
                tipBar = pickup != null ? pickup.darkColor : (Color?)null;
            }
            Tooltip.Show(tipTitle, tipBody, tipBar);
        }
    }
}
