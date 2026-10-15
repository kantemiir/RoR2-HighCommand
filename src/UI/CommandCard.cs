using System;
using System.Collections.Generic;
using HighCommand.Army;
using HighCommand.Client;
using HighCommand.Mode;
using HighCommand.Orders;
using RoR2;
using TMPro;
using UnityEngine;

namespace HighCommand.UI
{
    /// <summary>
    /// Bottom right, where the stock HUD keeps the skill bar: the selected hero's skills (Z X C and the equipment, with
    /// the stock cooldowns and the automatic-use checkbox) and, beside them, every order of the mode as a small icon with
    /// its hotkey (fighting, moving, stances, revival, the teleporter). Every icon does what its hotkey does; the one in
    /// force is framed.
    /// </summary>
    internal sealed class CommandCard
    {
        private const float SkillSize = 52f, EquipSize = 58f, SkillGap = 8f;
        private const float OrderSize = 38f, OrderGap = 4f;
        private const int Columns = 5, Rows = 3;
        private const float GridW = Columns * OrderSize + (Columns - 1) * OrderGap;
        private const float GridH = Rows * OrderSize + (Rows - 1) * OrderGap;
        private const float LabelRoom = Theme.KeyLabelH + 3f;    // the key plates under the skills
        private const float SkillsW = 3f * SkillSize + EquipSize + 3f * SkillGap;
        private const float NameH = 22f;
        private const float Width = GridW + Theme.GroupGap * 1.5f + SkillsW;
        private const float Height = GridH;

        /// <summary>Two commands of the commander that are not orders to the selection.</summary>
        private enum Upkeep { None, Revive, Teleporter }

        private sealed class OrderButton
        {
            internal IconButton button;
            internal OrderController.Act act;
            internal Upkeep upkeep;
            internal Func<KeyCode> key;
            internal string nameToken;
            internal bool global;          // usable with nobody selected
            internal string shownKey;
        }

        private sealed class SkillButton
        {
            internal SkillSlot slot;
            internal int index;            // 1 secondary, 2 utility, 3 special, 4 equipment
            internal OrderController.Act act;
            internal Func<KeyCode> key;
            internal object shownDef;      // the skill / equipment the texts below belong to
            internal string name = "", desc = "";
            internal string shownKey;
        }

        private readonly RectTransform root;
        private readonly TextMeshProUGUI title;
        private readonly List<OrderButton> orders = new List<OrderButton>();
        private readonly List<SkillButton> skills = new List<SkillButton>();

        private Hero titleHero;
        private int titleKey = int.MinValue;

        internal CommandCard(Transform parent)
        {
            root = UiKit.NewRect("CommandCard", parent);
            UiKit.Place(root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-Theme.Margin, Theme.Margin), new Vector2(Width, Height));

