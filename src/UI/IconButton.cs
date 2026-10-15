using UnityEngine;
using UnityEngine.UI;

namespace HighCommand.UI
{
    /// <summary>
    /// A square command on the HUD: a picture on a dark plate with its hotkey in the corner, like the small inventory and
    /// sprint icons beside the stock skill bar. States: normal, under the pointer (the settling white frame), active (a
    /// thin white frame: the current order, the chosen stance, a target being picked) and unavailable (greyed).
    /// </summary>
    internal sealed class IconButton
    {
        private const float IconInset = 4f;
        private const float KeyCorner = 9f;   // with the key in the corner the picture leaves it free

        private static readonly Color IconNormal = new Color(1f, 1f, 1f, 0.9f);
        private static readonly Color IconOff = new Color(Theme.TextDisabled.r, Theme.TextDisabled.g, Theme.TextDisabled.b, 0.45f);
        private static readonly Color PlateActive = Color.Lerp(Theme.Plate, Color.white, 0.14f);

        internal readonly RectTransform Root;
        private readonly Image plate;
        private readonly Image icon;
        private readonly Image activeFrame;
        private readonly KeyLabel key;
        private readonly HoverOutline hover;

        internal bool Hover { get; private set; }
        private bool enabled = true, active, known;

        internal IconButton(Transform parent, string name, float size, Sprite sprite, bool keyInCorner = true)
        {
            Root = UiKit.NewRect(name, parent);
            Root.anchorMin = Root.anchorMax = new Vector2(0f, 1f);
            Root.pivot = new Vector2(0f, 1f);
            Root.sizeDelta = new Vector2(size, size);

            plate = UiKit.Sliced("Plate", Root, HudArt.SkillPlate, Theme.Plate);
            UiKit.Stretch(plate.rectTransform);
            icon = UiKit.NewImage("Icon", Root, sprite, IconNormal);
            icon.preserveAspect = true;
            if (keyInCorner) UiKit.Stretch(icon.rectTransform, IconInset, KeyCorner, KeyCorner, IconInset);
            else UiKit.Stretch(icon.rectTransform, IconInset, IconInset, IconInset, IconInset);
            activeFrame = UiKit.Sliced("Active", Root, HudArt.HighlightThin, Theme.Highlight);
            UiKit.Stretch(activeFrame.rectTransform, -2f, -2f, -2f, -2f);
            activeFrame.enabled = false;

            key = keyInCorner ? new KeyLabel(Root, 14f, 12f) : new KeyLabel(Root, 15f, Theme.SizeSmall);
            if (keyInCorner) key.PlaceCorner();
            else key.PlaceBelow(2f);
            hover = HoverOutline.ForIcon(Root);
        }

        internal void SetPosition(float x, float y) => Root.anchoredPosition = new Vector2(x, -y);

        internal void SetIcon(Sprite sprite)
        {
            if (icon.sprite != sprite) icon.sprite = sprite;
        }

        internal void SetKey(string text) => key.Set(text);

        /// <summary>Updates the look; returns true while the pointer is over the button.</summary>
        internal bool Refresh(Vector2 mouse, bool isEnabled, bool isActive)
        {
            Hover = Root.gameObject.activeInHierarchy && UiKit.Contains(Root, mouse);
            hover.Tick(Hover && isEnabled);
            if (known && isEnabled == enabled && isActive == active) return Hover;
            known = true;
            enabled = isEnabled;
            active = isActive && isEnabled;
            icon.color = !enabled ? IconOff : active ? Color.white : IconNormal;
            plate.color = active ? PlateActive : Theme.Plate;
            activeFrame.enabled = active;
            key.SetDim(!enabled);
            return Hover;
        }

        /// <summary>True when the left button went down on it this frame (and it may be used).</summary>
        internal bool Clicked(bool mouseDown)
        {
            if (!(enabled && Hover && mouseDown)) return false;
            UiSound.Click();
            return true;
        }
    }
}
