using UnityEngine;

namespace HighCommand.UI
{
    /// <summary>
    /// Every colour, size and timing of the mode's interface, taken from the game's own HUD (docs\dev\UI_STYLE.md says
    /// where each value comes from). Panels read these and never invent their own.
    /// </summary>
    internal static class Theme
    {
        private static Color Hex(uint rgb, float alpha = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);

        // ---- text ----------------------------------------------------------------------------------------
        internal static readonly Color Text = Color.white;
        internal static readonly Color TextBody = Hex(0xDBDBDB);
        internal static readonly Color TextHud = Hex(0xBFD8F0);
        internal static readonly Color TextSub = Hex(0xCCD3E0);
        internal static readonly Color TextMuted = Hex(0x8990A7);
        internal static readonly Color TextDisabled = Hex(0x898989);
        internal static readonly Color Money = Hex(0xFFFCC4);
        internal static readonly Color Key = Hex(0xFFFF00);
        internal static readonly Color Damage = Hex(0xE5C962);
        internal static readonly Color Healing = Hex(0x9CE562);
        internal static readonly Color Utility = Hex(0x95CDE5);
        internal static readonly Color Death = Hex(0xFE7C7C);

        /// <summary>Rich-text forms of the colours above, for strings that mix them.</summary>
        internal const string HexMoney = "#FFFCC4", HexKey = "#FFFF00", HexSub = "#CCD3E0", HexMuted = "#8990A7",
            HexHealing = "#9CE562", HexDeath = "#FE7C7C", HexHud = "#BFD8F0", HexDamage = "#E5C962";

        // ---- surfaces ------------------------------------------------------------------------------------
        internal static readonly Color Plate = Hex(0x1A031A, 0.576f);
        internal static readonly Color PlateBlack = Hex(0x000000, 0.545f);
        internal static readonly Color PlateStrong = Hex(0x050404, 0.706f);
        internal static readonly Color ClusterFrame = Hex(0xAABBC3, 0.196f);
        internal static readonly Color MoneyStrip = Hex(0xC5BD90, 0.133f);

        internal static readonly Color Button = Hex(0x536778);
        internal static readonly Color ButtonHover = Hex(0xFCFFB1, 0.733f);
        internal static readonly Color ButtonPressed = Hex(0xBDC071, 0.984f);
        internal static readonly Color ButtonDisabled = Hex(0x413333, 0.714f);
        internal static readonly Color ButtonOutline = Hex(0xFFFFFF, 0.286f);
        internal static readonly Color Highlight = Color.white;

        internal static readonly Color TooltipTitle = Hex(0x000000, 0.937f);
        internal static readonly Color TooltipBody = Hex(0x000000, 0.86f);
        internal static readonly Color TooltipOutline = Hex(0x7190A4);

        internal static readonly Color WindowBody = Hex(0x070606, 0.97f);
        internal static readonly Color WindowHeader = Hex(0x534857);
        internal static readonly Color WindowInner = Hex(0x39323C);
        internal static readonly Color WindowDim = Hex(0x000000, 0.42f);

        internal static readonly Color Strip = Hex(0x536778, 0.87f);   // a scoreboard row
        internal static readonly Color StripBox = Hex(0x000000, 0.776f);
        internal static readonly Color CardFace = Hex(0x302525, 0.94f);  // a survivor card of the character select
        internal static readonly Color CardName = Hex(0x0F1011, 0.59f);

        internal static readonly Color Flash = Hex(0xAEF1FF);
        internal static readonly Color Backdrop = Hex(0x000000, 0.53f); // the stage-name strip
        internal static readonly Color AllyCard = Hex(0x000000, 0.247f);
        internal static readonly Color Dead = Hex(0xE92B2B);

        // ---- type sizes (1080p reference) ------------------------------------------------------------------
        internal const float SizeWindowTitle = 30f;
        internal const float SizeMoney = 28f;
        internal const float SizeName = 24f;
        internal const float SizeTooltipTitle = 22f;
        internal const float SizeBody = 18f;
        internal const float SizeKey = 16f;
        internal const float SizeSmall = 13f;
        internal const float SizeCooldown = 26f;

        // ---- grid -------------------------------------------------------------------------------------------
        internal const float Unit = 4f;
        internal const float Margin = 16f;      // from the screen edge
        internal const float Inset = 6f;        // inside a cluster
        internal const float Gap = 6f;          // between related things
        internal const float GroupGap = 12f;    // between groups

        internal const float KeyLabelH = 20f;
        internal const float TooltipWidth = 400f;
        internal const float TooltipPad = 12f;

        // ---- motion -----------------------------------------------------------------------------------------
        internal const float HoverIn = 0.06f;        // the hover outline settles (HGButton: SmoothDamp 0.03 s)
        internal const float HoverScale = 1.1f;
        internal const float WindowIn = 0.2f;
        internal const float WindowOut = 0.12f;
        internal const float WindowPan = 24f;
        internal const float FlashTime = 0.45f;
        internal const float ToastIn = 0.15f, ToastHold = 2.0f, ToastOut = 0.4f;
        internal const float CoinSoundGap = 0.12f;
    }
}
