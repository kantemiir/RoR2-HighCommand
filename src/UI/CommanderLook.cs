using UnityEngine;

namespace HighCommand.UI
{
    /// <summary>
    /// How each commander ability is marked while its target is chosen (the pointer's badge takes its colour and glyph):
    /// orbital strike, field medic, war cry, mark.
    /// </summary>
    internal static class CommanderLook
    {
        private static readonly Color[] Colors =
        {
            new Color(1f, 0.46f, 0.2f, 1f),
            new Color(0.36f, 0.92f, 0.5f, 1f),
            new Color(1f, 0.8f, 0.24f, 1f),
            new Color(0.76f, 0.48f, 1f, 1f)
        };

        private static readonly Glyph[] Glyphs = { Glyph.OrbitalStrike, Glyph.Medic, Glyph.WarCry, Glyph.Mark };

        internal static Color ColorOf(int index) => Colors[Mathf.Clamp(index, 0, Colors.Length - 1)];

        internal static Glyph GlyphOf(int index) => Glyphs[Mathf.Clamp(index, 0, Glyphs.Length - 1)];
    }
}
