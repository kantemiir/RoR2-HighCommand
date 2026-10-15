using UnityEngine;
using UnityEngine.UI;

namespace HighCommand.UI
{
    /// <summary>
    /// The thick white frame that settles round a button under the pointer, as the stock HGButton does: it appears at
    /// once, slightly too big, and shrinks onto the button. Writes the transform only while it is moving.
    /// </summary>
    internal sealed class HoverOutline
    {
        private readonly Image image;
        private readonly RectTransform rect;
        private float scale = Theme.HoverScale;
        private float velocity;
        private bool shown;

        /// <param name="outside">How far the frame reaches past the owner: left, bottom, right, top.</param>
        internal HoverOutline(RectTransform owner, Sprite sprite, Vector4 outside)
        {
            image = UiKit.Sliced("HoverOutline", owner, sprite, Theme.Highlight);
            rect = image.rectTransform;
            UiKit.Stretch(rect, -outside.x, -outside.y, -outside.z, -outside.w);
            rect.localScale = new Vector3(scale, scale, 1f);
            image.enabled = false;
        }

        /// <summary>The frame of a menu button (the sprite's shadow side is down and to the right).</summary>
        internal static HoverOutline ForButton(RectTransform owner) =>
            new HoverOutline(owner, HudArt.HighlightThick, new Vector4(4f, 12f, 12f, 4f));

        /// <summary>The frame of a square icon or a card.</summary>
        internal static HoverOutline ForIcon(RectTransform owner, float outside = 2f) =>
            new HoverOutline(owner, HudArt.HighlightIcon, new Vector4(outside, outside, outside, outside));

        internal void Tick(bool hover)
        {
            if (hover != shown)
            {
                shown = hover;
                image.enabled = hover;
                scale = Theme.HoverScale;
                velocity = 0f;
                rect.localScale = new Vector3(scale, scale, 1f);
            }
            if (!shown || scale == 1f) return;
            scale = Mathf.SmoothDamp(scale, 1f, ref velocity, Theme.HoverIn * 0.5f, float.PositiveInfinity, Time.unscaledDeltaTime);
            if (Mathf.Abs(scale - 1f) < 0.003f) scale = 1f;
            rect.localScale = new Vector3(scale, scale, 1f);
        }
    }

    /// <summary>
    /// Shows and hides a panel the way the stock UIJuice does: the alpha eases in while the panel slides the last few
    /// pixels into place; hiding only fades. The panel's GameObject is switched off when it is gone.
    /// </summary>
    internal sealed class Fader
    {
        private readonly RectTransform rect;
        private readonly CanvasGroup group;
        private readonly Vector2 pan;
        private Vector2 home;
        private float from, to, elapsed, duration;
        private bool moving;

        internal Fader(RectTransform rect, Vector2 pan)
        {
            this.rect = rect;
            this.pan = pan;
            group = rect.gameObject.GetComponent<CanvasGroup>();
            if (!group) group = rect.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        /// <summary>True while the panel is up or on its way up (not while it fades away).</summary>
        internal bool Open { get; private set; }

        /// <summary>Call when the panel's resting position changes (a re-layout).</summary>
        internal void SetHome(Vector2 position)
        {
            home = position;
            if (!moving) rect.anchoredPosition = home;
        }

        internal void Show()
        {
            if (Open) return;
            Open = true;
            home = moving ? home : rect.anchoredPosition;
            Start(group.gameObject.activeSelf ? group.alpha : 0f, 1f, Theme.WindowIn);
            rect.gameObject.SetActive(true);
            Tick();
        }

        internal void Hide(bool instant = false)
        {
            if (!Open && !moving) return;
            Open = false;
            if (instant || !rect.gameObject.activeSelf)
            {
                moving = false;
                group.alpha = 0f;
                rect.anchoredPosition = home;
                rect.gameObject.SetActive(false);
                return;
            }
            Start(group.alpha, 0f, Theme.WindowOut);
        }

        private void Start(float alphaFrom, float alphaTo, float time)
        {
            from = alphaFrom;
            to = alphaTo;
            elapsed = 0f;
            duration = time;
            moving = true;
        }

        internal void Tick()
        {
            if (!moving) return;
            elapsed += Time.unscaledDeltaTime;
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            group.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            if (to > from)
            {
                float k = 1f - (1f - t) * (1f - t) * (1f - t); // fast, then settling (UIJuice's sharp curve)
                rect.anchoredPosition = home - pan * (1f - k);
            }
            if (t < 1f) return;
            moving = false;
            rect.anchoredPosition = home;
            if (to <= 0f) rect.gameObject.SetActive(false);
        }
    }
}
