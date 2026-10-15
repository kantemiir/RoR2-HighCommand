using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HighCommand.UI
{
    /// <summary>
    /// The hotkey on a dark plate, as under the stock skill icons ("M1", "Shift", "R"). The plate grows with the text.
    /// The owner places <see cref="Root"/> (its pivot is the plate's top middle).
    /// </summary>
    internal sealed class KeyLabel
    {
        private const float PadX = 5f;

        internal readonly RectTransform Root;
        private readonly TextMeshProUGUI text;
        private readonly float height;
        private string shown;
        private bool dim;

        internal KeyLabel(Transform parent, float height = Theme.KeyLabelH, float fontSize = Theme.SizeKey)
        {
            this.height = height;
            Image plate = UiKit.Sliced("Key", parent, HudArt.Plate, Theme.Plate);
            Root = plate.rectTransform;
            Root.anchorMin = Root.anchorMax = new Vector2(0.5f, 0f);
            Root.pivot = new Vector2(0.5f, 1f);
            Root.sizeDelta = new Vector2(height, height);
            text = UiKit.NewText("Text", Root, "", fontSize, Theme.TextHud, TextAlignmentOptions.Center);
            UiKit.Stretch(text.rectTransform);
        }

        /// <summary>Under its owner, centred, <paramref name="gap"/> below it (the stock skill bar).</summary>
        internal void PlaceBelow(float gap)
        {
            Root.anchorMin = Root.anchorMax = new Vector2(0.5f, 0f);
            Root.pivot = new Vector2(0.5f, 1f);
            Root.anchoredPosition = new Vector2(0f, -gap);
        }

        /// <summary>In its owner's bottom right corner (small icons where a line below would not fit).</summary>
        internal void PlaceCorner()
        {
            Root.anchorMin = Root.anchorMax = new Vector2(1f, 0f);
            Root.pivot = new Vector2(1f, 0f);
            Root.anchoredPosition = new Vector2(1f, 1f);
        }

        internal void Set(string key)
        {
            key = key ?? "";
            if (string.Equals(shown, key, System.StringComparison.Ordinal)) return;
            shown = key;
            text.text = key;
            Root.gameObject.SetActive(key.Length > 0);
            float width = Mathf.Max(height, text.GetPreferredValues(key).x + 2f * PadX);
            Root.sizeDelta = new Vector2(Mathf.Ceil(width), height);
        }

        /// <summary>Greyed while the thing it belongs to cannot be used.</summary>
        internal void SetDim(bool value)
        {
            if (dim == value) return;
            dim = value;
            text.color = value ? Theme.TextMuted : Theme.TextHud;
        }
    }
}