            // the order grid on the left: fighting, moving, the army's stance and its upkeep
            AddOrder(0, 0, OrderController.Act.AttackMove, HudArt.Attack, "HIGHCOMMAND_CMD_ATTACKMOVE", () => Settings.KeyAttackMove.Value);
            AddOrder(1, 0, OrderController.Act.Focus, HudArt.Glyph(Glyph.Focus), "HIGHCOMMAND_CMD_FOCUS", () => Settings.KeyFocusFire.Value);
            AddOrder(2, 0, OrderController.Act.SeekEnemies, HudArt.Glyph(Glyph.SeekEnemies), "HIGHCOMMAND_CMD_SEEKENEMIES", () => Settings.KeySeekEnemies.Value);
            AddOrder(3, 0, OrderController.Act.SeekChests, HudArt.Glyph(Glyph.SeekChests), "HIGHCOMMAND_CMD_SEEKCHESTS", () => Settings.KeySeekChests.Value);
            AddOrder(4, 0, OrderController.Act.Patrol, HudArt.Glyph(Glyph.Patrol), "HIGHCOMMAND_CMD_PATROL", () => Settings.KeyPatrol.Value);
            AddOrder(0, 1, OrderController.Act.Stop, HudArt.Glyph(Glyph.Stop), "HIGHCOMMAND_CMD_STOP", () => Settings.KeyStop.Value);
            AddOrder(1, 1, OrderController.Act.Hold, HudArt.Glyph(Glyph.Hold), "HIGHCOMMAND_CMD_HOLD", () => Settings.KeyHold.Value);
            AddOrder(2, 1, OrderController.Act.Retreat, HudArt.Glyph(Glyph.Retreat), "HIGHCOMMAND_CMD_RETREAT", () => Settings.KeyRetreat.Value);
            AddOrder(3, 1, OrderController.Act.Rally, HudArt.Glyph(Glyph.Rally), "HIGHCOMMAND_CMD_RALLY", () => Settings.KeyRally.Value, global: true);
            AddOrder(4, 1, OrderController.Act.Formation, HudArt.Glyph(Glyph.Formation), "HIGHCOMMAND_CMD_FORMATION", () => Settings.KeyFormationCycle.Value, global: true);
            AddOrder(0, 2, OrderController.Act.StanceAggressive, HudArt.Glyph(Glyph.Aggressive), "HIGHCOMMAND_CMD_STANCE_AGGRESSIVE", () => Settings.KeyStanceAggressive.Value);
            AddOrder(1, 2, OrderController.Act.StanceDefensive, HudArt.Glyph(Glyph.Defensive), "HIGHCOMMAND_CMD_STANCE_DEFENSIVE", () => Settings.KeyStanceDefensive.Value);
            AddOrder(2, 2, OrderController.Act.StancePassive, HudArt.Glyph(Glyph.Passive), "HIGHCOMMAND_CMD_STANCE_PASSIVE", () => Settings.KeyStancePassive.Value);
            AddOrder(3, 2, OrderController.Act.Stop, HudArt.Glyph(Glyph.Medic), "HIGHCOMMAND_CMD_REVIVE", () => Settings.KeyRevive.Value, true, Upkeep.Revive);
            AddOrder(4, 2, OrderController.Act.Stop, HudArt.Teleporter, "HIGHCOMMAND_CMD_TELEPORTER", () => Settings.KeyTeleporter.Value, true, Upkeep.Teleporter);

            // the skills on the right, on the stock skill bar's line, the equipment a little bigger as there
            float x = GridW + Theme.GroupGap * 1.5f;
            AddSkill(x, 1, SkillSize, OrderController.Act.CastSecondary, () => Settings.KeyCastSecondary.Value);
            AddSkill(x + (SkillSize + SkillGap), 2, SkillSize, OrderController.Act.CastUtility, () => Settings.KeyCastUtility.Value);
            AddSkill(x + 2f * (SkillSize + SkillGap), 3, SkillSize, OrderController.Act.CastSpecial, () => Settings.KeyCastSpecial.Value);
            AddSkill(x + 3f * (SkillSize + SkillGap), 4, EquipSize, OrderController.Act.CastEquipment, () => Settings.KeyCastEquipment.Value);

            title = UiKit.NewText("Title", root, "", 18f, Theme.Text, TextAlignmentOptions.BottomLeft);
            UiKit.Place(title.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(x, LabelRoom + EquipSize + 6f), new Vector2(SkillsW, NameH));
            title.enableAutoSizing = true;
            title.fontSizeMin = 12f;
            title.fontSizeMax = 18f;

            ClientInput.RegisterUiHitTest(Contains);
            root.gameObject.SetActive(false);
        }

        private void AddOrder(int col, int row, OrderController.Act act, Sprite icon, string nameToken, Func<KeyCode> key,
            bool global = false, Upkeep upkeep = Upkeep.None)
        {
            var b = new IconButton(root, "Order_" + nameToken, OrderSize, icon);
            b.SetPosition(col * (OrderSize + OrderGap), row * (OrderSize + OrderGap));
            orders.Add(new OrderButton { button = b, act = act, upkeep = upkeep, key = key, nameToken = nameToken, global = global });
        }

