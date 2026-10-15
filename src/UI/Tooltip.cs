using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HighCommand.UI
{
    /// <summary>
    /// Immediate-mode tooltip: any panel calls <see cref="Show"/> every frame while the pointer rests on something; the
    /// view draws it beside the pointer and hides it again when nobody asked this frame.
    /// </summary>
    internal static class Tooltip
    {
        internal static bool Requested { get; private set; }
        internal static string Title { get; private set; }
        internal static string Body { get; private set; }
        internal static Color Bar { get; private set; }

        /// <param name="bar">The colour of the title strip, as in the game: an item's dark rarity colour, a survivor's colour;
        /// null = the plain black strip.</param>
        internal static void Show(string title, string body, Color? bar = null)
        {
            Requested = true;
            Title = title ?? "";
            Body = body ?? "";
            Bar = bar ?? Theme.TooltipTitle;
        }

        internal static void Clear() => Requested = false;

        /// <summary>A survivor's colour as the stock skill tooltips use it (never brighter than 70 %).</summary>
        internal static Color SurvivorBar(RoR2.CharacterBody body)
        {
            if (!body) return Theme.TooltipTitle;
            Color.RGBToHSV(body.bodyColor, out float h, out float s, out float v);
            return Color.HSVToRGB(h, s, Mathf.Min(v, 0.7f));
        }

        /// <summary>"[Q]" in the game's key colour, for the last line of a tooltip.</summary>
        internal static string KeyHint(string key) => string.IsNullOrEmpty(key) ? "" : "<style=cKeyBinding>" + key + "</style>";
    }

    /// <summary>
    /// The game's tooltip (Tooltip.prefab): a 400 wide card, the title on a coloured strip, the text below on black, a
    /// steel outline; it opens towards the middle of the screen from the pointer.
    /// </summary>
    internal sealed class TooltipView
    {
        private const float Offset = 22f;
        private const float FadeIn = 0.08f;

        private readonly Canvas canvas;
        private readonly RectTransform root;
        private readonly CanvasGroup group;
        private readonly Image titleStrip;
        private readonly RectTransform bodyStrip;
        private readonly TextMeshProUGUI title;
        private readonly TextMeshProUGUI body;
        private string shownTitle, shownBody;
        private Color shownBar;
        private float height;
        private float shownSince;

        internal TooltipView(Transform parent)
        {
            canvas = parent.GetComponent<Canvas>();
            root = UiKit.NewRect("Tooltip", parent);
            root.anchorMin = root.anchorMax = Vector2.zero;
            root.sizeDelta = new Vector2(Theme.TooltipWidth, 80f);
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            titleStrip = UiKit.NewImage("TitleStrip", root, UiKit.White, Theme.TooltipTitle);
            titleStrip.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleStrip.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleStrip.rectTransform.pivot = new Vector2(0.5f, 1f);
            title = UiKit.NewText("Title", titleStrip.rectTransform, "", Theme.SizeTooltipTitle, Theme.Text, TextAlignmentOptions.MidlineLeft);
            UiKit.Stretch(title.rectTransform, Theme.TooltipPad, 0f, Theme.TooltipPad, 0f);
            title.enableWordWrapping = true;

            Image bodyBack = UiKit.NewImage("BodyStrip", root, UiKit.White, Theme.TooltipBody);
            bodyStrip = bodyBack.rectTransform;
            bodyStrip.anchorMin = Vector2.zero;
            bodyStrip.anchorMax = new Vector2(1f, 1f);
            body = UiKit.NewText("Body", bodyStrip, "", Theme.SizeBody, Theme.TextBody, TextAlignmentOptions.TopLeft);
            UiKit.Stretch(body.rectTransform, Theme.TooltipPad, Theme.TooltipPad, Theme.TooltipPad, Theme.TooltipPad);
            body.enableWordWrapping = true;

            Image outline = UiKit.Sliced("Outline", root, HudArt.Outline, Theme.TooltipOutline);
            UiKit.Stretch(outline.rectTransform, -2f, -2f, -2f, -2f);

            root.gameObject.SetActive(false);
        }

        internal void Destroy()
        {
            if (root) Object.Destroy(root.gameObject);
        }

        /// <summary>Call after all panels have ticked.</summary>
        internal void Tick()
        {
            if (!root) return;
            if (!Tooltip.Requested)
            {
                if (root.gameObject.activeSelf) root.gameObject.SetActive(false);
                return;
            }
            Tooltip.Clear();

            if (!root.gameObject.activeSelf)
            {
                root.gameObject.SetActive(true);
                shownSince = Time.unscaledTime;
                group.alpha = 0f;
            }

            // the panels ask every frame: measuring the text is the costly part, so it is redone only for new text
            if (!string.Equals(shownTitle, Tooltip.Title, System.StringComparison.Ordinal)
                || !string.Equals(shownBody, Tooltip.Body, System.StringComparison.Ordinal))
            {
                shownTitle = Tooltip.Title;
                shownBody = Tooltip.Body;
                Layout();
            }
            if (shownBar != Tooltip.Bar)
            {
                shownBar = Tooltip.Bar;
                titleStrip.color = shownBar;
            }

            float alpha = Mathf.Clamp01((Time.unscaledTime - shownSince) / FadeIn);
            if (group.alpha != alpha) group.alpha = alpha;
            Place();
            root.SetAsLastSibling();
        }

        private void Layout()
        {
            float inner = Theme.TooltipWidth - 2f * Theme.TooltipPad;
            title.text = shownTitle;
            body.text = shownBody;
            float titleH = Mathf.Max(32f, title.GetPreferredValues(shownTitle, inner, 0f).y + 2f * Theme.TooltipPad - 6f);
            bool hasBody = !string.IsNullOrEmpty(shownBody);
            float bodyH = hasBody ? body.GetPreferredValues(shownBody, inner, 0f).y + 2f * Theme.TooltipPad : 0f;
            titleStrip.rectTransform.sizeDelta = new Vector2(0f, titleH);
            bodyStrip.gameObject.SetActive(hasBody);
            bodyStrip.offsetMin = Vector2.zero;
            bodyStrip.offsetMax = new Vector2(0f, -titleH);
            height = titleH + bodyH;
            root.sizeDelta = new Vector2(Theme.TooltipWidth, height);
        }

        // as the stock tooltip: it opens from the pointer towards the middle of the screen
        private void Place()
        {
            float scale = canvas ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f;
            Vector2 mouse = Client.ClientInput.Mouse / scale;
            Vector2 screen = new Vector2(Screen.width, Screen.height) / scale;
            bool left = mouse.x > screen.x * 0.5f;
            bool below = mouse.y > screen.y * 0.5f;
            float x = left ? mouse.x - Offset - Theme.TooltipWidth : mouse.x + Offset;
            float y = below ? mouse.y - Offset : mouse.y + Offset + height;
            x = Mathf.Clamp(x, 4f, Mathf.Max(4f, screen.x - Theme.TooltipWidth - 4f));
            y = Mathf.Clamp(y, height + 4f, Mathf.Max(height + 4f, screen.y - 4f));
            root.pivot = new Vector2(0f, 1f);
            Vector2 at = new Vector2(Mathf.Round(x), Mathf.Round(y));
            if (root.anchoredPosition != at) root.anchoredPosition = at;
        }
    }
}
