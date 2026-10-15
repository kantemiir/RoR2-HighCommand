using RoR2;
using UnityEngine;

namespace HighCommand.UI
{
    /// <summary>The game's own interface sounds, in one place so every panel sounds the same.</summary>
    internal static class UiSound
    {
        private static float lastCoin;
        private static int lastClickFrame = -1;

        /// <summary>The pointer came onto a menu button (windows only: the HUD's buttons stay silent while sweeping over them).</summary>
        internal static void Hover() => Play("Play_UI_menuHover");

        /// <summary>A button press or a confirmed order; one per frame (a button and the order it gives both confirm).</summary>
        internal static void Click()
        {
            if (Time.frameCount == lastClickFrame) return;
            lastClickFrame = Time.frameCount;
            Play("Play_UI_menuClick");
        }

        /// <summary>Not possible / not enough gold.</summary>
        internal static void Error() => Play("Play_UI_insufficient_funds");

        /// <summary>A commander ability is ready again.</summary>
        internal static void Ready() => Play("Play_UI_cooldownRefresh");

        internal static void Pickup() => Play("Play_UI_item_pickup");

        /// <summary>Gold came in (as the stock money counter does, but not more often than every few frames).</summary>
        internal static void Coin()
        {
            float now = Time.unscaledTime;
            if (now - lastCoin < Theme.CoinSoundGap) return;
            lastCoin = now;
            Play("Play_UI_coin");
        }

        private static void Play(string soundEvent)
        {
            try { Util.PlaySound(soundEvent, RoR2Application.instance ? RoR2Application.instance.gameObject : null); }
            catch (System.Exception) { /* a missing sound event is not worth an error */ }
        }
    }
}
