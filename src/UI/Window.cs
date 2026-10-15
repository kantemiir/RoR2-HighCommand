using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HighCommand.UI
{
    /// <summary>
    /// A window over the battle, built like the game's dialog box (SimpleDialogRoot): a white notched frame, a near-black
    /// body, a muted purple header with the title on the left and a short note on the right. The world behind is dimmed;
    /// it appears and leaves the way the stock panels do (<see cref="Fader"/>).
    /// </summary>
    internal sealed class Window
    {
        internal const float HeaderH = 56f;
        internal const float Pad = 16f;
        private const float FrameInset = 3f;
        private const float DimOverscan = 64f;   // the dim slides with the window: wider than the screen, no edge shows

        internal readonly RectTransform Panel;
        internal readonly RectTransform Body;
        internal readonly TextMeshProUGUI Title;
        internal readonly TextMeshProUGUI Note;
        private readonly RectTransform layer;
        private readonly Fader fader;

        internal Window(Transform parent, string name, Vector2 size)
        {
            layer = UiKit.NewRect(name, parent);
            UiKit.Stretch(layer, -DimOverscan, -DimOverscan, -DimOverscan, -DimOverscan);
            Image dim = UiKit.NewImage("Dim", layer, UiKit.White, Theme.WindowDim);
            UiKit.Stretch(dim.rectTransform);

            Image frame = UiKit.Sliced("Panel", layer, HudArt.ClusterFrame, new Color(1f, 1f, 1f, 0.85f));
            Panel = frame.rectTransform;
            UiKit.Place(Panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);

            Image back = UiKit.NewImage("Back", Panel, UiKit.White, Theme.WindowBody);
            UiKit.Stretch(back.rectTransform, FrameInset, FrameInset, FrameInset, FrameInset);

            Image header = UiKit.Sliced("Header", Panel, HudArt.CleanPanel, Theme.WindowHeader);
            RectTransform h = header.rectTransform;
            h.anchorMin = new Vector2(0f, 1f);
            h.anchorMax = new Vector2(1f, 1f);
            h.pivot = new Vector2(0.5f, 1f);
            h.offsetMin = new Vector2(FrameInset, -(FrameInset + HeaderH));
            h.offsetMax = new Vector2(-FrameInset, -FrameInset);

            Title = UiKit.NewText("Title", h, "", Theme.SizeWindowTitle, Theme.Text, TextAlignmentOptions.MidlineLeft);
            UiKit.Stretch(Title.rectTransform, Pad, 0f, Pad, 0f);
            Note = UiKit.NewText("Note", h, "", Theme.SizeBody, Theme.TextSub, TextAlignmentOptions.MidlineRight);
            UiKit.Stretch(Note.rectTransform, Pad, 0f, Pad, 0f);

            Body = UiKit.NewRect("Body", Panel);
            UiKit.Stretch(Body, FrameInset + Pad, FrameInset + Pad, FrameInset + Pad, FrameInset + HeaderH + Pad);

            fader = new Fader(layer, new Vector2(0f, Theme.WindowPan));
            layer.gameObject.SetActive(false);
        }

        internal bool Visible => fader.Open;

        /// <summary>The width the body offers to its content.</summary>
        internal float BodyWidth => Panel.sizeDelta.x - 2f * (FrameInset + Pad);

        /// <summary>The window's size for a body of <paramref name="bodyHeight"/> (the header and the paddings around it).</summary>
        internal static float HeightFor(float bodyHeight) => bodyHeight + HeaderH + 2f * (FrameInset + Pad);

        internal void SetSize(Vector2 size) => Panel.sizeDelta = size;

        /// <summary>Shrinks the whole window to fit a small screen or a big interface scale (never grows it).</summary>
        internal void FitToScreen()
        {
            var parent = layer.parent as RectTransform;
            if (!parent) return;
            Vector2 room = parent.rect.size - new Vector2(40f, 40f);
            Vector2 size = Panel.sizeDelta;
            float fit = Mathf.Min(1f, room.x / Mathf.Max(1f, size.x), room.y / Mathf.Max(1f, size.y));
            Panel.localScale = new Vector3(fit, fit, 1f);
        }

        internal void Show()
        {
            layer.SetAsLastSibling();
            fader.Show();
        }

        internal void Hide(bool instant = false) => fader.Hide(instant);

        internal void Tick() => fader.Tick();

        internal bool Contains(Vector2 screenPoint) => Visible && UiKit.Contains(Panel, screenPoint);

        internal void Destroy()
        {
            if (layer) Object.Destroy(layer.gameObject);
        }
    }
}
