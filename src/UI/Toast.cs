using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HighCommand.UI
{
    /// <summary>Short messages near the top of the screen ("Stance: defensive", "Not enough gold" ...).</summary>
    internal static class Toast
    {
        private struct Message
        {
            internal string text;
            internal bool error;
        }

        private static readonly Queue<Message> Pending = new Queue<Message>();

        internal static void Show(string message) => Enqueue(message, false);

        /// <summary>Something did not work: the line is red and the game's "cannot afford" sound plays.</summary>
        internal static void Error(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            Enqueue(message, true);
            UiSound.Error();
        }

        private static void Enqueue(string message, bool error)
        {
            if (string.IsNullOrEmpty(message)) return;
            Pending.Enqueue(new Message { text = message, error = error });
            while (Pending.Count > 4) Pending.Dequeue();
        }

        internal static bool TryTake(out string message, out bool error)
        {
            if (Pending.Count > 0)
            {
                Message m = Pending.Dequeue();
                message = m.text;
                error = m.error;
                return true;
            }
            message = null;
            error = false;
            return false;
        }

        internal static void Clear() => Pending.Clear();
    }

    /// <summary>
    /// Draws the messages one at a time on the strip the game uses for the stage name: black, fading out at both ends,
    /// the text in the middle. It eases in, holds, and fades.
    /// </summary>
    internal sealed class ToastView
    {
        private const float Top = 122f;
        private const float StripH = 46f;
        private const float EndsW = 140f;   // the faded ends of the strip on each side of the text

        private readonly RectTransform strip;
        private readonly CanvasGroup group;
        private readonly TextMeshProUGUI text;
        private float born = -100f;
        private float lastAlpha = -1f;

        internal ToastView(Transform parent)
        {
            Image back = UiKit.Sliced("Toast", parent, HudArt.BackdropFaded, Theme.Backdrop);
            strip = back.rectTransform;
            UiKit.Place(strip, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -Top), new Vector2(400f, StripH));
            group = strip.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            text = UiKit.NewText("Text", strip, "", Theme.SizeName, Theme.Text, TextAlignmentOptions.Center);
            UiKit.Stretch(text.rectTransform, EndsW * 0.5f, 0f, EndsW * 0.5f, 0f);
            strip.gameObject.SetActive(false);
        }

        private static float Life => Theme.ToastIn + Theme.ToastHold + Theme.ToastOut;

        internal void Tick()
        {
            float now = Time.unscaledTime;
            // a new message replaces the one on screen once that one has been readable for a moment
            if (now - born >= Theme.ToastIn + Theme.ToastHold * 0.5f && Toast.TryTake(out string next, out bool error))
            {
                text.text = next;
                text.color = error ? Theme.Death : Theme.Text;
                float width = Mathf.Clamp(text.GetPreferredValues(next).x + EndsW, 320f, 1500f);
                strip.sizeDelta = new Vector2(width, StripH);
                strip.gameObject.SetActive(true);
                strip.SetAsLastSibling(); // above an open window
                born = now;
                lastAlpha = -1f;
            }

            if (!strip.gameObject.activeSelf) return;
            float age = now - born;
            if (age >= Life) { strip.gameObject.SetActive(false); return; }
            float alpha = age < Theme.ToastIn ? age / Theme.ToastIn
                : age < Theme.ToastIn + Theme.ToastHold ? 1f
                : 1f - (age - Theme.ToastIn - Theme.ToastHold) / Theme.ToastOut;
            alpha = Mathf.Clamp01(alpha);
            if (alpha == lastAlpha || (Mathf.Abs(alpha - lastAlpha) < 0.02f && alpha < 1f && alpha > 0f)) return;
            lastAlpha = alpha;
            group.alpha = alpha;
        }

        internal void Destroy()
        {
            if (strip) Object.Destroy(strip.gameObject);
        }
    }
}
