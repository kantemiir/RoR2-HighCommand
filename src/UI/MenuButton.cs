using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HighCommand.UI
{
    /// <summary>
    /// The stock menu button (GenericMenuButton): a slate face with a faint outline that turns pale yellow under the
    /// pointer while the white frame settles round it; the game's hover and click sounds.
    /// </summary>
    internal sealed class MenuButton
    {
        internal readonly RectTransform Root;
        internal readonly TextMeshProUGUI Label;
        private readonly Image face;
        private readonly HoverOutline hover;

        internal bool Hover { get; private set; }
        private bool enabled = true, known, wasHover;

        internal MenuButton(Transform parent, string name, Vector2 size, float fontSize = Theme.SizeName)
        {
            Root = UiKit.NewRect(name, parent);
            Root.anchorMin = Root.anchorMax = new Vector2(0f, 1f);
            Root.pivot = new Vector2(0f, 1f);
            Root.sizeDelta = size;

            face = UiKit.Sliced("Face", Root, HudArt.CleanButton, Theme.Button);
            UiKit.Stretch(face.rectTransform);
            Image outline = UiKit.Sliced("Outline", Root, HudArt.Outline, Theme.ButtonOutline);
            UiKit.Stretch(outline.rectTransform);

            Label = UiKit.NewText("Label", Root, "", fontSize, Theme.Text, TextAlignmentOptions.Center);
            UiKit.Stretch(Label.rectTransform, 10f, 3f, 10f, 3f);
            Label.enableAutoSizing = true;
            Label.fontSizeMin = Mathf.Min(12f, fontSize);
            Label.fontSizeMax = fontSize;

            hover = HoverOutline.ForButton(Root);
        }

        internal void SetPosition(float x, float y) => Root.anchoredPosition = new Vector2(x, -y);

        internal bool Refresh(Vector2 mouse, bool isEnabled)
        {
            Hover = Root.gameObject.activeInHierarchy && UiKit.Contains(Root, mouse);
            bool lit = Hover && isEnabled;
            hover.Tick(lit);
            if (lit && !wasHover) UiSound.Hover();
            if (known && isEnabled == enabled && lit == wasHover) return Hover;
            known = true;
            enabled = isEnabled;
            wasHover = lit;
            face.color = !enabled ? Theme.ButtonDisabled : lit ? Theme.ButtonHover : Theme.Button;
            Label.color = enabled ? Theme.Text : Theme.TextDisabled;
            return Hover;
        }

        internal bool Clicked(bool mouseDown)
        {
            if (!(enabled && Hover && mouseDown)) return false;
            UiSound.Click();
            return true;
        }
    }
}
