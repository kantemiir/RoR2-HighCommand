using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HighCommand.UI
{
    /// <summary>
    /// The hotkey sheet (F1) in a stock dialog window: two columns of sections, every line the keys on the plates the HUD
    /// prints keys on and what they do. Lines are registered by each module so the sheet always matches the config.
    /// </summary>
    internal sealed class HelpOverlay
    {
        private enum Kind { Line, Section, Column }

        private struct Entry
        {
            internal Kind kind;
            internal string keys, what;
        }

        private static readonly List<Entry> Entries = new List<Entry>();

        /// <summary>Registers a help line: the keys and what they do (both already localised).</summary>
        internal static void Add(string keys, string what) => Entries.Add(new Entry { kind = Kind.Line, keys = keys, what = what });

        /// <summary>A heading over the lines that follow (Camera, Selection ...).</summary>
        internal static void Section(string title) => Entries.Add(new Entry { kind = Kind.Section, what = title });

        /// <summary>What follows goes into the right column.</summary>
        internal static void NextColumn() => Entries.Add(new Entry { kind = Kind.Column });

        internal static void Clear() => Entries.Clear();

        private const float BodyW = 1440f;
        private const float ColumnGap = 48f;
        private const float ColumnW = (BodyW - ColumnGap) / 2f;
        private const float KeysW = 236f;
        private const float RowH = 30f;
        private const float SectionH = 40f;
        private const float SectionGap = 10f;

        private static readonly Color Rule = new Color(1f, 1f, 1f, 0.14f);

        private readonly Window window;
        private readonly RectTransform rowsHost;

        internal HelpOverlay(Transform parent)
        {
            window = new Window(parent, "Help", new Vector2(BodyW + 2f * (Window.Pad + 3f), 600f));
            rowsHost = UiKit.NewRect("Rows", window.Body);
            UiKit.Stretch(rowsHost);
        }

        internal bool Visible => window.Visible;

        internal bool Contains(Vector2 screenPoint) => window.Contains(screenPoint);

        internal void Toggle()
        {
            if (window.Visible) Hide();
            else
            {
                // shown first: the lines measure their text, which needs them alive (an inactive text has no font yet)
                window.Show();
                Rebuild();
                UiSound.Click();
            }
        }

        internal void Hide() => window.Hide();

        internal void Tick() => window.Tick();

        // Every line is its own pair of texts, so a long description can never push the following lines out of
        // place (it shrinks instead of wrapping).
        private void Rebuild()
        {
            window.Title.text = Loc.Get("HIGHCOMMAND_HELP_TITLE");
            window.Note.text = Loc.Get("HIGHCOMMAND_HELP_CLOSE");

            for (int i = rowsHost.childCount - 1; i >= 0; i--)
                Object.Destroy(rowsHost.GetChild(i).gameObject);

            int column = 0;
            float y = 0f, tallest = 0f;
            for (int i = 0; i < Entries.Count; i++)
            {
                Entry e = Entries[i];
                float x = column * (ColumnW + ColumnGap);
                switch (e.kind)
                {
                    case Kind.Column:
                        tallest = Mathf.Max(tallest, y);
                        column = Mathf.Min(1, column + 1);
                        y = 0f;
                        break;

                    case Kind.Section:
                    {
                        float top = y > 0f ? y + SectionGap : y;
                        TextMeshProUGUI head = UiKit.NewText("Section", rowsHost, e.what, 22f, Theme.Text, TextAlignmentOptions.BottomLeft);
                        Place(head.rectTransform, x, top, ColumnW, SectionH - 8f);
                        Image rule = UiKit.NewImage("Rule", rowsHost, UiKit.White, Rule);
                        Place(rule.rectTransform, x, top + SectionH - 5f, ColumnW, 1f);
                        y = top + SectionH;
                        break;
                    }

                    default:
                    {
                        // the keys on a plate, as under the skill bar
                        Image plate = UiKit.Sliced("Keys", rowsHost, HudArt.Plate, Theme.Plate);
                        TextMeshProUGUI keys = UiKit.NewText("Text", plate.rectTransform, e.keys, Theme.SizeKey, Theme.TextHud, TextAlignmentOptions.Center);
                        UiKit.Stretch(keys.rectTransform, 6f, 0f, 6f, 0f);
                        Fit(keys, 11f, Theme.SizeKey);
                        float keyW = Mathf.Min(KeysW - 12f, keys.GetPreferredValues(e.keys).x + 14f);
                        Place(plate.rectTransform, x, y + 4f, keyW, RowH - 8f);

                        TextMeshProUGUI what = UiKit.NewText("What", rowsHost, e.what, Theme.SizeBody, Theme.TextBody, TextAlignmentOptions.MidlineLeft);
                        Place(what.rectTransform, x + KeysW, y, ColumnW - KeysW, RowH);
                        Fit(what, 12f, Theme.SizeBody);
                        y += RowH;
                        break;
                    }
                }
            }
            tallest = Mathf.Max(tallest, y);

            window.SetSize(new Vector2(window.Panel.sizeDelta.x, Window.HeightFor(tallest)));
            window.FitToScreen();
        }

        private static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void Fit(TextMeshProUGUI text, float min, float max)
        {
            text.enableWordWrapping = false;
            text.enableAutoSizing = true;
            text.fontSizeMin = min;
            text.fontSizeMax = max;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }

        internal void Destroy() => window.Destroy();
    }
}