        private void AddSkill(float x, int index, float size, OrderController.Act act, Func<KeyCode> key)
        {
            var slot = new SkillSlot(root, "Skill_" + index, size, autoSwitch: true);
            slot.SetPosition(x, LabelRoom);
            skills.Add(new SkillButton { slot = slot, index = index, act = act, key = key });
        }

        internal bool Contains(Vector2 screenPoint) => root.gameObject.activeSelf && UiKit.Contains(root, screenPoint);

        internal void Destroy()
        {
            ClientInput.UnregisterUiHitTest(Contains);
            if (root) UnityEngine.Object.Destroy(root.gameObject);
        }

        // ---- per frame -----------------------------------------------------------------------------------------

        internal void Tick(Selection selection, OrderController control, bool visible)
        {
            if (root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
            if (!visible) return;

            Vector2 mouse = ClientInput.MouseBlocked ? new Vector2(-1f, -1f) : ClientInput.Mouse;
            bool click = ClientInput.MouseDown(0);
            IReadOnlyList<Hero> chosen = selection.Heroes;
            Hero first = FirstAlive(chosen);
            bool any = chosen.Count > 0;
            HcState state = HcState.Instance;

            TickTitle(first, chosen.Count);

            foreach (OrderButton o in orders)
            {
                bool enabled, active;
                if (o.upkeep == Upkeep.Revive)
                {
                    Hero fallen = state != null ? ArmyService.FirstFallen(state.Heroes) : null;
                    enabled = fallen != null && state.Gold >= (uint)state.RevivePrice();
                    active = false;
                }
                else if (o.upkeep == Upkeep.Teleporter)
                {
                    HcState.TeleState t = state != null ? state.Teleporter : HcState.TeleState.None;
                    enabled = t == HcState.TeleState.Idle || t == HcState.TeleState.Charging || t == HcState.TeleState.Charged;
                    active = t == HcState.TeleState.Charging;
                }
                else
                {
                    enabled = any || o.global;
                    active = IsActive(o.act, control, first);
                }

                string key = UiKit.KeyText(o.key());
                if (!string.Equals(key, o.shownKey, StringComparison.Ordinal))
                {
                    o.shownKey = key;
                    o.button.SetKey(key);
                }
                if (o.button.Refresh(mouse, enabled, active)) ShowOrderTooltip(o, control, state);
                if (!o.button.Clicked(click)) continue;

                if (o.upkeep == Upkeep.Revive) control.Revive(null);
                else if (o.upkeep == Upkeep.Teleporter) control.TeleporterAction();
                else if (active && IsTargeting(o.act)) control.CancelMode();   // a second click puts the order away again
                else control.Perform(o.act);
            }

            CharacterBody body = first != null ? first.Body : null;
            foreach (SkillButton s in skills) TickSkill(s, first, body, mouse, click, control);
        }

        private static Hero FirstAlive(IReadOnlyList<Hero> chosen)
        {
            for (int i = 0; i < chosen.Count; i++) if (chosen[i].IsAlive) return chosen[i];
            return chosen.Count > 0 ? chosen[0] : null;
        }

        private void TickTitle(Hero first, int count)
        {
            CharacterBody body = first != null ? first.Body : null;
            int level = body ? (int)body.level : 0;
            int key = first != null ? (count * 128 + level) * 8 + first.Rank : -1;
            if (first == titleHero && key == titleKey) return;
            titleHero = first;
            titleKey = key;
            if (first == null)
            {
                title.text = "<color=" + Theme.HexMuted + ">" + Loc.Get("HIGHCOMMAND_CARD_NOSELECTION") + "</color>";
                return;
            }
            string text = first.DisplayName;
            if (body) text += "  <size=80%><style=cSub>" + Loc.Format("HIGHCOMMAND_LEVEL_SHORT", level) + "</style></size>";
            if (first.Rank > 0) text += " <size=80%><color=" + Theme.HexDamage + ">" + new string('★', first.Rank) + "</color></size>";
            if (count > 1) text += "  <size=80%><color=" + Theme.HexHud + ">+" + (count - 1) + "</color></size>";
            title.text = text;
        }

        private static bool IsTargeting(OrderController.Act act) =>
            act == OrderController.Act.AttackMove || act == OrderController.Act.Patrol || act == OrderController.Act.Rally || act == OrderController.Act.Focus;

        private static bool IsActive(OrderController.Act act, OrderController control, Hero first)
        {
            switch (act)
            {
                case OrderController.Act.AttackMove: return control.Mode == OrderController.Pending.AttackMove;
                case OrderController.Act.Patrol: return control.Mode == OrderController.Pending.Patrol;
                case OrderController.Act.Rally: return control.Mode == OrderController.Pending.Rally;
                case OrderController.Act.Focus: return control.Mode == OrderController.Pending.Focus;
                case OrderController.Act.StanceAggressive: return first != null && first.NetStance == (byte)Stance.Aggressive;
                case OrderController.Act.StanceDefensive: return first != null && first.NetStance == (byte)Stance.Defensive;
                case OrderController.Act.StancePassive: return first != null && first.NetStance == (byte)Stance.Passive;
                case OrderController.Act.Hold: return first != null && first.NetOrderKind == (byte)OrderKind.Hold;
                case OrderController.Act.SeekEnemies: return first != null && first.NetOrderKind == (byte)OrderKind.SeekEnemies;
                case OrderController.Act.SeekChests: return first != null && first.NetOrderKind == (byte)OrderKind.SeekChests;
                case OrderController.Act.Retreat: return first != null && first.NetOrderKind == (byte)OrderKind.Retreat;
                default: return false;
            }
        }

        // the texts of a hovered order are built when it or its state changes, not every frame
        private OrderButton tipOrder;
        private int tipKey = int.MinValue;
        private string tipTitle, tipBody;

        private void ShowOrderTooltip(OrderButton o, OrderController control, HcState state)
        {
            bool formation = o.upkeep == Upkeep.None && o.act == OrderController.Act.Formation;
            int price = state != null ? state.RevivePrice() : 0;
            int key = formation ? control.Formation : o.upkeep == Upkeep.Revive ? price : 0;
            if (o != tipOrder || key != tipKey)
            {
                tipOrder = o;
                tipKey = key;
                tipTitle = Loc.Get(o.nameToken);
                if (formation) tipTitle += ": " + control.FormationName;
                string desc = o.upkeep == Upkeep.Revive ? Loc.Format(o.nameToken + "_DESC", price) : Loc.Get(o.nameToken + "_DESC");
                tipBody = desc + "\n\n" + Tooltip.KeyHint(o.shownKey);
            }
            Tooltip.Show(tipTitle, tipBody);
        }

        // ---- the selected hero's skills ------------------------------------------------------------------------

        private void TickSkill(SkillButton s, Hero hero, CharacterBody body, Vector2 mouse, bool click, OrderController control)
        {
            bool has = false;
            float left = 0f, total = 1f;
            int charges = 0;
            object def = null;
            Sprite icon = null;

            if (body && hero.IsAlive)
            {
                if (s.index == 4) ReadEquipment(s, body, ref has, ref left, ref total, ref charges, ref def, ref icon);
                else ReadSkill(s, body, ref has, ref left, ref total, ref charges, ref def, ref icon);
            }
            if (!ReferenceEquals(def, s.shownDef)) s.shownDef = def;
            s.slot.SetIcon(has ? icon : null);

            string key = UiKit.KeyText(s.key());
            if (!string.Equals(key, s.shownKey, StringComparison.Ordinal))
            {
                s.shownKey = key;
                s.slot.SetKey(key);
            }

            bool? auto = hero != null && has ? (hero.NetAutoMask & (1 << s.index)) != 0 : (bool?)null;
            bool hover = s.slot.Refresh(mouse, has, left, total, charges, auto, out _);
            if (hover && has) ShowSkillTooltip(s, body, auto ?? true);
            if (!s.slot.Clicked(click)) return;
            // a click on the checkbox (or Alt+click) flips the automatic use instead of casting
            control.Perform(s.act, ClientInput.Alt || s.slot.OverCheckbox(mouse));
        }

        private static void ReadSkill(SkillButton s, CharacterBody body, ref bool has, ref float left, ref float total, ref int charges,
            ref object def, ref Sprite icon)
        {
            SkillLocator locator = body.skillLocator;
            GenericSkill skill = !locator ? null : s.index == 1 ? locator.secondary : s.index == 2 ? locator.utility : locator.special;
            if (!skill || !skill.skillDef) return;
            has = true;
            def = skill.skillDef;
            icon = skill.icon;
            // as the stock skill icon: it counts as ready while a charge is left
            float rate = Mathf.Max(0.0001f, skill.rechargeRate);
            total = Mathf.Max(0.5f, skill.CalculateFinalRechargeInterval() / rate);
            left = skill.stock > 0 || skill.skillDef.hideCooldown ? 0f : skill.cooldownRemaining / rate;
            charges = skill.maxStock > 1 && !skill.skillDef.hideStockCount ? skill.stock : 0;
            if (!ReferenceEquals(s.shownDef, def))
            {
                s.name = Language.GetString(skill.skillNameToken);
                s.desc = Language.GetString(skill.skillDescriptionToken);
            }
        }

        private static void ReadEquipment(SkillButton s, CharacterBody body, ref bool has, ref float left, ref float total, ref int charges,
            ref object def, ref Sprite icon)
        {
            Inventory inv = body.inventory;
            EquipmentIndex eq = inv ? inv.currentEquipmentIndex : EquipmentIndex.None;
            EquipmentDef equipment = eq != EquipmentIndex.None ? EquipmentCatalog.GetEquipmentDef(eq) : null;
            if (equipment == null) return;
            has = true;
            def = equipment;
            icon = PickupIcons.SpriteOf(PickupIcons.DefOf(eq));
            EquipmentState st = inv.currentEquipmentState;
            total = Mathf.Max(1f, equipment.cooldown);
            left = st.charges > 0 ? 0f : st.chargeFinishTime.timeUntilClamped;
            charges = st.charges > 1 ? st.charges : 0;
            if (!ReferenceEquals(s.shownDef, def))
            {
                s.name = Language.GetString(equipment.nameToken);
                s.desc = Language.GetString(equipment.pickupToken);
            }
        }

        private SkillButton tipSkill;
        private bool tipAuto;
        private object tipDef;
        private string skillTip;

        private void ShowSkillTooltip(SkillButton s, CharacterBody body, bool auto)
        {
            if (s != tipSkill || auto != tipAuto || !ReferenceEquals(s.shownDef, tipDef) || skillTip == null)
            {
                tipSkill = s;
                tipAuto = auto;
                tipDef = s.shownDef;
                skillTip = s.desc + "\n\n<style=cSub>" + Loc.Get(auto ? "HIGHCOMMAND_CARD_AUTO_ON" : "HIGHCOMMAND_CARD_AUTO_OFF") + "</style>\n"
                           + Loc.Format("HIGHCOMMAND_CARD_SKILL_KEYS", Tooltip.KeyHint(s.shownKey),
                               Tooltip.KeyHint(UiKit.KeyText(Settings.KeyToggleAuto.Value) + "+" + s.shownKey));
            }
            Tooltip.Show(s.name, skillTip, s.index == 4 ? ColorCatalog.GetColor(ColorCatalog.ColorIndex.EquipmentDark) : Tooltip.SurvivorBar(body));
        }
    }
}
