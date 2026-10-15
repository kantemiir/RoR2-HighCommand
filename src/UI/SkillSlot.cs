using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HighCommand.UI
{
    /// <summary>
    /// A skill on the bottom bar, built like the stock SkillDisplay: the icon on a dark plate, a thin white frame while it is
    /// ready, the stock cooldown (the remap material darkening the icon, the seconds on top), a flash when it is ready
    /// again, the charges in the corner and the hotkey on a plate below. Hero skills also carry the automatic-use switch
    /// (the game's own checkbox) in the top left corner.
    /// </summary>
    internal sealed class SkillSlot
    {
        private const float IconInset = 3f;
        private const float CheckboxSize = 17f;

        private static readonly Color Cooling = Color.gray;   // the stock icon colour while it recharges
        private static readonly Color SecondsColor = new Color(0.953f, 0.953f, 0.953f, 1f);

        internal readonly RectTransform Root;
        private readonly Image icon;
        private readonly Image readyFrame;
        private readonly Image flash;
        private readonly RawImage remap;      // the stock cooldown (when its material loaded)
        private readonly Image sweep;         // a plain dark sweep otherwise
        private readonly TextMeshProUGUI seconds;
        private readonly TextMeshProUGUI stock;
        private readonly KeyLabel key;
        private readonly Image checkbox;
        private readonly HoverOutline hover;

        internal bool Hover { get; private set; }

        // what is shown now (a value written every frame would rebuild the canvas)
        private bool hasSkill = true, ready = true, known;
        private float shownPart = -1f;
        private int shownSeconds = -1, shownStock = -1;
        private bool? shownAuto;
        private float flashAge = -1f;

        internal SkillSlot(Transform parent, string name, float size, bool autoSwitch)
        {
            Root = UiKit.NewRect(name, parent);
            Root.anchorMin = Root.anchorMax = new Vector2(0f, 0f);
            Root.pivot = new Vector2(0f, 0f);
            Root.sizeDelta = new Vector2(size, size);

            Image plate = UiKit.Sliced("Plate", Root, HudArt.SkillPlate, Theme.Plate);
            UiKit.Stretch(plate.rectTransform);

            readyFrame = UiKit.Sliced("Ready", Root, HudArt.HighlightThin, Theme.Highlight);
            UiKit.Stretch(readyFrame.rectTransform, -2f, -2f, -2f, -2f);

            icon = UiKit.NewImage("Icon", Root, null, Color.white);
            icon.preserveAspect = true;
            UiKit.Stretch(icon.rectTransform, IconInset, IconInset, IconInset, IconInset);

            Material cd = HudArt.CooldownMaterial;
            Texture ramp = HudArt.CooldownRemap;
            if (cd && ramp)
            {
                remap = UiKit.NewRawImage("Cooldown", Root, ramp, new Color(1f, 1f, 1f, 0f));
                remap.material = cd;
                UiKit.Stretch(remap.rectTransform, IconInset, IconInset, IconInset, IconInset);
                remap.enabled = false;
            }
            else
            {
                sweep = UiKit.NewImage("Cooldown", Root, UiKit.White, new Color(0f, 0f, 0f, 0.62f));
                UiKit.Stretch(sweep.rectTransform, IconInset, IconInset, IconInset, IconInset);
                sweep.type = Image.Type.Filled;
                sweep.fillMethod = Image.FillMethod.Radial360;
                sweep.fillOrigin = (int)Image.Origin360.Top;
                sweep.fillClockwise = false;
                sweep.fillAmount = 0f;
            }

            seconds = UiKit.NewText("Seconds", Root, "", Mathf.Round(size * 0.46f), SecondsColor, TextAlignmentOptions.Center);
            UiKit.Stretch(seconds.rectTransform);

            stock = UiKit.NewText("Stock", Root, "", Theme.SizeKey, Theme.TextHud, TextAlignmentOptions.BottomRight);
            UiKit.Place(stock.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(size, 18f));

            flash = UiKit.NewImage("Flash", Root, HudArt.FlashSquare, Theme.Flash);
            UiKit.Stretch(flash.rectTransform);
            flash.rectTransform.localScale = new Vector3(1.35f, 1.35f, 1f);
            flash.enabled = false;

            if (autoSwitch)
            {
                checkbox = UiKit.NewImage("Auto", Root, HudArt.CheckboxOn, Color.white);
                UiKit.Place(checkbox.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(2f, -2f),
                    new Vector2(CheckboxSize, CheckboxSize));
                checkbox.enabled = false;   // as shownAuto: no switch until a skill reports one
            }

            key = new KeyLabel(Root);
            key.PlaceBelow(3f);
            hover = HoverOutline.ForIcon(Root);
        }

        internal void SetPosition(float x, float y) => Root.anchoredPosition = new Vector2(x, y);

        internal void SetKey(string text) => key.Set(text);

        /// <summary>The picture; null leaves an empty plate (as the stock equipment slot without equipment).</summary>
        internal void SetIcon(Sprite sprite)
        {
            if (icon.sprite == sprite) return;
            icon.sprite = sprite;
            icon.enabled = sprite;
        }

        /// <summary>Is the pointer on the automatic-use checkbox?</summary>
        internal bool OverCheckbox(Vector2 mouse) => checkbox && checkbox.enabled && UiKit.Contains(checkbox.rectTransform, mouse);

        /// <summary>Updates the slot; returns true while the pointer is over it.</summary>
        /// <param name="has">There is a skill in the slot.</param>
        /// <param name="left">Seconds until ready (0 = ready).</param>
        /// <param name="total">The full recharge.</param>
        /// <param name="charges">Charges to print in the corner (below 2: none).</param>
        /// <param name="auto">The automatic-use switch (null: no switch).</param>
        /// <param name="becameReady">It is ready again since the last call (the caller may play a sound).</param>
        internal bool Refresh(Vector2 mouse, bool has, float left, float total, int charges, bool? auto, out bool becameReady)
        {
            Hover = Root.gameObject.activeInHierarchy && UiKit.Contains(Root, mouse);
            hover.Tick(Hover && has);

            bool nowReady = has && left <= 0f;
            becameReady = known && has && hasSkill && nowReady && !ready;
            if (becameReady) flashAge = 0f;

            if (!known || has != hasSkill || nowReady != ready)
            {
                known = true;
                hasSkill = has;
                ready = nowReady;
                readyFrame.enabled = nowReady;
                icon.color = nowReady || !has ? Color.white : Cooling;
                key.SetDim(!has);
            }

            float part = has && !nowReady && total > 0f ? Mathf.Clamp01(left / total) : 0f;
            if (Mathf.Abs(part - shownPart) > 0.002f || (part == 0f) != (shownPart == 0f))
            {
                shownPart = part;
                if (remap)
                {
                    remap.enabled = part > 0f;
                    remap.color = new Color(1f, 1f, 1f, 1f - part); // the stock panel: alpha = how far along the recharge is
                }
                else sweep.fillAmount = part;
            }

            int s = has && !nowReady ? Mathf.CeilToInt(left) : 0;
            if (s != shownSeconds)
            {
                shownSeconds = s;
                seconds.text = s > 0 ? s.ToString() : "";
            }

            int c = has && charges > 1 ? charges : 0;
            if (c != shownStock)
            {
                shownStock = c;
                stock.text = c > 0 ? c.ToString() : "";
            }

            if (checkbox)
            {
                bool? a = has ? auto : null;
                if (a != shownAuto)
                {
                    shownAuto = a;
                    checkbox.enabled = a.HasValue;
                    if (a.HasValue) checkbox.sprite = a.Value ? HudArt.CheckboxOn : HudArt.CheckboxOff;
                }
            }

            TickFlash();
            return Hover;
        }

        private bool active;

        /// <summary>Its target is being chosen right now: the ready frame breathes until the click.</summary>
        internal void SetActive(bool value)
        {
            if (value)
            {
                active = true;
                readyFrame.enabled = true;
                float a = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f));
                readyFrame.color = new Color(1f, 1f, 1f, a);
                return;
            }
            if (!active) return;
            active = false;
            readyFrame.color = Theme.Highlight;
            readyFrame.enabled = ready && hasSkill;
        }

        private void TickFlash()
        {
            if (flashAge < 0f) return;
            flashAge += Time.unscaledDeltaTime;
            float t = flashAge / Theme.FlashTime;
            if (t >= 1f)
            {
                flashAge = -1f;
                flash.enabled = false;
                return;
            }
            flash.enabled = true;
            Color c = Theme.Flash;
            c.a = 1f - t * t;
            flash.color = c;
        }

        internal bool Clicked(bool mouseDown)
        {
            if (!(hasSkill && Hover && mouseDown)) return false;
            UiSound.Click();
            return true;
        }
    }
}
