using System;
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
    /// "Who carries what", laid out as the stock scoreboard (Tab): one slate strip per hero with the portrait in a dark
    /// box, the name, the items in a long dark box and the equipment in its own box. Drag an item (Shift: the whole
    /// stack) onto another hero's strip to hand it over; drag equipment to swap; a click opens the game's item card. Under
    /// the strips the spare equipment and a button that deals everything out again by role. Reads the heroes'
    /// inventories, which the game replicates.
    /// </summary>
    internal sealed class InventoryPanel
    {
        private const float BodyW = 1180f;
        private const float MaxBodyH = 820f;
        private const float StripPad = 6f;
        private const float MinStripH = 64f;
        private const float RowGap = 6f;
        private const float NameW = 210f;
        private const float EquipSize = 52f, EquipPitch = 56f;
        private const float FooterH = 64f;
        private const float CellGap = 4f;
        private static readonly float[] CellSizes = { 48f, 42f, 36f, 30f };

        private static readonly Color DropTarget = new Color(Theme.ButtonHover.r, Theme.ButtonHover.g, Theme.ButtonHover.b, 0.55f);
        private static readonly Color EmptySlot = new Color(1f, 1f, 1f, 0.12f);

        private sealed class Cell
        {
            internal RectTransform root;
            internal RawImage icon;
            internal Image outline;
            internal TextMeshProUGUI count;
            internal ItemIndex item = ItemIndex.None;
            internal EquipmentIndex equipment = EquipmentIndex.None;
            internal int spareIndex = -1;
            internal int slot;
            internal Hero owner;
        }

        private sealed class Row
        {
            internal Hero hero;
            internal RectTransform root;
            internal Image strip;
            internal Image itemsBox;
            internal Image equipBox;
            internal RawImage portrait;
            internal TextMeshProUGUI name;
            internal TextMeshProUGUI role;
            internal TextMeshProUGUI more;
            internal TextMeshProUGUI empty;
            internal readonly List<Cell> equipCells = new List<Cell>();
            internal readonly List<Cell> cells = new List<Cell>();
            internal readonly List<KeyValuePair<ItemIndex, int>> items = new List<KeyValuePair<ItemIndex, int>>();
            internal float height;
            internal int stripState = -1;
        }

        private readonly Window window;
        private readonly RectTransform rowsHost;
        private readonly RectTransform footer;
        private readonly TextMeshProUGUI spareLabel;
        private readonly MenuButton autoButton;
        private readonly RectTransform ghost;
        private readonly RawImage ghostIcon;
        private readonly TextMeshProUGUI ghostCount;
        private readonly HoverOutline cellHover;
        private Cell hoverCell;

        private static readonly IReadOnlyList<Hero> NoHeroes = new List<Hero>();
        private static readonly IReadOnlyList<EquipmentIndex> NoEquipment = new List<EquipmentIndex>();

        private readonly List<Row> rows = new List<Row>();
        private readonly List<Cell> spareCells = new List<Cell>();
        private int lastSignature = int.MinValue;

        // drag state
        private bool dragging;
        private ItemIndex dragItem = ItemIndex.None;
        private EquipmentIndex dragEquipment = EquipmentIndex.None;
        private int dragSlot;
        private int dragSpareIndex = -1;
        private Hero dragFrom;
        private int dragCount;
        private Vector2 dragStart;
        private bool dragMoved;

        // a press that ends within this many pixels of where it began is a click (it opens the item's card), not a drag
        private const float ClickSlop = 6f;

        internal bool Visible => window.Visible;

        internal InventoryPanel(Transform parent)
        {
            window = new Window(parent, "Inventory", new Vector2(BodyW + 2f * (Window.Pad + 3f), 600f));

            rowsHost = UiKit.NewRect("Rows", window.Body);
            rowsHost.anchorMin = rowsHost.anchorMax = new Vector2(0f, 1f);
            rowsHost.pivot = new Vector2(0f, 1f);

            footer = UiKit.NewRect("Footer", window.Body);
            footer.anchorMin = new Vector2(0f, 0f);
            footer.anchorMax = new Vector2(1f, 0f);
            footer.pivot = new Vector2(0.5f, 0f);
            footer.offsetMin = Vector2.zero;
            footer.offsetMax = new Vector2(0f, FooterH);

            spareLabel = UiKit.NewText("SpareLabel", footer, "", 16f, Theme.TextSub, TextAlignmentOptions.TopLeft);
            UiKit.Place(spareLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(600f, 20f));

            autoButton = new MenuButton(footer, "AutoButton", new Vector2(260f, 44f), 20f);
            autoButton.Root.anchorMin = autoButton.Root.anchorMax = new Vector2(1f, 0f);
            autoButton.Root.pivot = new Vector2(1f, 0f);
            autoButton.Root.anchoredPosition = new Vector2(0f, 4f);

            ghost = UiKit.NewRect("DragGhost", window.Panel);
            ghost.anchorMin = ghost.anchorMax = new Vector2(0.5f, 0.5f);
            ghost.pivot = new Vector2(0.5f, 0.5f);
            ghost.sizeDelta = new Vector2(56f, 56f);
            ghostIcon = UiKit.NewRawImage("Icon", ghost, null, new Color(1f, 1f, 1f, 0.9f));
            UiKit.Stretch(ghostIcon.rectTransform);
            ghostCount = UiKit.NewText("Count", ghost, "", 20f, Theme.Text, TextAlignmentOptions.TopRight);
            ghostCount.fontStyle = FontStyles.Bold;
            UiKit.Stretch(ghostCount.rectTransform);
            ghost.gameObject.SetActive(false);

            // one settling frame, moved onto whatever cell the pointer rests on
            RectTransform hoverHost = UiKit.NewRect("CellHover", window.Panel);
            hoverHost.anchorMin = hoverHost.anchorMax = new Vector2(0.5f, 0.5f);
            cellHover = HoverOutline.ForIcon(hoverHost, 2f);
            hoverHostRect = hoverHost;

            ClientInput.RegisterUiHitTest(Contains);
        }

        private readonly RectTransform hoverHostRect;

        internal bool Contains(Vector2 screenPoint) => window.Contains(screenPoint);

        internal void Toggle()
        {
            if (window.Visible) Hide();
            else Show();
        }

        internal void Show()
        {
            if (window.Visible) return;
            lastSignature = int.MinValue;
            window.Title.text = Loc.Get("HIGHCOMMAND_TOP_ITEMS");
            window.Show();
            UiSound.Click();
        }

        internal void Hide()
        {
            CancelDrag();
            window.Hide();
        }

        internal void Destroy()
        {
            ClientInput.UnregisterUiHitTest(Contains);
            window.Destroy();
        }

        // ---- data helpers ---------------------------------------------------------------------------------

        /// <summary>The host's sharing mode (replicated), not this machine's own config value.</summary>
        private static bool IsMirror(HcState state) => state != null ? state.ItemMirror : LobbyRules.ItemMirror;

        private static PickupDef PickupOf(ItemIndex item) => PickupIcons.DefOf(item);

        private static PickupDef PickupOf(EquipmentIndex equipment) => PickupIcons.DefOf(equipment);

        private static void CollectItems(Hero hero, List<KeyValuePair<ItemIndex, int>> into)
        {
            into.Clear();
            CharacterMaster master = hero.Master;
            Inventory inv = master ? master.inventory : null;
            if (!inv) return;

            foreach (ItemIndex idx in inv.itemAcquisitionOrder)
            {
                int count = inv.GetItemCountPermanent(idx);
                if (count <= 0) continue;
                ItemDef def = ItemCatalog.GetItemDef(idx);
                if (def == null || def.hidden || def.tier == ItemTier.NoTier) continue;
                into.Add(new KeyValuePair<ItemIndex, int>(idx, count));
            }
            // rarest first; equal tiers keep the order the items were picked up in
            var order = new Dictionary<ItemIndex, int>();
            for (int i = 0; i < into.Count; i++) order[into[i].Key] = i;
            into.Sort((a, b) =>
            {
                int ta = ItemDistributor.TierRank(ItemCatalog.GetItemDef(a.Key).tier);
                int tb = ItemDistributor.TierRank(ItemCatalog.GetItemDef(b.Key).tier);
                return ta != tb ? ta.CompareTo(tb) : order[a.Key].CompareTo(order[b.Key]);
            });
        }

        private static int Signature(IReadOnlyList<Hero> heroes, IReadOnlyList<EquipmentIndex> spare)
        {
            unchecked
            {
                int h = 17;
                for (int i = 0; i < heroes.Count; i++)
                {
                    Hero hero = heroes[i];
                    h = h * 31 + hero.Id;
                    h = h * 31 + (hero.IsAlive ? 1 : 0);
                    CharacterMaster m = hero.Master;
                    Inventory inv = m ? m.inventory : null;
                    if (!inv) continue;
                    int slots = HeroEquipment.SlotCount(inv);
                    for (int s = 0; s < slots; s++) h = h * 31 + (int)HeroEquipment.InSlot(inv, s);
                    foreach (ItemIndex idx in inv.itemAcquisitionOrder)
                        h = h * 31 + ((int)idx * 7 + inv.GetItemCountPermanent(idx));
                }
                for (int i = 0; i < spare.Count; i++) h = h * 31 + (int)spare[i] + 3;
                return h;
            }
        }

        // ---- building ----------------------------------------------------------------------------------------

        private static Cell NewCell(Transform parent, string name)
        {
            var c = new Cell();
            c.root = UiKit.NewRect(name, parent);
            c.root.anchorMin = c.root.anchorMax = new Vector2(0f, 1f);
            c.root.pivot = new Vector2(0f, 1f);
            c.outline = UiKit.Sliced("Slot", c.root, HudArt.Outline, EmptySlot);
            UiKit.Stretch(c.outline.rectTransform);
            c.icon = UiKit.NewRawImage("Icon", c.root, null, Color.white);
            UiKit.Stretch(c.icon.rectTransform);
            // the stock stack count: "x3" in the top right corner
            c.count = UiKit.NewText("Count", c.root, "", 16f, Theme.Text, TextAlignmentOptions.TopRight);
            c.count.fontStyle = FontStyles.Bold;
            UiKit.Stretch(c.count.rectTransform, 0f, 0f, 1f, 0f);
            c.count.enableAutoSizing = true;
            c.count.fontSizeMin = 10f;
            c.count.fontSizeMax = 16f;
            return c;
        }

        private Row NewRow(Hero hero)
        {
            var row = new Row { hero = hero };
            row.strip = UiKit.Sliced("Row", rowsHost, HudArt.CleanButton, Theme.Strip);
            row.root = row.strip.rectTransform;
            row.root.anchorMin = row.root.anchorMax = new Vector2(0f, 1f);
            row.root.pivot = new Vector2(0f, 1f);

            // the portrait in a dark box, as the class icon of a scoreboard strip
            Image classBox = UiKit.Sliced("ClassBox", row.root, HudArt.ClusterFrame, Theme.StripBox);
            UiKit.Place(classBox.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(StripPad, 0f), new Vector2(52f, 52f));
            row.portrait = UiKit.NewRawImage("Portrait", classBox.rectTransform, null, Color.white);
            UiKit.Stretch(row.portrait.rectTransform, 3f, 3f, 3f, 3f);
            if (hero.Survivor && hero.Survivor.bodyPrefab)
            {
                CharacterBody b = hero.Survivor.bodyPrefab.GetComponent<CharacterBody>();
                if (b) row.portrait.texture = b.portraitIcon;
            }

            row.name = UiKit.NewText("Name", row.root, hero.DisplayName, 22f, Theme.Text, TextAlignmentOptions.BottomLeft);
            UiKit.Place(row.name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(StripPad + 60f, 0f), new Vector2(NameW - 66f, 28f));
            row.name.enableAutoSizing = true;
            row.name.fontSizeMin = 12f;
            row.name.fontSizeMax = 22f;
            string roleToken = HeroRoles.RoleToken(hero.Survivor ? hero.Survivor.cachedName : "");
            row.role = UiKit.NewText("Role", row.root, Loc.Get(roleToken), 15f, Theme.TextSub, TextAlignmentOptions.TopLeft);
            UiKit.Place(row.role.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(StripPad + 60f, -1f), new Vector2(NameW - 66f, 20f));

            row.equipBox = UiKit.Sliced("EquipBox", row.root, HudArt.ClusterFrame, Theme.StripBox);
            row.itemsBox = UiKit.Sliced("ItemsBox", row.root, HudArt.ClusterFrame, Theme.StripBox);

            row.empty = UiKit.NewText("Empty", row.itemsBox.rectTransform, Loc.Get("HIGHCOMMAND_INV_EMPTY"), 15f, Theme.TextMuted, TextAlignmentOptions.MidlineLeft);
            UiKit.Stretch(row.empty.rectTransform, 12f, 0f, 12f, 0f);
            row.more = UiKit.NewText("More", row.itemsBox.rectTransform, "", 15f, Theme.TextHud, TextAlignmentOptions.BottomRight);
            UiKit.Stretch(row.more.rectTransform, 6f, 3f, 8f, 3f);
            return row;
        }

        private void SyncRows(IReadOnlyList<Hero> heroes)
        {
            // drop rows of heroes that are gone, add rows for new ones, keep the roster's order
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                bool stillThere = false;
                for (int k = 0; k < heroes.Count; k++) if (heroes[k] == rows[i].hero) { stillThere = true; break; }
                if (stillThere) continue;
                if (hoverHostRect && rows[i].root && hoverHostRect.IsChildOf(rows[i].root))
                {
                    // the hover frame sits on one of this row's cells: keep it, the row goes
                    hoverHostRect.SetParent(window.Panel, false);
                    hoverCell = null;
                }
                if (rows[i].root) UnityEngine.Object.Destroy(rows[i].root.gameObject);
                rows.RemoveAt(i);
            }
            for (int k = 0; k < heroes.Count; k++)
            {
                bool exists = false;
                for (int i = 0; i < rows.Count; i++) if (rows[i].hero == heroes[k]) { exists = true; break; }
                if (!exists) rows.Add(NewRow(heroes[k]));
            }
            rows.Sort((a, b) => a.hero.JoinOrder.CompareTo(b.hero.JoinOrder));
        }

        private void Rebuild(IReadOnlyList<Hero> heroes, IReadOnlyList<EquipmentIndex> spare, HcState state)
        {
            SyncRows(heroes);
            foreach (Row r in rows) CollectItems(r.hero, r.items);

            // equipment places per hero (MUL-T has two): the item box starts after the widest of them
            int maxSlots = 1;
            foreach (Row r in rows)
            {
                CharacterMaster mm = r.hero.Master;
                maxSlots = Mathf.Max(maxSlots, HeroEquipment.SlotCount(mm ? mm.inventory : null));
            }
            float equipW = maxSlots * EquipPitch + 6f;
            float itemsX = NameW;
            float itemsW = BodyW - itemsX - equipW - StripPad * 2f;
            float gridW = itemsW - 12f;

            // the biggest icon size at which everything fits; the smallest caps the lines and counts the rest
            float avail = MaxBodyH - FooterH - Theme.GroupGap;
            float size = CellSizes[CellSizes.Length - 1];
            int maxLines = int.MaxValue;
            foreach (float candidate in CellSizes)
            {
                int perLine = Mathf.Max(1, Mathf.FloorToInt(gridW / (candidate + CellGap)));
                float total = 0f;
                foreach (Row r in rows)
                {
                    int lines = Mathf.Max(1, (r.items.Count + perLine - 1) / perLine);
                    total += Mathf.Max(MinStripH, lines * (candidate + CellGap) + 2f * StripPad + 6f) + RowGap;
                }
                size = candidate;
                if (total <= avail) { maxLines = int.MaxValue; break; }
                float per = rows.Count > 0 ? avail / rows.Count - RowGap : MinStripH;
                maxLines = Mathf.Max(1, Mathf.FloorToInt((per - 2f * StripPad - 6f) / (candidate + CellGap)));
            }

            int perLineFinal = Mathf.Max(1, Mathf.FloorToInt(gridW / (size + CellGap)));
            float y = 0f;
            foreach (Row r in rows)
            {
                int count = r.items.Count;
                int capacity = maxLines == int.MaxValue ? count : Mathf.Min(count, maxLines * perLineFinal);
                int hidden = count - capacity;
                int lines = Mathf.Max(1, (capacity + perLineFinal - 1) / perLineFinal);
                r.height = Mathf.Max(MinStripH, lines * (size + CellGap) + 2f * StripPad + 6f);

                r.root.anchoredPosition = new Vector2(0f, -y);
                r.root.sizeDelta = new Vector2(BodyW, r.height);
                y += r.height + RowGap;

                UiKit.Place(r.itemsBox.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(itemsX, -StripPad),
                    new Vector2(itemsW, r.height - 2f * StripPad));
                UiKit.Place(r.equipBox.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-StripPad, -StripPad),
                    new Vector2(equipW, r.height - 2f * StripPad));

                // equipment cells, one per place
                CharacterMaster m = r.hero.Master;
                Inventory heroInv = m ? m.inventory : null;
                int slots = HeroEquipment.SlotCount(heroInv);
                while (r.equipCells.Count < slots) r.equipCells.Add(NewCell(r.equipBox.rectTransform, "Equipment"));
                for (int s = 0; s < r.equipCells.Count; s++)
                {
                    Cell ec = r.equipCells[s];
                    if (s >= slots) { ec.root.gameObject.SetActive(false); continue; }
                    ec.root.gameObject.SetActive(true);
                    ec.owner = r.hero;
                    ec.slot = s;
                    ec.item = ItemIndex.None;
                    ec.equipment = heroInv ? HeroEquipment.InSlot(heroInv, s) : EquipmentIndex.None;
                    ec.root.anchoredPosition = new Vector2(5f + s * EquipPitch, -Mathf.Round((r.height - 2f * StripPad - EquipSize) * 0.5f));
                    ec.root.sizeDelta = new Vector2(EquipSize, EquipSize);
                    PickupDef eqDef = ec.equipment != EquipmentIndex.None ? PickupOf(ec.equipment) : null;
                    bool shown = PickupIcons.Apply(ec.icon, eqDef);
                    ec.icon.enabled = shown;
                    ec.outline.enabled = !shown;   // an empty place is a faint outline, not a black square
                    ec.count.text = "";
                }

                // item cells
                while (r.cells.Count < capacity) r.cells.Add(NewCell(r.itemsBox.rectTransform, "Item"));
                for (int i = 0; i < r.cells.Count; i++)
                {
                    Cell c = r.cells[i];
                    if (i >= capacity) { c.root.gameObject.SetActive(false); continue; }
                    c.root.gameObject.SetActive(true);
                    KeyValuePair<ItemIndex, int> kv = r.items[i];
                    c.owner = r.hero;
                    c.item = kv.Key;
                    c.equipment = EquipmentIndex.None;
                    int col = i % perLineFinal, line = i / perLineFinal;
                    c.root.anchoredPosition = new Vector2(6f + col * (size + CellGap), -(5f + line * (size + CellGap)));
                    c.root.sizeDelta = new Vector2(size, size);
                    PickupDef def = PickupOf(kv.Key);
                    bool shown = PickupIcons.Apply(c.icon, def);
                    c.icon.enabled = true;
                    c.icon.color = shown ? Color.white : (def != null ? def.baseColor : Color.gray);
                    c.outline.enabled = false;
                    c.count.text = kv.Value > 1 ? "x" + kv.Value : "";
                }

                r.empty.gameObject.SetActive(count == 0);
                r.more.gameObject.SetActive(hidden > 0);
                if (hidden > 0) r.more.text = Loc.Format("HIGHCOMMAND_INV_MORE", hidden);
                r.stripState = -1;
            }

            // spare equipment
            while (spareCells.Count < spare.Count) spareCells.Add(NewCell(footer, "Spare"));
            for (int i = 0; i < spareCells.Count; i++)
            {
                Cell c = spareCells[i];
                if (i >= spare.Count) { c.root.gameObject.SetActive(false); continue; }
                c.root.gameObject.SetActive(true);
                c.owner = null;
                c.item = ItemIndex.None;
                c.equipment = spare[i];
                c.spareIndex = i;
                c.root.anchoredPosition = new Vector2(i * 46f, -22f);
                c.root.sizeDelta = new Vector2(42f, 42f);
                PickupDef def = PickupOf(spare[i]);
                bool shown = PickupIcons.Apply(c.icon, def);
                c.icon.color = shown ? Color.white : Color.gray;
                c.outline.enabled = false;
                c.count.text = "";
            }
            spareLabel.text = spare.Count > 0 ? Loc.Get("HIGHCOMMAND_INV_SPARE") : "";

            float bodyH = Mathf.Max(0f, y - RowGap) + Theme.GroupGap + FooterH;
            rowsHost.sizeDelta = new Vector2(BodyW, Mathf.Max(0f, y));
            rowsHost.anchoredPosition = Vector2.zero;
            window.SetSize(new Vector2(window.Panel.sizeDelta.x, Window.HeightFor(bodyH)));
            window.FitToScreen();

            int totalItems = 0;
            foreach (Row r in rows) foreach (var kv in r.items) totalItems += kv.Value;
            window.Title.text = Loc.Format("HIGHCOMMAND_INV_TITLE", totalItems);
            bool mirror = IsMirror(state);
            window.Note.text = Loc.Get(mirror ? "HIGHCOMMAND_INV_HINT_MIRROR" : "HIGHCOMMAND_INV_HINT_SPLIT");
            autoButton.Label.text = Loc.Get("HIGHCOMMAND_INV_AUTO");
            autoButton.Root.gameObject.SetActive(!mirror);
        }

        // ---- per frame ---------------------------------------------------------------------------------------

        internal void Tick()
        {
            window.Tick();
            if (!window.Visible) return;

            // a pause screen or the console in the middle of a drag cancels it (nothing is handed over)
            if (dragging && ClientInput.MouseBlocked) CancelDrag();

            HcState state = HcState.Instance;
            IReadOnlyList<Hero> heroes = state != null ? state.Heroes : NoHeroes;
            IReadOnlyList<EquipmentIndex> spare = state != null ? state.SpareEquipment : NoEquipment;

            int signature = Signature(heroes, spare) * 31 + (IsMirror(state) ? 1 : 0);
            if (signature != lastSignature)
            {
                lastSignature = signature;
                try { Rebuild(heroes, spare, state); }
                catch (Exception e) { Plugin.Log.LogError("InventoryPanel rebuild failed: " + e); }
            }

            Vector2 mouse = ClientInput.MouseBlocked ? new Vector2(-1f, -1f) : ClientInput.Mouse;

            // the strip under a dragged item lights up as the stock button does; a fallen hero's strip is darker
            foreach (Row r in rows)
            {
                bool target = dragging && r.hero != dragFrom && UiKit.Contains(r.root, mouse);
                int s = target ? 2 : r.hero.IsAlive ? 1 : 0;
                if (s == r.stripState) continue;
                r.stripState = s;
                r.strip.color = s == 2 ? DropTarget : s == 1 ? Theme.Strip : Theme.ButtonDisabled;
                r.portrait.color = s == 0 ? new Color(0.4f, 0.4f, 0.4f, 1f) : Color.white;
                if (s == 0) r.role.text = "<color=" + Theme.HexDeath + ">" + Loc.Get("HIGHCOMMAND_INV_DEAD") + "</color>";
                else if (s == 1) r.role.text = Loc.Get(HeroRoles.RoleToken(r.hero.Survivor ? r.hero.Survivor.cachedName : ""));
            }

            Cell hovered = HoveredCell(mouse);
            TickCellHover(dragging ? null : hovered);
            if (hovered != null && !dragging) ShowCellTooltip(hovered);

            if (!dragging && hovered != null && ClientInput.MouseDown(0)) BeginDrag(hovered);
            if (dragging)
            {
                // the ghost comes out once the pointer leaves the spot: a click alone never shows it
                if (!dragMoved && (mouse - dragStart).sqrMagnitude > ClickSlop * ClickSlop)
                {
                    dragMoved = true;
                    ghost.gameObject.SetActive(true);
                    ghost.SetAsLastSibling();
                }
                PlaceGhost(mouse);
                if (!ClientInput.MouseHeld(0) || ClientInput.MouseUp(0)) EndDrag(mouse);
            }

            if (autoButton.Root.gameObject.activeSelf)
            {
                autoButton.Refresh(dragging ? new Vector2(-1f, -1f) : mouse, true);
                if (autoButton.Clicked(ClientInput.MouseDown(0)))
                {
                    string error = ArmyService.RedistributeItems();
                    if (error != Net.CommandChannel.Relayed) Toast.Show(error ?? Loc.Get("HIGHCOMMAND_INV_AUTO_DONE"));
                    lastSignature = int.MinValue;
                }
            }
        }

        // the white frame follows the hovered cell (one frame for the whole panel)
        private void TickCellHover(Cell cell)
        {
            if (cell != hoverCell)
            {
                hoverCell = cell;
                if (cell != null)
                {
                    hoverHostRect.SetParent(cell.root, false);
                    UiKit.Stretch(hoverHostRect);
                }
            }
            cellHover.Tick(cell != null);
        }

        // ---- test hooks (used by the hc_inv_* console commands) -----------------------------------------------

        private static Vector2 CenterOf(RectTransform rt)
        {
            Vector3 w = rt.TransformPoint(rt.rect.center); // overlay canvas: world units are screen pixels
            return new Vector2(w.x, w.y);
        }

        /// <summary>Screen position of an item cell (<paramref name="item"/>), a hero's equipment slot or a spare piece.</summary>
        internal bool TryFindCell(Hero owner, ItemIndex item, bool equipment, int spareIndex, out Vector2 center, int equipSlot = 0)
        {
            center = Vector2.zero;
            if (!Visible) return false;

            if (spareIndex >= 0)
            {
                if (spareIndex >= spareCells.Count || !spareCells[spareIndex].root.gameObject.activeSelf) return false;
                center = CenterOf(spareCells[spareIndex].root);
                return true;
            }
            foreach (Row r in rows)
            {
                if (r.hero != owner) continue;
                if (equipment)
                {
                    if (equipSlot < 0 || equipSlot >= r.equipCells.Count || !r.equipCells[equipSlot].root.gameObject.activeSelf) return false;
                    center = CenterOf(r.equipCells[equipSlot].root);
                    return true;
                }
                foreach (Cell c in r.cells)
                {
                    if (!c.root.gameObject.activeSelf || c.item != item) continue;
                    center = CenterOf(c.root);
                    return true;
                }
            }
            return false;
        }

        internal bool TryFindRow(Hero hero, out Vector2 center)
        {
            center = Vector2.zero;
            if (!Visible) return false;
            foreach (Row r in rows)
            {
                if (r.hero != hero) continue;
                // the name part of the strip, away from the item cells
                Vector2 c = CenterOf(r.root);
                Vector3 left = r.root.TransformPoint(new Vector3(r.root.rect.xMin + NameW * 0.6f, 0f, 0f));
                center = new Vector2(left.x, c.y);
                return true;
            }
            return false;
        }

        private Cell HoveredCell(Vector2 mouse)
        {
            foreach (Row r in rows)
            {
                foreach (Cell ec in r.equipCells)
                    if (ec.root.gameObject.activeSelf && UiKit.Contains(ec.root, mouse)) return ec;
                foreach (Cell c in r.cells)
                    if (c.root.gameObject.activeSelf && UiKit.Contains(c.root, mouse)) return c;
            }
            foreach (Cell c in spareCells)
                if (c.root.gameObject.activeSelf && UiKit.Contains(c.root, mouse)) return c;
            return null;
        }

        // the texts of the hovered cell are built when it changes, not every frame
        private Cell tipCell;
        private int tipCount = -1;
        private string tipTitle, tipBody;
        private Color? tipBar;

        private void ShowCellTooltip(Cell c)
        {
            if (c.item == ItemIndex.None && c.equipment == EquipmentIndex.None)
            {
                Tooltip.Show(Loc.Get("HIGHCOMMAND_INV_EQUIPMENT"), Loc.Get("HIGHCOMMAND_INV_NOEQUIP"));
                return;
            }
            int n = c.item != ItemIndex.None && c.owner != null && c.owner.Master && c.owner.Master.inventory
                ? c.owner.Master.inventory.GetItemCountPermanent(c.item) : 0;
            if (c != tipCell || n != tipCount || tipTitle == null)
            {
                PickupDef def = c.item != ItemIndex.None ? PickupOf(c.item) : PickupOf(c.equipment);
                if (def == null) return;
                tipCell = c;
                tipCount = n;
                tipTitle = Language.GetString(def.nameToken);
                tipBody = Language.GetString(def.pickupToken);
                if (n > 0) tipBody += "\n\n<style=cSub>" + Loc.Format("HIGHCOMMAND_INV_COUNT", n) + "</style>";
                tipBody += "\n" + Tooltip.KeyHint(Loc.Get("HIGHCOMMAND_KEY_CLICK")) + " <style=cSub>" + Loc.Get("HIGHCOMMAND_ITEM_CARD") + "</style>";
                tipBar = def.darkColor;
            }
            Tooltip.Show(tipTitle, tipBody, tipBar);
        }

        private void BeginDrag(Cell c)
        {
            if (c.item == ItemIndex.None && c.equipment == EquipmentIndex.None) return; // empty equipment slot
            if (IsMirror(HcState.Instance) && c.item != ItemIndex.None) return;

            dragging = true;
            dragItem = c.item;
            dragEquipment = c.item == ItemIndex.None ? c.equipment : EquipmentIndex.None;
            dragSpareIndex = c.spareIndex;
            dragSlot = c.slot;
            dragFrom = c.owner;

            dragCount = 1;
            if (dragItem != ItemIndex.None && ClientInput.Shift && dragFrom != null && dragFrom.Master && dragFrom.Master.inventory)
                dragCount = Mathf.Max(1, dragFrom.Master.inventory.GetItemCountPermanent(dragItem));

            PickupDef def = dragItem != ItemIndex.None ? PickupOf(dragItem) : PickupOf(dragEquipment);
            PickupIcons.Apply(ghostIcon, def);
            ghostCount.text = dragCount > 1 ? "x" + dragCount : "";
            dragStart = ClientInput.Mouse;
            dragMoved = false;
            PlaceGhost(dragStart);
        }

        private void PlaceGhost(Vector2 mouse)
        {
            // the ghost is a child of the panel (pivot and anchors at the centre): screen point -> panel space
            RectTransformUtility.ScreenPointToLocalPointInRectangle(window.Panel, mouse, null, out Vector2 local);
            ghost.anchoredPosition = local;
        }

        private void CancelDrag()
        {
            dragging = false;
            dragMoved = false;
            if (ghost) ghost.gameObject.SetActive(false);
        }

        private void EndDrag(Vector2 mouse)
        {
            if (!dragMoved && (mouse - dragStart).sqrMagnitude <= ClickSlop * ClickSlop)
            {
                // a click: the item's card, as a click on an item of the stock scoreboard
                if (dragItem != ItemIndex.None) ItemCard.Show(dragItem);
                else ItemCard.Show(dragEquipment);
                CancelDrag();
                return;
            }

            Hero target = null;
            foreach (Row r in rows)
                if (UiKit.Contains(r.root, mouse)) { target = r.hero; break; }

            string error = null;
            string what = null;
            if (target != null && target != dragFrom)
            {
                if (dragItem != ItemIndex.None && dragFrom != null)
                {
                    error = ArmyService.TryMoveItem(dragFrom, target, dragItem, dragCount);
                    PickupDef def = PickupOf(dragItem);
                    what = (def != null ? Language.GetString(def.nameToken) : "?") + (dragCount > 1 ? " x" + dragCount : "");
                }
                else if (dragEquipment != EquipmentIndex.None)
                {
                    if (dragFrom != null) error = ArmyService.TryMoveEquipment(dragFrom, dragSlot, target);
                    else error = ArmyService.TryEquipFromPool(dragSpareIndex, target);
                    PickupDef def = PickupOf(dragEquipment);
                    what = def != null ? Language.GetString(def.nameToken) : "?";
                }

                if (error == Net.CommandChannel.Relayed) lastSignature = int.MinValue; // the server answers with a toast
                else if (error != null) Toast.Error(error);
                else if (what != null)
                {
                    string from = dragFrom != null ? dragFrom.DisplayName : Loc.Get("HIGHCOMMAND_INV_EQUIPMENT");
                    Toast.Show(Loc.Format("HIGHCOMMAND_INV_MOVED", from, target.DisplayName, what));
                    UiSound.Click();
                    lastSignature = int.MinValue;
                }
            }
            CancelDrag();
        }
    }
}
