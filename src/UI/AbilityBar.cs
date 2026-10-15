using System;
using HighCommand.Army;
using HighCommand.Client;
using HighCommand.Mode;
using UnityEngine;

namespace HighCommand.UI
{
    /// <summary>
    /// Bottom centre: the commander's own abilities as a stock skill bar (the game's pictures for each, the stock
    /// cooldown, the ready flash and sound), and to their left the small icons of the commander's screens with their
    /// keys, as the stock HUD puts its inventory and sprint icons beside the skills: barracks, items, the hotkey sheet.
    /// </summary>
    internal sealed class AbilityBar
    {
        private const float SlotSize = 52f, SlotGap = 8f;
        private const float MiniSize = 34f, MiniGap = 8f;
        private const float LabelRoom = Theme.KeyLabelH + 3f;
        private const float SlotsW = CommanderAbilities.Count * SlotSize + (CommanderAbilities.Count - 1) * SlotGap;
        private const float MinisW = 3f * MiniSize + 2f * MiniGap;
        private const float Width = MinisW + 2f * Theme.GroupGap + SlotsW;
        private const float Height = LabelRoom + SlotSize;

        /// <summary>The commander's screens reachable from the bar.</summary>
        internal enum Screen { None, Barracks, Items, Help }

        private readonly RectTransform root;
        private readonly SkillSlot[] slots = new SkillSlot[CommanderAbilities.Count];
        private readonly string[] shownKeys = new string[CommanderAbilities.Count];
        private readonly IconButton barracks, items, help;
        private string barracksKey, itemsKey, helpKey;
        private string barracksTip, itemsTip, helpTip;

        internal AbilityBar(Transform parent)
        {
            root = UiKit.NewRect("AbilityBar", parent);
            UiKit.Place(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, Theme.Margin), new Vector2(Width, Height));

            barracks = Mini(0, HudArt.Glyph(Glyph.Person));
            items = Mini(1, HudArt.Inventory);
            help = Mini(2, HudArt.Glyph(Glyph.Help));

            float x = MinisW + 2f * Theme.GroupGap;
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = new SkillSlot(root, "Ability" + i, SlotSize, autoSwitch: false);
                slots[i].SetPosition(x + i * (SlotSize + SlotGap), LabelRoom);
                slots[i].SetIcon(HudArt.AbilityIcon(i));
            }

            ClientInput.RegisterUiHitTest(Contains);
            root.gameObject.SetActive(false);
        }

        private IconButton Mini(int index, Sprite icon)
        {
            var b = new IconButton(root, "Mini" + index, MiniSize, icon, keyInCorner: false);
            b.Root.anchorMin = b.Root.anchorMax = new Vector2(0f, 0f);
            b.Root.pivot = new Vector2(0f, 0f);
            b.Root.anchoredPosition = new Vector2(index * (MiniSize + MiniGap), LabelRoom + (SlotSize - MiniSize));
            return b;
        }

        internal bool Contains(Vector2 screenPoint) => root.gameObject.activeSelf && UiKit.Contains(root, screenPoint);

        internal void Destroy()
        {
            ClientInput.UnregisterUiHitTest(Contains);
            if (root) UnityEngine.Object.Destroy(root.gameObject);
        }

        /// <summary>Returns the screen whose icon was clicked this frame.</summary>
        internal Screen Tick(OrderController control, bool visible, bool barracksOpen, bool itemsOpen, bool helpOpen)
        {
            if (root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
            if (!visible) return Screen.None;

            Vector2 mouse = ClientInput.MouseBlocked ? new Vector2(-1f, -1f) : ClientInput.Mouse;
            bool click = ClientInput.MouseDown(0);
            HcState state = HcState.Instance;

            for (int i = 0; i < slots.Length; i++)
            {
                SetKey(slots[i], ref shownKeys[i], Settings.KeyAbility[i].Value);
                float left = CommanderAbilities.Remaining(state, i);
                float total = CommanderAbilities.Cooldown[i];
                bool targeting = control.AbilityIndex == i
                    && control.Mode == (CommanderAbilities.NeedsPoint(i) ? OrderController.Pending.AbilityPoint : OrderController.Pending.AbilityTarget);
                bool hover = slots[i].Refresh(mouse, true, left, total, 0, null, out bool ready);
                slots[i].SetActive(targeting);
                if (ready) UiSound.Ready();
                if (hover) ShowAbilityTooltip(i, total);
                if (slots[i].Clicked(click))
                {
                    if (targeting) control.CancelMode();
                    else control.UseAbility(i);
                }
            }

            Screen clicked = Screen.None;
            if (TickMini(barracks, ref barracksKey, ref barracksTip, Settings.KeyBarracks.Value, barracksOpen, mouse, click,
                    "HIGHCOMMAND_MINI_BARRACKS", "HIGHCOMMAND_HELP_BARRACKS")) clicked = Screen.Barracks;
            if (TickMini(items, ref itemsKey, ref itemsTip, Settings.KeyInventory.Value, itemsOpen, mouse, click,
                    "HIGHCOMMAND_MINI_ITEMS", "HIGHCOMMAND_HELP_INVENTORY")) clicked = Screen.Items;
            if (TickMini(help, ref helpKey, ref helpTip, Settings.KeyHelp.Value, helpOpen, mouse, click,
                    "HIGHCOMMAND_MINI_HELP", "HIGHCOMMAND_HELP_HELP")) clicked = Screen.Help;
            return clicked;
        }

        private static void SetKey(SkillSlot slot, ref string shown, KeyCode key)
        {
            string text = UiKit.KeyText(key);
            if (string.Equals(text, shown, StringComparison.Ordinal)) return;
            shown = text;
            slot.SetKey(text);
        }

        private static bool TickMini(IconButton b, ref string shown, ref string tip, KeyCode key, bool open, Vector2 mouse, bool click,
            string nameToken, string descToken)
        {
            string text = UiKit.KeyText(key);
            if (!string.Equals(text, shown, StringComparison.Ordinal))
            {
                shown = text;
                b.SetKey(text);
                tip = null;
            }
            if (b.Refresh(mouse, true, open))
            {
                if (tip == null) tip = Loc.Get(descToken) + "\n\n" + Tooltip.KeyHint(text);
                Tooltip.Show(Loc.Get(nameToken), tip);
            }
            return b.Clicked(click);
        }

        // the texts of a hovered ability are built once per ability and language
        private readonly string[] tipBodies = new string[CommanderAbilities.Count];
        private readonly string[] tipKeys = new string[CommanderAbilities.Count];

        private void ShowAbilityTooltip(int index, float total)
        {
            if (tipBodies[index] == null || !string.Equals(tipKeys[index], shownKeys[index], StringComparison.Ordinal))
            {
                tipKeys[index] = shownKeys[index];
                tipBodies[index] = Loc.Get(CommanderAbilities.DescToken(index)) + "\n\n<style=cSub>"
                                   + Loc.Format("HIGHCOMMAND_ABILITY_COOLDOWN_INFO", Mathf.RoundToInt(total)) + "</style>  "
                                   + Tooltip.KeyHint(shownKeys[index]);
            }
            Color c = CommanderLook.ColorOf(index);
            Color.RGBToHSV(c, out float h, out float s, out float v);
            Tooltip.Show(Loc.Get(CommanderAbilities.NameToken(index)), tipBodies[index], Color.HSVToRGB(h, s * 0.8f, Mathf.Min(v, 0.55f)));
        }
    }
}
