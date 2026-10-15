using HighCommand.Client;
using HighCommand.Mode;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HighCommand.UI
{
    /// <summary>
    /// "Every hero has fallen": the countdown to defeat on the game's stage-name strip, in the red of its death texts and
    /// pulsing so it cannot be missed. A click buys a revival (as the revive key does).
    /// </summary>
    internal sealed class DefeatBanner
    {
        private const float Top = 196f;
        private const float StripH = 58f;
        private const float EndsW = 200f;

        private readonly RectTransform strip;
        private readonly Image back;
        private readonly TextMeshProUGUI text;
        private int shownKey = int.MinValue;

        internal DefeatBanner(Transform parent)
        {
            back = UiKit.Sliced("Defeat", parent, HudArt.BackdropFaded, new Color(0f, 0f, 0f, 0.82f));
            strip = back.rectTransform;
            UiKit.Place(strip, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -Top), new Vector2(900f, StripH));
            text = UiKit.NewText("Text", strip, "", Theme.SizeWindowTitle, Theme.Death, TextAlignmentOptions.Center);
            UiKit.Stretch(text.rectTransform, EndsW * 0.5f, 0f, EndsW * 0.5f, 0f);
            strip.gameObject.SetActive(false);
        }

        internal bool Contains(Vector2 screenPoint) => strip.gameObject.activeSelf && UiKit.Contains(strip, screenPoint);

        internal void Tick(bool visible)
        {
            HcState state = HcState.Instance;
            bool show = visible && state != null && state.DefeatSeconds >= 0f;
            if (strip.gameObject.activeSelf != show) strip.gameObject.SetActive(show);
            if (!show) return;

            int seconds = Mathf.CeilToInt(state.DefeatSeconds);
            int key = seconds * 4 + (state.DefeatCanRevive ? 1 : 0) + (state.DefeatAutoRevive ? 2 : 0);
            if (key != shownKey)
            {
                shownKey = key;
                string revive = UiKit.KeyText(Settings.KeyRevive.Value);
                string line;
                if (state.DefeatAutoRevive) line = Loc.Format("HIGHCOMMAND_DEFEAT_AUTO_REVIVE", seconds, state.RevivePrice(), revive);
                else if (state.DefeatCanRevive) line = Loc.Format("HIGHCOMMAND_DEFEAT_CAN_REVIVE", seconds, revive);
                else line = Loc.Format("HIGHCOMMAND_DEFEAT_BROKE", seconds);
                text.text = line;
                strip.sizeDelta = new Vector2(Mathf.Clamp(text.GetPreferredValues(line).x + EndsW, 420f, 1600f), StripH);
            }

            // the only thing on the HUD that keeps pulsing: the run is about to end
            float pulse = 0.72f + 0.28f * Mathf.Sin(Time.unscaledTime * 6f);
            Color c = Theme.Death;
            c.a = pulse;
            text.color = c;
            if (state.DefeatCanRevive && Contains(ClientInput.Mouse) && !ClientInput.MouseBlocked)
                Tooltip.Show(Loc.Get("HIGHCOMMAND_DEFEAT_TIP_TITLE"), Loc.Format("HIGHCOMMAND_DEFEAT_TIP", state.RevivePrice())
                    + "\n\n" + Tooltip.KeyHint(UiKit.KeyText(Settings.KeyRevive.Value)));
        }

        internal void Destroy()
        {
            if (strip) Object.Destroy(strip.gameObject);
        }
    }
}
